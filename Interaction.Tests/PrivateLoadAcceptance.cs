using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using QuickLook.DicomRT;
internal static class PrivateLoadAcceptance {
 const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
 static T Get<T>(object o,string n)=>(T)o.GetType().GetField(n,F).GetValue(o);
 public static int Run(string folder){int result=0;var app=new Application();ViewerControl viewer=null;var window=new Window{Width=1200,Height=850,Left=-30000,Top=-30000,ShowActivated=false,ShowInTaskbar=false};
 window.Loaded+=async(s,e)=>{try{
 var cat=await Task.Run(()=>DicomCatalog.Scan(Directory.EnumerateFiles(folder).First(),System.Threading.CancellationToken.None));
 foreach(var entry in cat.Files.Where(x=>x.Modality=="CT").Take(1).Concat(cat.Files.Where(x=>x.Modality=="RTPLAN"))){
 viewer?.Dispose();viewer=new ViewerControl();window.Content=viewer;viewer.Open(entry.Path);
 if(entry.Modality=="CT") {while(Get<PixelPlane>(viewer,"native")==null&&!viewer.LoadCompletion.IsCompleted)await Task.Delay(10);var pane=Get<System.Collections.Generic.List<SlicePane>>(viewer,"panes")[0];((Action<double>)typeof(SlicePane).GetField("ZoomChanged",F).GetValue(pane))(1.2);}
 await viewer.LoadCompletion;
 var plan=Get<PlanData>(viewer,"selectedPlan");var focus=Get<Vec3>(viewer,"focus");var stack=Get<ImageStack>(viewer,"currentStack");
 double error=plan==null?double.NaN:plan.Beams.SelectMany(b=>b.ControlPoints).Select(c=>(c.Isocenter-focus).Length).DefaultIfEmpty(double.NaN).Min();
 if(plan==null||stack==null||!Get<bool>(viewer,"initialIsocenterApplied")||Get<bool>(viewer,"userNavigatedImage"))throw new InvalidOperationException("Final automatic positioning missing");
 var iso=plan.Beams.SelectMany(b=>b.ControlPoints).OrderBy(c=>(c.Isocenter-focus).Length).First().Isocenter;
 int nearest=Enumerable.Range(0,stack.Entries.Count).OrderBy(i=>Math.Abs((stack.Entries[i].Origin-iso).Dot(stack.Entries[i].AxisX.Cross(stack.Entries[i].AxisY)))).First();
 if(Get<int>(viewer,"sliceIndex")!=nearest)throw new InvalidOperationException("Final slice is not the closest isocenter plane");
 Console.WriteLine("LOAD modality="+entry.Modality+" applied="+Get<bool>(viewer,"initialIsocenterApplied")+" navigated="+Get<bool>(viewer,"userNavigatedImage")+" plan="+(plan!=null)+" volume="+(Get<VolumeData>(viewer,"volume")!=null)+" iso_error_mm="+error.ToString("0.000"));
 }
 }catch(Exception ex){result=1;Console.WriteLine("FAIL private loading: "+ex.GetType().Name);}finally{viewer?.Dispose();window.Close();}};app.Run(window);return result;}
}
