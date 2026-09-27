using System;
using System.Globalization;
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
   Action<double,double,double,double,Brush,Pen> rectangle=(x1,y1,x2,y2,fill,pen)=>{
    x1=Math.Max(-extent,x1);x2=Math.Min(extent,x2);y1=Math.Max(-extent,y1);y2=Math.Min(extent,y2);if(x2<=x1||y2<=y1)return;
    var vertices=new[]{world(x1,y1),world(x2,y1),world(x2,y2),world(x1,y2)};var screen=new Point[4];for(int i=0;i<4;i++)if(!Project(vertices[i],out screen[i]))return;
    var geometry=new StreamGeometry();using(var g=geometry.Open()){g.BeginFigure(screen[0],true,true);g.PolyLineTo(screen.Skip(1).ToArray(),true,false);}dc.DrawGeometry(fill,pen,geometry);
   };
   rectangle(-extent,-extent,extent,extent,new SolidColorBrush(Color.FromArgb(225,10,25,34)),null);
   for(int layer=0;layer<cp.MlcLayers.Count;layer++){
    var l=cp.MlcLayers[layer];int n=l.Boundaries.Length-1;if(n<1||l.Positions.Length!=2*n)continue;
    var fill=new SolidColorBrush(layer%2==0?Color.FromArgb(215,85,133,157):Color.FromArgb(170,178,143,84));
    var edge=new Pen(layer%2==0?Brushes.LightSteelBlue:Brushes.Wheat,.45);
    for(int k=0;k<n;k++){
     if(l.IsY){rectangle(l.Boundaries[k],-extent,l.Boundaries[k+1],l.Positions[k],fill,edge);rectangle(l.Boundaries[k],l.Positions[k+n],l.Boundaries[k+1],extent,fill,edge);}
     else{rectangle(-extent,l.Boundaries[k],l.Positions[k],l.Boundaries[k+1],fill,edge);rectangle(l.Positions[k+n],l.Boundaries[k],extent,l.Boundaries[k+1],fill,edge);}
    }
    if(l.IsY){rectangle(-extent,-extent,l.Boundaries[0],extent,fill,null);rectangle(l.Boundaries[n],-extent,extent,extent,fill,null);}
    else{rectangle(-extent,-extent,extent,l.Boundaries[0],fill,null);rectangle(-extent,l.Boundaries[n],extent,extent,fill,null);}
   }
   var jaws=new SolidColorBrush(Color.FromArgb(230,29,43,55));
   if(cp.XJaws?.Length==2){rectangle(-extent,-extent,cp.XJaws[0],extent,jaws,null);rectangle(cp.XJaws[1],-extent,extent,extent,jaws,null);}
   if(cp.YJaws?.Length==2){rectangle(-extent,-extent,extent,cp.YJaws[0],jaws,null);rectangle(-extent,cp.YJaws[1],extent,extent,jaws,null);}
   rectangle(-extent,-extent,extent,extent,null,new Pen(Brushes.Gold,1.2));
   Line(dc,world(-extent*.08,0),world(extent*.08,0),Brushes.OrangeRed,1);Line(dc,world(0,-extent*.08),world(0,extent*.08),Brushes.OrangeRed,1);
   Label(dc,"B"+scene.ActiveBeam.Number+" · CP "+(scene.ActiveControlPointIndex+1).ToString("0.0",CultureInfo.InvariantCulture),corners.OrderBy(v=>v.Y).First()+new Vector(3,-15),Brushes.Gold);
  }
  protected override void OnRender(DrawingContext dc)
  {
   base.OnRender(dc);MiniatureVisible=false;MiniatureAnchor=null;MiniatureCorners=new Vec3[0];if(!ShowFields||scene?.Plan==null||map==null)return;
   var tracks=Ready.Where(t=>!BeamMotion.IsImaging(t.Beam)||t.Beam==scene.ActiveBeam).ToArray();
   int row=0;foreach(var track in tracks.OrderBy(t=>t.Beam==scene.ActiveBeam?1:0)){
    bool active=track.Beam==scene.ActiveBeam;Brush brush=active?Brushes.Gold:new SolidColorBrush(Color.FromRgb(98,176,231));
    var samples=track.Samples;double max=samples.Select(s=>s.Angular).Where(v=>BeamProjection.Finite(v)&&v>=0).DefaultIfEmpty(0).Max();
    BeamMotion.Sample previous=null;
    foreach(var sample in samples){var at=sample.Iso+sample.SourceDirection*radius;if(previous!=null&&!sample.Break)Line(dc,previous.Iso+previous.SourceDirection*radius,at,brush,active?1.7:1);
     if(sample.Tick){double value=sample.Angular;bool known=BeamProjection.Finite(value)&&value>=0;double length=known&&max>0?value/max*radius*.18:radius*.015;Line(dc,at,sample.Iso+sample.SourceDirection*(radius+length),known?brush:Brushes.SlateGray,active?2:1.2);}previous=sample;
    }
    string reason;var cp=active?scene.ActiveControlPoint:track.Beam.ControlPoints.FirstOrDefault();var p=BeamProjection.Create(track.Beam,cp,map,out reason);
    if(p!=null){var source=p.Iso-p.Forward*radius;if(samples.Length==0||active)Arrow(dc,source,p.Iso,brush);Point at;var labelAt=samples.Length>0?samples[0].Iso+samples[0].SourceDirection*radius:source;if(Project(labelAt,out at))Label(dc,"B"+track.Beam.Number,at+new Vector(5,-13),brush);}
    if(samples.Length>0&&row<8){Label(dc,"B"+track.Beam.Number+": "+(max>0?"max "+max.ToString("0.####",CultureInfo.InvariantCulture)+" "+BeamMotion.Unit(track.Beam,BeamModulationMode.AngularMeterset):"modulation unavailable"),new Point(9,27+row*14),brush);row++;}
   }
   DrawActiveMiniature(dc);
   Label(dc,"Angular meterset modulation"+" · schematic tracks",new Point(9,8),Brushes.LightSteelBlue);
  }
 }
}
