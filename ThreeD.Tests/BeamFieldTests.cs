using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using QuickLook.DicomRT;
internal static class BeamFieldTests
{
 const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
 static object Get(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);}
 public static void Run()
 {
  using(var control=new ThreeDControl()){
   var plan=new PlanData();for(int i=0;i<3;i++){var beam=new PlanBeam{Number=i+1,PatientPosition="HFS",SourceAxisDistance=1000,Meterset=200,FinalCumulativeMetersetWeight=1,PrimaryDosimeterUnit="MU",TreatmentDeliveryType=i==2?"SETUP":"TREATMENT"};beam.ControlPoints.Add(new ControlPoint{Gantry=0,GantryRotationDirection="CW",DoseRateSet=600});beam.ControlPoints.Add(new ControlPoint{Gantry=90,GantryRotationDirection="CW",MetersetWeight=.2,DoseRateSet=600});beam.ControlPoints.Add(new ControlPoint{Gantry=180,MetersetWeight=1});plan.Beams.Add(beam);}
   control.SetScene(new RenderScene{Plan=plan,PlanToImage=Matrix4.Identity});var old=Get(control,"prepared");int generation=(int)Get(control,"generation");
   ((CheckBox)Get(control,"showBeamFields")).IsChecked=true;Check(ReferenceEquals(old,Get(control,"prepared"))&&generation==(int)Get(control,"generation"),"3D fields toggle does not rebuild ROI scene");
   var overlay=(FrameworkElement)Get(control,"beamFields");var type=overlay.GetType();FrameBenchmark.Pump(()=>((Array)Get(overlay,"Ready")).Length==3,10);
   var ready=Get(overlay,"Ready");var camera=new PerspectiveCamera(new Point3D(0,0,600),new Vector3D(0,0,-1),new Vector3D(0,1,0),60){NearPlaneDistance=.1,FarPlaneDistance=2000};
   type.GetMethod("Set",F).Invoke(overlay,new object[]{new RenderScene{Plan=plan,PlanToImage=Matrix4.Identity},camera,180d});
   overlay.Measure(new Size(600,600));overlay.Arrange(new Rect(0,0,600,600));overlay.UpdateLayout();var bitmap=new RenderTargetBitmap(600,600,96,96,PixelFormats.Pbgra32);bitmap.Render(overlay);var pixels=new byte[600*600*4];bitmap.CopyPixels(pixels,2400,0);Check(Enumerable.Range(0,600*600).Count(i=>pixels[i*4]>100)>1000,"3D all-field guide rasterizes paths, variable bars and legends");
   camera.Position=new Point3D(600,0,0);camera.LookDirection=new Vector3D(-1,0,0);type.GetMethod("Set",F).Invoke(overlay,new object[]{new RenderScene{Plan=plan,PlanToImage=Matrix4.Identity},camera,180d});Check(ReferenceEquals(ready,Get(overlay,"Ready")),"orbit reuses prepared field paths");
   control.SetCompact(true);Check(((CheckBox)Get(control,"showBeamFields")).IsChecked==true,"2x2 retains 3D field setting");
   type.GetMethod("Set",F).Invoke(overlay,new object[]{new RenderScene(),camera,180d});Check(((Array)Get(overlay,"Ready")).Length==0,"missing plan clears prior patient's field paths");
  }
  Console.WriteLine("PASS 3D field guide, additive toggle, camera reuse and context isolation");
 }
}
