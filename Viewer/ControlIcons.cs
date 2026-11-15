using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace QuickLook.DicomRT
{
    internal static class ControlIcons
    {
        static readonly DrawingImage gear=CreateGear();
        static DrawingImage CreateGear()
        {
            var group=new DrawingGroup();using(var dc=group.Open()){
                dc.DrawRectangle(Brushes.Transparent,null,new Rect(0,0,24,24));
                var outline=new StreamGeometry();using(var g=outline.Open()){
                    for(int i=0;i<32;i++){double a=i*Math.PI/16,r=i%4==1||i%4==2?10:7.5;var p=new Point(12+r*Math.Cos(a),12+r*Math.Sin(a));if(i==0)g.BeginFigure(p,true,true);else g.LineTo(p,true,false);}
                }outline.Freeze();var pen=new Pen(Theme.Accent,1.3){LineJoin=PenLineJoin.Round};dc.DrawGeometry(null,pen,outline);dc.DrawEllipse(null,pen,new Point(12,12),3.2,3.2);
            }group.Freeze();var image=new DrawingImage(group);image.Freeze();return image;
        }
        internal static Image Gear()=>new Image{Source=gear,Width=20,Height=20,ToolTip="Viewer settings"};
    }
}
