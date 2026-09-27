using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using QuickLook.DicomRT;
internal static class InteractiveContourScenarios {
 const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
 static object Get(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
 static void Settle(SlicePane pane){
  var frame=new DispatcherFrame();var watch=System.Diagnostics.Stopwatch.StartNew();
  var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(2)};
  timer.Tick+=(s,e)=>{if(Get(pane,"pending")==null||watch.Elapsed.TotalSeconds>5)frame.Continue=false;};timer.Start();try{Dispatcher.PushFrame(frame);}finally{timer.Stop();}
  if(Get(pane,"pending")!=null)throw new TimeoutException("Contour frame did not finish");
 }
 public static void Run(Action<bool,string> check){
  var volume=new VolumeData{Width=12,Height=12,Depth=12,Values=new float[1728],SpacingX=1,SpacingY=1,SpacingZ=1,AxisX=new Vec3(1,0,0),AxisY=new Vec3(0,1,0),AxisZ=new Vec3(0,0,1)};
  var roi=new StructureRoi{Visible=true};foreach(double z in new[]{3d,4d,5d,6d,7d})roi.Contours.Add(new Contour{GeometricType="CLOSED_PLANAR",Points=new List<Vec3>{new Vec3(3,3,z),new Vec3(7,3,z),new Vec3(7,7,z),new Vec3(3,7,z)}});
  foreach(string plane in new[]{"Axial","Coronal","Sagittal"})using(var pane=new SlicePane()){
   pane.Measure(new Size(320,320));pane.Arrange(new Rect(0,0,320,320));
   Func<double,bool,RenderScene> scene=(at,preview)=>new RenderScene{Volume=volume,Plane=plane,Focus=new Vec3(at,at,at),ViewCenter=new Vec3(5,5,5),InteractionPreview=preview,Structures=new List<RoiOverlay>{new RoiOverlay{Roi=roi}}};
   pane.Scene=scene(4,false);Settle(pane);var before=Get(pane,"frame");
   pane.Scene=scene(5,true);check(ReferenceEquals(before,Get(pane,"frame")),"pending crosshair frame retains complete previous image and contours: "+plane);Settle(pane);
   var preview=Get(pane,"frame");var lines=(IList)Get(preview,"Lines");check(lines.Count==1&&((IList)Get(lines[0],"Lines")).Count>0,"interactive slice already contains visible ROI contours: "+plane);
   var geometry=((SlicePixels)Get(preview,"Raster")).Geometry;var center=SliceGeometry.Create(pane.Scene).Center;check((geometry.Center-center).Length<1e-7,"interactive contours share current image plane: "+plane);
   int count=((IList)Get(lines[0],"Lines")).Count;pane.Scene=scene(5,false);Settle(pane);
   var finalLines=(IList)Get(Get(pane,"frame"),"Lines");check(((IList)Get(finalLines[0],"Lines")).Count==count,"release keeps the same ROI boundary rather than adding absent structures: "+plane);
  }
 }
}
