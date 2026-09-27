using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuickLook.DicomRT;
internal static class FieldClipScenarios
{
 const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
 static void Set(object o,string name,object value)=>o.GetType().GetField(name,F).SetValue(o,value);
 public static void Run(Action<bool,string> check)
 {
  var band=typeof(ViewerControl).Assembly.GetType("QuickLook.DicomRT.ArcModulationDrawing");
  var fraction=band.GetMethod("Fraction",BindingFlags.Static|BindingFlags.NonPublic);
  check((double)fraction.Invoke(null,new object[]{.1,4d})==.025&&(double)fraction.Invoke(null,new object[]{4d,4d})==1,"Arc band uses linear magnitude mapping without boosting low MU values");
  check(double.IsNaN((double)fraction.Invoke(null,new object[]{double.NaN,4d}))&&(double)fraction.Invoke(null,new object[]{0d,0d})==0,"Unknown angular values remain distinct from zero");
  var color=band.GetMethod("ColorFor",BindingFlags.Static|BindingFlags.NonPublic);
  var low=(SolidColorBrush)color.Invoke(null,new object[]{0d,4d,true});var high=(SolidColorBrush)color.Invoke(null,new object[]{4d,4d,true});
  check(low.IsFrozen&&high.IsFrozen&&low.Color.B>100&&high.Color.B>low.Color.B,"Reusable blue band palette keeps low values visible and peaks brighter");
  using(var pane=new SlicePane()){
   pane.Measure(new Size(1000,600));pane.Arrange(new Rect(0,0,1000,600));pane.UpdateLayout();
   var volume=new VolumeData{Width=21,Height=21,Depth=21,SpacingX=1,SpacingY=1,SpacingZ=1,AxisX=new Vec3(1,0,0),AxisY=new Vec3(0,1,0),AxisZ=new Vec3(0,0,1)};
   var scene=new RenderScene{Volume=volume,Plane="Axial",Focus=new Vec3(10,10,10),PlanToImage=Matrix4.Identity,ShowFields=true,Crosshair=false};
   var geometry=SliceGeometry.Create(scene);var iso=geometry.WorldAt(.95,.5);
   var cp=new ControlPoint{Isocenter=iso,GantryRotationDirection="CW",CouchRotationDirection="NONE",CollimatorRotationDirection="NONE"};
   var beam=new PlanBeam{PatientPosition="HFS",SourceAxisDistance=1000,Number=1,Meterset=100,FinalCumulativeMetersetWeight=1,ControlPoints={cp,new ControlPoint{Isocenter=iso,MetersetWeight=1}}};
   scene.Plan=new PlanData{Beams={beam}};scene.ActiveBeam=beam;scene.ActiveControlPoint=cp;
   var raster=new SlicePixels{Width=2,Height=2,Pixels=new byte[16],Geometry=geometry};
   var frame=Activator.CreateInstance(typeof(SlicePane).GetNestedType("Frame",BindingFlags.NonPublic));
   var bitmap=BitmapSource.Create(2,2,96,96,PixelFormats.Bgra32,null,new byte[16],8);bitmap.Freeze();
   Set(frame,"Bitmap",bitmap);Set(frame,"Raster",raster);Set(frame,"Scene",scene);Set(pane,"frame",frame);
   var rect=(Rect)typeof(SlicePane).GetMethod("ImageRect",F).Invoke(pane,new object[]{geometry});
   int outside=0;var timeout=Stopwatch.StartNew();
   do{
    var drawing=new DrawingVisual();using(var dc=drawing.RenderOpen())typeof(SlicePane).GetMethod("OnRender",F).Invoke(pane,new object[]{dc});
    // Inspect the actual WPF drawing tree, including inherited clip regions.
    // RenderTargetBitmap can return an empty surface in a disconnected session.
    outside=CountFieldSegments(drawing.Drawing,new Rect(0,0,1000,600),rect.Right+8);
    if(outside>0)break;Thread.Sleep(5);
   }while(timeout.ElapsedMilliseconds<3000);
   check(rect.Right<800&&outside>0,"gantry ring renders into available pane beyond CT image boundary");
   var fixedPoint=new ControlPoint{Gantry=90,Isocenter=geometry.Center,XJaws=new[]{-30d,30d},YJaws=new[]{-30d,30d}};string reason;
   var projection=BeamProjection.Create(beam,fixedPoint,Matrix4.Identity,out reason);
   var drawingType=typeof(ViewerControl).Assembly.GetType("QuickLook.DicomRT.FieldArrangementDrawing");
   var opening=(Geometry)drawingType.GetMethod("Opening",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{projection,fixedPoint,geometry,CancellationToken.None,new Rect(-.4,-.1,1.8,1.2)});
   check(opening!=null&&(opening.Bounds.Left<-.1||opening.Bounds.Right>1.1),"fixed-field aperture intersections extend beyond the finite CT plane into pane margins");
   check(pane.ClipToBounds,"field geometry remains clipped to its own view pane");
  }
 }
 static int CountFieldSegments(Drawing drawing,Rect clip,double right)
 {
  if(drawing is DrawingGroup group){if(group.ClipGeometry!=null)clip.Intersect(group.ClipGeometry.Bounds);int count=0;foreach(var child in group.Children)count+=CountFieldSegments(child,clip,right);return count;}
  if(drawing is GeometryDrawing geometry&&geometry.Brush is SolidColorBrush brush&&brush.Color.B>brush.Color.R&&brush.Color.B>100){var bounds=geometry.Bounds;bounds.Intersect(clip);return !bounds.IsEmpty&&bounds.Right>right?1:0;}return 0;
 }

}
