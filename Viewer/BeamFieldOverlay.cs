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
  internal bool ShowFields;internal BeamModulationMode Mode;
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
   InvalidateVisual();
  }
  internal System.Collections.Generic.IEnumerable<Vec3> Bounds()=>Ready.Where(t=>!BeamMotion.IsImaging(t.Beam)||t.Beam==scene?.ActiveBeam).SelectMany(t=>t.Samples.Select(s=>s.Iso+s.SourceDirection*radius*1.2));
  bool Project(Vec3 p,out Point at)=>IsocenterOverlay.Project(p,camera,RenderSize,out at,true);
  void Line(DrawingContext dc,Vec3 a,Vec3 b,Brush color,double width,bool dashed=false)
  {Point x,y;if(!Project(a,out x)||!Project(b,out y))return;dc.DrawLine(new Pen(color,width){DashStyle=dashed?DashStyles.Dash:DashStyles.Solid},x,y);}
  void Arrow(DrawingContext dc,Vec3 from,Vec3 iso,Brush color)
  {
   Line(dc,from,iso,color,1.1,true);Point a,b;if(!Project(from,out a)||!Project(iso,out b))return;var v=b-a;if(v.Length<3)return;v.Normalize();var tip=a+(b-a)*.3;var side=new Vector(-v.Y,v.X);var pen=new Pen(color,1.5);dc.DrawLine(pen,tip,tip-v*8+side*4);dc.DrawLine(pen,tip,tip-v*8-side*4);
  }
  void Label(DrawingContext dc,string text,Point at,Brush color)=>dc.DrawText(new FormattedText(text,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),10,color,VisualTreeHelper.GetDpi(this).PixelsPerDip),at);
  protected override void OnRender(DrawingContext dc)
  {
   base.OnRender(dc);if(!ShowFields||scene?.Plan==null||map==null)return;
   var tracks=Ready.Where(t=>!BeamMotion.IsImaging(t.Beam)||t.Beam==scene.ActiveBeam).ToArray();
   int row=0;foreach(var track in tracks.OrderBy(t=>t.Beam==scene.ActiveBeam?1:0)){
    bool active=track.Beam==scene.ActiveBeam;Brush brush=active?Brushes.Gold:new SolidColorBrush(Color.FromRgb(98,176,231));
    var samples=track.Samples;double max=samples.Select(s=>BeamMotion.Value(s,Mode)).Where(v=>BeamProjection.Finite(v)&&v>=0).DefaultIfEmpty(0).Max();
    BeamMotion.Sample previous=null;
    foreach(var sample in samples){var at=sample.Iso+sample.SourceDirection*radius;if(previous!=null&&!sample.Break)Line(dc,previous.Iso+previous.SourceDirection*radius,at,brush,active?1.7:1);
     if(sample.Tick){double value=BeamMotion.Value(sample,Mode);bool known=BeamProjection.Finite(value)&&value>=0;double length=known&&max>0?value/max*radius*.18:radius*.015;Line(dc,at,sample.Iso+sample.SourceDirection*(radius+length),known?brush:Brushes.SlateGray,active?2:1.2);}previous=sample;
    }
    string reason;var cp=active?scene.ActiveControlPoint:track.Beam.ControlPoints.FirstOrDefault();var p=BeamProjection.Create(track.Beam,cp,map,out reason);
    if(p!=null){var source=p.Iso-p.Forward*radius;if(samples.Length==0||active)Arrow(dc,source,p.Iso,brush);Point at;var labelAt=samples.Length>0?samples[0].Iso+samples[0].SourceDirection*radius:source;if(Project(labelAt,out at))Label(dc,"B"+track.Beam.Number,at+new Vector(5,-13),brush);}
    if(samples.Length>0&&row<8){Label(dc,"B"+track.Beam.Number+": "+(max>0?"max "+max.ToString("0.####",CultureInfo.InvariantCulture)+" "+BeamMotion.Unit(track.Beam,Mode):"modulation unavailable"),new Point(9,27+row*14),brush);row++;}
   }
   Label(dc,(Mode==BeamModulationMode.PlannedRate?"Planned rate setting":"Angular meterset modulation")+" · schematic tracks",new Point(9,8),Brushes.LightSteelBlue);
  }
 }
}
