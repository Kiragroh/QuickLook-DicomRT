using System;
using System.Globalization;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace QuickLook.DicomRT
{
    // A constant-width band encodes angular meterset by color only. Every
    // CP interval retains its own value; neither peaks nor low values are smoothed.
    internal static class ArcModulationDrawing
    {
        static readonly Pen unknown=new Pen(Brushes.SlateGray,1){DashStyle=DashStyles.Dot};
        static readonly Brush[] blue=Palette(true),gray=Palette(false);
        static Brush[] Palette(bool active)
        {
            var result=new Brush[256];
            for(int i=0;i<result.Length;i++){
                double t=i/255d;
                var color=active?Color.FromRgb((byte)(43+57*t),(byte)(77+104*t),(byte)(108+138*t)):
                    Color.FromArgb(150,(byte)(83+82*t),(byte)(92+81*t),(byte)(102+83*t));
                var brush=new SolidColorBrush(color);brush.Freeze();result[i]=brush;
            }
            unknown.Freeze();return result;
        }
        internal static double Fraction(double value,double maximum)
        {
            if(!BeamProjection.Finite(value)||value<0||!BeamProjection.Finite(maximum)||maximum<0)return double.NaN;
            return maximum>0?Math.Max(0,Math.Min(1,value/maximum)):0;
        }
        internal static Brush ColorFor(double value,double maximum,bool active)
        {double t=Fraction(value,maximum);return double.IsNaN(t)?Brushes.SlateGray:(active?blue:gray)[(int)Math.Round(t*255)];}
        internal static void Draw(DrawingContext dc,BeamMotion.Sample[] samples,Func<Vec3,Point?> project,double radius,double maximum,bool active,BeamModulationMode mode)
        {
            double outer=radius*(active?1.035:1.025);
            var inner=new List<Point>();var edge=new List<Point>();double value=double.NaN;
            Action flush=()=>{
                if(inner.Count>1){
                    if(double.IsNaN(Fraction(value,maximum))){for(int i=1;i<inner.Count;i++)dc.DrawLine(unknown,inner[i-1],inner[i]);}
                    else {var shape=new StreamGeometry();using(var g=shape.Open()){g.BeginFigure(inner[0],true,true);for(int i=1;i<inner.Count;i++)g.LineTo(inner[i],true,false);for(int i=edge.Count-1;i>=0;i--)g.LineTo(edge[i],true,false);}shape.Freeze();dc.DrawGeometry(ColorFor(value,maximum,active),null,shape);}
                }
                inner.Clear();edge.Clear();
            };
            foreach(var sample in samples){
                if(sample.Break){flush();value=BeamMotion.Value(sample,mode);}
                if(inner.Count==0)value=BeamMotion.Value(sample,mode);
                var a=project(sample.Iso+sample.SourceDirection*radius);var b=project(sample.Iso+sample.SourceDirection*outer);
                if(a.HasValue&&b.HasValue){inner.Add(a.Value);edge.Add(b.Value);}else flush();
            }
            flush();
        }

        internal static void Legend(DrawingContext dc,Point at,string label,double maximum,string unit,bool active)
        {
            const double width=54;for(int i=0;i<27;i++)dc.DrawRectangle(ColorFor(i/26d,1,active),null,new Rect(at.X+i*2,at.Y+5,2,5));
            string range=BeamProjection.Finite(maximum)&&maximum>=0?"0–"+maximum.ToString("0.##",CultureInfo.InvariantCulture)+" "+unit:"unavailable";
            dc.DrawText(new FormattedText(label+" · "+range,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),10,active?Theme.Foreground:Theme.Muted,1),new Point(at.X+width+7,at.Y));
        }
    }
}
