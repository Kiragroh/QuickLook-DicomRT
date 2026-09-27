using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using QuickLook.DicomRT;
internal static partial class Program
{
 static async Task ProjectionReview(string folder)
 {
  await Load(folder,"approved public nonpatient benchmark");Panels(true,false);Mode("MLC");
  var playback=Get<MlcPlaybackControl>(viewer,"centralPlayback");var plan=Get<PlanData>(viewer,"selectedPlan");var all=Get<List<StructureSet>>(viewer,"structures").SelectMany(s=>s.Rois).ToArray();
  foreach(var roi in all)roi.Visible=true;
  Invoke(viewer,"RefreshRt");Get<CheckBox>(playback,"showOrgans").IsChecked=true;Get<CheckBox>(playback,"showOther").IsChecked=true;Get<CheckBox>(playback,"showDrr").IsChecked=true;
  var aperture=Get<object>(playback,"aperture");var cache=Get<object>(playback,"projectionCache");
  var ct=Get<VolumeData>(viewer,"volume");var map=Get<Matrix4>(playback,"planMap");
  Func<bool> complete=()=>{
   var beam=Get<PlanBeam>(playback,"beam");var cp=Get<ControlPoint>(playback,"interpolated");string reason;
   var args=new object[]{BeamProjection.Create(beam,cp,map,out reason),ct,Invoke(playback,"SelectedOutlines"),MlcPlaybackControl.BeamExtent(beam),true,null};
   return (bool)Invoke(cache,"TryGet",args);
  };
  Console.WriteLine("SELECTED_OUTLINES count="+((Array)Invoke(playback,"SelectedOutlines")).Length);
  var watch=Stopwatch.StartNew();await Wait(complete,"initial full overlays",180);Console.WriteLine("PROJECTION_INITIAL_READY ms="+watch.ElapsedMilliseconds);
  int changed=0,missing=0;var times=new List<double>();var frameWatch=Stopwatch.StartNew();
  playback.FrameChanged+=(b,c)=>{if(!playback.IsPlaying)return;changed++;if(!complete())missing++;times.Add(frameWatch.Elapsed.TotalMilliseconds);frameWatch.Restart();};
  playback.TogglePlayback();await Task.Delay(12000);playback.Pause();
  Console.WriteLine("PLAYBACK_FIRST advances="+changed+" missing="+missing+" position="+playback.Position.ToString("0.0")+" max_interval_ms="+(times.Count>0?times.Max():0).ToString("0"));
  if(missing!=0||changed<6)throw new Exception("Playback overlays missing or preparation not progressing");
  // Repeat the same trajectory from its warmed origin, using the actual timer rather than manual frame stepping.
  Get<Slider>(playback,"cursor").Value=0;changed=missing=0;times.Clear();frameWatch.Restart();playback.TogglePlayback();await Task.Delay(8000);playback.Pause();
  Console.WriteLine("PLAYBACK_WARM advances="+changed+" missing="+missing+" median_interval_ms="+times.OrderBy(x=>x).ElementAt(times.Count/2).ToString("0")+" max_interval_ms="+times.Max().ToString("0"));
  if(missing!=0||changed<50)throw new Exception("Warm playback not continuous");
  await Wait(()=>{var f=Get<object>(aperture,"projection");return f!=null&&Get<BitmapSource>(f,"Drr")?.PixelWidth==384;},"stationary DRR refinement",60);
  Console.WriteLine("STATIONARY_DRR_REFINED pixels=384");
  int scrollMisses=0;var scrollTimes=new List<double>();for(int n=1;n<=12;n++){
   var clock=Stopwatch.StartNew();Get<Slider>(playback,"cursor").Value=n;if(!complete())scrollMisses++;scrollTimes.Add(clock.Elapsed.TotalMilliseconds);await Task.Delay(20);
  }
  Console.WriteLine("SCROLL_WARM missing="+scrollMisses+" median_dispatch_ms="+scrollTimes.OrderBy(x=>x).ElementAt(6).ToString("0.00"));
  if(scrollMisses!=0)throw new Exception("Prepared scroll position lost overlays");
  await Save("mlc-playback-drr.png","Exact-angle DRR, targets and organs during prepared playback.");
  Mode("Bild");Get<ComboBox>(viewer,"planes").SelectedItem="Axial";Get<CheckBox>(viewer,"showFields").IsChecked=true;await Settle();await Task.Delay(300);
  await Save("fields-pane.png","Field geometry can extend beyond the CT raster into the free image pane.");
  Console.WriteLine("PROJECTION_REVIEW_PASS");
 }
}
