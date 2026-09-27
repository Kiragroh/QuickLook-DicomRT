using System;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace QuickLook.DicomRT {
 // Display-only tone mapping. Cached ray integrals retain 16-bit detail.
 internal static class DrrWindow {
  internal static BitmapSource Encode(float[] values,int size){
   double high=Math.Max(1,values.Where(v=>!float.IsNaN(v)&&!float.IsInfinity(v)).DefaultIfEmpty(1).Max());
   var pixels=new ushort[values.Length];for(int i=0;i<pixels.Length;i++)pixels[i]=(ushort)Math.Max(0,Math.Min(65535,values[i]/high*65535));
   var image=BitmapSource.Create(size,size,96,96,PixelFormats.Gray16,null,pixels,size*2);image.Freeze();return image;
  }
  internal static BitmapSource Apply(BitmapSource source,string preset,double center,double width){
   int n=source.PixelWidth*source.PixelHeight;var raw=new ushort[n];
   if(source.Format==PixelFormats.Gray16)source.CopyPixels(raw,source.PixelWidth*2,0);
   else {var gray=new FormatConvertedBitmap(source,PixelFormats.Gray8,null,0);var bytes=new byte[n];gray.CopyPixels(bytes,source.PixelWidth,0);for(int i=0;i<n;i++)raw[i]=(ushort)(bytes[i]*257);}
   var hist=new int[4096];int count=0;foreach(var v in raw)if(v>0){hist[v>>4]++;count++;}
   Func<double,double> percentile=q=>{int sum=0,threshold=Math.Max(1,(int)Math.Ceiling(count*q));for(int i=0;i<hist.Length;i++){sum+=hist[i];if(sum>=threshold)return i*16/65535d;}return 1;};
   double low=0,high=1,gamma=1;
   if(preset=="Custom"){low=center-width/2;high=center+width/2;}
   else if(preset=="Original"){high=percentile(.99);gamma=.8;}
   else if(count>0){low=percentile(preset=="High contrast"?.08:.02);high=percentile(preset=="High contrast"?.95:.98);if(preset=="Soft") {low=Math.Max(0,low-(high-low)*.2);gamma=.8;}}
   if(high-low<.001){low=0;high=Math.Max(.001,high);}
   var lut=new byte[65536];for(int i=0;i<lut.Length;i++)lut[i]=(byte)Math.Round(255*Math.Pow(Math.Max(0,Math.Min(1,(i/65535d-low)/(high-low))),gamma));
   var output=new byte[n];for(int i=0;i<n;i++)output[i]=lut[raw[i]];
   var image=BitmapSource.Create(source.PixelWidth,source.PixelHeight,96,96,PixelFormats.Gray8,null,output,source.PixelWidth);image.Freeze();return image;
  }
 }
}