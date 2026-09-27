using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using QuickLook.DicomRT;
internal static class AvatarScenarios {
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
 static T Get<T>(object o,string n)=>(T)o.GetType().GetField(n,F).GetValue(o);
 static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,F).Invoke(o,args);
 public static void Run(Action<bool,string> check){
  var asm=typeof(ViewerControl).Assembly;var prefsType=asm.GetType("QuickLook.DicomRT.OrientationAvatarPreferences");var current=prefsType.GetField("Current",BindingFlags.Static|BindingFlags.NonPublic);var previous=current.GetValue(null);
  string dir=Path.Combine(Path.GetTempPath(),"dicomrt-avatar-"+Guid.NewGuid().ToString("N"));var prefs=Activator.CreateInstance(prefsType,F,null,new object[]{dir},null);current.SetValue(null,prefs);
  var badgeType=asm.GetType("QuickLook.DicomRT.PatientOrientationBadge");var glyph=asm.GetType("QuickLook.DicomRT.PatientOrientationGlyph");var create=glyph.GetMethod("Create",BindingFlags.NonPublic|BindingFlags.Static,null,new[]{typeof(string)},null);
  Window window=null;MlcPlaybackControl mlc=null;
  try{
   var a=(FrameworkElement)Activator.CreateInstance(badgeType);var b=(FrameworkElement)Activator.CreateInstance(badgeType);window=new Window{Content=a,Width=300,Height=200,Left=-30000,Top=-30000,ShowActivated=false,ShowInTaskbar=false};window.Show();
   mlc=new MlcPlaybackControl();var plan=new PlanData{Beams={new PlanBeam{PatientPosition="HFS",ControlPoints={new ControlPoint{Gantry=30,Couch=20,Collimator=10}}}}};mlc.SetPlan(plan);var linac=Get<object>(mlc,"orientation");
   var camera=Get<OrthographicCamera>(a,"camera");var direction=camera.LookDirection;var position=camera.Position;
   check(a.IsHitTestVisible&&a.Cursor==Cursors.Hand,"slice character is an interactive selection target");
   var click=new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonDownEvent};a.RaiseEvent(click);
   check(click.Handled&&Get<Popup>(a,"avatarPicker")==null,"mouse-down consumes input without opening a popup that the release could dismiss");
   a.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonUpEvent});
   check(click.Handled&&Get<Popup>(a,"avatarPicker").IsOpen,"left-click opens character picker and stops crosshair navigation");
   var buttons=Get<System.Collections.Generic.Dictionary<string,Button>>(a,"avatarButtons");check(buttons.Count==5,"picker contains human and all four requested characters");
   foreach(string id in new[]{"human","frieza","obelisk","elsa","saitama"}){
    var before=Get<ModelVisual3D>(b,"avatar").Content;var patient=Get<Model3DGroup>(linac,"patientHost").Children[0];
    buttons[id].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    check((string)prefsType.GetProperty("Selected",F).GetValue(prefs)==id&&!Get<Popup>(a,"avatarPicker").IsOpen,"character selection applies and closes picker: "+id);
    if(id!="human")check(!ReferenceEquals(before,Get<ModelVisual3D>(b,"avatar").Content)&&!ReferenceEquals(patient,Get<Model3DGroup>(linac,"patientHost").Children[0]),"character selection updates other badges and LINAC: "+id);
    var model=(Model3DGroup)create.Invoke(null,new object[]{id});var model2=(Model3DGroup)create.Invoke(null,new object[]{id});
    check(ReferenceEquals(model.Children[0],model2.Children[0])&&model.Children[0].IsFrozen,"characters reuse immutable geometry: "+id);
    var bounds=model.Bounds;check(bounds.SizeX>0&&bounds.SizeZ>1.5&&bounds.SizeZ<2.1&&bounds.Z<-.8,"character keeps normalized body axes and readable size: "+id);
    check(camera.LookDirection==direction&&camera.Position==position,"selection preserves orientation camera: "+id);
   }
   var reloaded=Activator.CreateInstance(prefsType,F,null,new object[]{dir},null);check((string)prefsType.GetProperty("Selected",F).GetValue(reloaded)=="saitama","character preference persists across viewer sessions");
   check(!(bool)Call(prefs,"Select","invalid")&&(string)prefsType.GetProperty("Selected",F).GetValue(prefs)=="saitama","unknown preference cannot replace a valid character");
  }finally{mlc?.Close();window?.Close();current.SetValue(null,previous);if(Directory.Exists(dir)){foreach(var file in Directory.GetFiles(dir))File.Delete(file);Directory.Delete(dir);}}
 }
}