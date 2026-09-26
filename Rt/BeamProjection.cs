using System;
using System.Threading;
using System.Threading.Tasks;

namespace QuickLook.DicomRT
{
    // Source-view IEC BLD coordinates at the isocentre, in a resolved image frame.
    // No default SAD or patient position: unsupported geometry is explicitly unavailable.
    public sealed class BeamProjection
    {
        public Vec3 Iso, Source, Right, Up, Forward;
        public double Sad;
        public static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
        public static bool IsRigid(Matrix4 map)
        {
            if(map==null)return false;
            var o=map.Transform(new Vec3());var x=map.Transform(new Vec3(1,0,0))-o;
            var y=map.Transform(new Vec3(0,1,0))-o;var z=map.Transform(new Vec3(0,0,1))-o;
            return Math.Abs(x.Length-1)<1e-5&&Math.Abs(y.Length-1)<1e-5&&Math.Abs(z.Length-1)<1e-5&&Math.Abs(x.Dot(y))<1e-5&&Math.Abs(x.Dot(z))<1e-5&&Math.Abs(y.Dot(z))<1e-5&&x.Cross(y).Dot(z)>.99999;
        }
        public static BeamProjection Create(PlanBeam beam,ControlPoint cp,Matrix4 planToImage,out string reason)
        {
            reason=null;
            if(beam==null||cp==null||!IsRigid(planToImage)){reason="No rigid plan-to-image association";return null;}
            var patient=PatientOrientation.ToIec(beam.PatientPosition);
            if(patient==null){reason="Patient position unavailable / unsupported";return null;}
            if(!Finite(beam.SourceAxisDistance)||beam.SourceAxisDistance<=0){reason="Source-axis distance unavailable";return null;}
            if(!Finite(cp.Gantry)||!Finite(cp.Couch)||!Finite(cp.Collimator)||!Finite(cp.Isocenter.X)||!Finite(cp.Isocenter.Y)||!Finite(cp.Isocenter.Z)){reason="Incomplete beam geometry";return null;}
            if(!Finite(cp.GantryPitch)||!Finite(cp.TablePitch)||!Finite(cp.TableRoll)||!Finite(cp.TableEccentric)||Math.Abs(cp.GantryPitch)+Math.Abs(cp.TablePitch)+Math.Abs(cp.TableRoll)+Math.Abs(cp.TableEccentric)>1e-6){reason="Pitch / roll / eccentric rotation not supported in DRR";return null;}
            double g=cp.Gantry*Math.PI/180,c=cp.Couch*Math.PI/180,k=cp.Collimator*Math.PI/180;
            // Undo patient-support rotation, then convert IEC FIXED to DICOM LPS.
            var inverse=patient.Inverse();var zero=planToImage.Transform(new Vec3());
            Func<Vec3,Vec3> toImage=v=>{
                var couch=new Vec3(v.X*Math.Cos(c)+v.Y*Math.Sin(c),-v.X*Math.Sin(c)+v.Y*Math.Cos(c),v.Z);
                return planToImage.Transform(inverse.Transform(couch))-zero;
            };
            var sourceAxis=toImage(new Vec3(Math.Sin(g),0,Math.Cos(g)));
            var right=toImage(new Vec3(Math.Cos(g),0,-Math.Sin(g)));var up=toImage(new Vec3(0,1,0));
            var iso=planToImage.Transform(cp.Isocenter);
            return new BeamProjection {Iso=iso,Source=iso+sourceAxis*beam.SourceAxisDistance,Forward=sourceAxis*-1,
                Right=right*Math.Cos(k)+up*Math.Sin(k),Up=up*Math.Cos(k)-right*Math.Sin(k),Sad=beam.SourceAxisDistance};
        }
        public Vec3 PlanePoint(double x,double y)=>Iso+Right*x+Up*y;
        public bool Project(Vec3 p,out double x,out double y)
        {
            var d=p-Source;double depth=d.Dot(Forward);x=y=double.NaN;
            if(depth<=1e-6)return false;
            x=d.Dot(Right)*Sad/depth;y=d.Dot(Up)*Sad/depth;return Finite(x)&&Finite(y);
        }
        public static Vec3 Voxel(VolumeData v,Vec3 p)
        {var d=p-v.Origin;return new Vec3(d.Dot(v.AxisX)/v.SpacingX,d.Dot(v.AxisY)/v.SpacingY,d.Dot(v.AxisZ)/v.SpacingZ);}
        static bool Slab(double s,double d,double max,ref double lo,ref double hi)
        {
            if(Math.Abs(d)<1e-12)return s>=0&&s<=max;
            double a=-s/d,b=(max-s)/d;if(a>b){double t=a;a=b;b=t;}lo=Math.Max(lo,a);hi=Math.Min(hi,b);return hi>lo;
        }
        public static bool Intersect(VolumeData v,Vec3 source,Vec3 unit,out double lo,out double hi)
        {
            var s=Voxel(v,source);var d=Voxel(v,source+unit)-s;lo=0;hi=double.PositiveInfinity;
            return Slab(s.X,d.X,v.Width-1,ref lo,ref hi)&&Slab(s.Y,d.Y,v.Height-1,ref lo,ref hi)&&Slab(s.Z,d.Z,v.Depth-1,ref lo,ref hi);
        }
        // HU-derived water-equivalent line integral; a display DRR, not a calibrated portal image.
        public float[] Integrate(VolumeData volume,double extent,int size,double step,CancellationToken token)
        {
            if(volume==null||size<2||!Finite(extent)||extent<=0||!Finite(step)||step<=0)throw new ArgumentException("Invalid projection request");
            var result=new float[size*size];var source=Voxel(volume,Source);
            Parallel.For(0,size,new ParallelOptions{CancellationToken=token,MaxDegreeOfParallelism=Math.Max(1,Math.Min(4,Environment.ProcessorCount-1))},y=>{
                for(int x=0;x<size;x++)
                {
                    if((x&15)==0)token.ThrowIfCancellationRequested();
                    var direction=(PlanePoint(((x+.5)/size*2-1)*extent,(1-(y+.5)/size*2)*extent)-Source).Normalized();
                    double lo,hi;if(!Intersect(volume,Source,direction,out lo,out hi))continue;
                    var delta=Voxel(volume,Source+direction)-source;int n=Math.Max(1,(int)Math.Ceiling((hi-lo)/step));double ds=(hi-lo)/n;
                    var p=source+delta*(lo+.5*ds);var inc=delta*ds;double sum=0;
                    for(int j=0;j<n;j++,p=p+inc){float hu=SampleVoxel(volume,p.X,p.Y,p.Z);if(Finite(hu))sum+=Math.Max(0,1+hu/1000.0)*ds;}
                    result[y*size+x]=(float)sum;
                }
            });return result;
        }
        static float SampleVoxel(VolumeData v,double x,double y,double z)
        {
            x=Math.Max(0,Math.Min(v.Width-1,x));y=Math.Max(0,Math.Min(v.Height-1,y));z=Math.Max(0,Math.Min(v.Depth-1,z));
            int a=(int)x,b=(int)y,c=(int)z,aa=Math.Min(a+1,v.Width-1),bb=Math.Min(b+1,v.Height-1),cc=Math.Min(c+1,v.Depth-1);
            double fx=x-a,fy=y-b,fz=z-c;int p=(c*v.Height+b)*v.Width,q=(c*v.Height+bb)*v.Width,r=(cc*v.Height+b)*v.Width,s=(cc*v.Height+bb)*v.Width;
            return (float)(((v.Values[p+a]*(1-fx)+v.Values[p+aa]*fx)*(1-fy)+(v.Values[q+a]*(1-fx)+v.Values[q+aa]*fx)*fy)*(1-fz)+((v.Values[r+a]*(1-fx)+v.Values[r+aa]*fx)*(1-fy)+(v.Values[s+a]*(1-fx)+v.Values[s+aa]*fx)*fy)*fz);
        }
    }
}
