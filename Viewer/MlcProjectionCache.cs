using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Media;

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
        readonly ProjectionMemoryCache<BitmapSource> drrs=new ProjectionMemoryCache<BitmapSource>(160,image=>image.PixelWidth*image.PixelHeight*2);
        readonly ProjectionMemoryCache<MlcProjectionFrame> targets=new ProjectionMemoryCache<MlcProjectionFrame>(96,f=>f.EstimatedBytes),others=new ProjectionMemoryCache<MlcProjectionFrame>(128,f=>f.EstimatedBytes);
        ProjectionMemoryCache<MlcProjectionFrame> Store(RoiOverlay roi)=>roi.Category==RoiCategory.Target?targets:others;
        readonly MlcProjectionRenderer foreground=new MlcProjectionRenderer(),background=new MlcProjectionRenderer(),drrBackground=new MlcProjectionRenderer();
        CancellationTokenSource warmLifetime=new CancellationTokenSource();
        string context,nearbyKey,playbackContext;bool disposed;
        MlcPlaybackBuffer playbackBuffer;
        public bool PlaybackPrepared=>playbackBuffer==null||playbackBuffer.Ready;
        public string PlaybackStatus=>playbackBuffer?.Text;
        Action<CancellationToken> nearby,nearbyDrr;int nearbyVersion;
        Task outlineWarm=Task.CompletedTask,drrWarm=Task.CompletedTask;
        internal PreparationProgress Progress {get;private set;}=new PreparationProgress();
        public int Prepared=>Progress.Completed;
        public int Total=>Progress.Total;
        public event Action FrameReady;
        public Task WarmCompletion {get;private set;}=Task.CompletedTask;
        static string Values(params double[] values)=>string.Join(",",values.Select(x=>Math.Round(x,8).ToString("R",CultureInfo.InvariantCulture)));
        static string GeometryKey(BeamProjection p,double extent)=>Values(p.Source.X,p.Source.Y,p.Source.Z,p.Iso.X,p.Iso.Y,p.Iso.Z,p.Right.X,p.Right.Y,p.Right.Z,p.Up.X,p.Up.Y,p.Up.Z,p.Sad,extent);
        static string OutlineKey(string geometry,RoiOverlay[] rois)=>geometry+"|"+string.Join(";",rois.Select(r=>Id(r.Roi)+":"+Values(r.RoiToImage.Values)));
        public bool TryGet(BeamProjection p,VolumeData ct,RoiOverlay[] rois,double extent,bool drr,out MlcProjectionFrame frame)
        {
            string geometry=GeometryKey(p,extent);BitmapSource image=null;var shapes=new MlcProjectionFrame();bool complete=true;
            lock(gate){if(drr&&ct!=null&&(image=drrs.Get(Id(ct)+":"+geometry,true))==null)complete=false;
                foreach(var roi in rois){var item=Store(roi).Get(OutlineKey(geometry,new[]{roi}),true);if(item!=null)shapes.Outlines.AddRange(item.Outlines);else complete=false;}}
            frame=Join(image,shapes,extent,drr&&ct!=null);if(!complete)frame.Note+=" - preparing remaining overlays";return complete;
        }
        static MlcProjectionFrame Join(BitmapSource image,MlcProjectionFrame shapes,double extent,bool wanted)
        {
            var frame=new MlcProjectionFrame{Extent=extent,Drr=image};if(shapes!=null)frame.Outlines.AddRange(shapes.Outlines);
            frame.Note=(image!=null?"CT-derived DRR \u00b7 cached":wanted?"Preparing CT-derived DRR":"DRR off / matching CT unavailable")+" \u00b7 "+frame.Outlines.Count+" outlines";
            return frame;
        }
        public MlcProjectionFrame Render(BeamProjection p,VolumeData ct,RoiOverlay[] rois,double extent,bool drr,CancellationToken token,bool warming=false,bool requested=false)
        {
            var renderer=warming?background:foreground;string geometry=GeometryKey(p,extent),dkey=Id(ct)+":"+geometry;
            BitmapSource image=null;var shapes=new MlcProjectionFrame();
            lock(gate){if(drr&&ct!=null)image=drrs.Get(dkey,!warming||requested);}
            foreach(var roi in rois){token.ThrowIfCancellationRequested();var okey=OutlineKey(geometry,new[]{roi});MlcProjectionFrame item;
                lock(gate)item=Store(roi).Get(okey,!warming||requested);
                if(item==null){item=renderer.Render(p,null,new[]{roi},extent,256,false,token);token.ThrowIfCancellationRequested();
                    item.EstimatedBytes=Math.Max(512,item.EstimatedBytes);
                    lock(gate){if(!disposed){if(!warming||requested)Store(roi).PutActive(okey,item);else Store(roi).Put(okey,item);}}FrameReady?.Invoke();}
                shapes.Outlines.AddRange(item.Outlines);
            }
            if(drr&&ct!=null&&image==null){image=(warming?drrBackground:renderer).Render(p,ct,new RoiOverlay[0],extent,warming?192:384,true,token,warming?1:2).Drr;token.ThrowIfCancellationRequested();
                lock(gate){if(!disposed){var existing=drrs.Get(dkey,!warming||requested);if(existing!=null)image=existing;else if(!warming||requested)drrs.PutActive(dkey,image);else drrs.Put(dkey,image);}}FrameReady?.Invoke();}
            return Join(image,shapes,extent,drr&&ct!=null);
        }
        public MlcProjectionFrame Refine(BeamProjection p,VolumeData ct,RoiOverlay[] rois,double extent,CancellationToken token)
        {
            var frame=Render(p,ct,rois,extent,true,token);
            if(frame.Drr!=null&&frame.Drr.PixelWidth<384){
                var image=foreground.Render(p,ct,new RoiOverlay[0],extent,384,true,token,2).Drr;token.ThrowIfCancellationRequested();
                string key=Id(ct)+":"+GeometryKey(p,extent);
                lock(gate){if(!disposed)drrs.PutActive(key,image);}
                frame.Drr=image;FrameReady?.Invoke();
            }
            return frame;
        }
        public void Configure(PlanData plan,RenderScene scene,Matrix4 map)
        {
            var ct=scene?.Entry?.Modality=="CT"?scene.Volume:null;
            var warmRois=(scene?.Structures??new List<RoiOverlay>()).OrderBy(r=>r.Category==RoiCategory.Target?0:r.Category==RoiCategory.Organ?1:2).ToArray();
            string next=Id(plan)+":"+Id(ct)+":"+(map==null?"none":Values(map.Values))+":"+OutlineKey("",warmRois);if(next==context||disposed)return;context=next;nearbyKey=null;playbackContext=null;playbackBuffer=null;
            warmLifetime.Cancel();warmLifetime=new CancellationTokenSource();var token=warmLifetime.Token;var progress=new PreparationProgress();Progress=progress;
            lock(gate){nearby=null;nearbyDrr=null;}
            if(plan==null||map==null||ct==null&&warmRois.Length==0)return;
            // Recorded views everywhere; fractional positions only in the active lookahead.
            // Aperture-only changes do not require a new anatomy projection.
            var allJobsTask=Task.Run(()=>BuildWarmViews(plan,map,scene?.ActiveBeam,token),token);
            var previous=WarmCompletion;
            outlineWarm=StartWarm(previous,token,()=>{
                var allJobs=allJobsTask.GetAwaiter().GetResult();progress.SetOutlineTotal(allJobs.Length*warmRois.Length);
                foreach(int pass in new[]{0,1})foreach(var job in allJobs){
                    token.ThrowIfCancellationRequested();Action<CancellationToken> local;lock(gate){local=nearby;nearby=null;}local?.Invoke(token);
                    var p=job.Projection;
                    foreach(var roi in warmRois.Where(r=>pass==0?IsTarget(r):!IsTarget(r))){
                        token.ThrowIfCancellationRequested();TakeNearby(false,token);Render(p,null,new[]{roi},job.Extent,false,token,true);progress.OutlineDone();
                    }
                    FrameReady?.Invoke();
                }
                DrainNearby(false,token);
            });
            // DRRs no longer wait behind every organ projection in the plan.
            drrWarm=StartWarm(previous,token,()=>{
                var allJobs=allJobsTask.GetAwaiter().GetResult();progress.SetDrrTotal(ct==null?0:allJobs.Length);
                foreach(var job in allJobs){token.ThrowIfCancellationRequested();Action<CancellationToken> local;lock(gate){local=nearbyDrr;nearbyDrr=null;}local?.Invoke(token);
                    var p=job.Projection;
                    if(ct!=null){Render(p,ct,new RoiOverlay[0],job.Extent,true,token,true);progress.DrrDone();}
                    FrameReady?.Invoke();
                }
                DrainNearby(true,token);
            });
            ObserveWarm();
        }
        internal sealed class WarmView {public BeamProjection Projection;public double Extent;public string Key;}
        internal static WarmView[] BuildWarmViews(PlanData plan,Matrix4 map,PlanBeam active,CancellationToken token){
            var views=new List<WarmView>();var seen=new HashSet<string>();
            foreach(var beam in MlcTimeline.PlaybackOrder(plan).Where(b=>!BeamMotion.IsImaging(b)||b==active).OrderBy(b=>b==active?0:1)){
                double extent=MlcPlaybackControl.BeamExtent(beam);
                foreach(var cp in beam.ControlPoints){token.ThrowIfCancellationRequested();string why;var projection=BeamProjection.Create(beam,cp,map,out why);
                    if(projection!=null&&seen.Add(GeometryKey(projection,extent)))views.Add(new WarmView{Projection=projection,Extent=extent});
                }
            }return views.ToArray();
        }
        void TakeNearby(bool image,CancellationToken token){Action<CancellationToken> job;lock(gate){job=image?nearbyDrr:nearby;if(image)nearbyDrr=null;else nearby=null;}job?.Invoke(token);}
        static bool IsTarget(RoiOverlay r)=>r.Category==RoiCategory.Target;
        static Task StartWarm(Task previous,CancellationToken token,Action work)=>Task.Run(async()=>{
            try{await previous.ConfigureAwait(false);}catch(OperationCanceledException){}catch(Exception){}
            token.ThrowIfCancellationRequested();await Task.Factory.StartNew(()=>{
                var thread=Thread.CurrentThread;var priority=thread.Priority;thread.Priority=ThreadPriority.BelowNormal;
                try{work();}finally{thread.Priority=priority;}
            },token,TaskCreationOptions.LongRunning,TaskScheduler.Default).ConfigureAwait(false);
        });
        void ObserveWarm(){WarmCompletion=Task.WhenAll(outlineWarm,drrWarm);WarmCompletion.ContinueWith(t=>{var ignored=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);}
        internal static WarmView[] BuildPlaybackViews(PlanBeam beam,Matrix4 map,double step)
        {
            double extent=MlcPlaybackControl.BeamExtent(beam);var views=new List<WarmView>();var seen=new HashSet<string>();
            for(double at=0;;at=MlcTimeline.NextLocal(at,beam.ControlPoints.Count-1,step)){
                int i=(int)at,j=Math.Min(i+1,beam.ControlPoints.Count-1);string reason;
                try{var p=BeamProjection.Create(beam,MlcTimeline.Interpolate(beam.ControlPoints[i],beam.ControlPoints[j],at-i),map,out reason);
                    if(p!=null){string key=GeometryKey(p,extent);if(seen.Add(key))views.Add(new WarmView{Projection=p,Extent=extent,Key=key});}}
                catch(ArgumentException){}
                if(at>=beam.ControlPoints.Count-1)break;
            }return views.ToArray();
        }
        public void PrepareNearby(PlanBeam beam,double local,Matrix4 map,VolumeData ct,RoiOverlay[] rois,double step=.4,bool playing=false)
        {
            if(disposed||beam==null||map==null||beam.ControlPoints.Count==0)return;
            string bufferKey=Id(beam)+":"+Id(ct)+":"+Values(step)+":"+Values(map.Values)+":"+OutlineKey("",rois);
            if(bufferKey!=playbackContext){playbackContext=bufferKey;playbackBuffer=new MlcPlaybackBuffer(ct==null&&rois.Length==0?new WarmView[0]:BuildPlaybackViews(beam,map,step),ct!=null,rois.Length>0);}
            var buffer=playbackBuffer;
            // Once this field is prepared, cached playback must not create worker tasks
            // or flicker the busy indicator merely to look up already available frames.
            if(buffer.Ready){
                int i=(int)local,j=Math.Min(i+1,beam.ControlPoints.Count-1);string reason;
                try{var p=BeamProjection.Create(beam,MlcTimeline.Interpolate(beam.ControlPoints[i],beam.ControlPoints[j],local-i),map,out reason);MlcProjectionFrame frame;
                    if(p!=null&&TryGet(p,ct,rois,MlcPlaybackControl.BeamExtent(beam),ct!=null,out frame))return;}
                catch(ArgumentException){}
            }
            string key=Id(beam)+":"+Id(ct)+":"+Values(local,step)+":"+playing+":"+Values(map.Values)+":"+OutlineKey("",rois);
            if(key==nearbyKey){ResumeNearby(warmLifetime.Token);return;}nearbyKey=key;
            int mine=Interlocked.Increment(ref nearbyVersion);var token=warmLifetime.Token;double extent=MlcPlaybackControl.BeamExtent(beam);
            var positions=new List<double>{local};
            if(!playing)for(int n=1;n<=4;n++){positions.Add(Math.Round(local)+n);positions.Add(Math.Round(local)-n);}
            double upcoming=local;for(int n=0;n<(playing?24:12)&&upcoming<beam.ControlPoints.Count-1;n++){upcoming=MlcTimeline.NextLocal(upcoming,beam.ControlPoints.Count-1,step);positions.Add(upcoming);}
            var views=positions.Where(at=>at>=0&&at<=beam.ControlPoints.Count-1).Distinct().Select(at=>{
                int i=(int)at,j=Math.Min(i+1,beam.ControlPoints.Count-1);string reason;
                try{return BeamProjection.Create(beam,MlcTimeline.Interpolate(beam.ControlPoints[i],beam.ControlPoints[j],at-i),map,out reason);}catch(ArgumentException){return null;}
            }).Where(p=>p!=null).Concat(buffer.Ready?Enumerable.Empty<BeamProjection>():buffer.Views.Select(v=>v.Projection)).GroupBy(p=>GeometryKey(p,extent)).Select(g=>g.First()).ToArray();
            Action<CancellationToken> outlines=t=>{
                foreach(var p in views){t.ThrowIfCancellationRequested();if(mine!=Volatile.Read(ref nearbyVersion))return;
                    Render(p,null,rois,extent,false,t,true,true);buffer.Mark(GeometryKey(p,extent),false);}
            };
            Action<CancellationToken> images=t=>{
                if(ct==null)return;foreach(var p in views){t.ThrowIfCancellationRequested();if(mine!=Volatile.Read(ref nearbyVersion))return;
                    Render(p,ct,new RoiOverlay[0],extent,true,t,true,true);buffer.Mark(GeometryKey(p,extent),true);}
            };
            lock(gate){nearby=outlines;nearbyDrr=images;}
            ResumeNearby(token);
        }
        void ResumeNearby(CancellationToken token){
            bool outlines,images;lock(gate){outlines=nearby!=null;images=nearbyDrr!=null;}
            if(outlines&&outlineWarm.IsCompleted)outlineWarm=StartWarm(outlineWarm,token,()=>DrainNearby(false,token));
            if(images&&drrWarm.IsCompleted)drrWarm=StartWarm(drrWarm,token,()=>DrainNearby(true,token));
            ObserveWarm();
        }
        void DrainNearby(bool image,CancellationToken token){while(true){Action<CancellationToken> job;lock(gate){job=image?nearbyDrr:nearby;if(image)nearbyDrr=null;else nearby=null;}if(job==null)return;token.ThrowIfCancellationRequested();job(token);}}
        public void Dispose(){disposed=true;warmLifetime.Cancel();lock(gate){nearby=null;nearbyDrr=null;drrs.Clear();targets.Clear();others.Clear();}}
    }
}
