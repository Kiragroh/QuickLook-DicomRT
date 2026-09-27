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
  using(var pane=new SlicePane()){
   pane.Measure(new Size(1000,600));pane.Arrange(new Rect(0,0,1000,600));pane.UpdateLayout();
   var volume=new VolumeData{Width=21,Height=21,Depth=21,SpacingX=1,SpacingY=1,SpacingZ=1,AxisX=new Vec3(1,0,0),AxisY=new Vec3(0,1,0),AxisZ=new Vec3(0,0,1)};
   var scene=new RenderScene{Volume=volume,Plane="Axial",Focus=new Vec3(10,10,10),PlanToImage=Matrix4.Identity,ShowFields=true,Crosshair=false};
   var geometry=SliceGeometry.Create(scene);var iso=geometry.WorldAt(.95,.5);
   var cp=new ControlPoint{Isocenter=iso,GantryRotationDirection="CW",CouchRotationDirection="NONE",CollimatorRotationDirection="NONE"};
   var beam=new PlanBeam{PatientPosition="HFS",SourceAxisDistance=1000,Number=1,ControlPoints={cp,new ControlPoint{Isocenter=iso}}};
   scene.Plan=new PlanData{Beams={beam}};scene.ActiveBeam=beam;scene.ActiveControlPoint=cp;
   var raster=new SlicePixels{Width=2,Height=2,Pixels=new byte[16],Geometry=geometry};
   var frame=Activator.CreateInstance(typeof(SlicePane).GetNestedType("Frame",BindingFlags.NonPublic));
   var bitmap=BitmapSource.Create(2,2,96,96,PixelFormats.Bgra32,null,new byte[16],8);bitmap.Freeze();
   Set(frame,"Bitmap",bitmap);Set(frame,"Raster",raster);Set(frame,"Scene",scene);Set(pane,"frame",frame);
   var rect=(Rect)typeof(SlicePane).GetMethod("ImageRect",F).Invoke(pane,new object[]{geometry});
   int outside=0;var timeout=Stopwatch.StartNew();
   do{
    var drawing=new DrawingVisual();using(var dc=drawing.RenderOpen())typeof(SlicePane).GetMethod("OnRender",F).Invoke(pane,new object[]{dc});
    var target=new RenderTargetBitmap(1000,600,96,96,PixelFormats.Pbgra32);target.Render(drawing);var pixels=new byte[1000*600*4];target.CopyPixels(pixels,4000,0);
    outside=0;for(int y=60;y<540;y++)for(int x=(int)Math.Ceiling(rect.Right)+8;x<970;x++){int k=(y*1000+x)*4;if(pixels[k+2]>160&&pixels[k+1]>130&&pixels[k]<125)outside++;}
    if(outside>30)break;Thread.Sleep(5);
   }while(timeout.ElapsedMilliseconds<3000);
   check(rect.Right<800&&outside>30,"gantry ring renders into available pane beyond CT image boundary");
   var fixedPoint=new ControlPoint{Gantry=90,Isocenter=geometry.Center,XJaws=new[]{-30d,30d},YJaws=new[]{-30d,30d}};string reason;
   var projection=BeamProjection.Create(beam,fixedPoint,Matrix4.Identity,out reason);
   var drawingType=typeof(ViewerControl).Assembly.GetType("QuickLook.DicomRT.FieldArrangementDrawing");
   var opening=(Geometry)drawingType.GetMethod("Opening",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{projection,fixedPoint,geometry,CancellationToken.None,new Rect(-.4,-.1,1.8,1.2)});
   check(opening!=null&&(opening.Bounds.Left<-.1||opening.Bounds.Right>1.1),"fixed-field aperture intersections extend beyond the finite CT plane into pane margins");
   check(pane.ClipToBounds,"field geometry remains clipped to its own view pane");
  }
 }
}
