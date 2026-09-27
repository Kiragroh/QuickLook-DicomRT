using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QuickLook.DicomRT
{
 public sealed class SlicePane : FrameworkElement, IDisposable
 {
  sealed class OverlayLines {public Pen Pen;public List<WorldLine> Lines;}
  sealed class Frame {public BitmapSource Bitmap;public SlicePixels Raster;public RenderScene Scene;public bool InterpolatedContours,ContourFallback;public List<OverlayLines> Lines=new List<OverlayLines>();}
  RenderScene scene; Frame frame; CancellationTokenSource pending; int generation; bool disposed; Point dragStart; double dragCenter,dragWidth; string status="Select an image";
  public RenderScene Scene {get=>scene;set{scene=value;if(picking&&value?.InteractionPreview==true){UpdateCrosshair(value.Focus);return;}Refresh();}}
  internal void CancelPending(){pending?.Cancel();}
  internal void UpdateFields(RenderScene value){if(scene!=null)CopyFields(scene,value);if(frame!=null)CopyFields(frame.Scene,value);InvalidateVisual();}
  static void CopyFields(RenderScene target,RenderScene value){target.Plan=value.Plan;target.PlanToImage=value.PlanToImage;target.ActiveBeam=value.ActiveBeam;target.ActiveControlPoint=value.ActiveControlPoint;target.ShowFields=value.ShowFields;target.ActiveControlPointIndex=value.ActiveControlPointIndex;}
  public event Action<bool> PickInteraction;
  readonly DrawingVisual crosshairVisual=new DrawingVisual();
  protected override int VisualChildrenCount=>1;
  protected override Visual GetVisualChild(int index){if(index!=0)throw new ArgumentOutOfRangeException(nameof(index));return crosshairVisual;}
  bool rightMoved;bool picking;Vec3? immediateFocus;SliceGeometry pickGeometry;Rect pickRect;
  public void UpdateCrosshair(Vec3 focus){immediateFocus=focus;DrawCrosshair();}
  public event Action<SlicePane,int> Scrolled; public event Action<SlicePane,Vec3> Picked;public event Action<double,double> WindowChanged;public event Action<double> ZoomChanged;
  public SlicePane(){AddVisualChild(crosshairVisual);Focusable=true;ClipToBounds=true;SizeChanged+=(s,e)=>Refresh();MouseWheel+=OnWheel;MouseLeftButtonDown+=OnPick;MouseLeftButtonUp+=(s,e)=>{if(!picking)return;PickAt(e.GetPosition(this));picking=false;ReleaseMouseCapture();PickInteraction?.Invoke(false);e.Handled=true;};LostMouseCapture+=(s,e)=>{if(picking){picking=false;PickInteraction?.Invoke(false);}};MouseRightButtonDown+=OnDragStart;MouseMove+=OnDrag;MouseRightButtonUp+=(s,e)=>{ReleaseMouseCapture();if(!rightMoved&&ContextMenu!=null){ContextMenu.PlacementTarget=this;ContextMenu.IsOpen=true;}e.Handled=true;};}
  public async void Refresh()
  {
   if(disposed)return;
   int mine=++generation;pending?.Cancel();var cancel=new CancellationTokenSource();pending=cancel;
   if(scene==null||(scene.Native==null&&scene.Volume==null)){frame=null;status="Select an image";InvalidateVisual();cancel.Dispose();if(pending==cancel)pending=null;return;}
   var copy=scene.Snapshot();var rect=ImageRect(SliceGeometry.Create(copy));int w=Math.Max(2,(int)Math.Ceiling(rect.Width)),h=Math.Max(2,(int)Math.Ceiling(rect.Height));if(copy.InteractionPreview){w=Math.Min(224,w);h=Math.Min(224,h);}
   // Keep pixels, overlays, geometry and crosshair together until the next complete frame is ready.
   // A genuinely different primary source must never display the previous series while loading.
   if(frame!=null&&!copy.SameImageSource(frame.Scene))frame=null;
   status=frame==null?"Rendering…":null;InvalidateVisual();
   try
   {
    var next=await Task.Run(()=>
    {
     var pixels=SliceRaster.Render(copy,w,h,cancel.Token);
     var bitmap=BitmapSource.Create(pixels.Width,pixels.Height,96,96,PixelFormats.Bgra32,null,pixels.Pixels,pixels.Width*4);bitmap.Freeze();
     var result=new Frame{Bitmap=bitmap,Raster=pixels,Scene=copy};
     var tolerance=copy.Plane=="Native"&&copy.Volume!=null?Math.Max(.001,copy.Volume.SpacingZ*.49):.01;
     foreach(var overlay in copy.InteractionPreview?new List<RoiOverlay>():copy.Structures)
     {
      cancel.Token.ThrowIfCancellationRequested();if(overlay?.Roi==null||!overlay.Roi.Visible)continue;
      var roi=overlay.Roi;var brush=new SolidColorBrush(Color.FromRgb(roi.Red,roi.Green,roi.Blue));brush.Freeze();var pen=new Pen(brush,1.3);pen.Freeze();
      ReformatContours.OutlineKind kind;
      var lines=ReformatContours.Outline(roi,overlay.RoiToImage,pixels.Geometry,tolerance,cancel.Token,out kind);
      result.InterpolatedContours|=kind==ReformatContours.OutlineKind.Interpolated;
      result.ContourFallback|=kind==ReformatContours.OutlineKind.IntersectionFallback;
      result.Lines.Add(new OverlayLines{Pen=pen,Lines=lines});
     }
     return result;
    },cancel.Token);
    if(disposed||mine!=generation)return;frame=next;if(!picking&&!copy.InteractionPreview)immediateFocus=null;status=copy.InteractionPreview?"Moving crosshair · contours on release":null;InvalidateVisual();
   }
   catch(OperationCanceledException){}
   catch(Exception){if(!disposed&&mine==generation){status=frame==null?"Image display unavailable":"Update failed; showing previous view";InvalidateVisual();}}
   finally {if(pending==cancel)pending=null;cancel.Dispose();}
  }
  Rect ImageRect(SliceGeometry g)
  {
   double aw=Math.Max(2,ActualWidth-32),ah=Math.Max(2,ActualHeight-56);double scale=Math.Min(aw/g.WidthMm,ah/g.HeightMm);
   double w=g.WidthMm*scale,h=g.HeightMm*scale;return new Rect((ActualWidth-w)/2,(ActualHeight-h)/2,w,h);
  }
  static Point Project(Vec3 p,SliceGeometry g,Rect rect)=>new Point(rect.Left+g.U(p)*rect.Width,rect.Top+g.V(p)*rect.Height);
  protected override void OnRender(DrawingContext dc)
  {
   base.OnRender(dc);dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(8,12,17)),null,new Rect(0,0,ActualWidth,ActualHeight));
   DrawCrosshair();
   if(frame==null){Text(dc,status??"Rendering…",new Point(12,12),Brushes.LightSlateGray);return;}
   var f=frame;var g=f.Raster.Geometry;var rect=ImageRect(g);
   dc.DrawImage(f.Bitmap,rect);dc.PushClip(new RectangleGeometry(rect));
   foreach(var item in f.Lines)foreach(var line in item.Lines)dc.DrawLine(item.Pen,Project(line.A,g,rect),Project(line.B,g,rect));
   var isoPens=new Dictionary<double,Pen>();foreach(var line in f.Raster.Isolines)
   {
    Pen pen;if(!isoPens.TryGetValue(line.DosePercent,out pen))
    {double red,green,blue;SliceRaster.IsodoseColor(f.Scene,line.DosePercent,out red,out green,out blue);pen=new Pen(new SolidColorBrush(Color.FromRgb((byte)red,(byte)green,(byte)blue)),.9);isoPens.Add(line.DosePercent,pen);}
    dc.DrawLine(pen,Project(line.A,g,rect),Project(line.B,g,rect));
   }
   foreach(var iso in f.Scene.Isocenters??new Vec3[0])
   {
    double distance=(iso-g.Center).Dot(g.Normal);var isoVolume=f.Scene.Volume;double isoStep=isoVolume==null?1:f.Scene.Plane=="Native"?isoVolume.SpacingZ:Math.Min(isoVolume.SpacingX,Math.Min(isoVolume.SpacingY,isoVolume.SpacingZ));double tolerance=Math.Max(.01,isoStep*.51);
    var point=Project(iso,g,rect);var brush=Math.Abs(distance)<=tolerance?Brushes.Gold:Brushes.DarkGoldenrod;
    var pen=new Pen(brush,1.7);if(Math.Abs(distance)>tolerance)pen.DashStyle=DashStyles.Dot;
    dc.DrawLine(new Pen(Brushes.Black,3.8),new Point(point.X-9,point.Y),new Point(point.X+9,point.Y));
    dc.DrawLine(new Pen(Brushes.Black,3.8),new Point(point.X,point.Y-9),new Point(point.X,point.Y+9));
    dc.DrawLine(pen,new Point(point.X-9,point.Y),new Point(point.X+9,point.Y));dc.DrawLine(pen,new Point(point.X,point.Y-9),new Point(point.X,point.Y+9));
    Text(dc,Math.Abs(distance)<=tolerance?"ISO":$"ISO {distance:+0.0;-0.0} mm",new Point(point.X+12,point.Y-8),brush,10);
   }
   FieldArrangementDrawing.Draw(dc,f.Scene,g,rect,InvalidateVisual);
   dc.Pop();
   Text(dc,f.Scene.Plane=="Native"?"Original plane":f.Scene.Plane=="Axial"?"Axial":f.Scene.Plane=="Coronal"?"Coronal":"Sagittal",new Point(10,7),Brushes.White);
   if(f.Scene.Entry?.HasGeometry!=false)
   {
    Text(dc,SliceGeometry.Direction(g.Right*-1),new Point(3,ActualHeight/2),Brushes.LightGray);
    Text(dc,SliceGeometry.Direction(g.Right),new Point(Math.Max(3,ActualWidth-25),ActualHeight/2),Brushes.LightGray);
    Text(dc,SliceGeometry.Direction(g.Down*-1),new Point(ActualWidth/2,7),Brushes.LightGray);
    Text(dc,SliceGeometry.Direction(g.Down),new Point(ActualWidth/2,Math.Max(7,ActualHeight-23)),Brushes.LightGray);
   }
   Text(dc,string.Format(CultureInfo.InvariantCulture,"Width {0:0}  Level {1:0}  ×{2:0.0}",f.Scene.WindowWidth,f.Scene.WindowCenter,f.Scene.Zoom),new Point(10,Math.Max(7,ActualHeight-23)),Brushes.LightGray);
   if(f.InterpolatedContours||f.ContourFallback)Text(dc,f.ContourFallback?"Contour intersections (unsupported stack)"+(f.InterpolatedContours?"; interpolated boundaries":""):"Interpolated contour-stack boundary",new Point(10,26),Brushes.LightSlateGray,10);
   if(f.Scene.Isodoses&&f.Scene.Doses.Any(d=>d.Dose.Visible&&d.Dose.Maximum>0))
   {
    var levels=SliceRaster.IsodoseLevels(f.Scene);int columns=levels.Length>10?2:1,rows=(levels.Length+columns-1)/columns;
    double legendWidth=columns==2?205:157,x=Math.Max(10,ActualWidth-legendWidth-107),y=58;
    dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(205,17,19,20)),null,new Rect(x-5,y-4,legendWidth,22+rows*16),4,4);
    Text(dc,f.Scene.AbsoluteIsodoses?"Isodoses · Gy":"% of each dose maximum",new Point(x,y),Brushes.LightGray,10);
    for(int i=0;i<levels.Length;i++)
    {
     double level=levels[i],lx=x+(i/rows)*100,ly=y+16+(i%rows)*16,red,green,blue;SliceRaster.IsodoseColor(f.Scene,level,out red,out green,out blue);
     dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb((byte)red,(byte)green,(byte)blue)),3),new Point(lx+2,ly+7),new Point(lx+22,ly+7));
     Text(dc,level.ToString("0.##",CultureInfo.InvariantCulture)+(f.Scene.AbsoluteIsodoses?" Gy":" %"),new Point(lx+30,ly),Brushes.LightGray,10);
    }
   }
   if(status!=null)Text(dc,status,new Point(10,55),Brushes.LightSlateGray,10);
  }
  void DrawCrosshair()
  {
   using(var dc=crosshairVisual.RenderOpen()){
    if(frame==null)return;var f=frame;var g=f.Raster.Geometry;var rect=ImageRect(g);dc.PushClip(new RectangleGeometry(rect));
   if(f.Scene.Crosshair&&f.Scene.Entry?.HasGeometry!=false)
   {
    var p=Project(immediateFocus??f.Scene.Focus,g,rect);var pen=new Pen(new SolidColorBrush(Color.FromArgb(155,100,181,246)),.8);
    dc.DrawLine(pen,new Point(rect.Left,p.Y),new Point(p.X-5,p.Y));dc.DrawLine(pen,new Point(p.X+5,p.Y),new Point(rect.Right,p.Y));
    dc.DrawLine(pen,new Point(p.X,rect.Top),new Point(p.X,p.Y-5));dc.DrawLine(pen,new Point(p.X,p.Y+5),new Point(p.X,rect.Bottom));
   }
    dc.Pop();
   }
  }
  void Text(DrawingContext dc,string text,Point p,Brush brush,double size=11)
  {dc.DrawText(new FormattedText(text??"",CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),size,brush,VisualTreeHelper.GetDpi(this).PixelsPerDip),p);}
  void OnWheel(object sender,MouseWheelEventArgs e){if(e.Delta==0)return;if((Keyboard.Modifiers&ModifierKeys.Control)!=0){ZoomChanged?.Invoke(e.Delta>0?1.15:1/1.15);e.Handled=true;return;}Scrolled?.Invoke(this,e.Delta>0?1:-1);e.Handled=true;}
  void OnPick(object sender,MouseButtonEventArgs e)
  {
   Focus();if(frame==null)return;var g=frame.Raster.Geometry;var r=ImageRect(g);var p=e.GetPosition(this);if(!r.Contains(p))return;
   picking=true;pickGeometry=g;pickRect=r;CaptureMouse();PickInteraction?.Invoke(true);PickAt(p);e.Handled=true;
  }
  void PickAt(Point p)
  {
   if(pickGeometry==null)return;double u=Math.Max(0,Math.Min(1,(p.X-pickRect.X)/pickRect.Width)),v=Math.Max(0,Math.Min(1,(p.Y-pickRect.Y)/pickRect.Height));
   var point=pickGeometry.WorldAt(u,v);UpdateCrosshair(point);Picked?.Invoke(this,point);
  }
  void OnDragStart(object sender,MouseButtonEventArgs e){if(scene==null||picking)return;rightMoved=false;dragStart=e.GetPosition(this);dragCenter=scene.WindowCenter;dragWidth=scene.WindowWidth;CaptureMouse();e.Handled=true;}
  void OnDrag(object sender,MouseEventArgs e)
  {
   if(picking&&IsMouseCaptured&&e.LeftButton==MouseButtonState.Pressed){PickAt(e.GetPosition(this));e.Handled=true;return;}
   if(!IsMouseCaptured||e.RightButton!=MouseButtonState.Pressed)return;var p=e.GetPosition(this);if(!rightMoved&&(p-dragStart).Length<3)return;rightMoved=true;double speed=Math.Max(1,dragWidth)/300;
   WindowChanged?.Invoke(dragCenter+(p.Y-dragStart.Y)*speed,Math.Max(1,dragWidth+(p.X-dragStart.X)*speed));e.Handled=true;
  }
  public void Dispose(){if(disposed)return;disposed=true;++generation;pending?.Cancel();pending=null;frame=null;scene=null;ReleaseMouseCapture();}
 }
}
