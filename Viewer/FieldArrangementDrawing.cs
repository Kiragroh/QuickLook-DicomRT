using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace QuickLook.DicomRT
{
    internal static class FieldArrangementDrawing
    {
        public static void Draw(DrawingContext dc,RenderScene scene,SliceGeometry g,Rect rect)
        {
            if(!scene.ShowFields||scene.Plan==null||scene.PlanToImage==null)return;
            Func<Vec3,Point> screen=p=>new Point(rect.Left+g.U(p)*rect.Width,rect.Top+g.V(p)*rect.Height);
            int missing=0;
            foreach(var beam in scene.Plan.Beams.OrderBy(b=>b==scene.ActiveBeam?1:0))
            {
                if(beam.TreatmentDeliveryType=="SETUP"||beam.TreatmentDeliveryType=="PORTFILM")continue;
                bool active=beam==scene.ActiveBeam;var cp=active?scene.ActiveControlPoint:beam.ControlPoints.FirstOrDefault();
                string reason;var projection=BeamProjection.Create(beam,cp,scene.PlanToImage,out reason);if(projection==null){missing++;continue;}
                double xmin=-50,xmax=50,ymin=-50,ymax=50;bool aperture=false;
                if(cp.XJaws?.Length==2){xmin=cp.XJaws[0];xmax=cp.XJaws[1];aperture=true;}
                if(cp.YJaws?.Length==2){ymin=cp.YJaws[0];ymax=cp.YJaws[1];aperture=true;}
                // Without jaws, a conservative envelope of actual leaf openings is shown.
                foreach(var layer in cp.MlcLayers){if(layer.Positions.Length==0)continue;aperture=true;if(layer.IsY){if(cp.XJaws?.Length!=2){xmin=layer.Boundaries.Min();xmax=layer.Boundaries.Max();}if(cp.YJaws?.Length!=2){ymin=layer.Positions.Min();ymax=layer.Positions.Max();}}
                    else{if(cp.XJaws?.Length!=2){xmin=layer.Positions.Min();xmax=layer.Positions.Max();}if(cp.YJaws?.Length!=2){ymin=layer.Boundaries.Min();ymax=layer.Boundaries.Max();}}}
                var color=active?Color.FromRgb(255,215,82):Color.FromArgb(105,95,172,228);var brush=new SolidColorBrush(color);var pen=new Pen(brush,active?1.6:.65);
                if(aperture&&xmax>xmin&&ymax>ymin)
                {
                    var corners=new[]{projection.PlanePoint(xmin,ymin),projection.PlanePoint(xmax,ymin),projection.PlanePoint(xmax,ymax),projection.PlanePoint(xmin,ymax)};
                    var polygon=new List<Vec3>{g.WorldAt(0,0),g.WorldAt(1,0),g.WorldAt(1,1),g.WorldAt(0,1)};
                    for(int i=0;i<4;i++){
                        var normal=(corners[i]-projection.Source).Cross(corners[(i+1)%4]-projection.Source).Normalized();
                        var center=projection.PlanePoint((xmin+xmax)*.5,(ymin+ymax)*.5);if(normal.Dot(center-projection.Source)<0)normal=normal*-1;
                        polygon=Clip(polygon,projection.Source,normal);
                    }
                    if(polygon.Count>1){var shape=new StreamGeometry();using(var context=shape.Open()){context.BeginFigure(screen(polygon[0]),false,true);context.PolyLineTo(polygon.Skip(1).Select(screen).ToArray(),true,false);}dc.DrawGeometry(null,pen,shape);}
                }
                // Central axis is projected into this slice; the aperture above is its true plane intersection.
                var direction=projection.Forward-g.Normal*projection.Forward.Dot(g.Normal);
                if(direction.Length>.01){var axis=new Pen(brush,active?1.3:.5){DashStyle=DashStyles.Dash};dc.DrawLine(axis,screen(projection.Iso-direction.Normalized()*Math.Max(g.WidthMm,g.HeightMm)),screen(projection.Iso+direction.Normalized()*Math.Max(g.WidthMm,g.HeightMm)));}
                if(active){var p=screen(projection.Iso);dc.DrawEllipse(null,new Pen(Brushes.Gold,1.5),p,4,4);double oblique=Math.Asin(Math.Min(1,Math.Abs(projection.Forward.Dot(g.Normal))))*180/Math.PI;
                    Label(dc,$"B{beam.Number}  {oblique:0}Â° out of plane",new Point(rect.Left+5,rect.Top+5),Brushes.Gold);}
            }
            if(missing>0)Label(dc,missing+" fields: geometry unavailable",new Point(rect.Left+5,rect.Bottom-15),Brushes.LightSlateGray);
        }
        static List<Vec3> Clip(List<Vec3> input,Vec3 source,Vec3 normal)
        {
            var output=new List<Vec3>();if(input.Count==0)return output;var previous=input[input.Count-1];double a=(previous-source).Dot(normal);
            foreach(var point in input){double b=(point-source).Dot(normal);if((a>=0)!=(b>=0))output.Add(previous+(point-previous)*(a/(a-b)));if(b>=0)output.Add(point);previous=point;a=b;}return output;
        }
        static void Label(DrawingContext dc,string text,Point p,Brush brush)=>dc.DrawText(new FormattedText(text,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),9,brush,1),p);
    }
}
