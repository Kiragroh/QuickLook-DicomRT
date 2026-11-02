using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using QuickLook.DicomRT;

internal static class PreparationSettingsScenarios
{
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
 static object Get(object o,string name)=>o.GetType().GetField(name,F).GetValue(o);
 static object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,F).Invoke(o,args);
 static Type Type(string name)=>typeof(ViewerControl).Assembly.GetType("QuickLook.DicomRT."+name);
 public static void Run(Action<bool,string> check,string directory)
 {
  var prefType=Type("ViewerPreferences");var prefs=prefType.GetField("Current",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
  foreach(string name in new[]{"DrrOnDemand","ProgressiveDrr","OutlinesFirst","DoseRelevantOutlines"})check((bool)Get(prefs,name),"acceleration enabled by default: "+name);
  var set=new StructureSet{Entry=new DicomEntry{SopUid="test-structures"}};var target=new StructureRoi{Name="PTV",Description="x target",InterpretedType="PTV"};var organ=new StructureRoi{Name="Bladder",Description="Bladder",InterpretedType="ORGAN"};var helper=new StructureRoi{Name="x_helper",Description="Useful description"};set.Rois.AddRange(new[]{target,organ,helper});
  using(var viewer=new ViewerControl())
  {
   ((List<StructureSet>)Get(viewer,"structures")).Add(set);var modes=(ComboBox)Get(viewer,"roiLabelMode");modes.SelectedIndex=3;Call(viewer,"BuildRoiList");
   Func<StructureRoi[]> order=()=>((IList)Get(viewer,"roiOutlineRows")).Cast<object>().Select(x=>(StructureRoi)Get(x,"Roi")).ToArray();
   check(order().Last()==target,"x description sorts after organs and helpers even when typed PTV");
   helper.Visible=false;var hide=(CheckBox)Get(viewer,"hideX");hide.IsChecked=true;
   check(!order().Contains(target)&&!target.Visible&&order().Contains(helper),"x filter uses primary description, not canonical ROI name");
   hide.IsChecked=false;check(target.Visible&&!helper.Visible,"disabling x filter restores prior individual visibility");
   modes.SelectedIndex=0;check(order().Last()==helper,"name mode re-evaluates x sorting");hide.IsChecked=true;
   check(!order().Contains(helper)&&order().Contains(target),"name mode hides canonical x labels");hide.IsChecked=false;
  }
  prefType.GetField("LabelMode").SetValue(prefs,3);prefType.GetField("RelativeIsodoses").SetValue(prefs,true);prefType.GetField("DrrOnDemand").SetValue(prefs,false);
  var args=new object[]{null};check((bool)Call(prefs,"Save",args),"settings saved to test directory");
  var reloaded=Activator.CreateInstance(prefType,F,null,new object[]{directory},null);
  check((int)Get(reloaded,"LabelMode")==3&&(bool)Get(reloaded,"RelativeIsodoses")&&!(bool)Get(reloaded,"DrrOnDemand"),"label, isodose mode and disabled acceleration survive reload");
  prefType.GetField("LabelMode").SetValue(prefs,0);prefType.GetField("RelativeIsodoses").SetValue(prefs,false);prefType.GetField("DrrOnDemand").SetValue(prefs,true);
  var indices=Type("MlcProjectionCache").GetMethod("ProgressiveIndices",BindingFlags.Static|BindingFlags.NonPublic);
  var order9=(int[])indices.Invoke(null,new object[]{9});check(order9.SequenceEqual(new[]{0,8,4,2,6,1,3,5,7}),"DRR coverage refines gaps coarse-to-fine");
  foreach(int count in new[]{0,1,2,7,91,273}){var order=(int[])indices.Invoke(null,new object[]{count});check(order.OrderBy(x=>x).SequenceEqual(Enumerable.Range(0,count)),"progressive preparation visits each CP exactly once");}
  var eligibility=Type("MlcDoseRelevance").GetMethod("Eligible",BindingFlags.Static|BindingFlags.NonPublic);
  Func<double,double,bool> eligible=(value,max)=>(bool)eligibility.Invoke(null,new object[]{value,max});
  check(!eligible(5,50)&&eligible(5.01,50)&&!eligible(0,50),"strict D2 > ten percent of the highest D2, including boundary");
  check(eligible(double.NaN,50)&&eligible(0,0)&&eligible(5,double.NaN),"unknown D2 or missing reference cannot hide outlines");
  var beam=new PlanBeam{PatientPosition="HFS",SourceAxisDistance=1000};beam.ControlPoints.Add(new ControlPoint{Gantry=0,GantryRotationDirection="CW",CollimatorRotationDirection="NONE",CouchRotationDirection="NONE"});beam.ControlPoints.Add(new ControlPoint{Gantry=10});var plan=new PlanData();plan.Beams.Add(beam);
  var ct=new VolumeData{Width=8,Height=8,Depth=8,AxisX=new Vec3(1,0,0),AxisY=new Vec3(0,1,0),AxisZ=new Vec3(0,0,1),SpacingX=1,SpacingY=1,SpacingZ=1,Values=new float[512]};var scene=new RenderScene{Volume=ct,Entry=new DicomEntry{Modality="CT"}};
  var cache=Activator.CreateInstance(Type("MlcProjectionCache"));try{
   Call(cache,"Configure",plan,scene,Matrix4.Identity);((Task)cache.GetType().GetProperty("WarmCompletion").GetValue(cache)).GetAwaiter().GetResult();string why;var projection=BeamProjection.Create(beam,beam.ControlPoints[0],Matrix4.Identity,out why);
   check(!(bool)Call(cache,"HasDrr",projection,ct,100d),"DRR stays uncomputed while disabled");cache.GetType().GetField("DrrEnabled").SetValue(cache,true);Call(cache,"Configure",plan,scene,Matrix4.Identity);((Task)cache.GetType().GetProperty("WarmCompletion").GetValue(cache)).GetAwaiter().GetResult();
   check((bool)Call(cache,"HasDrr",projection,ct,100d),"enabling DRR starts recorded-view preparation");
   Call(cache,"PrepareNearby",beam,.4,Matrix4.Identity,ct,new RoiOverlay[0],.4,true);((Task)cache.GetType().GetProperty("WarmCompletion").GetValue(cache)).GetAwaiter().GetResult();
   var fractional=BeamProjection.Create(beam,MlcTimeline.Interpolate(beam.ControlPoints[0],beam.ControlPoints[1],.4),Matrix4.Identity,out why);
   check(!(bool)Call(cache,"HasDrr",fractional,ct,100d),"progressive playback does not expand hidden fractional DRR work");
   cache.GetType().GetField("DrrEnabled").SetValue(cache,false);Call(cache,"Configure",plan,scene,Matrix4.Identity);check((bool)Call(cache,"HasDrr",projection,ct,100d),"switching DRR off retains computed cache");
  }finally{((IDisposable)cache).Dispose();}

  using(var mlc=new MlcPlaybackControl()){
   mlc.SetPlan(plan);mlc.GetType().GetField("anatomy",F).SetValue(mlc,scene);mlc.GetType().GetField("planMap",F).SetValue(mlc,Matrix4.Identity);
   check((bool)Call(mlc,"HasProgressiveProjection"),"supported beam uses progressive DRR playback");
   beam.PatientPosition="UNKNOWN";check(!(bool)Call(mlc,"HasProgressiveProjection"),"unsupported geometry retains aperture playback instead of waiting forever for a DRR");beam.PatientPosition="HFS";
   check((int)Call(mlc,"FindPreparedControlPoint")==-1,"adaptive playback does not advance to an unprepared view");
   string why;var last=BeamProjection.Create(beam,beam.ControlPoints[1],Matrix4.Identity,out why);Call(Get(mlc,"projectionCache"),"Render",last,ct,new RoiOverlay[0],100d,true,CancellationToken.None,true,false);
   check((int)Call(mlc,"FindPreparedControlPoint")==1,"adaptive playback selects a prepared exact CP without a full-field buffer");mlc.Close();
  }
  DoseFilterChecks(check);
  using(var viewer=new ViewerControl()){
   var window=(System.Windows.Window)Call(viewer,"CreateSettingsWindow");var content=(System.Windows.FrameworkElement)window.Content;
   content.Measure(new System.Windows.Size(540,780));content.Arrange(new System.Windows.Rect(0,0,540,content.DesiredSize.Height));content.UpdateLayout();
   check(content.ActualHeight<780,"all preference controls fit the compact settings window");
   var image=new System.Windows.Media.Imaging.RenderTargetBitmap(540,(int)Math.Ceiling(content.ActualHeight),96,96,System.Windows.Media.PixelFormats.Pbgra32);image.Render(content);
   string output=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"settings-preview.png");using(var stream=File.Create(output)){var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));encoder.Save(stream);}
  }
 }

 static void DoseFilterChecks(Action<bool,string> check)
 {
  var volume=new VolumeData{Width=12,Height=8,Depth=8,Values=new float[768],AxisX=new Vec3(1,0,0),AxisY=new Vec3(0,1,0),AxisZ=new Vec3(0,0,1),SpacingX=1,SpacingY=1,SpacingZ=1,Max=100};
  for(int i=0;i<volume.Values.Length;i++)volume.Values[i]=i%12<6?1:100;
  var makeDose=typeof(DoseGrid).GetMethod("FromDerivedVolume",BindingFlags.Static|BindingFlags.NonPublic);
  var dose=(DoseGrid)makeDose.Invoke(null,new object[]{volume,"synthetic-frame","synthetic dose"});
  Func<double,StructureRoi> box=x=>{var roi=new StructureRoi();foreach(double z in new[]{2d,3d,4d})roi.Contours.Add(new Contour{GeometricType="CLOSED_PLANAR",Points=new List<Vec3>{new Vec3(x,2,z),new Vec3(x+1,2,z),new Vec3(x+1,3,z),new Vec3(x,3,z)}});return roi;};
  var low=box(2);var high=box(8);var outside=box(20);
  var scene=new RenderScene();scene.Doses.Add(new DoseOverlay{Dose=dose});foreach(var roi in new[]{low,high,outside})scene.Structures.Add(new RoiOverlay{Roi=roi});
  var filter=Activator.CreateInstance(Type("MlcDoseRelevance"),new object[]{new Action(()=>{})});
  try{
   Task.Run(()=>Call(filter,"Configure",scene,true)).GetAwaiter().GetResult();((Task)filter.GetType().GetProperty("Completion").GetValue(filter)).GetAwaiter().GetResult();
   check(!(bool)Call(filter,"Allows",low)&&(bool)Call(filter,"Allows",high),"measured synthetic D2 filters low-dose ROI relative to highest ROI D2");
   check((bool)Call(filter,"Allows",outside),"incomplete dose coverage remains unknown and available");
   int cached=((IList)Get(filter,"saved")).Count;Call(filter,"Configure",scene,false);check((bool)Call(filter,"Allows",low),"disabling dose relevance restores low-dose outlines");
   Task.Run(()=>Call(filter,"Configure",scene,true)).GetAwaiter().GetResult();((Task)filter.GetType().GetProperty("Completion").GetValue(filter)).GetAwaiter().GetResult();
   check(((IList)Get(filter,"saved")).Count==cached,"re-enabling dose filter reuses calculated D2 values");
   var changed=(DoseGrid)makeDose.Invoke(null,new object[]{new VolumeData{Width=12,Height=8,Depth=8,Values=Enumerable.Repeat(50f,768).ToArray(),AxisX=new Vec3(1,0,0),AxisY=new Vec3(0,1,0),AxisZ=new Vec3(0,0,1),SpacingX=1,SpacingY=1,SpacingZ=1,Max=50},"synthetic-frame","other synthetic dose"});scene.Doses[0].Dose=changed;
   Task.Run(()=>Call(filter,"Configure",scene,true)).GetAwaiter().GetResult();((Task)filter.GetType().GetProperty("Completion").GetValue(filter)).GetAwaiter().GetResult();check((bool)Call(filter,"Allows",low),"dose changes never reuse the former dose's exclusion");
   scene.Doses.Clear();Call(filter,"Configure",scene,true);check((bool)Call(filter,"Allows",low),"no dose keeps all structures eligible");
  }finally{((IDisposable)filter).Dispose();}
 }
 public static int Inspect(string folder)
 {
  try{
   var entry=Directory.EnumerateFiles(folder).Select(DicomCatalog.ReadEntry).First(e=>e.Modality=="RTSTRUCT");var set=StructureSet.Load(entry);
   foreach(var roi in set.Rois)roi.LabelMode=RoiLabelMode.DescriptionFirst;
   int x=set.Rois.Count(r=>r.HasXPrefix);var group=Type("RoiLabels").GetMethod("GroupOrder");var sorted=set.Rois.OrderBy(r=>(int)group.Invoke(null,new object[]{r,null})).ThenBy(r=>r.PrimaryLabel,StringComparer.CurrentCultureIgnoreCase).ToArray();
   if(!sorted.Skip(sorted.Length-x).All(r=>r.HasXPrefix))throw new Exception("Prefix ordering failed");
   Console.WriteLine("PASS private label check: "+set.Rois.Count+" structures; "+x+" x-prefixed primary descriptions placed last; hidden list "+(set.Rois.Count-x)+". Source files read-only; no labels exported.");return 0;
  }catch(Exception e){Console.WriteLine("FAIL private label check: "+e.GetType().Name);return 1;}
 }
}




