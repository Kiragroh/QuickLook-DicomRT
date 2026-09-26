using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using QuickLook.DicomRT;
internal static class FrameBenchmark
{
 const BindingFlags Instance=BindingFlags.NonPublic|BindingFlags.Instance;
 static object Field(object target,string name)=>target.GetType().GetField(name,Instance).GetValue(target);
 public static int Run(string folder,string output=null)
 {
  SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
  try
  {
   var cat=DicomCatalog.Scan(Directory.EnumerateFiles(folder).First(),CancellationToken.None);var stack=cat.Stacks.First(x=>x.Modality=="CT"&&x.CanMpr);var volume=VolumeData.Load(stack,CancellationToken.None);var links=RegistrationReader.Read(cat);var scene=new RenderScene{Volume=volume,Entry=stack.Entries[0]};
   foreach(var entry in cat.Files.Where(e=>e.Modality=="RTSTRUCT"))foreach(var roi in StructureSet.Load(entry).Rois){var transform=RegistrationReader.Resolve(links,roi.FrameUid,stack.FrameUid);if(transform!=null)scene.Structures.Add(new RoiOverlay{Roi=roi,RoiToImage=transform});}
   using(var control=new ThreeDControl())
   {
    var window=new Window{Width=1000,Height=800,Left=-30000,Top=-30000,ShowInTaskbar=false,ShowActivated=false,WindowStyle=WindowStyle.None,Content=control};window.Show();
    try
    {
     control.SetScene(scene);Pump(()=>Field(control,"prepared")!=null,40);control.UpdateLayout();var bitmap=new RenderTargetBitmap(1000,800,96,96,PixelFormats.Pbgra32);
     var update=typeof(ThreeDControl).GetMethod("UpdateCamera",Instance);var yaw=typeof(ThreeDControl).GetField("yaw",Instance);double initial=(double)yaw.GetValue(control);
     var interactive=typeof(ThreeDControl).GetMethod("BeginInteraction",Instance);
     for(int mode=0;mode<(interactive==null?1:3);mode++)
     {
      if(mode==1)interactive.Invoke(control,null);control.UpdateLayout();var cameraTimes=new double[20];var paintTimes=new double[20];
      int visibleTriangles=((Model3DGroup)((ModelVisual3D)Field(control,"visual")).Content).Children.OfType<GeometryModel3D>().Sum(x=>((MeshGeometry3D)x.Geometry).TriangleIndices.Count/3);
      for(int frame=-3;frame<20;frame++)
      {
       yaw.SetValue(control,initial+(frame+3)*.035);var watch=Stopwatch.StartNew();update.Invoke(control,null);watch.Stop();double cameraMs=watch.Elapsed.TotalMilliseconds;
       watch.Restart();bitmap.Clear();bitmap.Render(control);watch.Stop();if(frame>=0){cameraTimes[frame]=cameraMs;paintTimes[frame]=watch.Elapsed.TotalMilliseconds;}
      }
      Console.WriteLine("FRAME_BENCH mode="+new[]{"quality","interaction","restored_quality"}[mode]+" size=1000x800 frames=20 triangles="+visibleTriangles+" camera_median_ms="+Median(cameraTimes).ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" paint_median_ms="+Median(paintTimes).ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" paint_p95_ms="+paintTimes.OrderBy(x=>x).ElementAt(18).ToString("F3",System.Globalization.CultureInfo.InvariantCulture));
      if(output!=null)Save(bitmap,output,new[]{"public-3d-quality.png","public-3d-interaction.png","public-3d-restored.png"}[mode]);
      if(mode==1){typeof(ThreeDControl).GetMethod("QueueQualityRestore",Instance).Invoke(control,null);Pump(()=>!(bool)Field(control,"interacting"),2);if(!ReferenceEquals(Field(control,"qualityModels"),((ModelVisual3D)Field(control,"visual")).Content))throw new InvalidOperationException("Full quality not restored");}
     }
     if(output!=null)
     {
      var selected=scene.Structures.Select(r=>r.Roi).FirstOrDefault(r=>!ThreeDGeometry.ExternalRoi(r)&&(r.Name??"").IndexOf("brainstem",StringComparison.OrdinalIgnoreCase)>=0)??scene.Structures.Select(r=>r.Roi).First(r=>!ThreeDGeometry.ExternalRoi(r)&&!ThreeDGeometry.DefaultRoi(r));
      control.FocusStructure(selected);Pump(()=>Field(control,"prepared")!=null,30);control.UpdateLayout();bitmap.Clear();bitmap.Render(control);Save(bitmap,output,"public-3d-focus.png");
      Console.WriteLine("FOCUS_CAPTURE nondefault="+!ThreeDGeometry.DefaultRoi(selected)+" all_types=false");
     }
    }
    finally{window.Close();}
   }
   return 0;
  }
  catch(Exception error){Console.WriteLine("Frame benchmark failed: "+error.GetType().Name+" "+(error.InnerException?.GetType().Name??""));return 1;}
 }
 static void Save(RenderTargetBitmap bitmap,string output,string name){Directory.CreateDirectory(output);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(output,name)))encoder.Save(file);}
 static double Median(double[] values){var sorted=values.OrderBy(x=>x).ToArray();return (sorted[9]+sorted[10])*.5;}
 internal static void Pump(Func<bool> complete,double seconds)
 {
  var frame=new DispatcherFrame();var clock=Stopwatch.StartNew();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(20)};timer.Tick+=(s,e)=>{if(complete()||clock.Elapsed.TotalSeconds>seconds)frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);timer.Stop();if(!complete())throw new TimeoutException();
 }
}
