using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace QuickLook.DicomRT
{
    public enum DvhStatus { Complete, PartialCoverage, Unsupported, Empty, BudgetExceeded }
    public sealed class DvhResult
    {
        public DvhStatus Status { get; internal set; }
        public string Message { get; internal set; }
        public string DoseUnits { get; internal set; }
        public double[] DoseValues { get; internal set; } = new double[0];
        // Denominator is the entire sampled ROI, including volume outside RTDOSE.
        public double[] CumulativeVolumePercent { get; internal set; } = new double[0];
        public double EstimatedVolumeCc { get; internal set; }
        public double SampledVolumeCc { get; internal set; }
        public double CoverageFraction => EstimatedVolumeCc > 0 ? SampledVolumeCc / EstimatedVolumeCc : 0;
        public double SamplingStepMm { get; internal set; }
    }

    /// <summary>Bounded preview DVH: parallel contour slabs, midpoint sampling, trilinear dose.
    /// Slab boundaries lie halfway between planes; end caps extend half the adjacent spacing.
    /// No shape interpolation is implied. Single-plane ROIs cannot define a volume.
    /// CLOSED_PLANAR polygons are united (keyholes use even-odd fill within a polygon);
    /// CLOSEDPLANAR_XOR polygons use XOR, per DICOM PS3.3 C.8.8.6.3.
    /// </summary>
    public static class DvhCalculator
    {
        const int Bins = 256, MaxCells = 600000;
        sealed class Polygon { public double[] X, Y; public double Z; }
        sealed class Plane { public double Z; public List<Polygon> Polygons = new List<Polygon>(); }
        struct Interval { public double A, B; public Interval(double a,double b){A=a;B=b;} }
        public static DvhResult Calculate(StructureRoi roi, DoseGrid dose, Matrix4 roiToDose, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var result = new DvhResult { DoseUnits = dose?.Units ?? "UNKNOWN" };
            if (roi == null || dose == null || roiToDose == null) return Fail(result, DvhStatus.Unsupported, "No unambiguous structure/dose association.");
            if (dose.Units != "GY" && dose.Units != "RELATIVE") return Fail(result,DvhStatus.Unsupported,"Unknown dose unit.");
            if (!Finite(dose.Maximum) || dose.Maximum < 0) return Fail(result,DvhStatus.Unsupported,"Invalid dose values.");
            var m=roiToDose.Values;
            var a=new Vec3(m[0],m[4],m[8]);var b=new Vec3(m[1],m[5],m[9]);var c=new Vec3(m[2],m[6],m[10]);
            if (Math.Abs(a.Length-1)>1e-4 || Math.Abs(b.Length-1)>1e-4 || Math.Abs(c.Length-1)>1e-4 || Math.Abs(a.Dot(b))+Math.Abs(a.Dot(c))+Math.Abs(b.Dot(c))>1e-4)
                return Fail(result,DvhStatus.Unsupported,"DVH supports rigid registrations only.");
            if (roi.Contours == null || roi.Contours.Count == 0) return Fail(result,DvhStatus.Empty,"No contours.");
            if (roi.Contours.Count>4000 || roi.Contours.Sum(p=>(long)p.Points.Count)>200000) return Fail(result,DvhStatus.BudgetExceeded,"Contours exceed the preview budget.");
            bool xor=roi.Contours[0].GeometricType=="CLOSEDPLANAR_XOR";
            if (roi.Contours.Any(p=>p.Points.Count<3 || p.GeometricType!=(xor?"CLOSEDPLANAR_XOR":"CLOSED_PLANAR")))
                return Fail(result,DvhStatus.Unsupported,"All contours must consistently use CLOSED_PLANAR or CLOSEDPLANAR_XOR.");
            var watch=Stopwatch.StartNew();
            Vec3 origin=roi.Contours[0].Points[0], normal=new Vec3(), u=new Vec3();
            foreach(var contour in roi.Contours)
            {
                for(int i=1;i<contour.Points.Count-1 && normal.Length<1e-8;i++)
                { u=contour.Points[i]-contour.Points[0]; normal=u.Cross(contour.Points[i+1]-contour.Points[0]); }
                if(normal.Length>=1e-8)break;
            }
            if(normal.Length<1e-8)return Fail(result,DvhStatus.Unsupported,"Degenerate contours.");
            normal=normal.Normalized();u=u.Normalized();var v=normal.Cross(u).Normalized();
            var polygons=new List<Polygon>();
            foreach(var contour in roi.Contours)
            {
                token.ThrowIfCancellationRequested();
                var p=new Polygon { X=new double[contour.Points.Count],Y=new double[contour.Points.Count],Z=(contour.Points[0]-origin).Dot(normal) };
                for(int i=0;i<contour.Points.Count;i++)
                {
                    var d=contour.Points[i]-origin;p.X[i]=d.Dot(u);p.Y[i]=d.Dot(v);
                    if(!Finite(p.X[i])||!Finite(p.Y[i])||!Finite(d.Dot(normal))||Math.Abs(d.Dot(normal)-p.Z)>.01)
                        return Fail(result,DvhStatus.Unsupported,"Contours are not parallel and planar.");
                }
                polygons.Add(p);
            }
            var planes=new List<Plane>();
            foreach(var p in polygons.OrderBy(p=>p.Z))
            { if(planes.Count==0 || Math.Abs(planes[planes.Count-1].Z-p.Z)>.01)planes.Add(new Plane{Z=p.Z}); planes[planes.Count-1].Polygons.Add(p); }
            if(planes.Count<2)return Fail(result,DvhStatus.Unsupported,"A single contour plane does not define a volume thickness.");
            double minX=polygons.Min(p=>p.X.Min()),maxX=polygons.Max(p=>p.X.Max()),minY=polygons.Min(p=>p.Y.Min()),maxY=polygons.Max(p=>p.Y.Max());
            double extentX=maxX-minX,extentY=maxY-minY;
            double minZ=planes[0].Z-(planes[1].Z-planes[0].Z)/2,maxZ=planes.Last().Z+(planes.Last().Z-planes[planes.Count-2].Z)/2;
            if(extentX<=0 || extentY<=0 || !Finite(extentX*extentY*(maxZ-minZ)))return Fail(result,DvhStatus.Unsupported,"Invalid contour volume.");
            double step=Math.Max(1,Math.Pow(extentX*extentY*(maxZ-minZ)/MaxCells,1.0/3));
            double cells;int nx,ny;int[] nz;
            do
            {
                nx=(int)Math.Min(MaxCells+1,Math.Ceiling(extentX/step));ny=(int)Math.Min(MaxCells+1,Math.Ceiling(extentY/step));
                nz=Enumerable.Range(0,planes.Count).Select(i=>(int)Math.Min(MaxCells+1,Math.Max(1,Math.Ceiling((Upper(planes,i)-Lower(planes,i))/step)))).ToArray();
                cells=(double)nx*ny*nz.Sum(z=>(long)z);if(cells>MaxCells)step*=1.25;
            }while(cells>MaxCells && step<1e9);
            if(cells>MaxCells)return Fail(result,DvhStatus.BudgetExceeded,"Volume exceeds the preview budget.");
            result.SamplingStepMm=step;
            double dx=extentX/nx,dy=extentY/ny;var histogram=new double[Bins+1];
            double total=0,covered=0,maxDose=Math.Max(1e-9,dose.Maximum);
            for(int k=0;k<planes.Count;k++)
            {
                double low=Lower(planes,k),dz=(Upper(planes,k)-low)/nz[k],weight=dx*dy*dz/1000;
                for(int y=0;y<ny;y++)
                {
                    token.ThrowIfCancellationRequested();
                    if(watch.ElapsedMilliseconds>10000)return Fail(result,DvhStatus.BudgetExceeded,"Calculation stopped after 10 s; no incomplete curve is displayed.");
                    double yy=minY+(y+.5)*dy;
                    var intervals=Intervals(planes[k].Polygons,yy,xor,token);
                    foreach(var range in intervals)
                    {
                        int x0=Math.Max(0,(int)Math.Ceiling((range.A-minX)/dx-.5)),x1=Math.Min(nx,(int)Math.Ceiling((range.B-minX)/dx-.5));
                        for(int x=x0;x<x1;x++)for(int z=0;z<nz[k];z++)
                        {
                            if((x&127)==0)token.ThrowIfCancellationRequested();
                            var world=origin+u*(minX+(x+.5)*dx)+v*yy+normal*(low+(z+.5)*dz);
                            total+=weight;float value=dose.Sample(roiToDose.Transform(world));
                            if(float.IsNaN(value)||float.IsInfinity(value))continue;
                            if(value<0)return Fail(result,DvhStatus.Unsupported,"Negative dose values are not supported.");
                            covered+=weight;int bin=(int)Math.Min(Bins,Math.Floor(value/maxDose*Bins));histogram[bin]+=weight;
                        }
                    }
                }
            }
            result.EstimatedVolumeCc=total;result.SampledVolumeCc=covered;
            if(total==0)return Fail(result,DvhStatus.Empty,"Structure is smaller than the sampling grid or has no interior volume.");
            if(covered==0)return Fail(result,DvhStatus.PartialCoverage,"No sample points inside the dose grid; no curve.");
            result.DoseValues=new double[Bins+2];result.CumulativeVolumePercent=new double[Bins+2];double cumulative=0;
            for(int i=Bins;i>=0;i--){cumulative+=histogram[i];result.DoseValues[i]=maxDose*i/Bins;result.CumulativeVolumePercent[i]=100*cumulative/total;}
            result.DoseValues[Bins+1]=maxDose*(Bins+1)/Bins;
            result.Status=covered/total<.999999?DvhStatus.PartialCoverage:DvhStatus.Complete;
            result.Message=result.Status==DvhStatus.PartialCoverage?"Partial coverage: the curve is a lower bound; missing dose remains unknown.":"Approximate preview · contour slabs with half-spacing end caps · 256 dose intervals.";
            return result;
        }
        static bool Finite(double v)=>!double.IsNaN(v)&&!double.IsInfinity(v);
        static DvhResult Fail(DvhResult r,DvhStatus status,string text){r.Status=status;r.Message=text;return r;}
        static double Lower(List<Plane> p,int i)=>i==0?p[0].Z-(p[1].Z-p[0].Z)/2:(p[i-1].Z+p[i].Z)/2;
        static double Upper(List<Plane> p,int i)=>i==p.Count-1?p[i].Z+(p[i].Z-p[i-1].Z)/2:(p[i].Z+p[i+1].Z)/2;
        static List<Interval> Intervals(List<Polygon> polygons,double y,bool xor,CancellationToken token)
        {
            var spans=new List<Interval>();var all=new List<double>();
            foreach(var p in polygons)
            {
                var crossings=new List<double>();
                for(int i=0,j=p.X.Length-1;i<p.X.Length;j=i++)
                { if((i&4095)==0)token.ThrowIfCancellationRequested();if((p.Y[i]>y)!=(p.Y[j]>y))crossings.Add(p.X[i]+(p.X[j]-p.X[i])*(y-p.Y[i])/(p.Y[j]-p.Y[i])); }
                if(xor)all.AddRange(crossings);
                else {crossings.Sort();for(int i=0;i+1<crossings.Count;i+=2)spans.Add(new Interval(crossings[i],crossings[i+1]));}
            }
            if(xor){all.Sort();for(int i=0;i+1<all.Count;i+=2)spans.Add(new Interval(all[i],all[i+1]));return spans;}
            var merged=new List<Interval>();
            foreach(var s in spans.OrderBy(s=>s.A))
            {if(merged.Count==0||s.A>merged[merged.Count-1].B)merged.Add(s);else {var last=merged[merged.Count-1];last.B=Math.Max(last.B,s.B);merged[merged.Count-1]=last;}}
            return merged;
        }
    }
}
