using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using QuickLook.DicomRT;
internal static partial class Program {
 static async Task DrrReview(string folder){
  await Load(folder,"approved public nonpatient benchmark");Panels(false,false);Mode("MLC");var mlc=Get<MlcPlaybackControl>(viewer,"centralPlayback");Get<CheckBox>(mlc,"showDrr").IsChecked=true;Get<Slider>(mlc,"mlcOpacity").Value=.1;
  await Wait(()=> {var frame=Get<object>(Get<object>(mlc,"aperture"),"projection");return frame!=null&&frame.GetType().GetField("Drr").GetValue(frame)!=null;},"DRR image",120);
  await Task.Delay(700);var aperture=Get<object>(mlc,"aperture");
  Invoke(aperture,"SetDrrWindow","Original",.5,1d);await Save("drr-original.png","Original DRR contrast on approved public CT.");
  Invoke(aperture,"SetDrrWindow","Auto",.5,1d);await Save("drr-auto.png","Automatic percentile DRR window on approved public CT.");
  Mode("3D");Get<CheckBox>(viewer,"showFields").IsChecked=true;await Settle();var three=Get<ThreeDControl>(viewer,"threeDView");
  Console.WriteLine("DRR_REVIEW_PASS");
 }
}