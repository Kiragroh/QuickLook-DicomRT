using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using QuickLook.DicomRT;
internal static class IsocenterOverlayTests
{
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);}
 public static void Run()
 {
  var type=typeof(ThreeDControl).Assembly.GetType("QuickLook.DicomRT.IsocenterOverlay");var project=type.GetMethod("Project",BindingFlags.Static|BindingFlags.NonPublic);
  var camera=new PerspectiveCamera(new Point3D(0,-100,0),new Vector3D(0,1,0),new Vector3D(0,0,1),90){NearPlaneDistance=.1,FarPlaneDistance=1000};
  object[] args={new Vec3(10,0,10),camera,new Size(800,400),new Point()};Check((bool)project.Invoke(null,args),"visible isocenter projected");var point=(Point)args[3];Check(Math.Abs(point.X-440)<1e-8&&Math.Abs(point.Y-160)<1e-8,"horizontal FOV projection respects non-square viewport");
  args[0]=new Vec3(0,-101,0);Check(!(bool)project.Invoke(null,args),"behind-camera isocenter not drawn");
  camera.Position=new Point3D(100,0,0);camera.LookDirection=new Vector3D(-1,0,0);args[0]=new Vec3(0,10,0);Check((bool)project.Invoke(null,args)&&Math.Abs(((Point)args[3]).X-440)<1e-8,"isocenter follows camera orbit");
  using(var control=new ThreeDControl())
  {
   var field=typeof(ThreeDControl).GetField("isocenterOverlay",BindingFlags.Instance|BindingFlags.NonPublic);var overlay=(FrameworkElement)field.GetValue(control);
   type.GetMethod("Set",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(overlay,new object[]{new[]{new Vec3(0,0,0)},camera});overlay.Measure(new Size(200,200));overlay.Arrange(new Rect(0,0,200,200));overlay.UpdateLayout();
   var bitmap=new RenderTargetBitmap(200,200,96,96,PixelFormats.Pbgra32);bitmap.Render(overlay);var pixels=new byte[200*200*4];bitmap.CopyPixels(pixels,800,0);
   Check(Enumerable.Range(0,200*200).Count(i=>pixels[i*4]>100&&pixels[i*4+1]>100&&pixels[i*4+2]<60)>30,"fixed-size cyan ISO locator actually rasterizes");
  }
  Console.WriteLine("PASS 3D isocenter screen projection, camera orbit, clipping and visible cross pixels");
 }
}
