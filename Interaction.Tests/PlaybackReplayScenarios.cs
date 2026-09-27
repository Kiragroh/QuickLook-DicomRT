using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using QuickLook.DicomRT;
internal static class PlaybackReplayScenarios
{
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 public static void Run(Action<bool,string> check)
 {
  foreach(double step in new[]{.08,.16,.4,.8,1.6}){
   var counts=new[]{3,0,4};var first=new List<double>();double position=0;int iterations=0;
   do{first.Add(position);position=MlcTimeline.NextPlayback(counts,position,step);if(++iterations>200)throw new Exception("Playback failed to loop");}while(position!=0);
   check(first.Contains(2)&&first.Contains(3)&&first.Contains(6),"playback visits every beam endpoint and next start at every offered speed");
   position=0;foreach(double expected in first){check(position==expected,"complete plan repeats identical fractional samples");position=MlcTimeline.NextPlayback(counts,position,step);}
  }
  using(var mlc=new MlcPlaybackControl()){
   var plan=new PlanData();for(int b=0;b<2;b++){var beam=new PlanBeam{Number=b+1};for(int n=0;n<3;n++)beam.ControlPoints.Add(new ControlPoint());plan.Beams.Add(beam);}
   mlc.SetPlan(plan);
   var advance=mlc.GetType().GetMethod("AdvancePlayback",F);
   var samples=new List<double>();for(int n=0;n<30;n++){advance.Invoke(mlc,null);if(mlc.Position>=3){samples.Add(mlc.LocalPosition);if(samples.Count==4)break;}}
   mlc.Navigate(plan.Beams[1],0);var replay=new List<double>{mlc.LocalPosition};for(int n=1;n<4;n++){advance.Invoke(mlc,null);replay.Add(mlc.LocalPosition);}
   check(samples.SequenceEqual(replay),"reselecting a beam uses the same exact-angle playback samples as entering from a previous field");
   mlc.Navigate(plan.Beams[1],.23);advance.Invoke(mlc,null);check(Math.Abs(mlc.LocalPosition-.4)<1e-8,"resuming after arbitrary scrubbing rejoins the reusable playback grid");
  }
 }
}
