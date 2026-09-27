using System;
using System.Reflection;
using System.Windows;
using QuickLook.DicomRT;
internal static class NavigationProgressScenarios {
 const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance;
 static object Get(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
 public static void Run(Action<bool,string> check){
  var volume=new VolumeData{Width=8,Height=8,Depth=8,Values=new float[512],SpacingX=1,SpacingY=1,SpacingZ=1,AxisX=new Vec3(1,0,0),AxisY=new Vec3(0,1,0),AxisZ=new Vec3(0,0,1)};
  foreach(string plane in new[]{"Axial","Coronal","Sagittal"})using(var pane=new SlicePane()){
   pane.Measure(new Size(300,300));pane.Arrange(new Rect(0,0,300,300));
   pane.Scene=new RenderScene{Plane=plane,Volume=volume,Focus=new Vec3(3,3,3),ViewCenter=new Vec3(3,3,3)};
   int generation=(int)Get(pane,"generation");var before=SliceGeometry.Create(pane.Scene);
   typeof(SlicePane).GetMethod("Navigate",F).Invoke(pane,new object[]{new Vec3(3,3,4)});
   check((int)Get(pane,"generation")==generation+(plane=="Axial"?1:0),"axial scroll only regenerates axial pixels/contours: "+plane);
   var after=SliceGeometry.Create(pane.Scene);check(plane=="Axial"||(after.Center-before.Center).Length<1e-7,"orthogonal geometry stays fixed during scroll: "+plane);
   check(pane.Scene.Focus.Z==4,"all panes receive linked focus: "+plane);
  }
  var type=typeof(ViewerControl).Assembly.GetType("QuickLook.DicomRT.PreparationProgress");var progress=Activator.CreateInstance(type);
  Action<string,int> total=(n,v)=>type.GetMethod(n).Invoke(progress,new object[]{v});
  total("SetDrrTotal",10);total("SetOutlineTotal",30);type.GetMethod("DrrDone").Invoke(progress,null);type.GetMethod("OutlineDone").Invoke(progress,null);
  string text=(string)type.GetProperty("Text").GetValue(progress);check(text.Contains("DRRs 1/10")&&text.Contains("ROI projections 1/30"),"DRR and ROI progress counted independently");
  for(int n=1;n<10;n++)type.GetMethod("DrrDone").Invoke(progress,null);for(int n=1;n<30;n++)type.GetMethod("OutlineDone").Invoke(progress,null);
  string finished=(string)type.GetProperty("Text").GetValue(progress);check(finished.Contains("Recorded views")&&finished.Contains("preload processed")&&!finished.Contains("refining"),"recorded preload completion does not pretend to describe fractional playback or refinement");
  var remaining=type.GetMethod("Remaining",BindingFlags.Static|BindingFlags.NonPublic);
  check(double.IsNaN((double)remaining.Invoke(null,new object[]{1,10,5d})),"ETA stays unknown until enough work is observed");
  check(Math.Abs((double)remaining.Invoke(null,new object[]{5,10,20d})-20)<1e-7,"ETA estimates remaining work from measured throughput");
  var next=Activator.CreateInstance(type);type.GetMethod("DrrDone").Invoke(progress,null);check(((string)type.GetProperty("Text").GetValue(next)).Contains("planning"),"cancelled context cannot advance a new generation's progress");
 }
}
