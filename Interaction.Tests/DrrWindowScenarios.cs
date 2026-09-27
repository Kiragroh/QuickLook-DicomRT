using System;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuickLook.DicomRT;
internal static class DrrWindowScenarios {
 static class DrrWindow {
  static readonly Type T=typeof(MlcPlaybackControl).Assembly.GetType("QuickLook.DicomRT.DrrWindow");
  internal static BitmapSource Encode(float[] v,int size)=>(BitmapSource)T.GetMethod("Encode",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{v,size});
  internal static BitmapSource Apply(BitmapSource s,string preset,double c,double w)=>(BitmapSource)T.GetMethod("Apply",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{s,preset,c,w});
 }
 static byte[] Pixels(BitmapSource image){var p=new byte[image.PixelWidth*image.PixelHeight];image.CopyPixels(p,image.PixelWidth,0);return p;}
 public static void Run(Action<bool,string> check){
 var values=Enumerable.Range(0,4096).Select(i=>i<100?0f:800+i/40f).ToArray();var raw=DrrWindow.Encode(values,64);
 check(raw.Format==PixelFormats.Gray16&&raw.IsFrozen,"DRR cache retains frozen 16-bit ray-integral detail");
 var automatic=Pixels(DrrWindow.Apply(raw,"Auto",.5,1));var original=Pixels(DrrWindow.Apply(raw,"Original",.5,1));
 check(automatic[3800]-automatic[300]>180&&original[3800]-original[300]<40,"automatic DRR window reveals low-contrast anatomy instead of uniformly white image");
 check(automatic.Take(100).All(v=>v==0),"auto DRR keeps empty rays black");
 check(automatic.Skip(100).Zip(automatic.Skip(101),(a,b)=>a<=b).All(v=>v),"auto mapping preserves intensity ordering");
 var dark=Pixels(DrrWindow.Apply(raw,"Custom",.95,.2));var bright=Pixels(DrrWindow.Apply(raw,"Custom",.6,.2));
 check(bright.Sum(v=>(long)v)>dark.Sum(v=>(long)v),"manual DRR level changes brightness without a new ray trace");
 check(!Pixels(DrrWindow.Apply(raw,"Soft",.5,1)).SequenceEqual(Pixels(DrrWindow.Apply(raw,"High contrast",.5,1))),"DRR soft and high-contrast presets are distinct");
 var empty=DrrWindow.Encode(new float[64],8);check(Pixels(DrrWindow.Apply(empty,"Auto",.5,1)).All(v=>v==0),"empty DRR auto remains black and finite");
 var flat=DrrWindow.Encode(Enumerable.Repeat(10f,64).ToArray(),8);check(Pixels(DrrWindow.Apply(flat,"Auto",.5,1)).Distinct().Count()==1,"constant DRR auto is stable");
 }
}