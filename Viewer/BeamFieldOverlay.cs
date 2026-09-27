using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace QuickLook.DicomRT
{
 // Camera-projected orientation guide. Prepared paths are independent of ROI meshes.
 // Drawn through surfaces intentionally; track radius is schematic, not physical SAD.
 internal sealed class BeamFieldOverlay : FrameworkElement
 {
  internal sealed class Track {public PlanBeam Beam;public BeamMotion.Sample[] Samples;}
  internal Track[] Ready=new Track[0];
  PlanData plan;string mapKey;Matrix4 map;RenderScene scene;PerspectiveCamera camera;double radius;int generation;
  internal event Action PathsReady;
  internal bool ShowFields;
  PlanBeam miniatureBeam;ControlPoint miniaturePoint;double miniatureExtent;
  List<BeamAperture.Opening> miniatureOpenings=new List<BeamAperture.Opening>();
  Geometry blockMiniature;
  List<ApertureEdge> miniatureEdges=new List<ApertureEdge>();
  internal Vec3[] MiniatureCorners=new Vec3[0];
  internal Point? MiniatureAnchor;internal Rect MiniatureBounds;
  internal bool MiniatureVisible;
  internal Task Preparation {get;private set;}=Task.CompletedTask;
  internal BeamFieldOverlay(){IsHitTestVisible=false;ClipToBounds=true;}
  internal void Set(RenderScene value,PerspectiveCamera view,double sceneRadius)
  {
   scene=value;camera=view;radius=Math.Max(35,sceneRadius*.55);
   string next=value?.PlanToImage==null?null:string.Join(",",value.PlanToImage.Values.Select(x=>x.ToString("R",CultureInfo.InvariantCulture)));
   if(plan!=value?.Plan||mapKey!=next){plan=value?.Plan;mapKey=next;map=value?.PlanToImage;Ready=new Track[0];int mine=++generation;
    if(plan!=null&&map!=null){var beams=plan.Beams.ToArray();var transform=map;
     Preparation=Task.Run(()=>beams.Select(b=>new Track{Beam=b,Samples=BeamMotion.IsArc(b)?BeamMotion.Path(b,transform):new BeamMotion.Sample[0]}).ToArray()).ContinueWith(t=>{
      if(t.IsFaulted){var ignored=t.Exception;return;}if(t.Status==TaskStatus.RanToCompletion&&!Dispatcher.HasShutdownStarted)Dispatcher.BeginInvoke(new Action(()=>{if(mine==generation){Ready=t.Result;InvalidateVisual();PathsReady?.Invoke();}}),DispatcherPriority.Background);
     },TaskScheduler.Default);
    }
   }
   if(value?.ActiveBeam!=miniatureBeam||value?.ActiveControlPoint!=miniaturePoint){
    if(value?.ActiveBeam!=miniatureBeam)miniatureExtent=MiniatureExtent(value?.ActiveBeam);
    miniatureBeam=value?.ActiveBeam;miniaturePoint=value?.ActiveControlPoint;
    blockMiniature=miniaturePoint?.Blocks.Count>0?BlockApertureGeometry.Create(miniaturePoint):null;miniatureOpenings=BeamAperture.Rectangles(miniaturePoint);miniatureEdges=ApertureBoundary(miniatureOpenings);

   }
   InvalidateVisual();
  }
  static double MiniatureExtent(PlanBeam beam)
  {
   if(beam==null)return 100;
   // Stable beam-wide jaw framing makes small SRS apertures readable. Do not
   // let distant closed leaf ends determine the miniature's magnification.
   if(beam.ControlPoints.Count>0&&beam.ControlPoints.All(p=>p.XJaws?.Length==2&&p.YJaws?.Length==2&&p.XJaws.Concat(p.YJaws).All(BeamProjection.Finite)))
    return Math.Max(10,beam.ControlPoints.SelectMany(p=>p.XJaws.Concat(p.YJaws)).Max(x=>Math.Abs(x))*1.1);
   return MlcPlaybackControl.BeamExtent(beam);
  }
  internal System.Collections.Generic.IEnumerable<Vec3> Bounds()
  {
   foreach(var track in Ready.Where(t=>!BeamMotion.IsImaging(t.Beam)||t.Beam==scene?.ActiveBeam))foreach(var sample in track.Samples)yield return sample.Iso+sample.SourceDirection*radius*1.2;
   if(scene?.ActiveBeam!=null){string reason;var p=BeamProjection.Create(scene.ActiveBeam,scene.ActiveControlPoint,map,out reason);if(p!=null){yield return p.Iso;yield return p.Iso-p.Forward*radius*1.2;}}
  }
  bool Project(Vec3 p,out Point at)=>IsocenterOverlay.Project(p,camera,RenderSize,out at,true);
  void Line(DrawingContext dc,Vec3 a,Vec3 b,Brush color,double width,bool dashed=false)
  {Point x,y;if(!Project(a,out x)||!Project(b,out y))return;dc.DrawLine(new Pen(color,width){DashStyle=dashed?DashStyles.Dash:DashStyles.Solid},x,y);}
  void Arrow(DrawingContext dc,Vec3 from,Vec3 iso,Brush color)
  {
   Line(dc,from,iso,color,1.1,true);Point a,b;if(!Project(from,out a)||!Project(iso,out b))return;var v=b-a;if(v.Length<3)return;v.Normalize();var tip=a+(b-a)*.3;var side=new Vector(-v.Y,v.X);var pen=new Pen(color,1.5);dc.DrawLine(pen,tip,tip-v*8+side*4);dc.DrawLine(pen,tip,tip-v*8-side*4);
  }
  void Label(DrawingContext dc,string text,Point at,Brush color)=>dc.DrawText(new FormattedText(text,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),10,color,VisualTreeHelper.GetDpi(this).PixelsPerDip),at);
  internal struct ApertureEdge {public bool Vertical;public double Fixed,From,To;public int Sign;}
  // Exact boundary of disjoint leaf-opening rectangles. Signed interval events
  // cancel shared edges, including staggered/orthogonal MLC layers and jaw clipping.
  internal static List<ApertureEdge> ApertureBoundary(IEnumerable<BeamAperture.Opening> openings)
  {
   var vertical=new Dictionary<double,SortedDictionary<double,int>>();var horizontal=new Dictionary<double,SortedDictionary<double,int>>();
   Action<Dictionary<double,SortedDictionary<double,int>>,double,double,double,int> add=(lines,fixedAt,from,to,sign)=>{
    SortedDictionary<double,int> events;if(!lines.TryGetValue(fixedAt,out events))lines[fixedAt]=events=new SortedDictionary<double,int>();
    int value;events.TryGetValue(from,out value);events[from]=value+sign;events.TryGetValue(to,out value);events[to]=value-sign;
   };
   foreach(var r in openings){add(vertical,r.Left,r.Bottom,r.Top,-1);add(vertical,r.Right,r.Bottom,r.Top,1);add(horizontal,r.Bottom,r.Left,r.Right,-1);add(horizontal,r.Top,r.Left,r.Right,1);}
   var result=new List<ApertureEdge>();
   foreach(bool v in new[]{true,false})foreach(var line in v?vertical:horizontal){int sum=0;double begin=0;
    foreach(var e in line.Value){int next=sum+e.Value;if(next==sum)continue;if(sum!=0&&e.Key>begin)result.Add(new ApertureEdge{Vertical=v,Fixed=line.Key,From=begin,To=e.Key,Sign=Math.Sign(sum)});sum=next;begin=e.Key;}
   }
   return result;
  }
  void DrawActiveMiniature(DrawingContext dc)
  {
   if(scene.ActiveBeam==null||scene.ActiveControlPoint==null)return;
   string reason;var p=BeamProjection.Create(scene.ActiveBeam,scene.ActiveControlPoint,map,out reason);Point anchor;
   var cp=scene.ActiveControlPoint;if(p==null)return;
   var center=p.Iso-p.Forward*radius;double half=radius*.25,extent=miniatureExtent;
   Func<double,double,Vec3> world=(x,y)=>center+p.Right*(x/extent*half)+p.Up*(y/extent*half);
   if(!Project(center,out anchor))return;
   MiniatureCorners=new[]{world(-extent,-extent),world(extent,-extent),world(extent,extent),world(-extent,extent)};
   var corners=new Point[4];for(int i=0;i<4;i++)if(!Project(MiniatureCorners[i],out corners[i]))return;
   MiniatureAnchor=anchor;MiniatureBounds=new Rect(new Point(corners.Min(v=>v.X),corners.Min(v=>v.Y)),new Point(corners.Max(v=>v.X),corners.Max(v=>v.Y)));MiniatureVisible=true;
   // All jaws and MLC layers intersect the opening. Leave this union completely
   // unpainted; only a short bank-side fringe fades away from the real boundary.
   if(blockMiniature!=null){
    var projected=new StreamGeometry();using(var context=projected.Open())foreach(var figure in blockMiniature.GetFlattenedPathGeometry().Figures){var vertices=new List<Point>{figure.StartPoint};foreach(var segment in figure.Segments){if(segment is PolyLineSegment poly)vertices.AddRange(poly.Points);else if(segment is LineSegment line)vertices.Add(line.Point);}
      var display=new List<Point>();foreach(var v in vertices){Point at;if(Project(world(v.X,v.Y),out at))display.Add(at);}if(display.Count==vertices.Count&&display.Count>2){context.BeginFigure(display[0],true,true);context.PolyLineTo(display.Skip(1).ToArray(),true,false);}}
    dc.PushClip(new CombinedGeometry(GeometryCombineMode.Exclude,new RectangleGeometry(new Rect(RenderSize)),projected));
    for(int i=12;i>=1;i--)dc.DrawGeometry(null,new Pen(new SolidColorBrush(Color.FromArgb((byte)(8+3*(12-i)),113,167,191)),i*1.2),projected);dc.Pop();dc.DrawGeometry(null,new Pen(Brushes.Gold,1.2),projected);
    Label(dc,"B"+scene.ActiveBeam.Number+" · C "+cp.Collimator.ToString("0.#",CultureInfo.InvariantCulture)+"°",corners.OrderBy(v=>v.Y).First()+new Vector(3,-15),Brushes.Gold);return;
   }
   var aperture=new StreamGeometry();using(var g=aperture.Open())foreach(var r in miniatureOpenings){
    var vertices=new[]{world(r.Left,r.Bottom),world(r.Right,r.Bottom),world(r.Right,r.Top),world(r.Left,r.Top)};
    var screen=new Point[4];bool visible=true;for(int i=0;i<4;i++)if(!Project(vertices[i],out screen[i]))visible=false;
    if(visible){g.BeginFigure(screen[0],true,true);g.PolyLineTo(screen.Skip(1).ToArray(),true,false);}
   }
   aperture.Freeze();
   dc.PushClip(new CombinedGeometry(GeometryCombineMode.Exclude,new RectangleGeometry(new Rect(RenderSize)),aperture));
   double fade=extent*.18;const int steps=12;
   foreach(var edge in miniatureEdges)for(int i=0;i<steps;i++){
    double near=fade*i/steps,far=fade*(i+1)/steps;
    Func<double,double,Vec3> at=(along,outward)=>edge.Vertical?world(edge.Fixed+edge.Sign*outward,along):world(along,edge.Fixed+edge.Sign*outward);
    var vertices=new[]{at(edge.From,near),at(edge.To,near),at(edge.To,far),at(edge.From,far)};var screen=new Point[4];bool visible=true;
    for(int k=0;k<4;k++)if(!Project(vertices[k],out screen[k]))visible=false;if(!visible)continue;
    var ribbon=new StreamGeometry();using(var g=ribbon.Open()){g.BeginFigure(screen[0],true,true);g.PolyLineTo(screen.Skip(1).ToArray(),true,false);}
    byte alpha=(byte)(170*Math.Pow(1-(i+.5)/steps,2));dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(alpha,113,167,191)),null,ribbon);
   }
   dc.Pop();
   foreach(var edge in miniatureEdges){var a=edge.Vertical?world(edge.Fixed,edge.From):world(edge.From,edge.Fixed);var b=edge.Vertical?world(edge.Fixed,edge.To):world(edge.To,edge.Fixed);Line(dc,a,b,Brushes.Gold,1.2);}
   Label(dc,"B"+scene.ActiveBeam.Number+" · CP "+(scene.ActiveControlPointIndex+1).ToString("0.0",CultureInfo.InvariantCulture)+" · C "+cp.Collimator.ToString("0.#",CultureInfo.InvariantCulture)+"°",corners.OrderBy(v=>v.Y).First()+new Vector(3,-15),Brushes.Gold);
  }
  protected override void OnRender(DrawingContext dc)
  {
   base.OnRender(dc);MiniatureVisible=false;MiniatureAnchor=null;MiniatureCorners=new Vec3[0];if(!ShowFields||scene?.Plan==null||map==null)return;
   var tracks=Ready.Where(t=>!BeamMotion.IsImaging(t.Beam)||t.Beam==scene.ActiveBeam).ToArray();
   foreach(var track in tracks.OrderBy(t=>t.Beam==scene.ActiveBeam?1:0)){
    bool active=track.Beam==scene.ActiveBeam;Brush brush=active?Brushes.Gold:new SolidColorBrush(Color.FromRgb(98,176,231));
    var samples=track.Samples;double max=samples.Select(s=>s.Angular).Where(v=>BeamProjection.Finite(v)&&v>=0).DefaultIfEmpty(double.NaN).Max();
    ArcModulationDrawing.Draw(dc,samples,v=>{Point at;return Project(v,out at)?(Point?)at:null;},radius,max,active,BeamModulationMode.AngularMeterset);
    string reason;var cp=active?scene.ActiveControlPoint:track.Beam.ControlPoints.FirstOrDefault();var p=BeamProjection.Create(track.Beam,cp,map,out reason);
    if(p!=null){var source=p.Iso-p.Forward*radius;if(samples.Length==0||active)Arrow(dc,source,p.Iso,brush);Point at;var labelAt=samples.Length>0?samples[0].Iso+samples[0].SourceDirection*radius:source;if(Project(labelAt,out at))Label(dc,"B"+track.Beam.Number,at+new Vector(5,-13),brush);}
    if(samples.Length>0&&active)ArcModulationDrawing.Legend(dc,new Point(9,27),"B"+track.Beam.Number,max,BeamMotion.Unit(track.Beam,BeamModulationMode.AngularMeterset),true);
   }
   DrawActiveMiniature(dc);
   Label(dc,scene.ActiveBeam==null?"Select a field to inspect angular meterset":"Radial length = angular meterset · active field",new Point(9,8),Brushes.LightSteelBlue);
  }
 }
}
