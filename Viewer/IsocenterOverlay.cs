using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace QuickLook.DicomRT
{
 // The world-space marker remains in the scene; this small, depth-independent
 // locator keeps its center readable through opaque surfaces at every zoom level.
 internal sealed class IsocenterOverlay : FrameworkElement
 {
  IReadOnlyList<Vec3> points;PerspectiveCamera camera;
  internal IsocenterOverlay(){IsHitTestVisible=false;ClipToBounds=true;}
  internal void Set(IReadOnlyList<Vec3> value,PerspectiveCamera view){points=value;camera=view;InvalidateVisual();}
  internal static bool Project(Vec3 world,PerspectiveCamera camera,Size size,out Point point,bool allowOutside=false)
  {
   point=new Point();if(camera==null||size.Width<=0||size.Height<=0)return false;
   var forward=camera.LookDirection;forward.Normalize();var right=Vector3D.CrossProduct(forward,camera.UpDirection);right.Normalize();var up=Vector3D.CrossProduct(right,forward);
   var delta=new Point3D(world.X,world.Y,world.Z)-camera.Position;double depth=Vector3D.DotProduct(delta,forward);
   if(double.IsNaN(depth)||double.IsInfinity(depth)||depth<camera.NearPlaneDistance||depth>camera.FarPlaneDistance)return false;
   double scale=size.Width/(2*depth*Math.Tan(camera.FieldOfView*Math.PI/360));
   double x=size.Width*.5+Vector3D.DotProduct(delta,right)*scale,y=size.Height*.5-Vector3D.DotProduct(delta,up)*scale;
   if(double.IsNaN(x)||double.IsNaN(y)||(!allowOutside&&(x<0||x>size.Width||y<0||y>size.Height)))return false;point=new Point(x,y);return true;
  }
  protected override void OnRender(DrawingContext dc)
  {
   base.OnRender(dc);if(points==null)return;
   var border=new Pen(Brushes.Black,4);var accent=new Pen(Brushes.Cyan,1.5);
   foreach(var iso in points)
   {
    Point p;if(!Project(iso,camera,RenderSize,out p))continue;
    foreach(var pen in new[]{border,accent})
    {
     dc.DrawLine(pen,new Point(p.X-9,p.Y),new Point(p.X+9,p.Y));dc.DrawLine(pen,new Point(p.X,p.Y-9),new Point(p.X,p.Y+9));
     dc.DrawEllipse(null,pen,p,4,4);
    }
    var label=new FormattedText("ISO",CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),10,Brushes.Cyan,VisualTreeHelper.GetDpi(this).PixelsPerDip);
    dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(175,12,16,20)),null,new Rect(p.X+11,p.Y-10,label.Width+5,label.Height+2),2,2);dc.DrawText(label,new Point(p.X+13,p.Y-9));
   }
  }
 }
}
