using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using QuickLook.DicomRT;
internal static class NavigationBenchmark
{
 const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static T Get<T>(object o,string n)=>(T)o.GetType().GetField(n,Flags).GetValue(o);
 static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,Flags).Invoke(o,args);
 public static int Run(string folder,bool quad=false)
 {
  int result=0;var app=new Application();var viewer=new ViewerControl();var window=new Window{Width=1200,Height=800,Left=-30000,Top=-30000,ShowInTaskbar=false,ShowActivated=false,Content=viewer};
  window.Loaded+=async(s,e)=>{try{
   var catalog=await Task.Run(()=>DicomCatalog.Scan(Directory.EnumerateFiles(folder).First(),CancellationToken.None));var ct=catalog.Files.First(x=>x.Modality=="CT");viewer.Open(ct.Path);await viewer.LoadCompletion;Call(viewer,"SetWorkspace","Bild");
   if(quad){Get<ComboBox>(viewer,"planes").SelectedItem="MPR + 3D";viewer.UpdateLayout();}
   var stack=Get<ImageStack>(viewer,"currentStack");if(stack.Entries.Count<30)throw new Exception("Requires 30 images");var dispatch=new double[20];var settled=new double[20];
   for(int pass=0;pass<25;pass++){var watch=Stopwatch.StartNew();if(quad)await (Task)Call(viewer,"ScrollAsync","Axial",pass%2==0?1:-1);else await (Task)Call(viewer,"ShowSliceAsync",stack.Entries.Count/2+pass-12,true);double dispatched=watch.Elapsed.TotalMilliseconds;var panes=Get<System.Collections.Generic.List<SlicePane>>(viewer,"panes");while(panes.Any(p=>Get<object>(p,"pending")!=null)){await Task.Delay(1);if(watch.Elapsed.TotalSeconds>10)throw new Exception("Frame timeout");}if(pass>=5){dispatch[pass-5]=dispatched;settled[pass-5]=watch.Elapsed.TotalMilliseconds;}}
   Console.WriteLine("NAVIGATION quad="+quad+" samples=20 warm hidden_tags viewport=1200x800 dispatch_median_ms="+dispatch.OrderBy(x=>x).ElementAt(10).ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+" settled_median_ms="+settled.OrderBy(x=>x).ElementAt(10).ToString("0.00",System.Globalization.CultureInfo.InvariantCulture));
  }catch(Exception ex){result=1;Console.WriteLine("Navigation benchmark failed: "+ex.GetType().Name);}finally{viewer.Dispose();window.Close();}};app.Run(window);return result;
 }
}
