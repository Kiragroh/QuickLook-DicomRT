using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace QuickLook.DicomRT
{
    // Viewer-lifetime cache: survives switching to images/3D. No disk or patient-data export.
    internal sealed class MlcProjectionCache : IDisposable
    {
        sealed class Identity { public int Value=Interlocked.Increment(ref identity); }
        static int identity;
        static readonly ConditionalWeakTable<object,Identity> ids=new ConditionalWeakTable<object,Identity>();
        static int Id(object o)=>o==null?0:ids.GetValue(o,k=>new Identity()).Value;
        readonly object gate=new object();
        readonly Dictionary<string,BitmapSource> drrs=new Dictionary<string,BitmapSource>();
        readonly Queue<string> drrOrder=new Queue<string>();
        readonly Dictionary<string,MlcProjectionFrame> outlines=new Dictionary<string,MlcProjectionFrame>();
        readonly Queue<string> outlineOrder=new Queue<string>();
        readonly MlcProjectionRenderer foreground=new MlcProjectionRenderer(),background=new MlcProjectionRenderer();
        long drrBytes;int outlineWeight;
        CancellationTokenSource warmLifetime=new CancellationTokenSource();
        string context;bool disposed;
        Action<CancellationToken> nearby;int nearbyVersion;
        public int Prepared {get;private set;}
        public int Total {get;private set;}
        public event Action FrameReady;
        public Task WarmCompletion {get;private set;}=Task.CompletedTask;
        static string Values(params double[] values)=>string.Join(",",values.Select(x=>Math.Round(x,8).ToString("R",CultureInfo.InvariantCulture)));
        static string GeometryKey(BeamProjection p,double extent)=>Values(p.Source.X,p.Source.Y,p.Source.Z,p.Iso.X,p.Iso.Y,p.Iso.Z,p.Right.X,p.Right.Y,p.Right.Z,p.Up.X,p.Up.Y,p.Up.Z,p.Sad,extent);
        static string OutlineKey(string geometry,RoiOverlay[] rois)=>geometry+"|"+string.Join(";",rois.Select(r=>Id(r.Roi)+":"+Values(r.RoiToImage.Values)));
        public bool TryGet(BeamProjection p,VolumeData ct,RoiOverlay[] rois,double extent,bool drr,out MlcProjectionFrame frame)
        {
            string geometry=GeometryKey(p,extent),okey=OutlineKey(geometry,rois);BitmapSource image=null;MlcProjectionFrame shapes=null;
            lock(gate){if(drr&&ct!=null&&!drrs.TryGetValue(Id(ct)+":"+geometry,out image)){frame=null;return false;}
                if(rois.Length>0&&!outlines.TryGetValue(okey,out shapes)){frame=null;return false;}}
            frame=Join(image,shapes,extent,drr&&ct!=null);return true;
        }
        static MlcProjectionFrame Join(BitmapSource image,MlcProjectionFrame shapes,double extent,bool wanted)
        {
            var frame=new MlcProjectionFrame{Extent=extent,Drr=image};if(shapes!=null)frame.Outlines.AddRange(shapes.Outlines);
            frame.Note=(image!=null?"CT-derived DRR \u00b7 cached":wanted?"Preparing CT-derived DRR":"DRR off / matching CT unavailable")+" \u00b7 "+frame.Outlines.Count+" outlines";
            return frame;
        }
        public MlcProjectionFrame Render(BeamProjection p,VolumeData ct,RoiOverlay[] rois,double extent,bool drr,CancellationToken token,bool warming=false)
        {
            var renderer=warming?background:foreground;string geometry=GeometryKey(p,extent),dkey=Id(ct)+":"+geometry,okey=OutlineKey(geometry,rois);
            BitmapSource image=null;MlcProjectionFrame shapes=null;
            lock(gate){if(drr&&ct!=null)drrs.TryGetValue(dkey,out image);if(rois.Length>0)outlines.TryGetValue(okey,out shapes);}
            if(drr&&ct!=null&&image==null){image=renderer.Render(p,ct,new RoiOverlay[0],extent,384,true,token,warming?1:2).Drr;token.ThrowIfCancellationRequested();
                lock(gate){if(!disposed&&!drrs.ContainsKey(dkey)){while(drrBytes+147456>160L*1024*1024&&drrOrder.Count>0){var old=drrOrder.Dequeue();drrs.Remove(old);drrBytes-=147456;}drrs[dkey]=image;drrOrder.Enqueue(dkey);drrBytes+=147456;}}}
            if(rois.Length>0&&shapes==null){shapes=renderer.Render(p,null,rois,extent,256,false,token);token.ThrowIfCancellationRequested();
                lock(gate){if(!disposed&&!outlines.ContainsKey(okey)){while((outlineWeight+rois.Length>2048||outlines.Count>=256)&&outlineOrder.Count>0){var old=outlineOrder.Dequeue();outlineWeight-=outlines[old].Outlines.Count;outlines.Remove(old);}outlines[okey]=shapes;outlineOrder.Enqueue(okey);outlineWeight+=shapes.Outlines.Count;}}}
            return Join(image,shapes,extent,drr&&ct!=null);
        }
        public void Configure(PlanData plan,RenderScene scene,Matrix4 map)
        {
            var ct=scene?.Entry?.Modality=="CT"?scene.Volume:null;
            var warmRois=(scene?.Structures??new List<RoiOverlay>()).Where(r=>string.Equals(r.Roi.InterpretedType,"PTV",StringComparison.OrdinalIgnoreCase)).ToArray();
            string next=Id(plan)+":"+Id(ct)+":"+(map==null?"none":Values(map.Values))+":"+OutlineKey("",warmRois);if(next==context||disposed)return;context=next;
            warmLifetime.Cancel();warmLifetime=new CancellationTokenSource();var token=warmLifetime.Token;Prepared=Total=0;
            lock(gate)nearby=null;
            if(plan==null||map==null||ct==null&&warmRois.Length==0)return;
            var jobs=MlcTimeline.PlaybackOrder(plan).SelectMany(b=>{var extent=MlcPlaybackControl.BeamExtent(b);return b.ControlPoints.Select(cp=>Tuple.Create(b,cp,extent));}).ToArray();
            Total=jobs.Length;
            // A dedicated low-priority worker and one ray thread leave CPU capacity for interaction.
            var previous=WarmCompletion;
            WarmCompletion=Task.Run(async()=>{try{await previous.ConfigureAwait(false);}catch(OperationCanceledException){}catch(Exception){}token.ThrowIfCancellationRequested();
                await Task.Factory.StartNew(()=>{
                    var thread=Thread.CurrentThread;var priority=thread.Priority;thread.Priority=ThreadPriority.BelowNormal;
                    try{foreach(var job in jobs){token.ThrowIfCancellationRequested();Action<CancellationToken> local;lock(gate){local=nearby;nearby=null;}local?.Invoke(token);
                        string reason;var p=BeamProjection.Create(job.Item1,job.Item2,map,out reason);if(p!=null)Render(p,ct,warmRois,job.Item3,true,token,true);
                        Prepared++;FrameReady?.Invoke();}
                    }finally{thread.Priority=priority;}
                },token,TaskCreationOptions.LongRunning,TaskScheduler.Default).ConfigureAwait(false);
            });
            // Observe cancellation/failures even when the UI is closed during background preparation.
            WarmCompletion.ContinueWith(t=>{var ignored=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);
        }
        public void PrepareNearby(PlanBeam beam,double local,Matrix4 map,VolumeData ct,RoiOverlay[] rois)
        {
            if(disposed||beam==null||map==null||beam.ControlPoints.Count==0)return;
            int mine=Interlocked.Increment(ref nearbyVersion);double anchor=Math.Round(local*10)/10;var token=warmLifetime.Token;
            Action<CancellationToken> work=t=>{
                double extent=MlcPlaybackControl.BeamExtent(beam);
                for(int n=0;n<=10;n++)foreach(int direction in n==0?new[]{1}:new[]{1,-1}){
                    t.ThrowIfCancellationRequested();if(mine!=Volatile.Read(ref nearbyVersion))return;
                    double at=anchor+n*direction*.1;if(at<0||at>beam.ControlPoints.Count-1)continue;
                    int i=(int)at,j=Math.Min(i+1,beam.ControlPoints.Count-1);ControlPoint cp;
                    try{cp=MlcTimeline.Interpolate(beam.ControlPoints[i],beam.ControlPoints[j],at-i);}catch(ArgumentException){continue;}
                    string reason;var p=BeamProjection.Create(beam,cp,map,out reason);if(p!=null)Render(p,ct,rois,extent,ct!=null,t,true);FrameReady?.Invoke();
                }
            };
            lock(gate){nearby=work;}
            if(WarmCompletion.IsCompleted){var previous=WarmCompletion;WarmCompletion=Task.Run(()=>{Action<CancellationToken> job;lock(gate){job=nearby;nearby=null;}try{job?.Invoke(token);}catch(OperationCanceledException){}catch(Exception){}});}
        }
        public void Dispose(){disposed=true;warmLifetime.Cancel();lock(gate){nearby=null;drrs.Clear();drrOrder.Clear();outlines.Clear();outlineOrder.Clear();drrBytes=outlineWeight=0;}}
    }
}
