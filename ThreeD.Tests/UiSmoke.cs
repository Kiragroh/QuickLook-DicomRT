using System;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using QuickLook.DicomRT;
internal static class UiSmoke
{
 public static void Run()
 {
  SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
  var v=new VolumeData{Width=40,Height=40,Depth=40,Origin=new Vec3(-20,-20,-20),AxisX=new Vec3(1,0,0),AxisY=new Vec3(0,1,0),AxisZ=new Vec3(0,0,1),SpacingX=1,SpacingY=1,SpacingZ=1,Values=new float[64000]};
  for(int z=0;z<40;z++)for(int y=0;y<40;y++)for(int x=0;x<40;x++){double r=v.WorldAt(x,y,z).Length;v.Values[(z*40+y)*40+x]=r<10?1000:r<16?0:-1000;}
  using(var control=new ThreeDControl())
  {
   var window=new Window{Width=700,Height=500,Left=-30000,Top=-30000,ShowInTaskbar=false,ShowActivated=false,WindowStyle=WindowStyle.None,Content=control};
   try
   {
    window.Show();((System.Windows.Controls.CheckBox)typeof(ThreeDControl).GetField("bone",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(control)).IsChecked=true;control.SetScene(new RenderScene{Volume=v,Entry=new DicomEntry{Modality="CT"},Focus=v.Center});
    var field=typeof(ThreeDControl).GetField("prepared",BindingFlags.Instance|BindingFlags.NonPublic);var frame=new DispatcherFrame();var started=DateTime.UtcNow;var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(30)};
    timer.Tick+=(s,e)=>{if(field.GetValue(control)!=null||(DateTime.UtcNow-started).TotalSeconds>10)frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);timer.Stop();
    if(field.GetValue(control)==null)throw new Exception("3D background build did not publish a frame");
    var visual=(ModelVisual3D)typeof(ThreeDControl).GetField("visual",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(control);var models=visual.Content;var prepared=field.GetValue(control);
    for(int orbit=0;orbit<30;orbit++)typeof(ThreeDControl).GetMethod("UpdateCamera",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(control,null);
    if(!ReferenceEquals(models,visual.Content)||!ReferenceEquals(prepared,field.GetValue(control)))throw new Exception("orbit rebuilds models or geometry");
    control.UpdateLayout();var bitmap=Direct3DTests.CaptureControl(control);int w=bitmap.PixelWidth,h=bitmap.PixelHeight;var pixels=new byte[w*h*4];bitmap.CopyPixels(pixels,w*4,0);int bright=0;for(int y=0;y<h;y++)for(int x=0;x<w;x++){int p=(y*w+x)*4;if(pixels[p]+pixels[p+1]+pixels[p+2]>400)bright++;}if(bright<500)throw new Exception("3D synthetic geometry not visible in composition");
    window.Hide();control.SetScene(new RenderScene{Volume=v,Entry=new DicomEntry{Modality="MR"}});window.Show();control.Dispose();
   }
   finally {window.Close();}
  }
 }
}
