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
        readonly CheckBox showDrr=new CheckBox{Content="DRR",IsChecked=true,Margin=new Thickness(5)};
        readonly CheckBox showPtv=new CheckBox{Content="PTV outlines",IsChecked=true,Margin=new Thickness(5)};
        readonly CheckBox showOrgans=new CheckBox{Content="Organ outlines",IsChecked=false,Margin=new Thickness(5)};
        readonly CheckBox showOther=new CheckBox{Content="Other outlines",IsChecked=false,Margin=new Thickness(5)};
        readonly TextBlock projectionStatus=Theme.Text("DRR needs an associated CT",10,Theme.Muted);
        readonly DispatcherTimer projectionDelay=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(65)};
        readonly MlcProjectionRenderer projectionRenderer=new MlcProjectionRenderer();
        readonly FieldArrangementControl fieldArrangement=new FieldArrangementControl{Width=265,Height=245,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(8)};
        RenderScene anatomy;Matrix4 planMap;ControlPoint interpolated;
        int wheelRemainder,projectionVersion;bool projectionBusy,projectionSuspended;
        CancellationTokenSource projectionLifetime=new CancellationTokenSource();
        string projectionKey;
        public Task ProjectionCompletion {get;private set;}=Task.CompletedTask;
        public event Action<PlanBeam,ControlPoint> FrameChanged;
        void InitializeAnatomy(StackPanel top,Grid area)
        {
            var row=new WrapPanel();foreach(var check in new[]{showDrr,showPtv,showOrgans,showOther}){check.Foreground=Theme.Foreground;row.Children.Add(check);check.Checked+=(s,e)=>RequestProjection(true);check.Unchecked+=(s,e)=>RequestProjection(true);}top.Children.Add(row);top.Children.Add(projectionStatus);
            showOther.ToolTip="Other selected ROIs, including CTV/GTV. Use the left structure list for individual visibility.";
            showPtv.ToolTip=showOrgans.ToolTip="Projected outer boundary of selected structures; visible above the leaf banks.";
            area.Children.Add(fieldArrangement);
            area.PreviewMouseWheel+=OnControlPointWheel;cursor.PreviewMouseWheel+=OnControlPointWheel;
            projectionDelay.Tick+=(s,e)=>{projectionDelay.Stop();if(!projectionBusy)ProjectionCompletion=RenderProjectionAsync();};
            IsVisibleChanged+=(s,e)=>{if(IsVisible){projectionSuspended=false;RequestProjection(true);}else SuspendProjection();};
        }
        void OnControlPointWheel(object sender,MouseWheelEventArgs e)
        {
            Pause();cursor.Value=MlcTimeline.WheelStep(cursor.Value,cursor.Maximum,e.Delta,ref wheelRemainder);e.Handled=true;
        }
        public void SetAnatomy(RenderScene scene,Matrix4 map)
        {
            bool changed=anatomy?.Volume!=scene?.Volume||anatomy?.Entry?.SeriesUid!=scene?.Entry?.SeriesUid||!SameMap(planMap,map)||
                !(anatomy?.Structures.Select(r=>r.Roi)??Enumerable.Empty<StructureRoi>()).SequenceEqual(scene?.Structures.Select(r=>r.Roi)??Enumerable.Empty<StructureRoi>())||
                anatomy!=null&&scene!=null&&!anatomy.Structures.Select(r=>MapKey(r.RoiToImage)).SequenceEqual(scene.Structures.Select(r=>MapKey(r.RoiToImage)));
            anatomy=scene;planMap=map;UpdateArrangement();if(changed)RequestProjection(true);
        }
        static string MapKey(Matrix4 map)=>map==null?"none":string.Join(",",map.Values.Select(x=>x.ToString("R",System.Globalization.CultureInfo.InvariantCulture)));
        static bool SameMap(Matrix4 a,Matrix4 b)=>MapKey(a)==MapKey(b);
        void UpdateArrangement()=>fieldArrangement.Set(anatomy,plan,beam,interpolated,planMap);
        void FrameAnatomy(ControlPoint cp)
        {interpolated=cp;UpdateArrangement();FrameChanged?.Invoke(beam,cp);RequestProjection(false);}
        void SuspendProjection(){projectionSuspended=true;projectionDelay.Stop();projectionVersion++;projectionLifetime.Cancel();projectionLifetime.Dispose();projectionLifetime=new CancellationTokenSource();projectionKey=null;}
        void RequestProjection(bool force)
        {
            if(projectionSuspended||beam==null||interpolated==null)return;
            var p=interpolated;
            string key=beam.Number+":"+string.Join(",",new[]{p.Gantry,p.Couch,p.Collimator,p.Isocenter.X,p.Isocenter.Y,p.Isocenter.Z,p.GantryPitch,p.TablePitch,p.TableRoll,p.TableEccentric,aperture.Extent}.Select(x=>x.ToString("R",System.Globalization.CultureInfo.InvariantCulture)))+":"+timer.IsEnabled;
            if(!force&&key==projectionKey)return;projectionKey=key;projectionVersion++;aperture.Projection=null;
            projectionStatus.Text="Preparing DRR / projected contours …";
            projectionDelay.Stop();projectionDelay.Start();
        }
        async Task RenderProjectionAsync()
        {
            if(projectionSuspended||beam==null||interpolated==null)return;
            int version=projectionVersion;string reason;
            var projection=BeamProjection.Create(beam,interpolated,planMap,out reason);
            if(projection==null){projectionStatus.Text=reason;aperture.Projection=null;return;}
            var rois=(anatomy?.Structures??new System.Collections.Generic.List<RoiOverlay>()).Where(r=>{
                string type=r.Roi.InterpretedType?.Trim().ToUpperInvariant();return type=="PTV"?showPtv.IsChecked==true:type=="ORGAN"?showOrgans.IsChecked==true:showOther.IsChecked==true;
            }).ToArray();
            var ct=anatomy?.Entry?.Modality=="CT"?anatomy.Volume:null;bool drr=showDrr.IsChecked==true;double extent=aperture.Extent;int size=timer.IsEnabled?192:384;
            var token=projectionLifetime.Token;projectionBusy=true;
            try{
                var frame=await Task.Run(()=>projectionRenderer.Render(projection,ct,rois,extent,size,drr,token),token);
                if(version==projectionVersion&&!projectionSuspended){aperture.Projection=frame;projectionStatus.Text=frame.Note;}
            }catch(OperationCanceledException){}catch(Exception){if(version==projectionVersion){projectionStatus.Text="DRR / contour projection unavailable for this geometry";aperture.Projection=null;}}
            finally{projectionBusy=false;if(!projectionSuspended&&version!=projectionVersion){projectionDelay.Stop();projectionDelay.Start();}}
        }
    }
}
