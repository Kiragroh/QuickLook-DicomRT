using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace QuickLook.DicomRT
{
    public sealed partial class MlcPlaybackControl : DockPanel, IDisposable
    {
        private readonly ComboBox beams = Theme.Combo(), speed = Theme.Combo(90);
        private readonly Slider cursor = new Slider { Minimum = 0, Margin = new Thickness(7, 2, 7, 0), ToolTip = "Entire plan · drag or click" };
        private readonly TextBlock details = Theme.Text("", 11);
        private readonly Button play = Theme.Button("▶ Play");
        private readonly DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
        private readonly MlcAperture aperture = new MlcAperture { MinHeight = 180, MinWidth = 160, Margin = new Thickness(0, 8, 0, 8) };
        private readonly LinacOrientationControl orientation = new LinacOrientationControl {Width=155,Height=160,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(8,8,12,12)};
        private readonly ComboBox layerView=Theme.Combo();
        private readonly Canvas markers = new Canvas { Height = 12, Margin = new Thickness(13,0,13,0) };
        private PlanBeam beam; private PlanData plan; private PlanBeam[] playbackBeams=new PlanBeam[0]; private int[] counts = new int[0]; private bool selecting;
        private readonly System.Collections.Generic.Dictionary<PlanBeam,double> extents=new System.Collections.Generic.Dictionary<PlanBeam,double>();
        private string bodyRegion;private bool planNoncoplanar;
        public void SetBodyRegion(string value){if(bodyRegion==value)return;bodyRegion=value;UpdateFrame();}
        public event Action<Vec3> IsocenterSelected;
        public event Action MprRequested;
        public double Position => cursor.Value;
        public void Navigate(PlanBeam selected,double local){Pause();int index=Array.IndexOf(playbackBeams,selected);if(index>=0)cursor.Value=counts.Take(index).Sum()+Math.Max(0,Math.Min(counts[index]-1,local));}
        public double LocalPosition {get {int index;double local;MlcTimeline.Locate(counts,cursor.Value,out index,out local);return local;}}
        public MlcPlaybackControl()
        {
            Background=Theme.Background;ViewerSnapshot.AttachMenu(this,null,"MLC");var top=new StackPanel();var settings=new StackPanel{Orientation=Orientation.Horizontal};var quad=ViewButtons.Create("MPR + 3D");quad.HorizontalAlignment=HorizontalAlignment.Right;quad.ToolTip="Open the linked 2 × 2 overview";quad.Click+=(s,e)=>MprRequested?.Invoke();settings.Children.Add(quad);beams.Width=190;beams.ToolTip="Select beam or arc";settings.Children.Add(beams);top.Children.Add(new ScrollViewer{Content=settings,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled});SetDock(top,Dock.Top);Children.Add(top);
            layerView.ItemsSource=new[]{"All layers"};layerView.SelectedIndex=0;layerView.SelectionChanged+=(s,e)=>{aperture.LayerIndex=layerView.SelectedIndex-1;};layerView.Width=100;layerView.ToolTip="Choose MLC layer";settings.Children.Add(layerView);
            var bottom=new StackPanel();bottom.Children.Add(details);
            var controls=new WrapPanel();controls.Children.Add(play);var restart=Theme.Button("↺");restart.ToolTip="Go to plan start";restart.Click+=(s,e)=>cursor.Value=0;controls.Children.Add(restart);
            controls.Children.Add(Theme.Text("CP/s",11,Theme.Muted));speed.ItemsSource=new[]{1,2,5,10,20};speed.SelectedItem=5;controls.Children.Add(speed);bottom.Children.Add(controls);
            bottom.Children.Add(Theme.Text("PLAN TIMELINE · markers indicate beam ends",10,Theme.Muted));bottom.Children.Add(cursor);bottom.Children.Add(markers);
            bottom.Children.Add(Theme.Text("Mouse wheel: 1 CP · Shift: 0.1 CP · MLC first · imaging last · preview, not delivery time",10,Theme.Muted));
            var jump=Theme.Button("Go to isocenter");jump.Click+=(s,e)=>{if(beam?.ControlPoints.Count>0){int bi;double local;MlcTimeline.Locate(counts,cursor.Value,out bi,out local);IsocenterSelected?.Invoke(beam.ControlPoints[(int)local].Isocenter);}};bottom.Children.Add(jump);
            SetDock(bottom,Dock.Bottom);Children.Add(bottom);var apertureArea=new Grid();apertureArea.Children.Add(aperture);apertureArea.Children.Add(orientation);Children.Add(apertureArea);InitializeAnatomy(settings,apertureArea);
            beams.SelectionChanged+=(s,e)=>{if(selecting)return;Pause();int index=beams.SelectedIndex;if(index>=0)cursor.Value=counts.Take(index).Sum();UpdateFrame();};
            cursor.ValueChanged+=(s,e)=>UpdateFrame();markers.SizeChanged+=(s,e)=>DrawMarkers();
            play.Click+=(s,e)=>{if(timer.IsEnabled)Pause();else if(cursor.Maximum>0){if(cursor.Value>=cursor.Maximum)cursor.Value=0;timer.Start();play.Content="Ⅱ Pause";}};
            timer.Tick+=(s,e)=>{cursor.Value=Math.Min(cursor.Maximum,cursor.Value+(int)(speed.SelectedItem??5)*timer.Interval.TotalSeconds);if(cursor.Value>=cursor.Maximum)Pause();};
            Unloaded+=(s,e)=>Pause();
        }
        public void SetPlan(PlanData value){Pause();SuspendProjection();projectionSuspended=!IsVisible;interpolated=null;aperture.Projection=null;fieldArrangement.Set(null,null,null,null,null);plan=value;extents.Clear();foreach(var item in value?.Beams??new System.Collections.Generic.List<PlanBeam>())extents[item]=BeamExtent(item);playbackBeams=MlcTimeline.PlaybackOrder(plan);planNoncoplanar=playbackBeams.SelectMany(b=>b.ControlPoints).Any(c=>!double.IsNaN(c.Couch)&&!double.IsInfinity(c.Couch)&&Math.Abs(Math.Sin(c.Couch*Math.PI/180))>.01);counts=playbackBeams.Select(b=>b.ControlPoints.Count).ToArray();selecting=true;beams.ItemsSource=playbackBeams;selecting=false;cursor.Maximum=Math.Max(0,counts.Sum()-1);cursor.Value=0;DrawMarkers();UpdateFrame();}
        private void DrawMarkers(){markers.Children.Clear();int offset=0;foreach(int count in counts){offset+=count;if(count==0)continue;var dot=new System.Windows.Shapes.Ellipse{Width=5,Height=5,Fill=Theme.Accent,ToolTip="Beam end · CP "+offset};Canvas.SetLeft(dot,Math.Max(0,markers.ActualWidth)*(offset-1)/Math.Max(1,cursor.Maximum)-2.5);Canvas.SetTop(dot,3);markers.Children.Add(dot);}}
        private void Pause(){bool playing=timer.IsEnabled;timer.Stop();play.Content="▶ Play";if(playing)RequestProjection(true);}
        public void Dispose(){Pause();SuspendProjection();}
        private void UpdateFrame()
        {
            int bi;double local;MlcTimeline.Locate(counts,cursor.Value,out bi,out local);
            if(plan==null||bi<0){beam=null;orientation.Set(double.NaN,double.NaN);orientation.SetCollimator(double.NaN);aperture.Set(null,null,0);details.Text="No control points";return;}
            beam=playbackBeams[bi];selecting=true;beams.SelectedIndex=bi;selecting=false;
            int i=(int)local,j=Math.Min(i+1,beam.ControlPoints.Count-1);double t=local-i;var a=beam.ControlPoints[i];var b=beam.ControlPoints[j];aperture.Set(a,b,t);aperture.Extent=extents[beam];
            var layerLabels=new[]{"All layers"}.Concat(a.MlcLayers.Select((l,k)=>"Layer "+(k+1)+" · "+l.Type)).ToArray();
            if(!layerView.Items.Cast<string>().SequenceEqual(layerLabels)){layerView.ItemsSource=layerLabels;layerView.SelectedIndex=0;}
            orientation.SetContext(beam,bodyRegion,planNoncoplanar);
            orientation.SetCollimator(MlcTimeline.Angle(a.Collimator,b.Collimator,t,a.CollimatorRotationDirection,false));
            orientation.Set(MlcTimeline.Angle(a.Gantry,b.Gantry,t,a.GantryRotationDirection,true),MlcTimeline.Angle(a.Couch,b.Couch,t,a.CouchRotationDirection,false));
            try{FrameAnatomy(MlcTimeline.Interpolate(a,b,t));}catch(ArgumentException){projectionStatus.Text="Incompatible beam geometry";aperture.Projection=null;}
            double weight=a.MetersetWeight+(b.MetersetWeight-a.MetersetWeight)*t;
            details.Text=$"Beam {beam.Number} · {bi+1}/{playbackBeams.Length} in preview · CP {local+1:0.0}/{beam.ControlPoints.Count} · Plan {cursor.Value+1:0.0}/{counts.Sum()}\nGantry {AngleText(MlcTimeline.Angle(a.Gantry,b.Gantry,t,a.GantryRotationDirection,true))} · Collimator {AngleText(MlcTimeline.Angle(a.Collimator,b.Collimator,t,a.CollimatorRotationDirection,false))}\nCouch {AngleText(MlcTimeline.Angle(a.Couch,b.Couch,t,a.CouchRotationDirection,false))} · Meterset {weight:0.0000}";
        }
        private static string AngleText(double angle)=>double.IsNaN(angle)?"n/a":angle.ToString("0.0")+"°";
    }
    internal sealed class MlcAperture : FrameworkElement
    {
        private ControlPoint first,second;private double fraction;private int layerIndex=-1;
        private MlcProjectionFrame projection;
        public MlcProjectionFrame Projection {get=>projection;set{projection=value;InvalidateVisual();}}
        double drrLeafOpacity=.75;
        public double DrrLeafOpacity {get=>drrLeafOpacity;set{drrLeafOpacity=Math.Max(.1,Math.Min(1,value));InvalidateVisual();}}
        public double Extent {get;set;}=100;
        public int LayerIndex {get=>layerIndex;set {layerIndex=value;InvalidateVisual();}}
        public void Set(ControlPoint a,ControlPoint b,double t){first=a;second=b;fraction=t;Extent=100;
            if(a!=null)foreach(var value in a.MlcLayers.Concat(b.MlcLayers).SelectMany(l=>l.Boundaries.Concat(l.Positions)).Concat(a.XJaws??new double[0]).Concat(a.YJaws??new double[0]).Concat(b.XJaws??new double[0]).Concat(b.YJaws??new double[0]))Extent=Math.Max(Extent,Math.Abs(value)+10);
            InvalidateVisual();}
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);dc.DrawRectangle(Theme.Background,null,new Rect(0,0,ActualWidth,ActualHeight));
            if(first==null){Label(dc,"No beam",8,8);return;}
            double[] xj,yj;MlcLayer[] layers;
            try{layers=MlcTimeline.Layers(first,second,fraction);xj=MlcTimeline.Positions(first.XJaws,second.XJaws,fraction);yj=MlcTimeline.Positions(first.YJaws,second.YJaws,fraction);}
            catch(ArgumentException){Label(dc,"Incompatible control points",5,8);return;}
            if(layers.Length==0&&xj.Length==0&&yj.Length==0){Label(dc,"No supported aperture data",5,8);return;}
            double extent=Extent;
            double scale=Math.Max(1,Math.Min(ActualWidth-24,ActualHeight-54))/(extent*2);double cx=ActualWidth/2,cy=ActualHeight/2+8;
            Func<double,double,Point> point=(x,y)=>new Point(cx+x*scale,cy-y*scale);
            Action<double,double,double,double,Brush> rect=(x1,y1,x2,y2,brush)=>dc.DrawRectangle(brush,null,new Rect(point(Math.Min(x1,x2),Math.Max(y1,y2)),point(Math.Max(x1,x2),Math.Min(y1,y2))));
            var imageRect=new Rect(point(-extent,extent),point(extent,-extent));
            rect(-extent,-extent,extent,extent,Theme.Brush("#172430"));
            if(projection?.Drr!=null)dc.DrawImage(projection.Drr,imageRect);
            for(int index=0;index<layers.Length;index++)
            {
                if(layerIndex>=0&&index!=layerIndex)continue;
                var layer=layers[index];var positions=layer.Positions;var bounds=layer.Boundaries;int n=bounds.Length-1;bool mlcY=layer.IsY;
                if(n<1||positions.Length!=n*2){Label(dc,"Missing layer geometry",5,8);return;}
                var leaf=projection?.Drr!=null?new SolidColorBrush(Color.FromArgb((byte)(255*DrrLeafOpacity),18,25,32)):Theme.Brush(index%2==0?"#BF416178":"#BF876C43");var edge=new Pen(Theme.Brush(index%2==0?"#A9C6D6":"#EAC28A"),.6);
                for(int k=0;k<n;k++)
                {
                    if(projection?.Drr!=null){var bankPen=new Pen(Theme.Brush(index%2==0?"#703599EF":"#70EAC28A"),.6);if(mlcY){dc.DrawLine(bankPen,point(bounds[k],-extent),point(bounds[k],positions[k]));dc.DrawLine(bankPen,point(bounds[k],positions[k+n]),point(bounds[k],extent));}else{dc.DrawLine(bankPen,point(-extent,bounds[k]),point(positions[k],bounds[k]));dc.DrawLine(bankPen,point(positions[k+n],bounds[k]),point(extent,bounds[k]));}}
                    if(mlcY){rect(bounds[k],-extent,bounds[k+1],positions[k],leaf);rect(bounds[k],positions[k+n],bounds[k+1],extent,leaf);}
                    else{rect(-extent,bounds[k],positions[k],bounds[k+1],leaf);rect(positions[k+n],bounds[k],extent,bounds[k+1],leaf);}
                    var a=mlcY?point(bounds[k],positions[k]):point(positions[k],bounds[k]);var b=mlcY?point(bounds[k+1],positions[k]):point(positions[k],bounds[k+1]);dc.DrawLine(edge,a,b);
                    a=mlcY?point(bounds[k],positions[k+n]):point(positions[k+n],bounds[k]);b=mlcY?point(bounds[k+1],positions[k+n]):point(positions[k+n],bounds[k+1]);dc.DrawLine(edge,a,b);
                }
                if(mlcY){rect(-extent,-extent,bounds[0],extent,leaf);rect(bounds[n],-extent,extent,extent,leaf);}
                else{rect(-extent,-extent,extent,bounds[0],leaf);rect(-extent,bounds[n],extent,extent,leaf);}
            }
            Label(dc,layers.Length==0?"Jaws":string.Join(" + ",layers.Where((l,k)=>layerIndex<0||layerIndex==k).Select(l=>l.Type+" · "+(l.Boundaries.Length-1)+" pairs")),6,4);
            Label(dc,"IEC beam limiting device plane · isocenter projection",6,18);
            var jaw=projection?.Drr!=null?new SolidColorBrush(Color.FromArgb((byte)(255*DrrLeafOpacity),20,25,30)):Theme.Brush("#D927313E");
            if(xj.Length==2){rect(-extent,-extent,xj[0],extent,jaw);rect(xj[1],-extent,extent,extent,jaw);}
            if(yj.Length==2){rect(-extent,-extent,extent,yj[0],jaw);rect(-extent,yj[1],extent,extent,jaw);}
            if(projection!=null){dc.PushClip(new RectangleGeometry(imageRect));dc.PushTransform(new MatrixTransform(imageRect.Width,0,0,imageRect.Height,imageRect.Left,imageRect.Top));foreach(var outline in projection.Outlines){dc.DrawGeometry(null,new Pen(Brushes.Black,3.5/imageRect.Width),outline.Boundary);dc.DrawGeometry(null,new Pen(new SolidColorBrush(Color.FromRgb(outline.Roi.Red,outline.Roi.Green,outline.Roi.Blue)),1.7/imageRect.Width),outline.Boundary);}dc.Pop();dc.Pop();}
            var cross=new Pen(Theme.Brush("#EE8068"),1);dc.DrawLine(cross,point(-6,0),point(6,0));dc.DrawLine(cross,point(0,-6),point(0,6));
            Label(dc,"+Y",cx+3,34);Label(dc,"+X",Math.Max(0,ActualWidth-25),cy+3);Label(dc,$"±{extent:0} mm",6,Math.Max(0,ActualHeight-18));
        }
        private void Label(DrawingContext dc,string text,double x,double y)
        {dc.DrawText(new FormattedText(text,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),10,Theme.Foreground,VisualTreeHelper.GetDpi(this).PixelsPerDip),new Point(x,y));}
    }
}
