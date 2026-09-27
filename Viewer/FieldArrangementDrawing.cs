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
        public static void Draw(DrawingContext dc,RenderScene scene,SliceGeometry g,Rect rect,Action invalidate=null,Rect? viewport=null)
        {
            if(!scene.ShowFields||scene.Plan==null||scene.PlanToImage==null)return;
            var bounds=viewport.HasValue?new Rect((viewport.Value.Left-rect.Left)/rect.Width,(viewport.Value.Top-rect.Top)/rect.Height,viewport.Value.Width/rect.Width,viewport.Value.Height/rect.Height):new Rect(0,0,1,1);
            var prepared=frames.GetValue(scene,_=>new Prepared());var origin=g.WorldAt(bounds.Left,bounds.Top);var end=g.WorldAt(bounds.Right,bounds.Bottom);
            if(prepared.Work==null||prepared.Point!=scene.ActiveControlPoint||prepared.Beam!=scene.ActiveBeam||prepared.Plan!=scene.Plan||prepared.Map!=scene.PlanToImage||(origin-prepared.Origin).Length>1e-8||(end-prepared.End).Length>1e-8)
            {
                prepared.Cancel?.Cancel();prepared.Cancel=new CancellationTokenSource();var token=prepared.Cancel.Token;
                prepared.Point=scene.ActiveControlPoint;prepared.Beam=scene.ActiveBeam;prepared.Plan=scene.Plan;prepared.Map=scene.PlanToImage;prepared.Origin=origin;prepared.End=end;
                var entries=scene.Plan.Beams.Where(b=>(!BeamMotion.IsImaging(b)||b==scene.ActiveBeam)&&!BeamMotion.IsArc(b)).Select(b=>Tuple.Create(b,b==scene.ActiveBeam?scene.ActiveControlPoint:b.ControlPoints.FirstOrDefault())).ToArray();var map=scene.PlanToImage;
                var dispatcher=Dispatcher.CurrentDispatcher;
                prepared.Work=Task.Run(()=>{var shapes=new Dictionary<PlanBeam,Geometry>();foreach(var entry in entries){token.ThrowIfCancellationRequested();string reason;var p=BeamProjection.Create(entry.Item1,entry.Item2,map,out reason);if(p!=null)shapes[entry.Item1]=Opening(p,entry.Item2,g,token,bounds);}return shapes;},token);
                prepared.Work.ContinueWith(t=>{if(t.IsFaulted){var ignored=t.Exception;}if(t.Status==TaskStatus.RanToCompletion&&!token.IsCancellationRequested&&invalidate!=null&&!dispatcher.HasShutdownStarted)dispatcher.BeginInvoke(invalidate,DispatcherPriority.Background);},TaskScheduler.Default);
            }
            var ready=prepared.Work.Status==TaskStatus.RanToCompletion?prepared.Work.Result:null;
            Func<Vec3,Point> screen=p=>new Point(rect.Left+g.U(p)*rect.Width,rect.Top+g.V(p)*rect.Height);
            int missing=0;
            foreach(var beam in scene.Plan.Beams.OrderBy(b=>b==scene.ActiveBeam?1:0))
            {
                if(BeamMotion.IsImaging(beam)&&beam!=scene.ActiveBeam)continue;
                bool active=beam==scene.ActiveBeam;var cp=active?scene.ActiveControlPoint:beam.ControlPoints.FirstOrDefault();
                string reason;var projection=BeamProjection.Create(beam,cp,scene.PlanToImage,out reason);if(projection==null){missing++;continue;}
                var color=active?Color.FromRgb(255,215,82):Color.FromArgb(125,170,177,187);var brush=new SolidColorBrush(color);var pen=new Pen(brush,active?1.7:.65);
                if(BeamMotion.IsArc(beam)){DrawArc(dc,beam,scene.PlanToImage,g,rect,brush,active,projection,BeamModulationMode.AngularMeterset,invalidate);continue;}
                Geometry shape=null;ready?.TryGetValue(beam,out shape);
                if(shape!=null){var display=shape.Clone();display.Transform=new MatrixTransform(rect.Width,0,0,rect.Height,rect.Left,rect.Top);dc.DrawGeometry(null,pen,display);}
                // One-way source arrow makes the incoming side unambiguous.
                var direction=projection.Forward-g.Normal*projection.Forward.Dot(g.Normal);
                if(direction.Length>.01){var from=screen(projection.Iso-direction.Normalized()*Math.Min(g.WidthMm,g.HeightMm)*.38);var to=screen(projection.Iso);Arrow(dc,from,to,brush,active?1.5:.7);Label(dc,"B"+beam.Number,from+new Vector(5,-14),brush);}
                else {var at=screen(projection.Iso);dc.DrawEllipse(null,pen,at,7,7);Label(dc,"B"+beam.Number+" perpendicular",at+new Vector(9,-10),brush);}
                if(active){var p=screen(projection.Iso);dc.DrawEllipse(null,new Pen(Brushes.Gold,1.5),p,4,4);}

            }
            if(missing>0)Label(dc,missing+" fields: geometry unavailable",new Point(rect.Left+5,rect.Bottom-15),Brushes.LightSlateGray);
        }
        sealed class Track {public string Key;public Task<BeamMotion.Sample[]> Work;}
        static readonly ConditionalWeakTable<PlanBeam,Track> tracks=new ConditionalWeakTable<PlanBeam,Track>();
        static void DrawArc(DrawingContext dc,PlanBeam beam,Matrix4 map,SliceGeometry g,Rect rect,Brush brush,bool active,BeamProjection current,BeamModulationMode mode,Action invalidate)
        {
            var track=tracks.GetValue(beam,_=>new Track());string key=string.Join(",",map.Values.Select(x=>x.ToString("R",CultureInfo.InvariantCulture)));
            if(track.Work==null||track.Key!=key){track.Key=key;var dispatcher=Dispatcher.CurrentDispatcher;track.Work=Task.Run(()=>BeamMotion.Path(beam,map));track.Work.ContinueWith(t=>{if(t.IsFaulted){var ignored=t.Exception;}if(invalidate!=null&&!dispatcher.HasShutdownStarted)dispatcher.BeginInvoke(invalidate,DispatcherPriority.Background);},TaskScheduler.Default);}
            if(track.Work.Status!=TaskStatus.RanToCompletion)return;
            var samples=track.Work.Result;if(samples.Length==0)return;
            double radius=Math.Min(g.WidthMm,g.HeightMm)*.36,maxRate=samples.Select(x=>BeamMotion.Value(x,mode)).Where(x=>BeamProjection.Finite(x)&&x>=0).DefaultIfEmpty(0).Max();
            Func<Vec3,Point> screen=p=>new Point(rect.Left+g.U(p)*rect.Width,rect.Top+g.V(p)*rect.Height);
            var pen=new Pen(brush,active?1.8:.8);Point? previous=null;
            foreach(var sample in samples){var at=screen(sample.Iso+sample.SourceDirection*radius);if(previous.HasValue&&!sample.Break)dc.DrawLine(pen,previous.Value,at);previous=at;
                if(sample.Tick){double value=BeamMotion.Value(sample,mode);bool known=BeamProjection.Finite(value)&&value>=0;double length=known&&maxRate>0?value/maxRate*radius*.16:radius*.025;var tickPen=new Pen(known?brush:Brushes.SlateGray,active?2:1);dc.DrawLine(tickPen,at,screen(sample.Iso+sample.SourceDirection*(radius+length)));}}
            var source=screen(current.Iso-current.Forward*radius);var iso=screen(current.Iso);
            if(active){dc.DrawEllipse(brush,null,source,4,4);Arrow(dc,source,iso,brush,1.4);dc.DrawEllipse(null,pen,iso,4,4);}
            var first=samples[0];if(active)Label(dc,"B"+beam.Number,screen(first.Iso+first.SourceDirection*(radius*1.2)),brush);
            if(active)Label(dc,(mode==BeamModulationMode.PlannedRate?"Planned rate setting":"Angular modulation")+(maxRate>0?" (max "+maxRate.ToString("0.##",CultureInfo.InvariantCulture)+" "+BeamMotion.Unit(beam,mode)+")":" unavailable"),new Point(rect.Left+5,rect.Top+5),brush);
        }
        static void Arrow(DrawingContext dc,Point from,Point to,Brush brush,double width)
        {
            var vector=to-from;if(vector.Length<2)return;vector.Normalize();var pen=new Pen(brush,width){DashStyle=DashStyles.Dash};dc.DrawLine(pen,from,to);
            var tip=from+(to-from)*.25;var side=new Vector(-vector.Y,vector.X);var solid=new Pen(brush,width);dc.DrawLine(solid,tip,tip-vector*8+side*4);dc.DrawLine(solid,tip,tip-vector*8-side*4);
        }
        // Merge the filled leaf ray intersections before stroking: no internal leaf seams.
        internal static Geometry Opening(BeamProjection projection,ControlPoint cp,SliceGeometry g,CancellationToken token=default(CancellationToken),Rect? viewport=null)
        {
            Geometry union=null;var bounds=viewport??new Rect(0,0,1,1);
            foreach(var r in BeamAperture.Rectangles(cp))
            {
                token.ThrowIfCancellationRequested();
                var corners=new[]{projection.PlanePoint(r.Left,r.Bottom),projection.PlanePoint(r.Right,r.Bottom),projection.PlanePoint(r.Right,r.Top),projection.PlanePoint(r.Left,r.Top)};
                var polygon=new List<Vec3>{g.WorldAt(bounds.Left,bounds.Top),g.WorldAt(bounds.Right,bounds.Top),g.WorldAt(bounds.Right,bounds.Bottom),g.WorldAt(bounds.Left,bounds.Bottom)};
                var center=projection.PlanePoint((r.Left+r.Right)*.5,(r.Bottom+r.Top)*.5);
                for(int i=0;i<4&&polygon.Count>0;i++){
                    var normal=(corners[i]-projection.Source).Cross(corners[(i+1)%4]-projection.Source).Normalized();
                    if(normal.Dot(center-projection.Source)<0)normal=normal*-1;
                    polygon=Clip(polygon,projection.Source,normal);
                }
                polygon=Clip(polygon,projection.Source+projection.Forward*.001,projection.Forward);
                if(polygon.Count<3)continue;
                var part=new StreamGeometry();using(var c=part.Open()){c.BeginFigure(new Point(g.U(polygon[0]),g.V(polygon[0])),true,true);c.PolyLineTo(polygon.Skip(1).Select(v=>new Point(g.U(v),g.V(v))).ToArray(),true,false);}union=union==null?(Geometry)part:Geometry.Combine(union,part,GeometryCombineMode.Union,null,.000001,ToleranceType.Absolute);
            }
            if(union==null)return null;
            var result=union.GetOutlinedPathGeometry(.000001,ToleranceType.Absolute);result.Freeze();return result;
        }
        static List<Vec3> Clip(List<Vec3> input,Vec3 source,Vec3 normal)
        {
            var output=new List<Vec3>();if(input.Count==0)return output;var previous=input[input.Count-1];double a=(previous-source).Dot(normal);
            foreach(var point in input){double b=(point-source).Dot(normal);if((a>=0)!=(b>=0))output.Add(previous+(point-previous)*(a/(a-b)));if(b>=0)output.Add(point);previous=point;a=b;}return output;
        }
        static void Label(DrawingContext dc,string text,Point p,Brush brush)=>dc.DrawText(new FormattedText(text,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),9,brush,1),p);
    }
}
