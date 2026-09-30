using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using QuickLook.DicomRT;
internal static class ContextTests
{
 const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
 static T Get<T>(object target,string name)=>(T)target.GetType().GetField(name,Private).GetValue(target);
 static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
 public static void Run()
 {
  foreach(string name in new[]{"BODY"," external "})Check(!ThreeDGeometry.DisplayRoi(new StructureRoi{Name=name},true),"untyped external fallback excluded");
  Check(!ThreeDGeometry.DisplayRoi(new StructureRoi{Name="BODY",InterpretedType="ORGAN"},true),"named whole-body contour remains excluded from organ group");
  Check(!ThreeDGeometry.DisplayRoi(new StructureRoi{Name="Brain",InterpretedType="ORGAN"},false),"organ requires explicit type switch");
  Check(ThreeDGeometry.RoiOpacityScale("PTV",new Vec3(100,100,100),new Vec3(1,1,1),true)==1,"target opacity unchanged");
  Check(ThreeDGeometry.RoiOpacityScale("ORGAN",new Vec3(100,100,100),new Vec3(20,20,20),true)==.18,"large enclosing organ is faint");
  Check(ThreeDGeometry.RoiOpacityScale("ORGAN",new Vec3(10,10,10),new Vec3(100,100,100),false)==1,"small organ opacity unchanged");
  var v=new VolumeData{Width=32,Height=32,Depth=32,Origin=new Vec3(-16,-16,-16),AxisX=new Vec3(1,0,0),AxisY=new Vec3(0,1,0),AxisZ=new Vec3(0,0,1),SpacingX=1,SpacingY=1,SpacingZ=1,Values=new float[32*32*32]};
  for(int z=0;z<32;z++)for(int y=0;y<32;y++)for(int x=0;x<32;x++)v.Values[(z*32+y)*32+x]=v.WorldAt(x,y,z).Length<13?0:-1000;
  var target=QualityTests.Sphere();target.InterpretedType="PTV";
  foreach(var contour in target.Contours)for(int i=0;i<contour.Points.Count;i++)contour.Points[i]=contour.Points[i]*.25;
  var scene=new RenderScene{Volume=v,Entry=new DicomEntry{Modality="CT"},Structures=new List<RoiOverlay>{new RoiOverlay{Roi=target},new RoiOverlay{Roi=QualityTests.Sphere()}}};
  foreach(bool compact in new[]{false,true})using(var control=new ThreeDControl(compact))
  {
   Check(Get<CheckBox>(control,"skin").IsChecked==true,"CT skin defaults on in full and compact 3D");
   Check(Math.Abs(Get<Slider>(control,"skinOpacity").Value-.06)<1e-8,"skin defaults to six percent");
   Get<CheckBox>(control,"organs").IsChecked=true;var cache=Get<object>(control,"cache");var prepare=typeof(ThreeDControl).GetMethod("PrepareCore",BindingFlags.Static|BindingFlags.NonPublic);object[] args={scene,true,.5,CancellationToken.None,cache,false,true,false,true};
   var prepared=prepare.Invoke(null,args);var again=prepare.Invoke(null,args);Check((int)again.GetType().GetField("CacheHits").GetValue(again)==3,"skin and both ROI meshes reused from cache");
   typeof(ThreeDControl).GetField("scene",Private).SetValue(control,scene);typeof(ThreeDControl).GetField("prepared",Private).SetValue(control,prepared);
   typeof(ThreeDControl).GetMethod("ApplyModels",Private).Invoke(control,null);
   var parts=((IEnumerable)prepared.GetType().GetField("Parts").GetValue(prepared)).Cast<object>().ToArray();
   Func<string,MeshGeometry3D> mesh=kind=>(MeshGeometry3D)parts.Single(p=>(string)p.GetType().GetField(kind=="Skin"?"Kind":"RoiType").GetValue(p)==kind).GetType().GetField("Mesh").GetValue(parts.Single(p=>(string)p.GetType().GetField(kind=="Skin"?"Kind":"RoiType").GetValue(p)==kind));
   Func<string,double> alpha=kind=>((SolidColorBrush)((DiffuseMaterial)((Model3DGroup)Get<ModelVisual3D>(control,"visual").Content).Children.OfType<GeometryModel3D>().Single(p=>ReferenceEquals(p.Geometry,mesh(kind))).Material).Brush).Opacity;
   Check(Math.Abs(alpha("Skin")-.06)<1e-8&&Math.Abs(alpha("PTV")-.7)<1e-8&&alpha("ORGAN")<.15,"independent default context and target opacity");
   Get<Slider>(control,"opacity").Value=.9;Check(Math.Abs(alpha("Skin")-.06)<1e-8&&Math.Abs(alpha("PTV")-.9)<1e-8,"ROI slider never changes skin opacity");
   Get<Slider>(control,"skinOpacity").Value=.12;Check(Math.Abs(alpha("Skin")-.12)<1e-8&&Math.Abs(alpha("PTV")-.9)<1e-8,"skin slider never changes target opacity");
   Check(ReferenceEquals(prepared,Get<object>(control,"prepared")),"opacity adjustment retains prepared geometry");
  }
  Console.WriteLine("PASS skin/context defaults, independent opacity, large-organ extent heuristic, external exclusion and geometry cache");
  FullCameraFit();
 }
 static void FullCameraFit()
 {
  double h=Math.Sqrt(.5);var volume=new VolumeData{Width=80,Height=56,Depth=64,Origin=new Vec3(-80,-50,-60),AxisX=new Vec3(h,h,0),AxisY=new Vec3(-h,h,0),AxisZ=new Vec3(0,0,1),SpacingX=2,SpacingY=2,SpacingZ=2,Values=new float[80*56*64]};
  // A tiny central target must not determine the full view when CT context is on.
  var target=QualityTests.Sphere();target.InterpretedType="PTV";var center=volume.Center;foreach(var contour in target.Contours)for(int i=0;i<contour.Points.Count;i++)contour.Points[i]=center+contour.Points[i]*.2;
  var scene=new RenderScene{Volume=volume,Entry=new DicomEntry{Modality="CT"},Focus=center,Structures=new List<RoiOverlay>{new RoiOverlay{Roi=target}}};
  using(var control=new ThreeDControl())
  {
   var window=new Window{Width=1100,Height=500,Left=-30000,Top=-30000,ShowInTaskbar=false,ShowActivated=false,WindowStyle=WindowStyle.None,Content=control};
   try
   {
    control.SetScene(scene);window.Show();
    foreach(var size in new[]{new Size(1100,500),new Size(420,850)})
    {
     window.Width=size.Width;window.Height=size.Height;window.UpdateLayout();
     var camera=Get<PerspectiveCamera>(control,"camera");var viewport=Get<Viewport3D>(control,"viewport");var view=new Vec3(camera.LookDirection.X,camera.LookDirection.Y,camera.LookDirection.Z).Normalized();var right=view.Cross(new Vec3(0,0,1)).Normalized();var up=right.Cross(view);var eye=new Vec3(camera.Position.X,camera.Position.Y,camera.Position.Z);double tangent=Math.Tan(camera.FieldOfView*Math.PI/360),aspect=viewport.ActualWidth/viewport.ActualHeight,occupancy=0;
     for(int i=0;i<8;i++)
     {
      var corner=volume.WorldAt((i&1)==0?-.5:volume.Width-.5,(i&2)==0?-.5:volume.Height-.5,(i&4)==0?-.5:volume.Depth-.5)-eye;double depth=corner.Dot(view),horizontal=Math.Abs(corner.Dot(right))/(depth*tangent),vertical=Math.Abs(corner.Dot(up))/(depth*tangent/aspect);occupancy=Math.Max(occupancy,Math.Max(horizontal,vertical));
      Check(depth>camera.NearPlaneDistance&&horizontal<1&&vertical<1,"full skin view fits every oblique CT corner at wide and tall aspect ratios");
     }
     Check(occupancy>.85&&occupancy<=.90001,"full view tightly fits CT envelope with ten percent margin");
    }
    typeof(ThreeDControl).GetField("cameraAdjusted",Private).SetValue(control,true);typeof(ThreeDControl).GetField("yaw",Private).SetValue(control,.7);typeof(ThreeDControl).GetMethod("UpdateCamera",Private).Invoke(control,null);
    var orbit=Get<PerspectiveCamera>(control,"camera");var position=orbit.Position;var direction=orbit.LookDirection;int generation=Get<int>(control,"generation");scene.Focus+=new Vec3(0,0,1);control.SetScene(scene);window.Width=700;window.UpdateLayout();
    Check(orbit.Position==position&&orbit.LookDirection==direction,"full view preserves manual orbit on scroll and resize");Check(generation==Get<int>(control,"generation"),"full view scroll and resize do not rebuild cached geometry");
   }
   finally{window.Close();}
  }
  Console.WriteLine("PASS full 3D CT skin framing at wide/tall aspect ratios and manual camera preservation");
 }
}
