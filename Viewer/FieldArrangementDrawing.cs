using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace QuickLook.DicomRT
{
    internal static class FieldArrangementDrawing
    {
        sealed class Prepared {public ControlPoint Point;public PlanBeam Beam;public PlanData Plan;public Matrix4 Map;public Vec3 Origin,End;public CancellationTokenSource Cancel;public Task<Dictionary<PlanBeam,Geometry>> Work;}
        static readonly ConditionalWeakTable<RenderScene,Prepared> frames=new ConditionalWeakTable<RenderScene,Prepared>();
        public static void Draw(DrawingContext dc,RenderScene scene,SliceGeometry g,Rect rect,Action invalidate=null)
        {
            if(!scene.ShowFields||scene.Plan==null||scene.PlanToImage==null)return;
            var prepared=frames.GetValue(scene,_=>new Prepared());var origin=g.WorldAt(0,0);var end=g.WorldAt(1,1);
            if(prepared.Work==null||prepared.Point!=scene.ActiveControlPoint||prepared.Beam!=scene.ActiveBeam||prepared.Plan!=scene.Plan||prepared.Map!=scene.PlanToImage||(origin-prepared.Origin).Length>1e-8||(end-prepared.End).Length>1e-8)
            {
                prepared.Cancel?.Cancel();prepared.Cancel=new CancellationTokenSource();var token=prepared.Cancel.Token;
                prepared.Point=scene.ActiveControlPoint;prepared.Beam=scene.ActiveBeam;prepared.Plan=scene.Plan;prepared.Map=scene.PlanToImage;prepared.Origin=origin;prepared.End=end;
                var entries=scene.Plan.Beams.Where(b=>b.TreatmentDeliveryType!="SETUP"&&b.TreatmentDeliveryType!="PORTFILM").Select(b=>Tuple.Create(b,b==scene.ActiveBeam?scene.ActiveControlPoint:b.ControlPoints.FirstOrDefault())).ToArray();var map=scene.PlanToImage;
                var dispatcher=Dispatcher.CurrentDispatcher;
                prepared.Work=Task.Run(()=>{var shapes=new Dictionary<PlanBeam,Geometry>();foreach(var entry in entries){token.ThrowIfCancellationRequested();string reason;var p=BeamProjection.Create(entry.Item1,entry.Item2,map,out reason);if(p!=null)shapes[entry.Item1]=Opening(p,entry.Item2,g,token);}return shapes;},token);
                prepared.Work.ContinueWith(t=>{if(t.IsFaulted){var ignored=t.Exception;}if(t.Status==TaskStatus.RanToCompletion&&!token.IsCancellationRequested&&invalidate!=null&&!dispatcher.HasShutdownStarted)dispatcher.BeginInvoke(invalidate,DispatcherPriority.Background);},TaskScheduler.Default);
            }
            var ready=prepared.Work.Status==TaskStatus.RanToCompletion?prepared.Work.Result:null;
            Func<Vec3,Point> screen=p=>new Point(rect.Left+g.U(p)*rect.Width,rect.Top+g.V(p)*rect.Height);
            int missing=0;
            foreach(var beam in scene.Plan.Beams.OrderBy(b=>b==scene.ActiveBeam?1:0))
            {
                if(beam.TreatmentDeliveryType=="SETUP"||beam.TreatmentDeliveryType=="PORTFILM")continue;
                bool active=beam==scene.ActiveBeam;var cp=active?scene.ActiveControlPoint:beam.ControlPoints.FirstOrDefault();
                string reason;var projection=BeamProjection.Create(beam,cp,scene.PlanToImage,out reason);if(projection==null){missing++;continue;}
                var color=active?Color.FromRgb(255,215,82):Color.FromArgb(80,95,172,228);var brush=new SolidColorBrush(color);var pen=new Pen(brush,active?1.7:.65);
                Geometry shape=null;ready?.TryGetValue(beam,out shape);
                if(shape!=null){var display=shape.Clone();display.Transform=new MatrixTransform(rect.Width,0,0,rect.Height,rect.Left,rect.Top);dc.DrawGeometry(null,pen,display);}
                // Central axis is projected into this slice; the aperture above is its true plane intersection.
                var direction=projection.Forward-g.Normal*projection.Forward.Dot(g.Normal);
                if(direction.Length>.01){var axis=new Pen(brush,active?1.3:.5){DashStyle=DashStyles.Dash};dc.DrawLine(axis,screen(projection.Iso-direction.Normalized()*Math.Max(g.WidthMm,g.HeightMm)),screen(projection.Iso+direction.Normalized()*Math.Max(g.WidthMm,g.HeightMm)));}
                if(active){var p=screen(projection.Iso);dc.DrawEllipse(null,new Pen(Brushes.Gold,1.5),p,4,4);double oblique=Math.Asin(Math.Min(1,Math.Abs(projection.Forward.Dot(g.Normal))))*180/Math.PI;
                    Label(dc,$"B{beam.Number}  {oblique:0}° out of plane",new Point(rect.Left+5,rect.Top+5),Brushes.Gold);}
            }
            if(missing>0)Label(dc,missing+" fields: geometry unavailable",new Point(rect.Left+5,rect.Bottom-15),Brushes.LightSlateGray);
        }
        // Merge the filled leaf ray intersections before stroking: no internal leaf seams.
        internal static Geometry Opening(BeamProjection projection,ControlPoint cp,SliceGeometry g,CancellationToken token=default(CancellationToken))
        {
            var group=new GeometryGroup{FillRule=FillRule.Nonzero};
            foreach(var r in BeamAperture.Rectangles(cp))
            {
                token.ThrowIfCancellationRequested();
                var corners=new[]{projection.PlanePoint(r.Left,r.Bottom),projection.PlanePoint(r.Right,r.Bottom),projection.PlanePoint(r.Right,r.Top),projection.PlanePoint(r.Left,r.Top)};
                var polygon=new List<Vec3>{g.WorldAt(0,0),g.WorldAt(1,0),g.WorldAt(1,1),g.WorldAt(0,1)};
                var center=projection.PlanePoint((r.Left+r.Right)*.5,(r.Bottom+r.Top)*.5);
                for(int i=0;i<4&&polygon.Count>0;i++){
                    var normal=(corners[i]-projection.Source).Cross(corners[(i+1)%4]-projection.Source).Normalized();
                    if(normal.Dot(center-projection.Source)<0)normal=normal*-1;
                    polygon=Clip(polygon,projection.Source,normal);
                }
                polygon=Clip(polygon,projection.Source+projection.Forward*.001,projection.Forward);
                if(polygon.Count<3)continue;
                var part=new StreamGeometry();using(var c=part.Open()){c.BeginFigure(new Point(g.U(polygon[0]),g.V(polygon[0])),true,true);c.PolyLineTo(polygon.Skip(1).Select(v=>new Point(g.U(v),g.V(v))).ToArray(),true,false);}group.Children.Add(part);
            }
            if(group.Children.Count==0)return null;
            var result=group.GetOutlinedPathGeometry(.00001,ToleranceType.Absolute);result.Freeze();return result;
        }
        static List<Vec3> Clip(List<Vec3> input,Vec3 source,Vec3 normal)
        {
            var output=new List<Vec3>();if(input.Count==0)return output;var previous=input[input.Count-1];double a=(previous-source).Dot(normal);
            foreach(var point in input){double b=(point-source).Dot(normal);if((a>=0)!=(b>=0))output.Add(previous+(point-previous)*(a/(a-b)));if(b>=0)output.Add(point);previous=point;a=b;}return output;
        }
        static void Label(DrawingContext dc,string text,Point p,Brush brush)=>dc.DrawText(new FormattedText(text,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),9,brush,1),p);
    }
}
