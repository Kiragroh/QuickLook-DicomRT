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
internal static class PlaybackBufferBenchmark {
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static T Get<T>(object o,string n)=>(T)o.GetType().GetField(n,F).GetValue(o);
 static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,F).Invoke(o,args);
 public static int Run(string folder){int result=0;var app=new Application();var viewer=new ViewerControl();var window=new Window{Content=viewer,Width=1200,Height=800,Left=-30000,Top=-30000,ShowActivated=false,ShowInTaskbar=false};
 window.Loaded+=async(s,e)=>{try{
  var catalog=await Task.Run(()=>DicomCatalog.Scan(Directory.EnumerateFiles(folder).First(),CancellationToken.None));viewer.Open(catalog.Files.First(x=>x.Modality=="RTPLAN").Path);await viewer.LoadCompletion;
  Call(viewer,"SetWorkspace","MLC");var mlc=Get<MlcPlaybackControl>(viewer,"centralPlayback");var scene=Get<RenderScene>(mlc,"anatomy");var map=Get<Matrix4>(mlc,"planMap");var plan=Get<PlanData>(mlc,"plan");var beam=plan.Beams.Where(b=>b.ControlPoints.Count>10).Last();
  // Isolate computation from UI/preload contention. Only aggregate timings are printed.
  Call(mlc,"SuspendProjection");((IDisposable)Get<object>(mlc,"projectionCache")).Dispose();
  var rois=scene.Structures.Where(r=>r.Roi.InterpretedType=="PTV"||r.Roi.InterpretedType=="ORGAN").ToArray();
  Console.WriteLine("BUFFER selected_rois="+rois.Length+" control_points="+beam.ControlPoints.Count);
  await Task.Run(()=>{
   var cacheType=typeof(ViewerControl).Assembly.GetType("QuickLook.DicomRT.MlcProjectionCache");var cache=Activator.CreateInstance(cacheType);var watch=Stopwatch.StartNew();
   try{
    cacheType.GetMethod("PrepareNearby").Invoke(cache,new object[]{beam,0d,map,scene.Volume,rois,.4,true});
    ((Task)cacheType.GetProperty("WarmCompletion").GetValue(cache)).GetAwaiter().GetResult();
    Console.WriteLine("BUFFER prepare_ms="+watch.Elapsed.TotalMilliseconds.ToString("0")+" ready="+cacheType.GetProperty("PlaybackPrepared").GetValue(cache));
    for(int repeat=0;repeat<2;repeat++){int frames=0,misses=0;watch.Restart();
     for(double at=0;;at=MlcTimeline.NextLocal(at,beam.ControlPoints.Count-1,.4)){
      int i=(int)at,j=Math.Min(i+1,beam.ControlPoints.Count-1);string why;var p=BeamProjection.Create(beam,MlcTimeline.Interpolate(beam.ControlPoints[i],beam.ControlPoints[j],at-i),map,out why);
      if(!(bool)cacheType.GetMethod("TryGet").Invoke(cache,new object[]{p,scene.Volume,rois,MlcPlaybackControl.BeamExtent(beam),true,null}))misses++;frames++;
      if(at>=beam.ControlPoints.Count-1)break;
     }
     Console.WriteLine("BUFFER repeat="+repeat+" frames="+frames+" misses="+misses+" total_lookup_ms="+watch.Elapsed.TotalMilliseconds.ToString("0"));if(misses!=0)throw new Exception("Prepared field misses");
    }
   }finally{((IDisposable)cache).Dispose();}
  });
 }catch(Exception ex){result=1;Console.WriteLine("BUFFER failed="+ex.GetType().Name);}finally{viewer.Dispose();window.Close();}};app.Run(window);return result;}
}
