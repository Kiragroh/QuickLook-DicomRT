using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace QuickLook.DicomRT
{
    // Radial length is linear angular meterset. Dense visual samples cover each
    // original CP interval without changing its value or inventing delivery CPs.
    internal static class ArcModulationDrawing
    {
        static readonly Brush muted=Theme.Brush("#7D8997");
        static readonly Pen activeLine=Pen(Brushes.Gold,1.4),inactiveLine=Pen(muted,.7);
        static readonly Pen activeTick=Pen(Brushes.Gold,1),inactiveTick=Pen(muted,.6);
        static readonly Pen unknown=Pen(Brushes.SlateGray,.8);
        static Pen Pen(Brush brush,double width){var pen=new Pen(brush,width);pen.Freeze();return pen;}
        internal static double Fraction(double value,double maximum)
        {
            if(!BeamProjection.Finite(value)||value<0||!BeamProjection.Finite(maximum)||maximum<0)return double.NaN;
            return maximum>0?Math.Max(0,Math.Min(1,value/maximum)):0;
        }
        internal static void Draw(DrawingContext dc,BeamMotion.Sample[] samples,Func<Vec3,Point?> project,double radius,double maximum,bool active,BeamModulationMode mode)
        {
            Point? previous=null;
            foreach(var sample in samples){
                var at=project(sample.Iso+sample.SourceDirection*radius);
                if(previous.HasValue&&at.HasValue&&!sample.Break)dc.DrawLine(active?activeLine:inactiveLine,previous.Value,at.Value);
                previous=at;if(!sample.Tick||!at.HasValue)continue;
                double fraction=Fraction(BeamMotion.Value(sample,mode),maximum);
                double length=double.IsNaN(fraction)?radius*.015:radius*.17*fraction;
                var tip=project(sample.Iso+sample.SourceDirection*(radius+length));
                if(tip.HasValue)dc.DrawLine(double.IsNaN(fraction)?unknown:active?activeTick:inactiveTick,at.Value,tip.Value);
            }
        }
        internal static void Legend(DrawingContext dc,Point at,string label,double maximum,string unit,bool active)
        {
            string range=BeamProjection.Finite(maximum)&&maximum>=0?"0–"+maximum.ToString("0.##",CultureInfo.InvariantCulture)+" "+unit:"unavailable";
            dc.DrawText(new FormattedText(label+" · "+range+" · ≤1° samples",CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),10,active?Brushes.Gold:Theme.Muted,1),at);
        }
    }
}
