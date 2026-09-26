using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using QuickLook.DicomRT;
using H=HelixToolkit.Wpf.SharpDX;
internal static class GpuSceneBenchmark
{
 const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
 static object Get(object o,string n)=>o.GetType().GetField(n,Flags).GetValue(o);
 static object Call(object o,string n,params object[] p)=>o.GetType().GetMethod(n,Flags).Invoke(o,p);
 public static int Run(string folder,string approvedOutput=null)
 {
  SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
  try{
   var cat=DicomCatalog.Scan(Directory.EnumerateFiles(folder).First(),CancellationToken.None);var stack=cat.Stacks.First(x=>x.Modality=="CT"&&x.CanMpr);var volume=VolumeData.Load(stack,CancellationToken.None);var links=RegistrationReader.Read(cat);var scene=new RenderScene{Volume=volume,Entry=stack.Entries[0]};
   foreach(var e in cat.Files.Where(e=>e.Modality=="RTSTRUCT"))foreach(var r in StructureSet.Load(e).Rois){var transform=RegistrationReader.Resolve(links,r.FrameUid,stack.FrameUid);if(transform!=null)scene.Structures.Add(new RoiOverlay{Roi=r,RoiToImage=transform});}
   using(var control=new ThreeDControl()){
    var window=new Window{Width=1000,Height=800,Left=-30000,Top=-30000,ShowInTaskbar=false,ShowActivated=false,WindowStyle=WindowStyle.None,Content=control};
    try{window.Show();var watch=Stopwatch.StartNew();control.SetScene(scene);FrameBenchmark.Pump(()=>Get(control,"prepared")!=null,120);Console.WriteLine("GPU_SCENE prepare_ms="+watch.ElapsedMilliseconds+" "+((TextBlock)Get(control,"status")).Text);
     var bridge=Get(control,"gpu");if(bridge==null)throw new Exception("GPU unavailable");var view=(H.Viewport3DX)Get(bridge,"View");FrameBenchmark.Pump(()=>view.RenderHost?.RenderTargetBufferView!=null,10);
     double yaw=(double)Get(control,"yaw");var times=new double[20];BitmapSource bitmap=null;
     for(int i=-3;i<20;i++){typeof(ThreeDControl).GetField("yaw",Flags).SetValue(control,yaw+(i+3)*.035);Call(control,"UpdateCamera");watch.Restart();view.RenderHost.InvalidateRender();view.RenderHost.UpdateAndRender();bitmap=(BitmapSource)Call(bridge,"Capture");watch.Stop();if(i>=0)times[i]=watch.Elapsed.TotalMilliseconds;}
     Console.WriteLine("GPU_SCENE orbit_render_readback_median_ms="+times.OrderBy(x=>x).ElementAt(10).ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+" p95_ms="+times.OrderBy(x=>x).ElementAt(18).ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+" feature="+view.EffectsManager.Device.FeatureLevel+" driver="+((H.EffectsManager)view.EffectsManager).DriverType);
     if(approvedOutput!=null){Directory.CreateDirectory(approvedOutput);Save(bitmap,Path.Combine(approvedOutput,"3d-skin.png"));}
     var skin=(CheckBox)Get(control,"skin");skin.IsChecked=false;FrameBenchmark.Pump(()=>Get(control,"pending")==null,90);view.RenderHost.InvalidateRender();view.RenderHost.UpdateAndRender();bitmap=(BitmapSource)Call(bridge,"Capture");if(approvedOutput!=null)Save(bitmap,Path.Combine(approvedOutput,"3d-no-skin.png"));
     skin.IsChecked=true;FrameBenchmark.Pump(()=>Get(control,"pending")==null,90);var selected=scene.Structures.Select(x=>x.Roi).FirstOrDefault(r=>(r.Name??"").IndexOf("brainstem",StringComparison.OrdinalIgnoreCase)>=0);if(selected!=null){control.FocusStructure(selected);FrameBenchmark.Pump(()=>Get(control,"pending")==null,90);view.RenderHost.InvalidateRender();view.RenderHost.UpdateAndRender();if(approvedOutput!=null)Save((BitmapSource)Call(bridge,"Capture"),Path.Combine(approvedOutput,"3d-focused.png"));}
     Console.WriteLine("GPU_SCENE completed; full geometry retained while orbiting; no source writes");
    }finally{window.Close();}
   }return 0;
  }catch(Exception ex){Console.WriteLine("GPU_SCENE_FAILED "+ex.GetType().Name+" "+ex.InnerException?.GetType().Name);return 1;}
 }
 static void Save(BitmapSource bitmap,string path){var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(path))png.Save(file);}
}
