using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using QuickLook.DicomRT;
internal static class MiniatureApertureScenarios
{
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 public static void Run(Action<bool,string> check)
 {
  var type=typeof(ViewerControl).Assembly.GetType("QuickLook.DicomRT.BeamFieldOverlay");var guide=(FrameworkElement)Activator.CreateInstance(type,true);
  var cp=new ControlPoint{XJaws=new[]{-40d,40d},YJaws=new[]{-30d,30d}};
  cp.MlcLayers.Add(new MlcLayer{Type="MLCX",Key="X",Boundaries=new[]{-20d,0d,20d},Positions=new[]{-10d,-10d,10d,10d}});
  var beam=new PlanBeam{PatientPosition="HFS",SourceAxisDistance=1000};beam.ControlPoints.Add(cp);var plan=new PlanData();plan.Beams.Add(beam);
  string reason;var projection=BeamProjection.Create(beam,cp,Matrix4.Identity,out reason);var center=projection.Iso-projection.Forward*55;
  var eye=center-projection.Forward*80;var camera=new PerspectiveCamera{Position=new Point3D(eye.X,eye.Y,eye.Z),LookDirection=new Vector3D(projection.Forward.X*80,projection.Forward.Y*80,projection.Forward.Z*80),UpDirection=new Vector3D(projection.Up.X,projection.Up.Y,projection.Up.Z),FieldOfView=42};
  var scene=new RenderScene{Plan=plan,PlanToImage=Matrix4.Identity,ActiveBeam=beam,ActiveControlPoint=cp};
  guide.Measure(new Size(700,600));guide.Arrange(new Rect(0,0,700,600));
  Action set=()=>type.GetMethod("Set",F).Invoke(guide,new object[]{scene,camera,100d});set();
  Func<byte[]> pixels=()=>{var visual=new DrawingVisual();using(var dc=visual.RenderOpen())type.GetMethod("DrawActiveMiniature",F).Invoke(guide,new object[]{dc});var image=new RenderTargetBitmap(700,600,96,96,PixelFormats.Pbgra32);image.Render(visual);var data=new byte[700*600*4];image.CopyPixels(data,2800,0);return data;};
  Func<byte[],double,double,byte> alpha=(data,x,y)=>{var world=center+projection.Right*(x/44*13.75)+projection.Up*(y/44*13.75);var args=new object[]{world,new Point()};if(!(bool)type.GetMethod("Project",F).Invoke(guide,args))throw new Exception("Test projection failed");var point=(Point)args[1];return data[((int)point.Y*700+(int)point.X)*4+3];};
  var image1=pixels();check(alpha(image1,0,5)==0,"actual aperture interior has zero alpha, no background or cross");
  check(alpha(image1,12,5)>alpha(image1,16,5)&&alpha(image1,16,5)>0&&alpha(image1,25,5)==0,"bank fringe fades outward to fully transparent");
  var edges=(IEnumerable)type.GetField("miniatureEdges",F).GetValue(guide);check(edges.Cast<object>().Count()==4,"shared leaf edges cancel into actual aperture outline");
  var second=new ControlPoint{XJaws=cp.XJaws,YJaws=cp.YJaws};second.MlcLayers.Add(cp.MlcLayers[0]);second.MlcLayers.Add(new MlcLayer{Type="MLCX",Key="X2",Boundaries=new[]{-20d,20d},Positions=new[]{2d,15d}});scene.ActiveControlPoint=second;set();var image2=pixels();
  check(alpha(image2,6,5)==0&&alpha(image2,0,5)>0,"double-layer aperture remains open only in the shared opening");
 }
}
