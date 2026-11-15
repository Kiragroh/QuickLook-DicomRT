using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
namespace QuickLook.DicomRT
{
    internal sealed partial class MlcAperture
    {
        bool showJawGuides=true;
        public bool ShowJawGuides {get=>showJawGuides;set{showJawGuides=value;InvalidateVisual();}}
        static Pen GuidePen(string color,double width){var pen=new Pen(Theme.Brush(color),width);pen.Freeze();return pen;}
        static readonly Pen backing=GuidePen("#15191D",3.3),tip=GuidePen("#8DDEFF",1.6),secondTip=GuidePen("#FFD39A",1.6),jawX=GuidePen("#FFD166",1.6),jawY=GuidePen("#D991FF",1.6);
        internal static bool KnownJaw(double[] jaws)=>jaws?.Length==2&&jaws.All(BeamProjection.Finite);
        internal static Rect TipBounds(double[] x,double[] y,double extent)
        {
            double left=KnownJaw(x)?Math.Max(-extent,x[0]):-extent,right=KnownJaw(x)?Math.Min(extent,x[1]):extent;
            double bottom=KnownJaw(y)?Math.Max(-extent,y[0]):-extent,top=KnownJaw(y)?Math.Min(extent,y[1]):extent;
            return right<=left||top<=bottom?Rect.Empty:new Rect(left,bottom,right-left,top-bottom);
        }
        void DrawGuides(DrawingContext dc,MlcLayer[] layers,double[] x,double[] y,double extent,Func<double,double,Point> point)
        {
            var bounds=TipBounds(x,y,extent);
            if(!bounds.IsEmpty){
                dc.PushClip(new RectangleGeometry(new Rect(point(bounds.Left,bounds.Bottom),point(bounds.Right,bounds.Top))));
                for(int i=0;i<layers.Length;i++){
                    if(layerIndex>=0&&i!=layerIndex)continue;var layer=layers[i];int n=layer.Boundaries.Length-1;
                    if(n<1||layer.Positions.Length!=n*2)continue;
                    for(int k=0;k<n;k++)foreach(int bank in new[]{0,n}){
                        double v=layer.Positions[k+bank],a=layer.Boundaries[k],b=layer.Boundaries[k+1];
                        var from=layer.IsY?point(a,v):point(v,a);var to=layer.IsY?point(b,v):point(v,b);
                        dc.DrawLine(backing,from,to);dc.DrawLine(i%2==0?tip:secondTip,from,to);
                    }
                }dc.Pop();
            }
            if(!ShowJawGuides)return;
            if(KnownJaw(x))for(int i=0;i<2;i++){var a=point(x[i],-extent);var b=point(x[i],extent);dc.DrawLine(backing,a,b);dc.DrawLine(jawX,a,b);GuideLabel(dc,"X"+(i+1),b+jawOffset,jawX.Brush);}
            if(KnownJaw(y))for(int i=0;i<2;i++){var a=point(-extent,y[i]);var b=point(extent,y[i]);dc.DrawLine(backing,a,b);dc.DrawLine(jawY,a,b);GuideLabel(dc,"Y"+(i+1),a+jawOffset,jawY.Brush);}
        }
        static readonly Vector jawOffset=new Vector(4,3);
        void GuideLabel(DrawingContext dc,string text,Point at,Brush color)
        {
            var label=new FormattedText(text,System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),10,color,VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawRoundedRectangle(Theme.Background,null,new Rect(at-new Vector(2,1),new Size(label.Width+4,label.Height+2)),2,2);dc.DrawText(label,at);
        }
    }
}
