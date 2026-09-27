using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace QuickLook.DicomRT
{
    public sealed partial class MlcPlaybackControl
    {
        Slider mlcOpacity;
        readonly CheckBox showFieldArrangement=new CheckBox{Content="Fields",IsChecked=false,Margin=new Thickness(5),ToolTip="Field arrangement overlay; synchronized with image and 3D views"};
        public event Action<bool> FieldsVisibilityChanged;
        public void SetFieldsVisible(bool enabled){showFieldArrangement.IsChecked=enabled;UpdateArrangement();}
        readonly CheckBox showDrr=new CheckBox{Content="DRR",IsChecked=false,Margin=new Thickness(5)};
        readonly CheckBox showPtv=new CheckBox{Content="PTV outlines",IsChecked=true,Margin=new Thickness(5)};
        readonly CheckBox showOrgans=new CheckBox{Content="Organ outlines",IsChecked=false,Margin=new Thickness(5)};
        readonly CheckBox showOther=new CheckBox{Content="Other outlines",IsChecked=false,Margin=new Thickness(5)};
        readonly TextBlock projectionStatus=Theme.Text("DRR needs an associated CT",10,Theme.Muted);
        readonly DispatcherTimer projectionDelay=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(20)};
        readonly MlcProjectionCache projectionCache=new MlcProjectionCache();
        public static double BeamExtent(PlanBeam beam)=>Math.Max(100,beam.ControlPoints.SelectMany(c=>c.MlcLayers.SelectMany(l=>l.Boundaries.Concat(l.Positions)).Concat(c.XJaws??new double[0]).Concat(c.YJaws??new double[0])).Where(BeamProjection.Finite).Select(v=>Math.Abs(v)+10).DefaultIfEmpty(100).Max());
        public void Close(){Dispose();projectionCache.Dispose();}
        public void Preload(PlanData value,RenderScene scene,Matrix4 map){projectionCache.Configure(value,scene,map);}
        RoiOverlay[] SelectedOutlines()=>(anatomy?.Structures??new System.Collections.Generic.List<RoiOverlay>()).Where(r=>{string type=r.Roi.InterpretedType?.Trim().ToUpperInvariant();return type=="PTV"?showPtv.IsChecked==true:type=="ORGAN"?showOrgans.IsChecked==true:showOther.IsChecked==true;}).ToArray();
        readonly FieldArrangementControl fieldArrangement=new FieldArrangementControl{Width=265,Height=245,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(8)};
        RenderScene anatomy;Matrix4 planMap;ControlPoint interpolated;
        int wheelRemainder,projectionVersion;bool projectionBusy,projectionSuspended;
        CancellationTokenSource projectionLifetime=new CancellationTokenSource();
        string projectionKey,displayedProjectionKey;
        public Task ProjectionCompletion {get;private set;}=Task.CompletedTask;
        public event Action<PlanBeam,ControlPoint> FrameChanged;
        long lastCacheNotification;
        void InitializeAnatomy(StackPanel top,Grid area)
        {
            projectionCache.FrameReady+=()=>{long now=System.Diagnostics.Stopwatch.GetTimestamp();if(now-Interlocked.Read(ref lastCacheNotification)<System.Diagnostics.Stopwatch.Frequency/20)return;Interlocked.Exchange(ref lastCacheNotification,now);if(!Dispatcher.HasShutdownStarted)Dispatcher.BeginInvoke(new Action(()=>{if(IsVisible&&!projectionSuspended)TryCached();}),DispatcherPriority.Background);};
            var row=top;showFieldArrangement.Foreground=Theme.Foreground;row.Children.Add(showFieldArrangement);showFieldArrangement.Checked+=(s,e)=>{UpdateArrangement();FieldsVisibilityChanged?.Invoke(true);};showFieldArrangement.Unchecked+=(s,e)=>{UpdateArrangement();FieldsVisibilityChanged?.Invoke(false);};UpdateArrangement();foreach(var check in new[]{showDrr,showPtv,showOrgans,showOther}){check.Foreground=Theme.Foreground;row.Children.Add(check);check.Checked+=(s,e)=>RequestProjection(true);check.Unchecked+=(s,e)=>RequestProjection(true);}
            mlcOpacity=new Slider{Minimum=.1,Maximum=1,Value=.75,Width=70,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(4),ToolTip="MLC opacity over DRR: transparent to dark"};
            mlcOpacity.ValueChanged+=(s,e)=>aperture.DrrLeafOpacity=e.NewValue;row.Children.Add(Theme.Text("MLC",10,Theme.Muted));row.Children.Add(mlcOpacity);
            var info=Theme.Button("i");info.ToolTip=projectionStatus;row.Children.Add(info);
            showOther.ToolTip="Other selected ROIs, including CTV/GTV. Use the left structure list for individual visibility.";
            showPtv.ToolTip=showOrgans.ToolTip="Projected outer boundary of selected structures; visible above the leaf banks.";
            area.Children.Add(fieldArrangement);
            area.PreviewMouseWheel+=OnControlPointWheel;cursor.PreviewMouseWheel+=OnControlPointWheel;
            projectionDelay.Tick+=(s,e)=>{projectionDelay.Stop();if(!projectionBusy)ProjectionCompletion=RenderProjectionAsync();};
            IsVisibleChanged+=(s,e)=>{if(IsVisible){projectionSuspended=false;RequestProjection(true);}else SuspendProjection();};
        }
        void OnControlPointWheel(object sender,MouseWheelEventArgs e)
        {
            Pause();cursor.Value=((Keyboard.Modifiers&ModifierKeys.Shift)!=0?MlcTimeline.WheelStep(cursor.Value,cursor.Maximum,e.Delta,ref wheelRemainder):MlcTimeline.RecordedWheelStep(cursor.Value,cursor.Maximum,e.Delta,ref wheelRemainder));e.Handled=true;
        }
        public void SetAnatomy(RenderScene scene,Matrix4 map)
        {
            bool changed=anatomy?.Volume!=scene?.Volume||anatomy?.Entry?.SeriesUid!=scene?.Entry?.SeriesUid||!SameMap(planMap,map)||
                !(anatomy?.Structures.Select(r=>r.Roi)??Enumerable.Empty<StructureRoi>()).SequenceEqual(scene?.Structures.Select(r=>r.Roi)??Enumerable.Empty<StructureRoi>())||
                anatomy!=null&&scene!=null&&!anatomy.Structures.Select(r=>MapKey(r.RoiToImage)).SequenceEqual(scene.Structures.Select(r=>MapKey(r.RoiToImage)));
            bool geometryChanged=anatomy?.Volume!=scene?.Volume||anatomy?.Entry?.SeriesUid!=scene?.Entry?.SeriesUid||!SameMap(planMap,map)||
                anatomy!=null&&scene!=null&&anatomy.Structures.Any(old=>scene.Structures.Any(next=>next.Roi==old.Roi&&!SameMap(old.RoiToImage,next.RoiToImage)));
            anatomy=scene;planMap=map;if(IsVisible)UpdateArrangement();if(changed){projectionVersion++;if(geometryChanged){displayedProjectionKey=null;aperture.Projection=null;}RequestProjection(true);}
        }
        static string MapKey(Matrix4 map)=>map==null?"none":string.Join(",",map.Values.Select(x=>x.ToString("R",System.Globalization.CultureInfo.InvariantCulture)));
        static bool SameMap(Matrix4 a,Matrix4 b)=>MapKey(a)==MapKey(b);
        void UpdateArrangement(){fieldArrangement.Visibility=showFieldArrangement.IsChecked==true?Visibility.Visible:Visibility.Collapsed;if(showFieldArrangement.IsChecked==true)fieldArrangement.Set(anatomy,plan,beam,interpolated,planMap);}
        void FrameAnatomy(ControlPoint cp)
        {interpolated=cp;if(IsVisible)UpdateArrangement();FrameChanged?.Invoke(beam,cp);RequestProjection(false);}
        void SuspendProjection(){projectionSuspended=true;projectionDelay.Stop();projectionVersion++;projectionLifetime.Cancel();projectionLifetime.Dispose();projectionLifetime=new CancellationTokenSource();projectionKey=null;}
        void RequestProjection(bool force)
        {
            if(projectionSuspended||beam==null||interpolated==null)return;
            var p=interpolated;
            string key=beam.Number+":"+string.Join(",",new[]{p.Gantry,p.Couch,p.Collimator,p.Isocenter.X,p.Isocenter.Y,p.Isocenter.Z,p.GantryPitch,p.TablePitch,p.TableRoll,p.TableEccentric,aperture.Extent}.Select(x=>x.ToString("R",System.Globalization.CultureInfo.InvariantCulture)))+":"+timer.IsEnabled;
            if(!force&&key==projectionKey)return;bool geometryChanged=projectionKey!=key;projectionKey=key;projectionVersion++;projectionLifetime.Cancel();projectionLifetime.Dispose();projectionLifetime=new CancellationTokenSource();if(geometryChanged){aperture.Projection=null;displayedProjectionKey=null;}
            if(TryCached()){projectionCache.PrepareNearby(beam,LocalPosition,planMap,anatomy?.Entry?.Modality=="CT"?anatomy.Volume:null,SelectedOutlines());return;}
            projectionCache.PrepareNearby(beam,LocalPosition,planMap,anatomy?.Entry?.Modality=="CT"?anatomy.Volume:null,SelectedOutlines());
            projectionStatus.Text="Preparing overlays in background …";
            projectionDelay.Stop();projectionDelay.Start();
        }
        void ApplyProjection(MlcProjectionFrame frame)
        {
            var previous=aperture.Projection;
            if(previous!=null&&displayedProjectionKey==projectionKey){
                var wanted=SelectedOutlines().Select(r=>r.Roi).ToArray();
                foreach(var outline in previous.Outlines)if(wanted.Contains(outline.Roi)&&!frame.Outlines.Any(o=>o.Roi==outline.Roi))frame.Outlines.Add(outline);
                if(showDrr.IsChecked==true&&frame.Drr==null)frame.Drr=previous.Drr;
            }
            displayedProjectionKey=projectionKey;aperture.Projection=frame;
        }
        bool TryCached()
        {
            if(beam==null||interpolated==null)return false;string reason;var p=BeamProjection.Create(beam,interpolated,planMap,out reason);if(p==null)return false;
            MlcProjectionFrame frame;var ct=anatomy?.Entry?.Modality=="CT"?anatomy.Volume:null;
            if(!projectionCache.TryGet(p,ct,SelectedOutlines(),aperture.Extent,showDrr.IsChecked==true,out frame)){ApplyProjection(frame);projectionStatus.Text=frame.Note;return false;}
            projectionDelay.Stop();ApplyProjection(frame);projectionStatus.Text=frame.Note+(projectionCache.Total>0?$" · Views prepared {projectionCache.Prepared}/{projectionCache.Total}":"");return true;
        }
        async Task RenderProjectionAsync()
        {
            if(projectionSuspended||beam==null||interpolated==null)return;
            int version=projectionVersion;string reason;
            var projection=BeamProjection.Create(beam,interpolated,planMap,out reason);
            if(projection==null){projectionStatus.Text=reason;aperture.Projection=null;return;}
            var rois=SelectedOutlines();
            var ct=anatomy?.Entry?.Modality=="CT"?anatomy.Volume:null;bool drr=showDrr.IsChecked==true;double extent=aperture.Extent;
            var token=projectionLifetime.Token;projectionBusy=true;
            try{
                var frame=await Task.Run(()=>projectionCache.Render(projection,ct,rois,extent,drr,token),token);
                if(version==projectionVersion&&!projectionSuspended){ApplyProjection(frame);projectionStatus.Text=frame.Note;projectionCache.PrepareNearby(beam,LocalPosition,planMap,ct,rois);}
            }catch(OperationCanceledException){}catch(Exception){if(version==projectionVersion){projectionStatus.Text="DRR / contour projection unavailable for this geometry";aperture.Projection=null;}}
            finally{projectionBusy=false;if(!projectionSuspended&&version!=projectionVersion){projectionDelay.Stop();projectionDelay.Start();}}
        }
    }
}
