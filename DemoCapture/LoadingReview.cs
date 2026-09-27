using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
internal static partial class Program
{
 static async Task EarlyFieldsReview(string folder)
 {
  sourceKind="approved public nonpatient benchmark";viewer?.Dispose();viewer=new QuickLook.DicomRT.ViewerControl{Width=Width,Height=Height};window.Content=viewer;window.Show();viewer.UpdateLayout();
  var cat=await Task.Run(()=>QuickLook.DicomRT.DicomCatalog.Scan(System.IO.Directory.EnumerateFiles(folder).First(),System.Threading.CancellationToken.None));
  viewer.Open(cat.Files.First(e=>e.Modality=="RTPLAN").Path);
  await Wait(()=>Get<QuickLook.DicomRT.PlanData>(viewer,"selectedPlan")!=null,"initial RT plan");
  if(Get<object>(viewer,"volume")!=null)throw new Exception("Early-load precondition missed");
  Mode("MLC");Get<System.Windows.Controls.CheckBox>(viewer,"showFields").IsChecked=true;Mode("3D");
  await viewer.LoadCompletion;await Settle();await Save("early-fields.png","Fields enabled before CT loading completes; final 3D context fits the loaded image volume.");
  var three=Get<QuickLook.DicomRT.ThreeDControl>(viewer,"threeDView");double before=Get<double>(three,"radius"),distance=Get<double>(three,"distance");
  Invoke(three,"ResetCamera");Invoke(three,"FitFieldGuides");
  if(Math.Abs(before-Get<double>(three,"radius"))>1e-6||Math.Abs(distance-Get<double>(three,"distance"))>1e-4)throw new Exception("Early field fit differs from settled reset");
  Console.WriteLine("EARLY_FIELDS_PASS automatic_fit_matches_reset=True radius_mm="+before.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture));
 }
 static async Task LoadingReview(string folder)
 {
  var samples=new List<double>();var watch=Stopwatch.StartNew();double last=watch.Elapsed.TotalMilliseconds;
  var timer=new DispatcherTimer(DispatcherPriority.Input){Interval=TimeSpan.FromMilliseconds(16)};
  timer.Tick+=(s,e)=>{double now=watch.Elapsed.TotalMilliseconds,dt=now-last;last=now;samples.Add(dt);if(dt>150)Console.WriteLine("LOAD_STALL ms="+dt.ToString("0")+" at="+now.ToString("0")+" volume="+(viewer!=null&&Get<object>(viewer,"volume")!=null));};timer.Start();
  try{await Load(folder,"approved public nonpatient benchmark",true);await Task.Delay(3000);Console.WriteLine("LOAD_RESPONSIVENESS samples="+samples.Count+" p95_ms="+samples.OrderBy(t=>t).ElementAt((int)(samples.Count*.95)).ToString("0.0")+" max_ms="+samples.Max().ToString("0.0"));}finally{timer.Stop();}
 }
}
