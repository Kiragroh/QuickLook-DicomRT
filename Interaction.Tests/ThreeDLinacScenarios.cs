using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Media3D;
using QuickLook.DicomRT;
internal static class ThreeDLinacScenarios {
 const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
 static T Get<T>(object o,string n)=>(T)o.GetType().GetField(n,F).GetValue(o);
 public static void Run(Action<bool,string> check){
  using(var mlc=new MlcPlaybackControl())check(Get<string>(Get<object>(mlc,"aperture"),"drrPreset")=="High contrast","new MLC viewers start DRRs with high contrast");
  foreach(bool compact in new[]{false,true})using(var three=new ThreeDControl(compact)){
   var overlay=Get<Viewbox>(three,"linacOverlay");var model=Get<object>(three,"linacOrientation");check(overlay.Visibility==Visibility.Collapsed&&!overlay.IsHitTestVisible,"3D LINAC stays hidden without a selected field and does not block orbit input");
   var beam=new PlanBeam{PatientPosition="HFS",ControlPoints={new ControlPoint{Gantry=10,Couch=25,Collimator=33},new ControlPoint{Gantry=90,Couch=25,Collimator=75}}};var plan=new PlanData{Beams={beam}};
   var scene=new RenderScene{Plan=plan,ActiveBeam=beam,ActiveControlPoint=beam.ControlPoints[0],PlanToImage=Matrix4.Identity};three.SetScene(scene);
   check(overlay.Visibility==Visibility.Visible&&Get<AxisAngleRotation3D>(model,"gantryRotation").Angle==10&&Get<AxisAngleRotation3D>(model,"couchRotation").Angle==25,"LINAC displays selected gantry and couch in "+(compact?"2x2":"standalone 3D"));
   var patient=Get<Model3DGroup>(model,"patientHost").Children[0];int generation=Get<int>(three,"generation");double distance=Get<double>(three,"distance");
   scene.ActiveControlPoint=beam.ControlPoints[1];scene.ActiveControlPointIndex=1;three.UpdateFields(scene);
   var dial=Get<object>(model,"collimator");check(Get<AxisAngleRotation3D>(model,"gantryRotation").Angle==90&&(double)dial.GetType().GetProperty("Angle").GetValue(dial)==75,"3D LINAC follows current CP and collimator");
   check(ReferenceEquals(patient,Get<Model3DGroup>(model,"patientHost").Children[0])&&generation==Get<int>(three,"generation")&&distance==Get<double>(three,"distance"),"LINAC playback reuses patient geometry without rebuilding anatomy or changing camera");
   scene.ActiveBeam=null;scene.ActiveControlPoint=null;three.UpdateFields(scene);check(overlay.Visibility==Visibility.Collapsed,"neutral field selection removes stale LINAC orientation");
  }
 }
}