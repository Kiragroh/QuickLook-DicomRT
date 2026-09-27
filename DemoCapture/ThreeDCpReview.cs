using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using QuickLook.DicomRT;
internal static partial class Program
{
 static async Task ThreeDCpReview(string folder)
 {
  await Load(folder,"user-approved public nonpatient benchmark");Panels(true,false);Mode("3D");await Settle();
  var three=Get<ThreeDControl>(viewer,"threeDView");Get<CheckBox>(three,"showBeamFields").IsChecked=true;
  var plan=Get<PlanData>(viewer,"selectedPlan");var beam=plan.Beams.First(b=>BeamMotion.IsArc(b)&&b.ControlPoints.Any(c=>c.MlcLayers.Count>0));
  Get<ComboBox>(three,"activeBeamPicker").SelectedItem=beam;var cursor=Get<Slider>(three,"beamCursor");cursor.Value=0;await Task.Delay(300);
  await Save("3d-cp-start.png","Actual 3D field guide and moving MLC BEV at the active control point.");
  var overlay=Get<object>(three,"beamFields");if(!Get<bool>(overlay,"MiniatureVisible"))throw new Exception("Mini MLC absent");var start=Get<Point?>(overlay,"MiniatureAnchor");
  var prepared=Get<object>(three,"prepared");int generation=Get<int>(three,"generation");var pathCache=Get<object>(overlay,"Ready");
  cursor.Value=Math.Floor(cursor.Maximum*.55);await Save("3d-cp-middle.png","Changed control point: source marker and actual leaf aperture follow the selected beam.");
  var middle=Get<Point?>(overlay,"MiniatureAnchor");if(!start.HasValue||!middle.HasValue||(start.Value-middle.Value).Length<5)throw new Exception("Source marker did not follow arc");
  var times=new double[60];for(int i=0;i<times.Length;i++){var clock=Stopwatch.StartNew();cursor.Value=(i%30)/29d*cursor.Maximum;times[i]=clock.Elapsed.TotalMilliseconds;await Task.Delay(16);}
  if(!ReferenceEquals(prepared,Get<object>(three,"prepared"))||generation!=Get<int>(three,"generation")||!ReferenceEquals(pathCache,Get<object>(overlay,"Ready")))throw new Exception("CP movement rebuilt anatomy or tracks");
  var playback=Get<MlcPlaybackControl>(viewer,"centralPlayback");double position=cursor.Value;Mode("MLC");if(Math.Abs(playback.LocalPosition-position)>1e-6)throw new Exception("View switch lost CP");Mode("3D");await Settle();
  Console.WriteLine("3D_CP_REVIEW_PASS samples=60 dispatch_median_ms="+times.OrderBy(t=>t).ElementAt(30).ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+" dispatch_p95_ms="+times.OrderBy(t=>t).ElementAt(56).ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+" meshes_reused=True tracks_reused=True shared_position=True");
  Mode("Bild");Get<ComboBox>(viewer,"planes").SelectedItem="MPR + 3D";await Save("3d-cp-quad.png","Linked overview preserves the 3D field, control point and mini MLC.");
 }
}
