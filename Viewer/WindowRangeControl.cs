using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
namespace QuickLook.DicomRT
{
    internal sealed class WindowRangeControl:FrameworkElement
    {
        float[] source;double[] histogram;int version;double low=-160,high=240,min=-1024,max=2000;bool upper;
        public event Action<double,double> WindowChanged;
        public WindowRangeControl(){Width=142;Height=184;ToolTip="Drag the blue upper / lower window handles; histogram shows sampled image intensities";MouseLeftButtonDown+=(s,e)=>{upper=Math.Abs(e.GetPosition(this).Y-Y(high))<=Math.Abs(e.GetPosition(this).Y-Y(low));CaptureMouse();Change(e.GetPosition(this).Y);e.Handled=true;};MouseMove+=(s,e)=>{if(IsMouseCaptured&&e.LeftButton==MouseButtonState.Pressed){Change(e.GetPosition(this).Y);e.Handled=true;}};MouseLeftButtonUp+=(s,e)=>{ReleaseMouseCapture();e.Handled=true;};}
        public void SetWindow(double center,double width){low=center-width/2;high=center+width/2;InvalidateVisual();}
        public async void SetSource(float[] values,double minimum,double maximum,string unit)
        {
            Units=unit;if(ReferenceEquals(source,values))return;source=values;int mine=++version;min=minimum;max=Math.Max(min+1,maximum);histogram=null;InvalidateVisual();if(values==null)return;
            double lo=min,span=max-min;var bins=await Task.Run(()=>{var counts=new double[128];int stride=Math.Max(1,values.Length/200000);for(int i=0;i<values.Length;i+=stride){double v=values[i];if(double.IsNaN(v)||double.IsInfinity(v))continue;int bin=Math.Max(0,Math.Min(127,(int)((v-lo)/span*127)));counts[bin]++;}double peak=counts.Max();if(peak>0)for(int i=0;i<counts.Length;i++)counts[i]=Math.Log(1+counts[i])/Math.Log(1+peak);return counts;});if(mine==version){histogram=bins;InvalidateVisual();}
        }
        string Units="";
        double Y(double value)=>14+(max-Math.Max(min,Math.Min(max,value)))/(max-min)*(ActualHeight-28);
        void Change(double y){double value=max-Math.Max(0,Math.Min(1,(y-14)/Math.Max(1,ActualHeight-28)))*(max-min);if(upper)high=Math.Max(low+1,value);else low=Math.Min(high-1,value);WindowChanged?.Invoke((low+high)/2,high-low);InvalidateVisual();}
        protected override void OnRender(DrawingContext dc)
        {
            dc.DrawRectangle(Brushes.Transparent,null,new Rect(0,0,ActualWidth,ActualHeight));double h=ActualHeight-28;
            var bar=new Rect(91,14,14,h);dc.DrawRectangle(new LinearGradientBrush(Colors.White,Colors.Black,90),null,bar);
            if(histogram!=null)for(int i=0;i<histogram.Length;i++){double y=14+(1-(i+.5)/128)*h;dc.DrawLine(new Pen(Theme.Accent,Math.Max(1,h/128)),new Point(89-histogram[i]*26,y),new Point(89,y));}
            dc.DrawRectangle(null,new Pen(Theme.Accent,1),new Rect(90,Y(high),16,Math.Max(1,Y(low)-Y(high))));
            foreach(var v in new[]{low,high}){double y=Y(v);dc.DrawLine(new Pen(Theme.Accent,3),new Point(89,y),new Point(118,y));Text(dc,v.ToString("0",CultureInfo.CurrentCulture)+" "+Units,2,y-6,Theme.Foreground);}
            Text(dc,max.ToString("0",CultureInfo.CurrentCulture),69,0,Theme.Muted);Text(dc,min.ToString("0",CultureInfo.CurrentCulture),69,ActualHeight-13,Theme.Muted);
        }
        void Text(DrawingContext dc,string text,double x,double y,Brush brush)=>dc.DrawText(new FormattedText(text,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),10,brush,VisualTreeHelper.GetDpi(this).PixelsPerDip),new Point(x,y));
    }
}
