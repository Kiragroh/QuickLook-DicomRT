using QuickLook.DicomRT;
using System.Collections.Concurrent;

sealed partial class PreviewSession
{
    ConcurrentDictionary<string,Lazy<ContourStack>> contourCache=new();
    List<WorldLine> ContourLines(StructureRoi roi,Matrix4 map,SliceGeometry geometry,out ReformatContours.OutlineKind kind)
    {
        var stack=contourCache.GetOrAdd(CacheKey(RoiKey(roi)),_=>new Lazy<ContourStack>(()=>ContourStack.Create(roi,map,token),LazyThreadSafetyMode.ExecutionAndPublication)).Value;
        if(stack!=null&&stack.Normal.Cross(geometry.Normal).Length<1e-7){kind=ReformatContours.OutlineKind.Original;return stack.At(stack.Normal.Dot(geometry.Center));}
        return ReformatContours.Outline(roi,map,geometry,.01,token,out kind);
    }
    sealed class ContourStack
    {
        public Vec3 Normal;double[] levels;List<WorldLine>[] lines;double typical;
        public static ContourStack Create(StructureRoi roi,Matrix4 map,CancellationToken token)
        {
            if(roi.Contours.Count==0||roi.Contours.Any(c=>c.Points.Count<3||c.GeometricType!=roi.Contours[0].GeometricType)||!(roi.Contours[0].GeometricType=="CLOSED_PLANAR"||roi.Contours[0].GeometricType=="CLOSEDPLANAR_XOR"))return null;
            var first=roi.Contours[0].Points.Select(map.Transform).ToArray();Vec3 normal=new();for(int i=0;i<first.Length;i++)normal+=first[i].Cross(first[(i+1)%first.Length]);if(normal.Length<1e-8)return null;normal=normal.Normalized();
            var groups=new SortedDictionary<double,List<WorldLine>>();
            foreach(var contour in roi.Contours){token.ThrowIfCancellationRequested();var points=contour.Points.Select(map.Transform).ToArray();double z=normal.Dot(points[0]);if(points.Any(p=>Math.Abs(normal.Dot(p)-z)>.01))return null;
                double level=groups.Keys.FirstOrDefault(k=>Math.Abs(k-z)<.001,double.NaN);if(double.IsNaN(level))level=z;if(!groups.TryGetValue(level,out var lines))groups[level]=lines=new();for(int i=0;i<points.Length;i++)lines.Add(new WorldLine(points[i],points[(i+1)%points.Length]));
            }
            var levels=groups.Keys.ToArray();var gaps=levels.Zip(levels.Skip(1),(a,b)=>b-a).Where(d=>d>.001).OrderBy(d=>d).ToArray();
            return new ContourStack{Normal=normal,levels=levels,lines=groups.Values.ToArray(),typical=gaps.Length==0?.02:gaps[(gaps.Length-1)/2]};
        }
        public List<WorldLine> At(double z)
        {
            int lo=0,hi=levels.Length-1;while(hi-lo>1){int mid=(hi+lo)/2;if(levels[mid]<z)lo=mid;else hi=mid;}
            int index=Math.Abs(levels[lo]-z)<=Math.Abs(levels[hi]-z)?lo:hi;
            double adjacent=z<levels[index]&&index>0?levels[index]-levels[index-1]:z>levels[index]&&index+1<levels.Length?levels[index+1]-levels[index]:typical;
            return Math.Abs(levels[index]-z)>Math.Max(.01,Math.Min(adjacent,typical)*.5)+1e-6?new List<WorldLine>():lines[index];
        }
    }
}
