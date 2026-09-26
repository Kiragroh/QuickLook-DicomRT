using System.Collections.Generic;
using System;
using System.Linq;
using Dicom;
namespace QuickLook.DicomRT
{
    public sealed class Contour { public List<Vec3> Points=new List<Vec3>(); public string GeometricType; public List<string> ReferencedSops=new List<string>(); }
    public sealed class StructureRoi { public int Number; public string Name,FrameUid,InterpretedType; public byte Red,Green,Blue; public List<Contour> Contours=new List<Contour>(); public Vec3 Center; public bool Visible=true; }
    public sealed class StructureSet
    {
        public DicomEntry Entry; public List<StructureRoi> Rois=new List<StructureRoi>(); public HashSet<string> ReferencedSeries=new HashSet<string>();
        public static StructureSet Load(DicomEntry entry)
        {
            var d=RtDicom.Full(entry); var result=new StructureSet {Entry=entry};
            var rois=new Dictionary<int,StructureRoi>();
            foreach(var item in RtDicom.Items(d,DicomTag.StructureSetROISequence))
            {
                int n=RtDicom.Int(item,DicomTag.ROINumber,-1); if(n<0 || rois.ContainsKey(n)) continue;
                var roi=new StructureRoi {Number=n,Name=RtDicom.Text(item,DicomTag.ROIName,"ROI "+n),FrameUid=RtDicom.Text(item,DicomTag.ReferencedFrameOfReferenceUID),Red=255,Green=210,Blue=70};
                rois.Add(n,roi); result.Rois.Add(roi);
            }
            foreach(var observation in RtDicom.Items(d,DicomTag.RTROIObservationsSequence))
            {
                StructureRoi roi;if(rois.TryGetValue(RtDicom.Int(observation,DicomTag.ReferencedROINumber,-1),out roi))
                    roi.InterpretedType=RtDicom.Text(observation,DicomTag.RTROIInterpretedType).Trim().ToUpperInvariant();
            }
            foreach(var frame in RtDicom.Items(d,DicomTag.ReferencedFrameOfReferenceSequence))
                foreach(var study in RtDicom.Items(frame,DicomTag.RTReferencedStudySequence))
                    foreach(var series in RtDicom.Items(study,DicomTag.RTReferencedSeriesSequence))
                    { string uid=RtDicom.Text(series,DicomTag.SeriesInstanceUID); if(uid.Length>0) result.ReferencedSeries.Add(uid); }
            foreach(var item in RtDicom.Items(d,DicomTag.ROIContourSequence))
            {
                StructureRoi roi; if(!rois.TryGetValue(RtDicom.Int(item,DicomTag.ReferencedROINumber,-1),out roi)) continue;
                var color=RtDicom.Numbers(item,DicomTag.ROIDisplayColor);
                if(color.Length==3 && color.All(v=>RtDicom.Finite(v))) {roi.Red=(byte)Math.Max(0,Math.Min(255,color[0]));roi.Green=(byte)Math.Max(0,Math.Min(255,color[1]));roi.Blue=(byte)Math.Max(0,Math.Min(255,color[2]));}
                foreach(var contour in RtDicom.Items(item,DicomTag.ContourSequence))
                {
                    var data=RtDicom.Numbers(contour,DicomTag.ContourData);
                    if(data.Length==0 || data.Length%3!=0 || data.Any(v=>!RtDicom.Finite(v))) continue;
                    int count=RtDicom.Int(contour,DicomTag.NumberOfContourPoints,data.Length/3); if(count!=data.Length/3) continue;
                    var c=new Contour {GeometricType=RtDicom.Text(contour,DicomTag.ContourGeometricType)};
                    for(int i=0;i<data.Length;i+=3) c.Points.Add(new Vec3(data[i],data[i+1],data[i+2]));
                    foreach(var image in RtDicom.Items(contour,DicomTag.ContourImageSequence)) {string uid=RtDicom.Text(image,DicomTag.ReferencedSOPInstanceUID);if(uid.Length>0)c.ReferencedSops.Add(uid);}
                    roi.Contours.Add(c);
                }
            }
            foreach(var roi in result.Rois) roi.Center=JumpPoint(roi.Contours);
            return result;
        }
        static Vec3 Mean(List<Vec3> points) {var sum=new Vec3(); foreach(var p in points)sum+=p;return sum/points.Count;}
        // Select an actual component near the overall center. A point on its contour
        // stays meaningful for concave contours, holes, and disconnected components.
        static Vec3 JumpPoint(List<Contour> contours)
        {
            var candidates=contours.Where(c=>c.Points.Count>0).ToArray();
            if(candidates.Length==0)return new Vec3(double.NaN,double.NaN,double.NaN);
            var means=candidates.Select(c=>Mean(c.Points)).ToArray(); var global=Mean(means.ToList());
            int index=0; for(int i=1;i<candidates.Length;i++)if((means[i]-global).Length<(means[index]-global).Length)index=i;
            var points=candidates[index].Points; var center=means[index];
            bool closed=candidates[index].GeometricType=="CLOSED_PLANAR" || candidates[index].GeometricType=="CLOSEDPLANAR_XOR";
            if(closed && points.Count>=3)
            {
                var normal=new Vec3(); for(int i=0;i<points.Count;i++)normal+=points[i].Cross(points[(i+1)%points.Count]);
                if(normal.Length>1e-9)
                {
                    normal=normal.Normalized(); int axis=Math.Abs(normal.X)>Math.Abs(normal.Y)?0:1;
                    if(Math.Abs(normal.Z)>(axis==0?Math.Abs(normal.X):Math.Abs(normal.Y)))axis=2;
                    // Accept an interior mean only if it does not fall into another
                    // coplanar contour (a potential hole). Otherwise use the surface.
                    bool inside=Inside(points,center,axis);
                    for(int i=0;i<candidates.Length && inside;i++)if(i!=index && candidates[i].Points.Count>=3 && candidates[i].Points.All(p=>Math.Abs((p-center).Dot(normal))<1e-3) && Inside(candidates[i].Points,center,axis))inside=false;
                    if(inside)return center;
                }
            }
            var best=points[0]; double distance=double.PositiveInfinity;
            for(int i=0;i<(closed?points.Count:Math.Max(1,points.Count-1));i++)
            {
                // Boundary midpoint is guaranteed to belong to the represented ROI surface.
                var p=points.Count==1?points[0]:(points[i]+points[(i+1)%points.Count])/2;
                double delta=(p-center).Length; if(delta<distance){best=p;distance=delta;}
            }
            return best;
        }
        static double U(Vec3 p,int axis)=>axis==0?p.Y:p.X;
        static double V(Vec3 p,int axis)=>axis==2?p.Y:p.Z;
        static bool Inside(List<Vec3> points,Vec3 p,int axis)
        {
            bool inside=false; double x=U(p,axis),y=V(p,axis);
            for(int i=0,j=points.Count-1;i<points.Count;j=i++)
            {
                double xi=U(points[i],axis),yi=V(points[i],axis),xj=U(points[j],axis),yj=V(points[j],axis);
                if((yi>y)!=(yj>y) && x<(xj-xi)*(y-yi)/(yj-yi)+xi)inside=!inside;
            }
            return inside;
        }
    }
}
