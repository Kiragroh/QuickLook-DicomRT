using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuickLook.DicomRT;
internal static class ThreeDControlPointScenarios
{
 const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
 static T Get<T>(object o,string n)=>(T)o.GetType().GetField(n,F).GetValue(o);
 static void Set(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
 static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,F).Invoke(o,args);
 static byte[] Pixels(FrameworkElement element){element.Measure(new Size(700,600));element.Arrange(new Rect(0,0,700,600));element.UpdateLayout();var image=new RenderTargetBitmap(700,600,96,96,PixelFormats.Pbgra32);image.Render(element);var pixels=new byte[700*600*4];image.CopyPixels(pixels,2800,0);return pixels;}
 public static void Run(Action<bool,string> check)
 {
  using(var viewer=new ViewerControl()){
   var entry=new DicomEntry{Modality="RTPLAN",FrameUid="fixture",SopUid="fixture-plan"};var plan=new PlanData{Entry=entry,FrameUid="fixture",Label="Demo"};
   for(int b=0;b<2;b++){var beam=new PlanBeam{Number=b+1,Name=b==0?"Arc":"Static",PatientPosition="HFS",SourceAxisDistance=1000};
    for(int c=0;c<3;c++){var cp=new ControlPoint{Gantry=b==0?c*45:90,GantryRotationDirection=b==0?"CW":"NONE",XJaws=new[]{-40d,40d},YJaws=new[]{-30d,30d}};
     cp.MlcLayers.Add(new MlcLayer{Type="MLCX",Key="X",Boundaries=new[]{-30d,0d,30d},Positions=new[]{-25d+c*8,-15d,15d,25d-c*8}});cp.MlcLayers.Add(new MlcLayer{Type="MLCX",Key="X2",Boundaries=new[]{-30d,0d,30d},Positions=new[]{-30d,-10d+c*4,20d,30d}});beam.ControlPoints.Add(cp);}plan.Beams.Add(beam);}
   var window=new Window{Content=viewer,Width=1400,Height=900,Left=-30000,Top=-30000,ShowActivated=false,ShowInTaskbar=false};window.Show();
   try{Set(viewer,"selectedPlan",plan);Set(viewer,"currentEntry",entry);Call(viewer,"SetWorkspace","3D");var three=Get<ThreeDControl>(viewer,"threeDView");viewer.Measure(new Size(1400,900));viewer.Arrange(new Rect(0,0,1400,900));viewer.UpdateLayout();Get<CheckBox>(three,"showBeamFields").IsChecked=true;
   var slider=Get<Slider>(three,"beamCursor");var picker=Get<ComboBox>(three,"activeBeamPicker");var playback=Get<MlcPlaybackControl>(viewer,"centralPlayback");
   check(Get<CheckBox>(viewer,"showFields").IsChecked==true&&Get<CheckBox>(playback,"showFieldArrangement").IsChecked==true,"3D fields toggle synchronizes image and MLC");
   Get<CheckBox>(playback,"showFieldArrangement").IsChecked=false;check(Get<CheckBox>(three,"showBeamFields").IsChecked==false&&Get<CheckBox>(viewer,"showFields").IsChecked==false,"MLC fields toggle synchronizes all views");
   Get<CheckBox>(viewer,"showFields").IsChecked=true;check(Get<CheckBox>(three,"showBeamFields").IsChecked==true,"image fields toggle synchronizes 3D");
   check(picker.SelectedItem==plan.Beams[0]&&slider.Maximum==2,"standalone 3D exposes active field and complete CP range without CT");
   var prepared=Get<object>(three,"prepared");int generation=Get<int>(three,"generation");var guide=Get<FrameworkElement>(three,"beamFields");
   Pixels(guide);var anchor0=Get<Point?>(guide,"MiniatureAnchor");check(Get<bool>(guide,"MiniatureVisible"),"active 3D field has a mini MLC even before tracks finish preparing");
   slider.Value=1;Pixels(guide);check(playback.LocalPosition==1&&Get<RenderScene>(viewer,"latestScene").ActiveControlPoint.Gantry==45,"3D CP slider drives shared interpolation");
   var anchor1=Get<Point?>(guide,"MiniatureAnchor");check(anchor0.HasValue&&anchor1.HasValue&&(anchor1.Value-anchor0.Value).Length>10,"active mini MLC travels along the arc");
   var corners=Get<Vec3[]>(guide,"MiniatureCorners");string reason;var projection=BeamProjection.Create(plan.Beams[0],Get<RenderScene>(viewer,"latestScene").ActiveControlPoint,Matrix4.Identity,out reason);
   var middle=(corners[0]+corners[2])*.5;var normal=(corners[1]-corners[0]).Cross(corners[3]-corners[0]).Normalized();
   check(Math.Abs(normal.Dot(projection.Forward))>.999999&&(middle-projection.Iso+projection.Forward*Get<double>(guide,"radius")).Length<1e-6,"MLC plane sits on source ring and faces isocenter in patient space");
   var cp=Get<RenderScene>(viewer,"latestScene").ActiveControlPoint;cp.Collimator=37;cp.Couch=23;guide.InvalidateVisual();Pixels(guide);
   var turned=Get<Vec3[]>(guide,"MiniatureCorners");var oriented=BeamProjection.Create(plan.Beams[0],cp,Matrix4.Identity,out reason);
   check((turned[1]-turned[0]).Normalized().Dot(oriented.Right)>.999999&&(turned[3]-turned[0]).Normalized().Dot(oriented.Up)>.999999,"MLC edges follow nonzero collimator and couch orientation");
   cp.Collimator=0;cp.Couch=0;
   check(Get<ControlPoint>(guide,"miniaturePoint").MlcLayers.Count==2,"3D miniature includes double-layer MLC");
   check(ReferenceEquals(prepared,Get<object>(three,"prepared"))&&generation==Get<int>(three,"generation"),"CP changes preserve surfaces without scheduling mesh rebuild");
   slider.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,0,-120){RoutedEvent=Mouse.PreviewMouseWheelEvent});check(slider.Value==2&&playback.LocalPosition==2,"3D CP wheel uses recorded points");
   picker.SelectedItem=plan.Beams[1];slider.Value=0;Pixels(guide);var fixedAnchor=Get<Point?>(guide,"MiniatureAnchor");var first=Pixels(guide);
   slider.Value=1;Pixels(guide);var second=Pixels(guide);check(Get<Point?>(guide,"MiniatureAnchor")==fixedAnchor&&!first.SequenceEqual(second),"static-field MLC changes while its source marker stays fixed");
   Call(viewer,"SetWorkspace","MLC");check(playback.LocalPosition==1&&Get<PlanBeam>(viewer,"activeField")==plan.Beams[1],"3D to MLC preserves beam and CP");
   playback.Navigate(plan.Beams[0],1.5);Call(viewer,"SetWorkspace","3D");check(slider.Value==1.5&&picker.SelectedItem==plan.Beams[0],"MLC to 3D synchronizes fractional CP");
   check(Get<TextBlock>(three,"beamPosition").Text.Contains("Coll"),"3D CP information includes collimator angle");
   var play3=Get<Button>(three,"beamPlay");play3.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(playback.IsPlaying&&play3.Content.ToString()=="Ⅱ","3D Play starts shared playback");
   Call(playback,"AdvancePlayback");double playingAt=playback.Position;Call(viewer,"SetWorkspace","MLC");check(playback.IsPlaying&&playback.Position==playingAt,"playback continues at same position when switching into MLC");
   Get<Button>(playback,"play").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Call(viewer,"SetWorkspace","3D");check(!playback.IsPlaying&&playback.Position==playingAt&&play3.Content.ToString()=="▶","MLC pause is retained and reflected in 3D");
   var global=Get<Slider>(playback,"cursor");global.Value=global.Maximum;playback.TogglePlayback();global.Value=global.Maximum;Call(playback,"AdvancePlayback");check(playback.IsPlaying&&global.Value==0&&picker.SelectedItem==plan.Beams[0],"shared plan loops at end with synchronized active field");playback.Pause();
   plan.Beams[0].ControlPoints[2].MlcLayers.RemoveAt(1);playback.Navigate(plan.Beams[0],1.6);Pixels(guide);check(!Get<bool>(guide,"MiniatureVisible")&&Get<RenderScene>(viewer,"latestScene").ActiveControlPoint==null,"incompatible CP geometry clears the prior miniature instead of presenting stale leaves");
   picker.SelectedIndex=0;Pixels(guide);check(!Get<bool>(guide,"MiniatureVisible")&&slider.Visibility==Visibility.Collapsed,"neutral all-fields view has no miniature or active CP slider");
   }finally{window.Close();}
  }
 }
}
