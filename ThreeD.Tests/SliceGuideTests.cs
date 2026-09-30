using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using QuickLook.DicomRT;
internal static class SliceGuideTests
{
 const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
 static T Get<T>(object target,string name)=>(T)target.GetType().GetField(name,Private).GetValue(target);
 static Vec3[] Polygon(VolumeData volume,Vec3 focus,Vec3 normal)=>(Vec3[])typeof(ThreeDControl).GetMethod("SlicePlanePolygon",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{volume,focus,normal});
 public static void Run()
 {
  double h=Math.Sqrt(.5);var volume=new VolumeData{Width=11,Height=9,Depth=7,Origin=new Vec3(100,-20,50),AxisX=new Vec3(h,h,0),AxisY=new Vec3(-.5,.5,h),AxisZ=new Vec3(.5,-.5,h),SpacingX=1.2,SpacingY=2.5,SpacingZ=4,Values=new float[11*9*7]};var focus=volume.WorldAt(4,3,2);
  var axes=new[]{volume.AxisX,volume.AxisY,volume.AxisZ};var size=new[]{volume.Width,volume.Height,volume.Depth};var spacing=new[]{volume.SpacingX,volume.SpacingY,volume.SpacingZ};
  foreach(var normal in new[]{new Vec3(1,0,0),new Vec3(0,1,0),new Vec3(0,0,1)})
  {
   var polygon=Polygon(volume,focus,normal);Check(polygon.Length>=3&&polygon.Length<=6,"oblique box slice polygon has bounded size");
   foreach(var point in polygon){Check(Math.Abs((point-focus).Dot(normal))<1e-7,"guide plane follows exact LPS focus coordinate");for(int i=0;i<3;i++){double index=(point-volume.Origin).Dot(axes[i])/spacing[i];Check(index>=-.500001&&index<=size[i]-.499999,"guide polygon clipped to oriented image extent");}}
   var moved=focus+normal*.7;Check(Polygon(volume,moved,normal).All(p=>Math.Abs((p-moved).Dot(normal))<1e-7),"slice guide follows scroll position");Check(Polygon(volume,focus+normal*1000,normal).Length==0,"out-of-volume plane never extrapolated");
  }
  var previous=SynchronizationContext.Current;SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
  using(var control=new ThreeDControl(compact:true))
  {
   var window=new Window{Width=450,Height=350,Left=-30000,Top=-30000,ShowInTaskbar=false,ShowActivated=false,WindowStyle=WindowStyle.None,Content=control};
   try
   {
    window.Show();control.SetScene(new RenderScene{Volume=volume,Entry=new DicomEntry{Modality="CT",SeriesUid="synthetic-series"},Focus=focus});control.SetSlicePlanes(focus,volume);
    var frame=new DispatcherFrame();var started=DateTime.UtcNow;var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(10)};timer.Tick+=(s,e)=>{if(Get<object>(control,"prepared")!=null||(DateTime.UtcNow-started).TotalSeconds>5)frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);timer.Stop();Check(Get<object>(control,"prepared")!=null,"compact scene preparation completes");
    var fittedCamera=Get<PerspectiveCamera>(control,"camera");var viewport=Get<Viewport3D>(control,"viewport");var view=new Vec3(fittedCamera.LookDirection.X,fittedCamera.LookDirection.Y,fittedCamera.LookDirection.Z).Normalized();var right=view.Cross(new Vec3(0,0,1)).Normalized();var up=right.Cross(view);var eye=new Vec3(fittedCamera.Position.X,fittedCamera.Position.Y,fittedCamera.Position.Z);double tangent=Math.Tan(fittedCamera.FieldOfView*Math.PI/360),aspect=viewport.ActualWidth/viewport.ActualHeight;
    double occupancy=0;for(int i=0;i<8;i++){var corner=volume.WorldAt((i&1)==0?-.5:volume.Width-.5,(i&2)==0?-.5:volume.Height-.5,(i&4)==0?-.5:volume.Depth-.5)-eye;double depth=corner.Dot(view);occupancy=Math.Max(occupancy,Math.Max(Math.Abs(corner.Dot(right))/(depth*tangent),Math.Abs(corner.Dot(up))/(depth*tangent/aspect)));Check(depth>0&&Math.Abs(corner.Dot(right))<depth*tangent&&Math.Abs(corner.Dot(up))<depth*tangent/aspect,"compact default camera fits entire oriented voxel envelope");}Check(occupancy>.95&&occupancy<=.99001,"compact camera tightly fits projected volume corners with the compact one percent margin");
    typeof(ThreeDControl).GetField("yaw",Private).SetValue(control,.75);typeof(ThreeDControl).GetMethod("UpdateCamera",Private).Invoke(control,null);
    var prepared=Get<object>(control,"prepared");var visual=Get<ModelVisual3D>(control,"visual").Content;int generation=Get<int>(control,"generation");var camera=Get<PerspectiveCamera>(control,"camera");var position=camera.Position;var direction=camera.LookDirection;var guide=Get<ModelVisual3D>(control,"sliceVisual");var oldGuide=guide.Content;
    var nextFocus=focus+new Vec3(0,0,.9);control.SetScene(new RenderScene{Volume=volume,Entry=new DicomEntry{Modality="CT",SeriesUid="synthetic-series"},Focus=nextFocus});control.SetSlicePlanes(nextFocus,volume);
    Check(ReferenceEquals(prepared,Get<object>(control,"prepared"))&&ReferenceEquals(visual,Get<Model3DGroup>(control,"qualityModels"))&&generation==Get<int>(control,"generation"),"scroll guide updates never rebuild 3D scene or models");Check(camera.Position==position&&camera.LookDirection==direction,"scroll guide retains orbit and zoom");Check(!ReferenceEquals(oldGuide,guide.Content),"only coordinate guide changes on scroll");
    var group=(Model3DGroup)guide.Content;int triangles=group.Children.Cast<GeometryModel3D>().Sum(m=>((MeshGeometry3D)m.Geometry).TriangleIndices.Count/3);Check(triangles<150,"guide geometry remains tiny");var currentGuide=guide.Content;control.SetSlicePlanes(nextFocus,volume);Check(ReferenceEquals(currentGuide,guide.Content),"unchanged guide uses existing model");
    control.UpdateLayout();var bitmap=Direct3DTests.CaptureControl(control);int w=bitmap.PixelWidth,ph=bitmap.PixelHeight;var pixels=new byte[w*ph*4];bitmap.CopyPixels(pixels,w*4,0);int colored=0;for(int y=0;y<ph;y++)for(int x=0;x<w;x++){int p=(y*w+x)*4;int minimum=Math.Min(pixels[p],Math.Min(pixels[p+1],pixels[p+2])),maximum=Math.Max(pixels[p],Math.Max(pixels[p+1],pixels[p+2]));if(maximum-minimum>40&&maximum>70)colored++;}Check(colored>100,"compact slice guide visibly renders without anatomy surfaces");
    control.SetSlicePlanes(focus,null);Check(guide.Content==null,"no image volume clears coordinate planes");
   }
   finally{window.Close();SynchronizationContext.SetSynchronizationContext(previous);}
  }
  Console.WriteLine("PASS compact 3D slice guides: oblique bounds, exact LPS position, camera/cache preservation and visible composition");
 }
}
