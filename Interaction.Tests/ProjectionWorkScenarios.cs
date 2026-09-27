using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using QuickLook.DicomRT;
internal static class ProjectionWorkScenarios {
 static readonly MethodInfo build=typeof(ViewerControl).Assembly.GetType("QuickLook.DicomRT.MlcProjectionCache").GetMethod("BuildWarmViews",BindingFlags.Static|BindingFlags.NonPublic);
 static Array Views(PlanData plan,PlanBeam active=null)=>(Array)build.Invoke(null,new object[]{plan,Matrix4.Identity,active,CancellationToken.None});
 static PlanBeam Beam()=>new PlanBeam{PatientPosition="HFS",SourceAxisDistance=1000};
 public static void Run(Action<bool,string> check){
  var playback=typeof(ViewerControl).Assembly.GetType("QuickLook.DicomRT.MlcProjectionCache").GetMethod("BuildPlaybackViews",BindingFlags.Static|BindingFlags.NonPublic);
  var longArc=Beam();for(int n=0;n<91;n++)longArc.ControlPoints.Add(new ControlPoint{Gantry=n,GantryRotationDirection="CW",CollimatorRotationDirection="NONE",CouchRotationDirection="NONE"});
  var buffer=(Array)playback.Invoke(null,new object[]{longArc,Matrix4.Identity,.4});
  check(buffer.Length==226,"91-point arc prebuffers all 226 exact playback views, not only 91 recorded angles or 24 lookahead frames");
  var progressType=typeof(ViewerControl).Assembly.GetType("QuickLook.DicomRT.MlcPlaybackBuffer");var progress=Activator.CreateInstance(progressType,new object[]{buffer,true,true});
  check(!(bool)progressType.GetProperty("Ready").GetValue(progress),"new playback buffer starts pending even when recorded preload finished");
  var mark=progressType.GetMethod("Mark");foreach(var job in buffer)mark.Invoke(progress,new object[]{job.GetType().GetField("Key").GetValue(job),false});
  check(!(bool)progressType.GetProperty("Ready").GetValue(progress)&&((string)progressType.GetProperty("Text").GetValue(progress)).Contains("DRRs 0/226"),"completed contours do not hide missing DRR playback work");
  foreach(var job in buffer){var key=job.GetType().GetField("Key").GetValue(job);mark.Invoke(progress,new object[]{key,true});mark.Invoke(progress,new object[]{key,true});}
  check((bool)progressType.GetProperty("Ready").GetValue(progress),"all requested playback overlays complete exactly once");
  var fixedBeam=Beam();for(int i=0;i<100;i++)fixedBeam.ControlPoints.Add(new ControlPoint{Gantry=20,Collimator=5,MetersetWeight=i/99d,XJaws=new[]{-10d+i*.01,10d}});
  var plan=new PlanData();plan.Beams.Add(fixedBeam);
  check(Views(plan).Length==1,"100 fixed-field aperture CPs need only one anatomical projection");
  fixedBeam.ControlPoints[99].Collimator=6;check(Views(plan).Length==2,"different collimator requires its own exact projection");
  var arc=Beam();for(int i=0;i<4;i++)arc.ControlPoints.Add(new ControlPoint{Gantry=i*10});plan.Beams.Add(arc);
  check(Views(plan).Length==6,"whole-plan preparation uses recorded distinct views without fractional expansion");
  var imaging=Beam();imaging.TreatmentDeliveryType="SETUP";imaging.ControlPoints.Add(new ControlPoint{Gantry=130});plan.Beams.Add(imaging);
  check(Views(plan).Length==6&&Views(plan,imaging).Length==7,"imaging projection is only warmed when explicitly selected");
  var jobs=Views(plan,arc);var projection=(BeamProjection)jobs.GetValue(0).GetType().GetField("Projection").GetValue(jobs.GetValue(0));string why;
  var expected=BeamProjection.Create(arc,arc.ControlPoints[0],Matrix4.Identity,out why);check((expected.Source-projection.Source).Length<1e-8,"active field is prepared first");
  var duplicate=Beam();duplicate.ControlPoints.Add(new ControlPoint{Gantry=0});plan.Beams.Add(duplicate);check(Views(plan).Length==6,"identical anatomy view reused across fields");
  var unsupported=Beam();unsupported.PatientPosition="";unsupported.ControlPoints.Add(new ControlPoint());plan.Beams.Add(unsupported);check(Views(plan).Length==6,"unsupported geometry creates no misleading prepared count");
 }
 public static int Inspect(string folder){try{
  var cat=DicomCatalog.Scan(Directory.EnumerateFiles(folder).First(),CancellationToken.None);int plans=0,old=0,now=0;
  foreach(var entry in cat.Files.Where(e=>e.Modality=="RTPLAN")){
   var plan=PlanData.Load(entry);plans++;old+=plan.Beams.Sum(b=>b.ControlPoints.Count+Math.Max(0,b.ControlPoints.Count-1)*4);now+=Views(plan).Length;
  }
  Console.WriteLine("PROJECTION_WORKLOAD plans="+plans+" previous_view_jobs="+old+" unique_recorded_views="+now+" reduction_percent="+(old==0?0:100d*(old-now)/old).ToString("0.0",System.Globalization.CultureInfo.InvariantCulture));return 0;
 }catch(Exception ex){Console.WriteLine("Projection inspection failed: "+ex.GetType().Name);return 1;}}
}
