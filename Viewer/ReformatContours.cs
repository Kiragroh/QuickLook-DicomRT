using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace QuickLook.DicomRT
{
 // Reformat the contour stack as a signed interval field. Draw its boundary,
 // not every axial chord through its interior. Coplanar originals stay unchanged,
 // including native images whose plane differs from the RTSTRUCT stack.
 public static class ReformatContours
 {
  public enum OutlineKind { Original, Interpolated, IntersectionFallback }
  sealed class Row {public double H;public List<Tuple<double,double>> Intervals=new List<Tuple<double,double>>();}
  public static List<WorldLine> Outline(StructureRoi roi,Matrix4 map,SliceGeometry g,double tolerance,CancellationToken token)
  {OutlineKind kind;return Outline(roi,map,g,tolerance,token,out kind);}
  public static List<WorldLine> Outline(StructureRoi roi,Matrix4 map,SliceGeometry g,double tolerance,CancellationToken token,out OutlineKind kind)
  {
   kind=OutlineKind.IntersectionFallback;
   token.ThrowIfCancellationRequested();
   if(roi?.Contours==null)return new List<WorldLine>();
   var loops=roi.Contours.Where(c=>c.Points!=null&&c.Points.Count>=3&&(c.GeometricType=="CLOSED_PLANAR"||c.GeometricType=="CLOSEDPLANAR_XOR")).ToArray();
   if(loops.Length==0||loops.Length!=roi.Contours.Count||loops.Any(c=>c.GeometricType!=loops[0].GeometricType))return SliceGeometry.ContourLines(roi,map,g,tolerance);
   Vec3 normal=new Vec3();var first=loops[0].Points.Select(map.Transform).ToArray();
   for(int i=0;i<first.Length;i++)normal+=first[i].Cross(first[(i+1)%first.Length]);
   if(normal.Length<1e-8)return SliceGeometry.ContourLines(roi,map,g,tolerance);normal=normal.Normalized();
   var viewNormal=g.Normal;
   if(normal.Cross(viewNormal).Length<1e-7){
    kind=OutlineKind.Original;
    var levels=loops.Select(c=>normal.Dot(map.Transform(c.Points[0]))).Distinct().OrderBy(z=>z).ToArray();
    double z=normal.Dot(g.Center);int nearest=Enumerable.Range(0,levels.Length).OrderBy(i=>Math.Abs(levels[i]-z)).First();
    double local=tolerance;
    if(levels.Length>1){var localGaps=levels.Zip(levels.Skip(1),(a,b)=>b-a).Where(d=>d>.001).OrderBy(d=>d).ToArray();if(localGaps.Length>0){double localTypical=localGaps[(localGaps.Length-1)/2];double adjacent=z<levels[nearest]&&nearest>0?levels[nearest]-levels[nearest-1]:z>levels[nearest]&&nearest+1<levels.Length?levels[nearest+1]-levels[nearest]:localTypical;local=Math.Max(local,Math.Min(adjacent,localTypical)*.5);}}
    if(Math.Abs(levels[nearest]-z)>local+1e-6)return new List<WorldLine>();
    var selected=new StructureRoi{Contours=loops.Where(c=>Math.Abs(normal.Dot(map.Transform(c.Points[0]))-levels[nearest])<.001).ToList()};
    return SliceGeometry.ContourLines(selected,map,g,Math.Abs(levels[nearest]-z)+.001);
   }
   if(loops.Length<2)return SliceGeometry.ContourLines(roi,map,g,tolerance);
   var axis=normal.Cross(viewNormal).Normalized();var stack=viewNormal.Cross(axis).Normalized();if(stack.Dot(normal)<0)stack=stack*-1;
   double scale=normal.Dot(stack);var rows=new List<Row>();bool xor=loops.Any(c=>c.GeometricType=="CLOSEDPLANAR_XOR");
   foreach(var loop in loops)
   {
    token.ThrowIfCancellationRequested();var p=loop.Points.Select(map.Transform).ToArray();double plane=normal.Dot(p[0]);
    if(p.Any(q=>Math.Abs(normal.Dot(q)-plane)>.05))return SliceGeometry.ContourLines(roi,map,g,tolerance);
    double h=(plane-normal.Dot(g.Center))/scale;var row=rows.FirstOrDefault(r=>Math.Abs(r.H-h)<.001);
    if(row==null){row=new Row{H=h};rows.Add(row);}var cuts=new List<double>();
    for(int i=0;i<p.Length;i++)
    {var a=p[i];var b=p[(i+1)%p.Length];double da=(a-g.Center).Dot(viewNormal),db=(b-g.Center).Dot(viewNormal);
     if((da<=0&&db>0)||(db<=0&&da>0)){var q=a+(b-a)*(da/(da-db));cuts.Add((q-g.Center).Dot(axis));}}
    cuts.Sort();for(int i=0;i+1<cuts.Count;i+=2)if(cuts[i+1]-cuts[i]>1e-7)row.Intervals.Add(Tuple.Create(cuts[i],cuts[i+1]));
   }
   rows=rows.OrderBy(r=>r.H).ToList();if(rows.Count<2)return SliceGeometry.ContourLines(roi,map,g,tolerance);
   kind=OutlineKind.Interpolated;
   foreach(var row in rows)row.Intervals=Combine(row.Intervals,xor);
   var intervals=rows.SelectMany(r=>r.Intervals).ToArray();if(intervals.Length==0)return new List<WorldLine>();
   var gaps=Enumerable.Range(1,rows.Count-1).Select(i=>rows[i].H-rows[i-1].H).OrderBy(x=>x).ToArray();double typical=gaps[(gaps.Length-1)/2];
   double lo=intervals.Min(x=>x.Item1),hi=intervals.Max(x=>x.Item2),bottom=rows[0].H-Math.Min(typical,rows[1].H-rows[0].H)*.5,top=rows.Last().H+Math.Min(typical,rows.Last().H-rows[rows.Count-2].H)*.5;
   double pitch=Math.Max(.15,Math.Min(g.WidthMm,g.HeightMm)/512);pitch=Math.Max(pitch,Math.Max(hi-lo,top-bottom)/192);
   lo-=pitch*2;hi+=pitch*2;bottom-=pitch*2;top+=pitch*2;int w=(int)Math.Ceiling((hi-lo)/pitch)+1,hgt=(int)Math.Ceiling((top-bottom)/pitch)+1;
   var values=new double[w*hgt];double empty=-Math.Max(hi-lo,top-bottom);int lower=0;
   for(int y=0;y<hgt;y++)
   {
    token.ThrowIfCancellationRequested();double yy=bottom+y*pitch;while(lower+1<rows.Count&&rows[lower+1].H<yy)lower++;
    for(int x=0;x<w;x++)
    {
     double xx=lo+x*pitch,value;
     if(yy<rows[0].H)value=Math.Min(Distance(rows[0],xx,empty),yy-(rows[0].H-Math.Min(typical,rows[1].H-rows[0].H)*.5));
     else if(lower==rows.Count-1)value=Math.Min(Distance(rows[lower],xx,empty),rows[lower].H+Math.Min(typical,rows[lower].H-rows[lower-1].H)*.5-yy);
     else
     {
      var a=rows[lower];var b=rows[lower+1];double span=b.H-a.H,t=(yy-a.H)/span;
      if(span>typical*2.1)value=Math.Max(Math.Min(Distance(a,xx,empty),a.H+typical*.5-yy),Math.Min(Distance(b,xx,empty),yy-b.H+typical*.5));
      // An empty intersection row has no lateral distance. Cap its neighbour at
      // the midpoint rather than blending an arbitrary large negative sentinel.
      else if(a.Intervals.Count==0&&b.Intervals.Count==0)value=empty;
      else if(a.Intervals.Count==0)value=Math.Min(Distance(b,xx,empty),yy-(a.H+b.H)*.5);
      else if(b.Intervals.Count==0)value=Math.Min(Distance(a,xx,empty),(a.H+b.H)*.5-yy);
      else value=Distance(a,xx,empty)*(1-t)+Distance(b,xx,empty)*t;
     }
     values[y*w+x]=value;
    }
   }
   var lines=new List<WorldLine>();var v=new double[4];var px=new double[4];var py=new double[4];var points=new Vec3[4];
   for(int y=0;y<hgt-1;y++){token.ThrowIfCancellationRequested();for(int x=0;x<w-1;x++)
   {
    v[0]=values[y*w+x];v[1]=values[y*w+x+1];v[2]=values[(y+1)*w+x+1];v[3]=values[(y+1)*w+x];
    if((v[0]<=0&&v[1]<=0&&v[2]<=0&&v[3]<=0)||(v[0]>0&&v[1]>0&&v[2]>0&&v[3]>0))continue;
    px[0]=px[3]=lo+x*pitch;px[1]=px[2]=lo+(x+1)*pitch;py[0]=py[1]=bottom+y*pitch;py[2]=py[3]=bottom+(y+1)*pitch;
    int count=0;for(int i=0;i<4;i++){int j=(i+1)%4;if((v[i]<=0&&v[j]>0)||(v[j]<=0&&v[i]>0)){double t=v[i]/(v[i]-v[j]);points[count++]=g.Center+axis*(px[i]+t*(px[j]-px[i]))+stack*(py[i]+t*(py[j]-py[i]));}}
    // Asymptotic saddle decision keeps diagonal islands from connecting arbitrarily.
    if(count==4&&v[0]*v[2]-v[1]*v[3]<0){var last=points[3];points[3]=points[2];points[2]=points[1];points[1]=points[0];points[0]=last;}
    for(int i=0;i+1<count;i+=2)if((points[i+1]-points[i]).Length>1e-9)lines.Add(new WorldLine(points[i],points[i+1]));
   }}
   return lines;
  }
  static double Distance(Row row,double x,double empty)
  {if(row.Intervals.Count==0)return empty;double distance=double.MaxValue;bool inside=false;foreach(var pair in row.Intervals){distance=Math.Min(distance,Math.Min(Math.Abs(x-pair.Item1),Math.Abs(x-pair.Item2)));if(x>=pair.Item1&&x<=pair.Item2)inside=true;}return inside?distance:-distance;}
  static List<Tuple<double,double>> Combine(List<Tuple<double,double>> input,bool xor)
  {
   var events=input.SelectMany(p=>new[]{Tuple.Create(p.Item1,1),Tuple.Create(p.Item2,-1)}).GroupBy(p=>p.Item1).OrderBy(g=>g.Key);
   var output=new List<Tuple<double,double>>();int count=0;double start=0;
   foreach(var e in events){bool before=xor?count%2!=0:count>0;count+=e.Sum(p=>p.Item2);bool after=xor?count%2!=0:count>0;if(!before&&after)start=e.Key;if(before&&!after)output.Add(Tuple.Create(start,e.Key));}return output;
  }
 }
}
