using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace QuickLook.DicomRT
{
 public sealed class RoiOverlay { public StructureRoi Roi {get;set;} public Matrix4 RoiToImage {get;set;} = Matrix4.Identity; }
 public sealed class DoseOverlay { public DoseGrid Dose {get;set;} public Matrix4 ImageToDose {get;set;} = Matrix4.Identity; }
 public sealed class RenderScene
 {
  public bool InteractionPreview;public PlanData Plan;public Matrix4 PlanToImage;public PlanBeam ActiveBeam;public ControlPoint ActiveControlPoint;public bool ShowFields;
  public VolumeData Volume {get;set;} public PixelPlane Native {get;set;} public DicomEntry Entry {get;set;}
  public string Plane {get;set;} = "Native"; public Vec3 Focus {get;set;} public Vec3? ViewCenter {get;set;}
  public double WindowCenter {get;set;} public double WindowWidth {get;set;} = 400; public double Zoom {get;set;} = 1;
  public VolumeData OverlayVolume {get;set;} public Matrix4 ImageToOverlay {get;set;} = Matrix4.Identity;
  public double OverlayOpacity {get;set;} = .5; public double OverlayWindowCenter {get;set;} public double OverlayWindowWidth {get;set;} = 400;
  public List<RoiOverlay> Structures {get;set;} = new List<RoiOverlay>(); public List<DoseOverlay> Doses {get;set;} = new List<DoseOverlay>();
  public IReadOnlyList<Vec3> Isocenters {get;set;} = new Vec3[0];
  public bool Crosshair {get;set;} = true; public bool Isodoses {get;set;} public double DoseOpacity {get;set;} = .35;
  public bool DoseWash {get;set;} = true; public double DoseMinimumPercent {get;set;} = 5; public double DoseMaximumPercent {get;set;} = 100;
  public double[] IsoLevels {get;set;} = new double[]{10,20,30,40,50,60,70,80,90,100};
  public bool AbsoluteIsodoses {get;set;} public double IsoColorMaximum {get;set;} = 100;
  public Dictionary<double,int> IsoColors {get;set;} = new Dictionary<double,int>();
  internal RenderScene Snapshot() { var s=(RenderScene)MemberwiseClone();s.Structures=(Structures??new List<RoiOverlay>()).Where(x=>x!=null).Select(x=>new RoiOverlay{Roi=x.Roi,RoiToImage=x.RoiToImage}).ToList();s.Doses=(Doses??new List<DoseOverlay>()).Where(x=>x!=null).Select(x=>new DoseOverlay{Dose=x.Dose,ImageToDose=x.ImageToDose}).ToList();s.Isocenters=(Isocenters??new Vec3[0]).ToArray();s.IsoLevels=(double[])(IsoLevels??new double[0]).Clone();s.IsoColors=new Dictionary<double,int>(IsoColors??new Dictionary<double,int>());return s; }
  // A pending scroll keeps the complete previous frame, including its own geometry and labels.
  internal bool SameImageSource(RenderScene other)
  {
   if(other==null||Plane!=other.Plane)return false;
   if(Entry!=null&&other.Entry!=null&&!string.IsNullOrEmpty(Entry.SeriesUid)&&!string.IsNullOrEmpty(other.Entry.SeriesUid))
    return Entry.SeriesUid==other.Entry.SeriesUid&&Entry.StudyUid==other.Entry.StudyUid&&Entry.FrameUid==other.Entry.FrameUid&&Entry.PatientKey==other.Entry.PatientKey;
   if(Volume!=null||other.Volume!=null)return ReferenceEquals(Volume,other.Volume);
   return ReferenceEquals(Entry,other.Entry)&&ReferenceEquals(Native,other.Native);
  }
 }
 public struct WorldLine { public Vec3 A,B; public double DosePercent; public double DoseLevel => DosePercent; public WorldLine(Vec3 a,Vec3 b,double dosePercent=double.NaN){A=a;B=b;DosePercent=dosePercent;} }
 public sealed class SliceGeometry
 {
  public Vec3 Center {get;private set;} public Vec3 Right {get;private set;} public Vec3 Down {get;private set;}
  public Vec3 Normal => Right.Cross(Down).Normalized(); public double WidthMm {get;private set;} public double HeightMm {get;private set;}
  public Vec3 WorldAt(double u,double v) => Center+Right*((u-.5)*WidthMm)+Down*((v-.5)*HeightMm);
  public double U(Vec3 p) => .5+(p-Center).Dot(Right)/WidthMm;
  public double V(Vec3 p) => .5+(p-Center).Dot(Down)/HeightMm;
  public static SliceGeometry Create(RenderScene s)
  {
   var g=new SliceGeometry(); var zoom=Math.Max(.1,Math.Min(20,s.Zoom));
   if(s.Plane=="Native"&&s.Entry!=null&&s.Native!=null)
   {
    var e=s.Entry;g.Right=e.HasGeometry?e.AxisX.Normalized():new Vec3(1,0,0);g.Down=e.HasGeometry?e.AxisY.Normalized():new Vec3(0,1,0);
    var sx=e.SpacingX>0?e.SpacingX:1;var sy=e.SpacingY>0?e.SpacingY:1;
    g.Center=e.Origin+g.Right*((s.Native.Width-1)*sx*.5)+g.Down*((s.Native.Height-1)*sy*.5);
    if(zoom>1&&e.HasGeometry){var delta=s.Focus-g.Center;g.Center=g.Center+g.Right*delta.Dot(g.Right)+g.Down*delta.Dot(g.Down);}
    g.WidthMm=s.Native.Width*sx/zoom;g.HeightMm=s.Native.Height*sy/zoom;
   }
   else if(s.Volume!=null)
   {
    g.Center=s.Focus;
    g.Right=s.Plane=="Sagittal"?new Vec3(0,1,0):new Vec3(1,0,0);
    g.Down=s.Plane=="Axial"?new Vec3(0,1,0):new Vec3(0,0,-1);
    var v=s.Volume;double minU=double.PositiveInfinity,maxU=double.NegativeInfinity,minV=minU,maxV=maxU;
    foreach(double x in new[]{-.5,v.Width-.5})foreach(double y in new[]{-.5,v.Height-.5})foreach(double z in new[]{-.5,v.Depth-.5})
    {var p=v.WorldAt(x,y,z);var u=p.Dot(g.Right);var h=p.Dot(g.Down);minU=Math.Min(minU,u);maxU=Math.Max(maxU,u);minV=Math.Min(minV,h);maxV=Math.Max(maxV,h);}
    g.WidthMm=Math.Max(.01,maxU-minU)/zoom;g.HeightMm=Math.Max(.01,maxV-minV)/zoom;
   }
   else {g.Center=s.Focus;g.Right=new Vec3(1,0,0);g.Down=new Vec3(0,1,0);g.WidthMm=g.HeightMm=1;}
   if(s.ViewCenter.HasValue&&(s.Plane!="Native"||zoom>1)){var delta=s.ViewCenter.Value-g.Center;g.Center=g.Center+g.Right*delta.Dot(g.Right)+g.Down*delta.Dot(g.Down);}
   return g;
  }
  public static string Direction(Vec3 v)
  {
   var a=new[]{Tuple.Create(Math.Abs(v.X),v.X>=0?"L":"R"),Tuple.Create(Math.Abs(v.Y),v.Y>=0?"P":"A"),Tuple.Create(Math.Abs(v.Z),v.Z>=0?"S":"I")};
   return string.Concat(a.OrderByDescending(x=>x.Item1).Where(x=>x.Item1>.15).Select(x=>x.Item2));
  }
  // Closed planar loops intersected with the displayed plane. No surface is inferred between slices.
  public static List<WorldLine> ContourLines(StructureRoi roi,Matrix4 transform,SliceGeometry g,double tolerance)
  {
   var result=new List<WorldLine>();if(roi?.Contours==null)return result;var normal=g.Normal;
   foreach(var contour in roi.Contours)
   {
    if(contour.Points==null||contour.Points.Count<2)continue;
    var p=new Vec3[contour.Points.Count];var d=new double[p.Length];double min=double.PositiveInfinity,max=double.NegativeInfinity;
    for(int i=0;i<p.Length;i++){p[i]=transform.Transform(contour.Points[i]);d[i]=(p[i]-g.Center).Dot(normal);min=Math.Min(min,d[i]);max=Math.Max(max,d[i]);}
    var closed=contour.GeometricType=="CLOSED_PLANAR"||contour.GeometricType=="CLOSEDPLANAR_XOR";
    var count=closed?p.Length:p.Length-1;
    if(min>=-tolerance&&max<=tolerance) {for(int i=0;i<count;i++)result.Add(new WorldLine(p[i],p[(i+1)%p.Length]));continue;}
    if(min>0||max<0)continue;
    if(!closed)continue;
    var crossings=new List<Vec3>();
    for(int i=0;i<p.Length;i++)
    {
     int j=(i+1)%p.Length;
     if(Math.Abs(d[i])<1e-7&&Math.Abs(d[j])<1e-7){result.Add(new WorldLine(p[i],p[j]));continue;}
     if((d[i]<=0&&d[j]>0)||(d[j]<=0&&d[i]>0))
     {var q=p[i]+(p[j]-p[i])*(d[i]/(d[i]-d[j]));if(!crossings.Any(x=>(x-q).Length<1e-5))crossings.Add(q);}
    }
    if(crossings.Count<2)continue;
    var axis=(crossings[1]-crossings[0]).Normalized();crossings.Sort((a,b)=>a.Dot(axis).CompareTo(b.Dot(axis)));
    for(int i=0;i+1<crossings.Count;i+=2)if((crossings[i+1]-crossings[i]).Length>1e-6)result.Add(new WorldLine(crossings[i],crossings[i+1]));
   }
   return result;
  }
 }
 public sealed class SlicePixels
 {
  public int Width,Height;public byte[] Pixels;public SliceGeometry Geometry;public List<WorldLine> Isolines=new List<WorldLine>();
 }
 public static class IsodoseConfiguration
 {
  public static bool IsPhysicalGy(DoseGrid dose) => dose!=null&&string.Equals(dose.Units,"GY",StringComparison.OrdinalIgnoreCase)&&(string.IsNullOrEmpty(dose.DoseType)||string.Equals(dose.DoseType,"PHYSICAL",StringComparison.OrdinalIgnoreCase));
  public static double[] AutomaticLevels(double maximum,bool absolute)
  {
   if(!absolute)return Enumerable.Range(1,10).Select(i=>i*10d).ToArray();
   if(double.IsNaN(maximum)||double.IsInfinity(maximum)||maximum<=1)return new double[0];
   double target=Math.Max(1,maximum/14),power=Math.Pow(10,Math.Floor(Math.Log10(target)));
   double step=new[]{1d,2d,5d,10d}.Select(n=>n*power).First(n=>n>=target);
   int count=Math.Min(24,(int)Math.Ceiling(maximum/step)-1);
   return Enumerable.Range(1,Math.Max(0,count)).Select(i=>i*step).ToArray();
  }
  public static bool TryParse(string text,bool absolute,out double[] levels,out string error)
  {
   levels=null;error="Enter 1 to 24 positive "+(absolute?"Gy":"percentage")+" levels, at most 2 decimal places, separated by semicolons (e.g. 2; 4.25).";
   var parts=(text??"").Split(new[]{';',' ','\t','\r','\n'},StringSplitOptions.RemoveEmptyEntries);
   if(parts.Length<1||parts.Length>24)return false;
   var result=new List<double>();
   foreach(var part in parts)
   {
    double value;
    if(!System.Text.RegularExpressions.Regex.IsMatch(part,@"^\d+(\.\d{1,2})?$")||!double.TryParse(part,System.Globalization.NumberStyles.AllowDecimalPoint,System.Globalization.CultureInfo.InvariantCulture,out value)||double.IsInfinity(value)||double.IsNaN(value)||value<=0||(!absolute&&value>100))return false;
    result.Add(value);
   }
   levels=result.Distinct().OrderBy(x=>x).ToArray();error=null;return true;
  }
 }
 public static class SliceRaster
 {
  public static double[] IsodoseLevels(RenderScene s) => (s.IsoLevels??new double[0]).Where(x=>!double.IsNaN(x)&&!double.IsInfinity(x)&&x>0&&(s.AbsoluteIsodoses||x<=100)).Distinct().OrderBy(x=>x).ToArray();
  // Colorwash remains a percentage of each grid maximum; isodose levels may instead be absolute Gy.
  public static void DoseColor(double percent,out double red,out double green,out double blue)
  {double t=Math.Max(0,Math.Min(1,percent/100));red=255*Math.Min(1,2*t);green=255*Math.Max(0,1-Math.Abs(2*t-1));blue=255*Math.Max(0,1-2*t);}
  public static void IsodoseColor(RenderScene scene,double percent,out double red,out double green,out double blue)
  {int color;if(scene?.IsoColors!=null&&scene.IsoColors.TryGetValue(percent,out color)){red=(color>>16)&255;green=(color>>8)&255;blue=color&255;}else DoseColor(scene?.AbsoluteIsodoses==true?percent/Math.Max(.0001,scene.IsoColorMaximum)*100:percent,out red,out green,out blue);}
  public static byte Window(float value,double center,double width,bool invert)
  {if(float.IsNaN(value))return 0;double t=Math.Max(0,Math.Min(1,(value-center)/Math.Max(1,width)+.5));if(invert)t=1-t;return (byte)Math.Round(t*255);}
  public static float SampleImage(RenderScene s,Vec3 p)
  {
   if(s.Plane!="Native")return s.Volume==null?float.NaN:s.Volume.Sample(p);
   if(s.Native==null||s.Entry==null)return float.NaN;
   var e=s.Entry;var n=s.Native;var dx=e.HasGeometry?e.AxisX.Normalized():new Vec3(1,0,0);var dy=e.HasGeometry?e.AxisY.Normalized():new Vec3(0,1,0);var delta=p-e.Origin;
   if(Math.Abs(delta.Dot(dx.Cross(dy)))>.01)return float.NaN;
   return SampleNative(n,delta.Dot(dx)/(e.SpacingX>0?e.SpacingX:1),delta.Dot(dy)/(e.SpacingY>0?e.SpacingY:1));
  }
  static float SampleNative(PixelPlane n,double x,double y)
  {
   if(x<-.50001||y<-.50001||x>n.Width-.49999||y>n.Height-.49999)return float.NaN;
   x=Math.Max(0,Math.Min(n.Width-1,x));y=Math.Max(0,Math.Min(n.Height-1,y));int x0=(int)x,y0=(int)y,x1=Math.Min(x0+1,n.Width-1),y1=Math.Min(y0+1,n.Height-1);double fx=x-x0,fy=y-y0;
   return (float)((n.Values[y0*n.Width+x0]*(1-fx)+n.Values[y0*n.Width+x1]*fx)*(1-fy)+(n.Values[y1*n.Width+x0]*(1-fx)+n.Values[y1*n.Width+x1]*fx)*fy);
  }
  public static SlicePixels Render(RenderScene s,int requestedWidth,int requestedHeight,CancellationToken token)
  {
   var g=SliceGeometry.Create(s);int w=Math.Max(2,Math.Min(512,requestedWidth)),h=Math.Max(2,Math.Min(512,requestedHeight));
   var r=new SlicePixels{Width=w,Height=h,Pixels=new byte[w*h*4],Geometry=g};
   var doses=(s.Doses??new List<DoseOverlay>()).Where(d=>d?.Dose!=null&&d.Dose.Visible&&d.Dose.Maximum>0).ToArray();
   var maps=s.Isodoses?doses.Select(d=>new float[w*h]).ToArray():null;
   bool invert=s.Plane=="Native"?(s.Native?.Invert??false):(s.Volume?.Invert??false);
   // Native image orientation and pixel spacing are invariant across this frame.
   // Calculate patient-to-pixel axes once, retaining the same bilinear sampler.
   bool native=s.Plane=="Native"&&s.Native!=null&&s.Entry!=null;var nx=new Vec3();var ny=new Vec3();var nn=new Vec3();double nativeSx=1,nativeSy=1;
   if(native){var e=s.Entry;nx=e.HasGeometry?e.AxisX.Normalized():new Vec3(1,0,0);ny=e.HasGeometry?e.AxisY.Normalized():new Vec3(0,1,0);nn=nx.Cross(ny);nativeSx=e.SpacingX>0?e.SpacingX:1;nativeSy=e.SpacingY>0?e.SpacingY:1;}
   for(int y=0;y<h;y++)
   {
    token.ThrowIfCancellationRequested();
    for(int x=0;x<w;x++)
    {
     var p=g.WorldAt((x+.5)/w,(y+.5)/h);float v;
     if(native){var delta=p-s.Entry.Origin;v=Math.Abs(delta.Dot(nn))>.01?float.NaN:SampleNative(s.Native,delta.Dot(nx)/nativeSx,delta.Dot(ny)/nativeSy);}else v=SampleImage(s,p);
     double red=Window(v,s.WindowCenter,s.WindowWidth,invert),green=red,blue=red;
     if(s.OverlayVolume!=null&&s.OverlayOpacity>0)
     {
      var overlay=s.OverlayVolume.Sample((s.ImageToOverlay??Matrix4.Identity).Transform(p));
      if(!float.IsNaN(overlay))
      {double value=Window(overlay,s.OverlayWindowCenter,s.OverlayWindowWidth,s.OverlayVolume.Invert),alpha=Math.Max(0,Math.Min(1,s.OverlayOpacity));red=red*(1-alpha)+value*alpha;green=red;blue=red;}
     }
     for(int d=0;d<doses.Length;d++)
     {
      float val=doses[d].Dose.Sample(doses[d].ImageToDose.Transform(p));float ratio=val/doses[d].Dose.Maximum;
      if(maps!=null)maps[d][y*w+x]=s.AbsoluteIsodoses?val:ratio;
      if(!s.DoseWash||float.IsNaN(ratio)||ratio*100<s.DoseMinimumPercent||ratio*100>s.DoseMaximumPercent)continue;
      double t=Math.Max(0,Math.Min(1,ratio));double a=Math.Max(0,Math.Min(.9,s.DoseOpacity))*Math.Min(1,t*3);
      double rr,gg,bb;DoseColor(ratio*100,out rr,out gg,out bb);
      red=red*(1-a)+rr*a;green=green*(1-a)+gg*a;blue=blue*(1-a)+bb*a;
     }
     int k=(y*w+x)*4;r.Pixels[k]=(byte)blue;r.Pixels[k+1]=(byte)green;r.Pixels[k+2]=(byte)red;r.Pixels[k+3]=255;
    }
   }
   if(maps!=null)for(int doseIndex=0;doseIndex<maps.Length;doseIndex++)foreach(double displayLevel in IsodoseLevels(s))
   {
    if(s.AbsoluteIsodoses&&(!IsodoseConfiguration.IsPhysicalGy(doses[doseIndex].Dose)||displayLevel>doses[doseIndex].Dose.Maximum))continue;
    var map=maps[doseIndex];double level=s.AbsoluteIsodoses?displayLevel:displayLevel/100;var vals=new float[4];var uu=new double[4];var vv=new double[4];var points=new Vec3[4];
    for(int y=0;y<h-1;y++) {token.ThrowIfCancellationRequested();for(int x=0;x<w-1;x++)
    {
     vals[0]=map[y*w+x];vals[1]=map[y*w+x+1];vals[2]=map[(y+1)*w+x+1];vals[3]=map[(y+1)*w+x];
     if((vals[0]<level&&vals[1]<level&&vals[2]<level&&vals[3]<level)||(vals[0]>=level&&vals[1]>=level&&vals[2]>=level&&vals[3]>=level))continue;
     uu[0]=uu[3]=(x+.5)/w;uu[1]=uu[2]=(x+1.5)/w;vv[0]=vv[1]=(y+.5)/h;vv[2]=vv[3]=(y+1.5)/h;
     int count=0;for(int i=0;i<4;i++){int j=(i+1)%4;if(float.IsNaN(vals[i])||float.IsNaN(vals[j]))continue;if((vals[i]<level&&vals[j]>=level)||(vals[j]<level&&vals[i]>=level)){double t=(level-vals[i])/(vals[j]-vals[i]);points[count++]=g.WorldAt(uu[i]+t*(uu[j]-uu[i]),vv[i]+t*(vv[j]-vv[i]));}}
     for(int i=0;i+1<count;i+=2)r.Isolines.Add(new WorldLine(points[i],points[i+1],displayLevel));
    }}
   }
   return r;
  }
 }
}
