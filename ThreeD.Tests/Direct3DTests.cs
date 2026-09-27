using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using QuickLook.DicomRT;
using H=HelixToolkit.Wpf.SharpDX;
internal static class Direct3DTests
{
 const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
 static object Call(object o,string n,params object[] p)=>o.GetType().GetMethod(n,Flags).Invoke(o,p);
 static byte[] Pixels(BitmapSource bmp){var converted=new FormatConvertedBitmap(bmp,PixelFormats.Bgra32,null,0);var bytes=new byte[converted.PixelWidth*converted.PixelHeight*4];converted.CopyPixels(bytes,converted.PixelWidth*4,0);return bytes;}
 internal static BitmapSource CaptureControl(ThreeDControl control)
 {
  var initialization=(System.Threading.Tasks.Task)typeof(ThreeDControl).GetField("gpuInitialization",Flags).GetValue(control);FrameBenchmark.Pump(()=>initialization.IsCompleted,10);
  var bridge=typeof(ThreeDControl).GetField("gpu",Flags).GetValue(control);if(bridge==null)throw new Exception("Expected Direct3D test device");
  var view=(H.Viewport3DX)bridge.GetType().GetField("View",Flags).GetValue(bridge);FrameBenchmark.Pump(()=>view.RenderHost?.RenderTargetBufferView!=null,10);
  view.RenderHost.InvalidateRender();view.RenderHost.UpdateAndRender();return (BitmapSource)Call(bridge,"Capture");
 }
 static GeometryModel3D Cube(double size,Color color,double alpha)
 {
  var builder=new H.MeshBuilder(true,false);builder.AddBox(new SharpDX.Vector3(),size,size,size);var m=builder.ToMesh();var mesh=new MeshGeometry3D();foreach(var p in m.Positions)mesh.Positions.Add(new Point3D(p.X,p.Y,p.Z));foreach(var n in m.Normals)mesh.Normals.Add(new Vector3D(n.X,n.Y,n.Z));foreach(int i in m.Indices)mesh.TriangleIndices.Add(i);mesh.Freeze();
  var material=new DiffuseMaterial(new SolidColorBrush(color){Opacity=alpha});var model=new GeometryModel3D(mesh,material);model.Freeze();return model;
 }
 internal static int Run()
 {
  var type=typeof(ThreeDControl).Assembly.GetType("QuickLook.DicomRT.Direct3DSurface");object bridge=null;Window window=null;
  try{
   bridge=Activator.CreateInstance(type,true);var view=(H.Viewport3DX)type.GetField("View",Flags).GetValue(bridge);
   window=new Window{Width=400,Height=400,Left=-30000,Top=-30000,ShowInTaskbar=false,ShowActivated=false,WindowStyle=WindowStyle.None,Content=view};window.Show();window.UpdateLayout();
   FrameBenchmark.Pump(()=>view.RenderHost!=null&&view.RenderHost.RenderTargetBufferView!=null,10);
   var camera=new PerspectiveCamera(new Point3D(0,-6,1),new Vector3D(0,6,-1),new Vector3D(0,0,1),42){NearPlaneDistance=.1,FarPlaneDistance=100};Call(bridge,"SetCamera",camera);
   var target=Cube(1,Colors.Red,1);var skin=Cube(2,Color.FromRgb(205,172,151),.06);
   Func<Model3DGroup,byte[]> capture=g=>{Call(bridge,"SetSurfaces",g);view.RenderHost.InvalidateRender();view.RenderHost.UpdateAndRender();return Pixels((BitmapSource)Call(bridge,"Capture"));};
   var a=capture(new Model3DGroup{Children={target}});var b=capture(new Model3DGroup{Children={skin,target}});var c=capture(new Model3DGroup{Children={target,skin}});
   Func<byte[],int> red=bytes=>Enumerable.Range(0,bytes.Length/4).Count(i=>bytes[i*4+2]>80&&bytes[i*4+2]>bytes[i*4+1]*1.8&&bytes[i*4+2]>bytes[i*4]*1.8);
   int baseline=red(a),withSkin=red(b);if(baseline<1000||withSkin<baseline*.85)throw new Exception("Transparent enclosing skin hides internal target: "+baseline+" / "+withSkin);
   double difference=b.Zip(c,(x,y)=>Math.Abs((int)x-y)).Average();if(difference>.05)throw new Exception("Transparent draw order changes image: "+difference);
   var emission=new EmissiveMaterial(new SolidColorBrush(Colors.Blue){Opacity=.09});var guide=new GeometryModel3D(skin.Geometry,emission){BackMaterial=emission};Call(bridge,"SetGuides",new Model3DGroup{Children={guide}});
   var guided=capture(new Model3DGroup{Children={target}});if(red(guided)<baseline*.85)throw new Exception("Emissive slice guide hides target");
   Call(bridge,"SetGuides",new Model3DGroup());
   foreach(var size in new[]{new Size(300,600),new Size(700,350)}){window.Width=size.Width;window.Height=size.Height;window.UpdateLayout();Call(bridge,"SetCamera",camera);var actual=(H.PerspectiveCamera)view.Camera;double aspect=view.ActualWidth/view.ActualHeight;double horizontal=Math.Atan(Math.Tan(actual.FieldOfView*Math.PI/360)*aspect)*360/Math.PI;if(Math.Abs(horizontal-camera.FieldOfView)>1e-8)throw new Exception("Camera FOV differs at portrait/landscape aspect");}
   Console.WriteLine("PASS Direct3D actual pixel checks: target="+baseline+" skin_target="+withSkin+" order_difference="+difference.ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+" feature="+view.EffectsManager.Device.FeatureLevel);
   return 0;
  }catch(Exception ex){Console.WriteLine("FAIL Direct3D "+ex);return 1;}
  finally{window?.Close();(bridge as IDisposable)?.Dispose();}
 }
}
