using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using QuickLook.DicomRT;
internal static class PrivateSumAcceptance
{
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
 static T Get<T>(object o,string n)=>(T)o.GetType().GetField(n,F).GetValue(o);
 static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,F).Invoke(o,args);
 static void Check(bool value){if(!value)throw new InvalidOperationException("Verification failed");}
 public static int Run(string folder)
 {
  int result=0;var app=new Application();var viewer=new ViewerControl();var window=new Window{Content=viewer,Width=1250,Height=850,Left=-30000,Top=-30000,ShowActivated=false,ShowInTaskbar=false,Title="Local sum verification"};
  window.Loaded+=async(s,e)=>{try{
   var cat=await Task.Run(()=>DicomCatalog.Scan(Directory.EnumerateFiles(folder).First(),CancellationToken.None));
   viewer.Open(cat.Files.First(f=>f.Modality=="CT").Path);await viewer.LoadCompletion;
   var doses=Get<List<DoseGrid>>(viewer,"doses");var plans=Get<List<PlanData>>(viewer,"planData");var groups=DoseSum.CompatibleGroups(doses,Get<List<RegistrationLink>>(viewer,"registrations"));Check(groups.Count==1&&groups[0].Count==3);Check(doses.Count==4&&plans.Count==4);
   var blocks=plans.SelectMany(p=>p.Beams).SelectMany(b=>b.ControlPoints).SelectMany(c=>c.Blocks).Distinct().ToArray();Check(blocks.Length==3&&blocks.All(b=>b.Triangles.Length>0));
   var picker=Get<ComboBox>(viewer,"plans");var sumChoice=picker.Items.Cast<object>().Single(c=>(bool)c.GetType().GetField("Sum").GetValue(c));picker.SelectedItem=sumChoice;
   var clock=Stopwatch.StartNew();while(Get<DoseSumResult>(viewer,"sumResult")?.Dose==null){await Task.Delay(30);if(clock.Elapsed.TotalSeconds>90)throw new TimeoutException();}
   await Task.Delay(300);var sum=Get<DoseSumResult>(viewer,"sumResult");Check(sum.IncludedCount==3);Check(Get<DicomEntry>(viewer,"currentEntry").FrameUid==sum.Dose.FrameUid);
   await (Task)Call(viewer,"JumpToPlanDoseAsync",true);Check((Get<Vec3>(viewer,"focus")-sum.Dose.MaximumPosition.Value).Length<=3.1);
   var exclusions=Get<HashSet<string>>(viewer,"sumExcluded");exclusions.Add(groups[0][0].PlanUid);Call(viewer,"MarkSumPending");Check(Get<DoseSumResult>(viewer,"sumResult").IncludedCount==3);await (Task)Call(viewer,"BuildSumAsync");Check(Get<DoseSumResult>(viewer,"sumResult").IncludedCount==2);
   foreach(var choice in picker.Items.Cast<object>().Where(c=>!(bool)c.GetType().GetField("Sum").GetValue(c)).ToArray()){
    typeof(ViewerControl).GetField("changing",F).SetValue(viewer,true);picker.SelectedItem=choice;typeof(ViewerControl).GetField("changing",F).SetValue(viewer,false);await (Task)Call(viewer,"SelectPlanChoiceAsync");
    var plan=Get<PlanData>(viewer,"selectedPlan");var iso=plan.Beams.SelectMany(b=>b.ControlPoints).First().Isocenter;Check((Get<Vec3>(viewer,"focus")-iso).Length<3.1);
   }
   var renderer=typeof(ViewerControl).Assembly.GetType("QuickLook.DicomRT.BlockApertureGeometry").GetMethod("Create",F);var cp=plans.SelectMany(p=>p.Beams).SelectMany(b=>b.ControlPoints).First(c=>c.Blocks.Count>0);clock.Restart();var shape=(System.Windows.Media.Geometry)renderer.Invoke(null,new[]{cp});double first=clock.Elapsed.TotalMilliseconds;clock.Restart();for(int i=0;i<100;i++)renderer.Invoke(null,new[]{cp});Check(shape.Bounds.Width<150&&shape.Bounds.Height<150);
   Console.WriteLine("PASS private case: 4 plans; separate coordinate groups; 3-plan and 2-plan sums; Dmax navigation; 3 custom electron cutouts. No identifiers or images exported.");Console.WriteLine("BLOCK first_ms="+first.ToString("0.0")+" cached_100_ms="+clock.Elapsed.TotalMilliseconds.ToString("0.0"));
  }catch(Exception ex){result=1;Console.WriteLine("FAIL private case: "+ex.GetType().Name+" at "+ex.StackTrace?.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).FirstOrDefault());}finally{viewer.Dispose();window.Close();}};app.Run(window);return result;
 }
}
