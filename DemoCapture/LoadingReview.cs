using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
internal static partial class Program
{
 static async Task LoadingReview(string folder)
 {
  var samples=new List<double>();var watch=Stopwatch.StartNew();double last=watch.Elapsed.TotalMilliseconds;
  var timer=new DispatcherTimer(DispatcherPriority.Input){Interval=TimeSpan.FromMilliseconds(16)};
  timer.Tick+=(s,e)=>{double now=watch.Elapsed.TotalMilliseconds,dt=now-last;last=now;samples.Add(dt);if(dt>150)Console.WriteLine("LOAD_STALL ms="+dt.ToString("0")+" at="+now.ToString("0")+" volume="+(viewer!=null&&Get<object>(viewer,"volume")!=null));};timer.Start();
  try{await Load(folder,"approved public nonpatient benchmark",true);await Task.Delay(3000);Console.WriteLine("LOAD_RESPONSIVENESS samples="+samples.Count+" p95_ms="+samples.OrderBy(t=>t).ElementAt((int)(samples.Count*.95)).ToString("0.0")+" max_ms="+samples.Max().ToString("0.0"));}finally{timer.Stop();}
 }
}
