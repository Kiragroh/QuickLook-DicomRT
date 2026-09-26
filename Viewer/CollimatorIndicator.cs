using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
namespace QuickLook.DicomRT
{
 internal sealed class CollimatorIndicator : FrameworkElement
 {
  double angle=double.NaN;
  public double Angle{get=>angle;set{if(angle.Equals(value))return;angle=value;InvalidateVisual();}}
  public CollimatorIndicator(){Height=25;IsHitTestVisible=false;}
  protected override void OnRender(DrawingContext dc)
  {
   bool valid=!double.IsNaN(angle)&&!double.IsInfinity(angle);var center=new Point(13,12);var muted=new Pen(Theme.Muted,.7);dc.DrawEllipse(null,muted,center,9,9);dc.DrawLine(muted,new Point(13,1),new Point(13,4));
   if(valid){dc.PushTransform(new RotateTransform(-angle,13,12));dc.DrawRectangle(null,new Pen(Brushes.Gold,1.3),new Rect(8,5,10,14));dc.DrawEllipse(Brushes.Gold,null,new Point(13,5),1.7,1.7);dc.Pop();}
   string text=valid?"COLL "+angle.ToString("0.0",CultureInfo.InvariantCulture)+"°  ·  source view":"COLL unavailable";
   dc.DrawText(new FormattedText(text,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),10,valid?Brushes.Gold:Theme.Muted,VisualTreeHelper.GetDpi(this).PixelsPerDip),new Point(30,5));
  }
 }
}
