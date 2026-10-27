using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using QuickLook.DicomRT;
internal static class IsodoseVisibilityScenarios
{
 const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
 static T Get<T>(object o,string name)=>(T)o.GetType().GetField(name,F).GetValue(o);
 static object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,F).Invoke(o,args);
 static HashSet<double> Hidden(ViewerControl v)=>(HashSet<double>)typeof(ViewerControl).GetProperty("HiddenIsodoses",F).GetValue(v);
 static byte[] Pixels(FrameworkElement e){var b=new RenderTargetBitmap(640,480,96,96,PixelFormats.Pbgra32);b.Render(e);var bytes=new byte[640*480*4];b.CopyPixels(bytes,2560,0);return bytes;}
 public static void Run(Action<bool,string> check)
 {
  using(var viewer=new ViewerControl())
  {
   var doses=Get<List<DoseGrid>>(viewer,"doses");var first=new DoseGrid{Maximum=60,Units="GY",DoseType="PHYSICAL"};doses.Add(first);Call(viewer,"UpdateIsodoseContext");
   var levels=Get<double[]>(viewer,"displayedIsoLevels");var level=levels[0];Call(viewer,"UpdateIsodoseLegend",new List<DoseOverlay>{new DoseOverlay{Dose=first}});
   var legend=Get<StackPanel>(viewer,"isodoseLegend");var toggle=legend.Children.OfType<DockPanel>().First().Children.OfType<CheckBox>().Single();toggle.IsChecked=false;
   check(Hidden(viewer).Contains(level),"Unchecking one legend level hides only that isodose");
   check(legend.Children.OfType<DockPanel>().Count()==levels.Length&&legend.Children.OfType<DockPanel>().First().Children.OfType<CheckBox>().Single().IsChecked==false,"Hidden levels remain available for reactivation");
   Call(viewer,"Redraw");check(Get<RenderScene>(viewer,"latestScene").HiddenIsoLevels.Contains(level),"Redraw retains shared visibility for image and MPR views");
   doses.Clear();doses.Add(new DoseGrid{Maximum=60,Units="GY",DoseType="PHYSICAL"});Call(viewer,"UpdateIsodoseContext");check(Hidden(viewer).Count==0,"Different dose does not inherit absolute Gy exclusions");
   doses.Clear();doses.Add(first);Call(viewer,"UpdateIsodoseContext");check(Hidden(viewer).Contains(level),"Returning to a dose restores its visibility selection");
   Get<ComboBox>(viewer,"isodoseMode").SelectedIndex=1;check(Hidden(viewer).Count==0,"Gy exclusions do not become percent exclusions");
   Get<ComboBox>(viewer,"isodoseMode").SelectedIndex=0;check(Hidden(viewer).Contains(level),"Returning from percent to Gy preserves visibility");
   Call(viewer,"DefaultIsodoseLevels");check(Hidden(viewer).Count==0,"Default re-enables all levels");
  }
  using(var pane=new SlicePane())
  {
   var vol=new VolumeData{Width=64,Height=64,Depth=2,SpacingX=1,SpacingY=1,SpacingZ=1,AxisX=new Vec3(1,0,0),AxisY=new Vec3(0,1,0),AxisZ=new Vec3(0,0,1),Max=100,Values=Enumerable.Range(0,64*64*2).Select(i=>(float)(100*(i%64)/63d)).ToArray()};
   var dose=(DoseGrid)typeof(DoseGrid).GetMethod("FromDerivedVolume",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{vol,"synthetic","Synthetic ramp"});
   var scene=new RenderScene{Native=new PixelPlane{Width=64,Height=64,Values=new float[4096]},Entry=new DicomEntry{HasGeometry=true,SpacingX=1,SpacingY=1,AxisX=vol.AxisX,AxisY=vol.AxisY},IsoLevels=new[]{50d},Isodoses=true,DoseWash=false,Crosshair=false};scene.Doses.Add(new DoseOverlay{Dose=dose});
   pane.Measure(new Size(640,480));pane.Arrange(new Rect(0,0,640,480));pane.Scene=scene;
   var timer=System.Diagnostics.Stopwatch.StartNew();while(Get<object>(pane,"frame")==null){if(timer.ElapsedMilliseconds>5000)throw new Exception("Isodose frame timeout");var pump=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>pump.Continue=false));Dispatcher.PushFrame(pump);System.Threading.Thread.Sleep(5);}
   pane.UpdateLayout();var before=Pixels(pane);var frame=Get<object>(pane,"frame");int generation=Get<int>(pane,"generation");
   var hits=Get<List<KeyValuePair<Rect,double>>>(pane,"isodoseHitAreas");check(hits.Count==1,"Image legend exposes a hit target for the level");
   bool clicked=false;pane.IsodoseToggled+=l=>{clicked=l==50;Call(pane,"UpdateIsodoseVisibility",new HashSet<double>{l});};var rect=hits[0].Key;
   check((bool)Call(pane,"ToggleIsodoseAt",new Point(rect.Left+3,rect.Top+3))&&clicked,"Clicking the image legend toggles the isodose instead of navigating");
   pane.UpdateLayout();var after=Pixels(pane);check(!before.SequenceEqual(after),"Hidden contour and muted legend change rendered pixels");
   check(ReferenceEquals(frame,Get<object>(pane,"frame"))&&generation==Get<int>(pane,"generation"),"Toggle reuses the frame without CT/dose/contour recalculation");
   Call(pane,"UpdateIsodoseVisibility",new HashSet<double>());pane.UpdateLayout();check(before.SequenceEqual(Pixels(pane)),"Re-enabling restores the exact prior rendered view");
  }
 }
}
