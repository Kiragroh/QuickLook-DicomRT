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
using System.Windows.Threading;
using QuickLook.DicomRT;
internal static class FocusInteractionTests
{
 const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
 static T Get<T>(object target,string name)=>(T)target.GetType().GetField(name,Private).GetValue(target);
 static void Call(object target,string name)=>target.GetType().GetMethod(name,Private).Invoke(target,null);
 static void Check(bool value,string reason){if(!value)throw new Exception(reason);}
 static object[] Parts(ThreeDControl control){var prepared=Get<object>(control,"prepared");return ((IEnumerable)prepared.GetType().GetField("Parts").GetValue(prepared)).Cast<object>().ToArray();}
 static StructureRoi Roi(object part)=>(StructureRoi)part.GetType().GetField("Roi").GetValue(part);
 public static void Run()
 {
  SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
  var organ=QualityTests.Sphere();organ.Center=new Vec3(0,0,0);
  var avoidance=QualityTests.Sphere();avoidance.InterpretedType="AVOIDANCE";avoidance.Visible=false;avoidance.Center=new Vec3(1,2,3);
  var additional=QualityTests.Sphere();additional.InterpretedType="AVOIDANCE";
  var external=QualityTests.Sphere();external.InterpretedType="EXTERNAL";
  var transform=new Matrix4(new double[]{0,-1,0,40,1,0,0,-20,0,0,1,8,0,0,0,1});
  foreach(bool compact in new[]{false,true})using(var control=new ThreeDControl(compact))
  {
   var window=new Window{Width=620,Height=480,Left=-30000,Top=-30000,ShowActivated=false,ShowInTaskbar=false,WindowStyle=WindowStyle.None,Content=control};
   try
   {
    window.Show();control.SetScene(new RenderScene{Structures=new List<RoiOverlay>{new RoiOverlay{Roi=organ},new RoiOverlay{Roi=avoidance,RoiToImage=transform},new RoiOverlay{Roi=additional},new RoiOverlay{Roi=external}}});FrameBenchmark.Pump(()=>Get<object>(control,"prepared")!=null,10);
    var original=Get<object>(control,"prepared");int generation=Get<int>(control,"generation");double yaw=Get<double>(control,"yaw"),pitch=Get<double>(control,"pitch"),distance=Get<double>(control,"distance");
    control.FocusStructure(organ);Check(ReferenceEquals(original,Get<object>(control,"prepared"))&&generation==Get<int>(control,"generation"),"default ROI focus reuses scene meshes");
    Check(((Model3DGroup)Get<ModelVisual3D>(control,"visual").Content).Children.OfType<GeometryModel3D>().Any(m=>m.Material is MaterialGroup&&((MaterialGroup)m.Material).Children.OfType<EmissiveMaterial>().Any()),"focused ROI has emissive highlight");
    control.FocusStructure(avoidance);FrameBenchmark.Pump(()=>Get<object>(control,"prepared")!=null,10);
    Check((Get<Vec3>(control,"target")-transform.Transform(avoidance.Center)).Length<1e-8,"focus maps representative center through registration");
    Check(Get<double>(control,"yaw")==yaw&&Get<double>(control,"pitch")==pitch&&Get<double>(control,"distance")>=distance,"focus preserves orientation and useful magnification");
    Check(Parts(control).Count(p=>Roi(p)==avoidance)==1&&Parts(control).All(p=>Roi(p)!=additional&&Roi(p)!=external),"explicit nondefault focus adds only chosen ROI and excludes EXTERNAL");
    Check(Get<CheckBox>(control,"allRois").IsChecked==false&&!avoidance.Visible,"explicit focus does not mutate ROI visibility or all-types filter");
    control.FocusStructure(external);Check(Get<StructureRoi>(control,"focusedRoi")==avoidance,"EXTERNAL cannot be focused into surface view");
    var prepared=Get<object>(control,"prepared");var quality=Get<Model3DGroup>(control,"qualityModels");var preview=Get<Model3DGroup>(control,"interactionModels");generation=Get<int>(control,"generation");
    Check(quality.IsFrozen&&preview.IsFrozen,"both presentation modes are cached frozen scene graphs");
    Call(control,"BeginInteraction");for(int i=0;i<30;i++){Call(control,"BeginInteraction");Call(control,"UpdateCamera");}
    Check(ReferenceEquals(prepared,Get<object>(control,"prepared"))&&generation==Get<int>(control,"generation")&&ReferenceEquals(preview,Get<ModelVisual3D>(control,"visual").Content),"orbit changes only camera and cached graph selection");
    Call(control,"QueueQualityRestore");FrameBenchmark.Pump(()=>!Get<bool>(control,"interacting"),2);Check(ReferenceEquals(quality,Get<ModelVisual3D>(control,"visual").Content),"idle restores exact full-quality graph");
    var volume=new VolumeData{Width=4,Height=4,Depth=4,SpacingX=1,SpacingY=1,SpacingZ=1,AxisX=new Vec3(1,0,0),AxisY=new Vec3(0,1,0),AxisZ=new Vec3(0,0,1)};
    control.SetSlicePlanes(new Vec3(1,1,1),volume);control.SetSlicePlanes(new Vec3(1,1,2),volume);Check(Get<bool>(control,"interacting"),"MPR scroll enters temporary interaction mode");FrameBenchmark.Pump(()=>!Get<bool>(control,"interacting"),2);Check(ReferenceEquals(quality,Get<ModelVisual3D>(control,"visual").Content),"MPR scroll idle restores full detail");
    Call(control,"BeginInteraction");Call(control,"QueueQualityRestore");window.Hide();Check(!Get<bool>(control,"interacting")&&!Get<DispatcherTimer>(control,"interactionIdle").IsEnabled,"hidden view stops pending restoration timer");window.Show();
    control.FocusStructure(avoidance);FrameBenchmark.Pump(()=>Get<object>(control,"prepared")!=null,10);Check(Get<StructureRoi>(control,"focusedRoi")==null&&Parts(control).All(p=>Roi(p)!=avoidance),"repeat selection clears highlight and temporary nondefault surface");
    control.FocusStructure(avoidance);control.FocusStructure(avoidance);FrameBenchmark.Pump(()=>Get<object>(control,"prepared")!=null,10);Check(Parts(control).All(p=>Roi(p)!=avoidance),"cancelled focus build cannot publish stale selected surface");
    Call(control,"BeginInteraction");Call(control,"QueueQualityRestore");control.Dispose();Check(!Get<DispatcherTimer>(control,"interactionIdle").IsEnabled&&Get<ModelVisual3D>(control,"visual").Content==null,"disposed view cancels restoration and releases scene");
   }
   finally{window.Close();}
  }
  Console.WriteLine("PASS 3D focus, registered center, emissive selection, nondefault/external filtering, orbit cache, idle restoration and cancellation (full + compact)");
 }
}
