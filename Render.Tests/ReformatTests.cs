using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using QuickLook.DicomRT;

internal static class ReformatTests
{
 static int checks;
 static void Check(bool value,string message){checks++;if(!value)throw new Exception("Reformat: "+message);}
 static Contour Rectangle(double z,double x=-5,double y=-5,double width=10,double height=10,string type="CLOSED_PLANAR")=>new Contour {GeometricType=type,Points=new List<Vec3>{new Vec3(x,y,z),new Vec3(x+width,y,z),new Vec3(x+width,y+height,z),new Vec3(x,y+height,z)}};
 static StructureRoi Box(params double[] planes)=>new StructureRoi {Contours=planes.Select(z=>Rectangle(z)).ToList()};
 static SliceGeometry Geometry(string plane="Coronal",Vec3? focus=null)=>SliceGeometry.Create(new RenderScene {Plane=plane,Focus=focus??new Vec3(),Volume=new VolumeData{Width=41,Height=41,Depth=41,SpacingX=1,SpacingY=1,SpacingZ=1,Origin=new Vec3(-20,-20,-20),AxisX=new Vec3(1,0,0),AxisY=new Vec3(0,1,0),AxisZ=new Vec3(0,0,1)}});
 static List<WorldLine> Outline(StructureRoi roi,SliceGeometry geometry=null,Matrix4 transform=null)=>ReformatContours.Outline(roi,transform??Matrix4.Identity,geometry??Geometry(),.01,CancellationToken.None);
 static IEnumerable<Vec3> Points(List<WorldLine> lines)=>lines.SelectMany(l=>new[]{l.A,l.B});
 static string Key(Vec3 p)=>Math.Round(p.X,5)+","+Math.Round(p.Y,5)+","+Math.Round(p.Z,5);
 static void Closed(List<WorldLine> lines,string label)
 {
  Check(lines.Count>0,label+" present");
  var degree=Points(lines).GroupBy(Key).Select(g=>g.Count()).ToArray();
  Check(degree.All(n=>n==2),label+" forms closed boundary without internal chords");
 }
 public static void Run()
 {
  foreach(string plane in new[]{"Coronal","Sagittal"})
  {
   var g=Geometry(plane);var lines=Outline(Box(0,2,4),g);Closed(lines,plane+" box");
   Check(Points(lines).All(p=>Math.Abs((p-g.Center).Dot(g.Normal))<1e-7),plane+" remains on viewing plane");
   Check(Points(lines).All(p=>Math.Abs(plane=="Coronal"?p.X:p.Y)<=5.001&&p.Z>=-1.001&&p.Z<=5.001),plane+" no extent overshoot");
   Check(lines.All(l=>!(Math.Abs(l.A.Z-l.B.Z)<1e-7&&Math.Abs(l.A.Z-2)<.2&&Math.Abs(l.A.X-l.B.X)>1)),plane+" no interior slice hatching");
  }
  var cylinder=Box(0,2,4);
  foreach(var c in cylinder.Contours){double z=c.Points[0].Z;c.Points=Enumerable.Range(0,96).Select(i=>new Vec3(5*Math.Cos(i*Math.PI/48),5*Math.Sin(i*Math.PI/48),z)).ToList();}
  var cylinderLines=Outline(cylinder,Geometry(focus:new Vec3(0,3,0)));Closed(cylinderLines,"cylinder");
  Check(Math.Abs(Points(cylinderLines).Max(p=>p.X)-4)<.03,"cylinder uses true plane chord extent");
  var hollow=Box(0,2,4);foreach(var c in hollow.Contours)c.GeometricType="CLOSEDPLANAR_XOR";
  foreach(double z in new[]{0d,2,4})hollow.Contours.Add(Rectangle(z,-2,-2,4,4,"CLOSEDPLANAR_XOR"));
  var holes=Outline(hollow);Closed(holes,"XOR annulus");Check(Points(holes).All(p=>Math.Abs(p.X)>=1.99),"XOR hole retained");
  foreach(var c in hollow.Contours)c.GeometricType="CLOSED_PLANAR";
  var union=Outline(hollow);Closed(union,"nested CLOSED_PLANAR union");Check(union.Count<holes.Count,"ordinary nested polygons remain union");
  var separated=Outline(Box(0,2,20,22));Closed(separated,"gapped stack");
  Check(Points(separated).All(p=>p.Z<=3.001||p.Z>=18.999),"large missing-level gap stays empty");
  var three=Outline(Box(0,2,20));Check(Points(three).All(p=>p.Z<=3.001||p.Z>=18.999),"lower median detects gap in three-plane stack");
  Check(Points(three).Max(p=>p.Z)<=21.001,"gap does not inflate end cap");
  var empty=Box(0,2,4);empty.Contours[1]=Rectangle(2,y:10);
  var emptyLines=Outline(empty);Closed(emptyLines,"empty intersection row");
  Check(Points(emptyLines).Any(p=>Math.Abs(p.Z-1)<.01)&&Points(emptyLines).Any(p=>Math.Abs(p.Z-3)<.01),"empty row caps neighbours at midpoint");
  Check(Points(emptyLines).All(p=>p.Z<=1.001||p.Z>=2.999),"empty row creates no bridge");
  var map=new Matrix4(new double[]{0,-1,0,30,1,0,0,10,0,0,1,4,0,0,0,1});
  var transformed=Outline(Box(0,2,4),Geometry(focus:new Vec3(30,10,4)),map);Closed(transformed,"rigid mapped contour stack");
  Check(Points(transformed).All(p=>p.X>=24.999&&p.X<=35.001&&Math.Abs(p.Y-10)<1e-7&&p.Z>=2.999&&p.Z<=9.001),"rigid mapping preserved in physical space");
  double angle=.3,cs=Math.Cos(angle),sn=Math.Sin(angle);
  var tilt=new Matrix4(new double[]{1,0,0,0,0,cs,-sn,0,0,sn,cs,0,0,0,0,1});
  var tilted=Outline(Box(0,2,4),Geometry(),tilt);Closed(tilted,"oblique registered stack");
  Check(Points(tilted).All(p=>Math.Abs(p.X)<=5.001&&Math.Abs(cs*p.Y+sn*p.Z)<=5.001&&-sn*p.Y+cs*p.Z>=-1.001&&-sn*p.Y+cs*p.Z<=5.001),"oblique mapping stays inside source slab extents");
  var leadingGap=Outline(Box(0,18,20));Closed(leadingGap,"leading stack gap");
  Check(Points(leadingGap).Min(p=>p.Z)>=-1.001,"leading gap does not inflate first end cap");
  var axial=Geometry("Axial",new Vec3(0,0,2));var original=SliceGeometry.ContourLines(Box(0,2,4),Matrix4.Identity,axial,.01);var retained=Outline(Box(0,2,4),axial);
  Check(original.Count==retained.Count&&original.Zip(retained,(a,b)=>(a.A-b.A).Length+(a.B-b.B).Length).All(d=>d<1e-9),"parallel original contours unchanged");
  // A native coronal image with an axial RTSTRUCT must use the same boundary
  // as MPR. The image's Native label says nothing about the contour orientation.
  var native=SliceGeometry.Create(new RenderScene{Plane="Native",Entry=new DicomEntry{HasGeometry=true,Origin=new Vec3(-20,0,20),AxisX=new Vec3(1,0,0),AxisY=new Vec3(0,0,-1),SpacingX=1,SpacingY=1},Native=new PixelPlane{Width=41,Height=41}});
  ReformatContours.OutlineKind kind;var nativeLines=ReformatContours.Outline(Box(0,2,4),Matrix4.Identity,native,.49,CancellationToken.None,out kind);
  Closed(nativeLines,"native coronal image plus axial contour stack");
  Check(kind==ReformatContours.OutlineKind.Interpolated,"native off-axis stack is explicitly interpolated");
  Check(nativeLines.All(l=>!(Math.Abs(l.A.Z-l.B.Z)<1e-7&&Math.Abs(l.A.Z-2)<.2&&Math.Abs(l.A.X-l.B.X)>1)),"native has no interior RTSTRUCT chords");
  ReformatContours.Outline(Box(0,2,4),Matrix4.Identity,axial,.49,CancellationToken.None,out kind);Check(kind==ReformatContours.OutlineKind.Original,"aligned original contours labeled original");
  ReformatContours.Outline(Box(0),Matrix4.Identity,native,.49,CancellationToken.None,out kind);Check(kind==ReformatContours.OutlineKind.IntersectionFallback,"single unsupported off-axis contour explicitly falls back");
  using(var cancel=new CancellationTokenSource()){cancel.Cancel();bool threw=false;try{ReformatContours.Outline(Box(0),Matrix4.Identity,Geometry(),.01,cancel.Token);}catch(OperationCanceledException){threw=true;}Check(threw,"cancellation includes early fallback");}
  Console.WriteLine("PASS "+checks+" contour reformat boundary checks");
 }
 public static int Benchmark(string folder)
 {
  try
  {
   var catalog=DicomCatalog.Scan(Directory.EnumerateFiles(folder,"*.dcm").First(),CancellationToken.None);
   var rois=catalog.Files.Where(e=>e.Modality=="RTSTRUCT").SelectMany(e=>StructureSet.Load(e).Rois).ToArray();
   var dose=DoseGrid.Load(catalog.Files.First(e=>e.Modality=="RTDOSE"));var links=RegistrationReader.Read(catalog);
   var scene=new RenderScene{Volume=dose.Volume,Focus=dose.Volume.Center};
   foreach(var plane in new[]{"Coronal","Sagittal"})
   {
    scene.Plane=plane;var g=SliceGeometry.Create(scene);var watch=Stopwatch.StartNew();int lines=0,shown=0;
    foreach(var roi in rois){var map=RegistrationReader.Resolve(links,roi.FrameUid,dose.FrameUid);if(map==null)continue;var outline=ReformatContours.Outline(roi,map,g,.01,CancellationToken.None);lines+=outline.Count;if(outline.Count>0)shown++;}
    Console.WriteLine("Public contour aggregate: plane="+plane+" ROIs="+rois.Length+" intersected="+shown+" segments="+lines+" elapsed_ms="+watch.ElapsedMilliseconds);
    watch.Restart();lines=0;shown=0;
    foreach(var roi in rois)
    {
     var map=RegistrationReader.Resolve(links,roi.FrameUid,dose.FrameUid);if(map==null)continue;
     var vertices=roi.Contours.SelectMany(c=>c.Points).ToArray();if(vertices.Length==0)continue;
     scene.Focus=map.Transform(new Vec3(vertices.Average(p=>p.X),vertices.Average(p=>p.Y),vertices.Average(p=>p.Z)));
     var outline=ReformatContours.Outline(roi,map,SliceGeometry.Create(scene),.01,CancellationToken.None);lines+=outline.Count;if(outline.Count>0)shown++;
    }
    Console.WriteLine("Public contour centered aggregate: plane="+plane+" ROIs="+rois.Length+" intersected="+shown+" segments="+lines+" elapsed_ms="+watch.ElapsedMilliseconds);
    scene.Focus=dose.Volume.Center;
   }
   return 0;
  }
  catch(Exception e){Console.WriteLine("Public contour aggregate failed: "+e.GetType().Name);return 1;}
 }
}
