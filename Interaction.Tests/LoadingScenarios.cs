using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using QuickLook.DicomRT;
internal static class LoadingScenarios
{
 const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
 static T Get<T>(object o,string n)=>(T)o.GetType().GetField(n,F).GetValue(o);
 static void Set(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
 static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,F).Invoke(o,args);
 public static void Run(Action<bool,string> check)
 {
  using(var viewer=new ViewerControl()){
   Set(viewer,"backgroundImageLoads",1);Call(viewer,"UpdateBackgroundIndicator");
   var indicator=Get<Border>(viewer,"backgroundIndicator");check(indicator.Visibility==Visibility.Visible&&!indicator.IsHitTestVisible&&indicator.HorizontalAlignment==HorizontalAlignment.Right,"image load indicator appears at upper right without blocking interaction");
   Set(viewer,"backgroundImageLoads",0);Call(viewer,"UpdateBackgroundIndicator");check(indicator.Visibility==Visibility.Collapsed,"load indicator disappears when tracked work finishes");
   var rt=Get<Button>(viewer,"layersButton");var tags=Get<Button>(viewer,"tagsButton");
   check(!Get<bool>(viewer,"layersVisible")&&!Get<bool>(viewer,"tagsVisible"),"sidebars stay closed without discovered RT");
   Call(viewer,"AutoOpenRtPanel");check(Get<bool>(viewer,"layersVisible")&&((SolidColorBrush)rt.Foreground).Color.B>200&&rt.ToolTip.ToString().StartsWith("Close"),"first RT automatically opens panel with blue close state");
   rt.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Call(viewer,"AutoOpenRtPanel");check(!Get<bool>(viewer,"layersVisible")&&rt.ToolTip.ToString().StartsWith("Open"),"later discoveries respect manual RT close");
   tags.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(Get<bool>(viewer,"tagsVisible")&&tags.ToolTip.ToString().StartsWith("Close"),"tag toggle reflects open state");
   tags.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(!Get<bool>(viewer,"tagsVisible")&&tags.ToolTip.ToString().StartsWith("Open"),"tag toggle closes again");
  }
  using(var three=new ThreeDControl()){
   var scene=new RenderScene{Entry=new DicomEntry{FrameUid="A",SeriesUid="series",Modality="CT"}};three.SetScene(scene);
   three.Measure(new Size(1000,800));three.Arrange(new Rect(0,0,1000,800));three.UpdateLayout();
   Call(three,"FitFieldGuides");check(!Get<bool>(three,"cameraAdjusted"),"automatic field fit does not lock the early ROI-only camera");
   var prepared=Activator.CreateInstance(typeof(ThreeDControl).GetNestedType("Prepared",BindingFlags.NonPublic));Set(three,"prepared",prepared);Set(three,"cameraAdjusted",true);
   var next=new RenderScene{Entry=scene.Entry,Volume=new VolumeData{Width=2,Height=2,Depth=2,Values=new float[8],SpacingX=1,SpacingY=1,SpacingZ=1,AxisX=new Vec3(1,0,0),AxisY=new Vec3(0,1,0),AxisZ=new Vec3(0,0,1)}};three.SetScene(next);
   check(Get<double>(three,"radius")==10,"CT completion updates physical track radius even after manual camera input");
   check(ReferenceEquals(prepared,Get<object>(three,"prepared"))&&Get<bool>(three,"cameraAdjusted"),"background CT completion retains prepared scene and user camera");
   three.SetScene(new RenderScene{FrameUid="A",Entry=new DicomEntry{Modality="RTSTRUCT"}});
   Set(three,"prepared",prepared);Set(three,"cameraAdjusted",true);three.SetScene(next);
   check(ReferenceEquals(prepared,Get<object>(three,"prepared")),"RTSTRUCT frame association retains prepared surfaces when CT arrives");
   three.SetScene(new RenderScene{FrameUid="B",Entry=next.Entry,Volume=next.Volume});
   check(Get<object>(three,"prepared")==null&&!Get<bool>(three,"cameraAdjusted"),"different spatial context clears prior surfaces and camera");
  }
 }
}
