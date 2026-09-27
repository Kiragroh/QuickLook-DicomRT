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
  readonly MlcAperture miniature=new MlcAperture{Compact=true,Width=112,Height=112,MinWidth=0,MinHeight=0};
  readonly VisualBrush miniatureBrush;PlanBeam miniatureBeam;ControlPoint miniaturePoint;double miniatureExtent;
  internal Point? MiniatureAnchor;internal Rect MiniatureBounds;
  internal bool MiniatureVisible;
  internal Task Preparation {get;private set;}=Task.CompletedTask;
  internal BeamFieldOverlay(){IsHitTestVisible=false;ClipToBounds=true;miniatureBrush=new VisualBrush(miniature){ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(0,0,112,112),Stretch=Stretch.Fill};}
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
    miniature.Set(miniaturePoint,miniaturePoint,0);miniature.Extent=miniatureExtent;miniature.Measure(new Size(112,112));miniature.Arrange(new Rect(0,0,112,112));miniature.UpdateLayout();
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
   if(p==null||!Project(p.Iso-p.Forward*radius,out anchor))return;
   double width=124,height=143;if(ActualWidth<width+16||ActualHeight<height+16)return;
   var box=new Rect(Math.Max(8,Math.Min(ActualWidth-width-8,anchor.X+15)),Math.Max(8,Math.Min(ActualHeight-height-8,anchor.Y-height-12)),width,height);
   MiniatureAnchor=anchor;MiniatureBounds=box;MiniatureVisible=true;
   dc.DrawLine(new Pen(Brushes.Gold,1),anchor,new Point(Math.Max(box.Left,Math.Min(box.Right,anchor.X)),Math.Max(box.Top,Math.Min(box.Bottom,anchor.Y))));dc.DrawEllipse(Brushes.Gold,null,anchor,4,4);
   dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(240,13,20,27)),new Pen(Brushes.Gold,1),box,5,5);
   Label(dc,"B"+scene.ActiveBeam.Number+" · CP "+(scene.ActiveControlPointIndex+1).ToString("0.0",CultureInfo.InvariantCulture)+" · BEV",new Point(box.Left+6,box.Top+5),Brushes.Gold);
   dc.DrawRectangle(miniatureBrush,null,new Rect(box.Left+6,box.Top+23,112,112));
  }
  protected override void OnRender(DrawingContext dc)
  {
   base.OnRender(dc);MiniatureVisible=false;MiniatureAnchor=null;if(!ShowFields||scene?.Plan==null||map==null)return;
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
