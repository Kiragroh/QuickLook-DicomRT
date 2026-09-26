using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace QuickLook.DicomRT
{
    public sealed class MlcPlaybackControl : DockPanel, IDisposable
    {
        private readonly ComboBox beams = Theme.Combo(), speed = Theme.Combo(90);
        private readonly Slider cursor = new Slider { Minimum = 0, Margin = new Thickness(7, 2, 7, 0), ToolTip = "Entire plan · drag or click" };
        private readonly TextBlock details = Theme.Text("", 11);
        private readonly Button play = Theme.Button("▶ Play");
        private readonly DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
        private readonly MlcAperture aperture = new MlcAperture { MinHeight = 180, MinWidth = 160, Margin = new Thickness(0, 8, 0, 8) };
        private readonly Canvas markers = new Canvas { Height = 12, Margin = new Thickness(13,0,13,0) };
        private PlanBeam beam; private PlanData plan; private int[] counts = new int[0]; private bool selecting;
        public event Action<Vec3> IsocenterSelected;
        public MlcPlaybackControl()
        {
            var top=new StackPanel(); top.Children.Add(Theme.Text("BEAM / ARC",11,Theme.Accent));top.Children.Add(beams);SetDock(top,Dock.Top);Children.Add(top);
            var bottom=new StackPanel();bottom.Children.Add(details);
            var controls=new WrapPanel();controls.Children.Add(play);var restart=Theme.Button("↺");restart.ToolTip="Go to plan start";restart.Click+=(s,e)=>cursor.Value=0;controls.Children.Add(restart);
            controls.Children.Add(Theme.Text("CP/s",11,Theme.Muted));speed.ItemsSource=new[]{1,2,5,10,20};speed.SelectedItem=5;controls.Children.Add(speed);bottom.Children.Add(controls);
            bottom.Children.Add(Theme.Text("PLAN TIMELINE · markers indicate beam ends",10,Theme.Muted));bottom.Children.Add(cursor);bottom.Children.Add(markers);
            bottom.Children.Add(Theme.Text("Interpolated control points · not actual delivery time",10,Theme.Muted));
            var jump=Theme.Button("Go to isocenter");jump.Click+=(s,e)=>{if(beam?.ControlPoints.Count>0){int bi;double local;MlcTimeline.Locate(counts,cursor.Value,out bi,out local);IsocenterSelected?.Invoke(beam.ControlPoints[(int)local].Isocenter);}};bottom.Children.Add(jump);
            SetDock(bottom,Dock.Bottom);Children.Add(bottom);Children.Add(aperture);
            beams.SelectionChanged+=(s,e)=>{if(selecting)return;Pause();int index=beams.SelectedIndex;if(index>=0)cursor.Value=counts.Take(index).Sum();UpdateFrame();};
            cursor.ValueChanged+=(s,e)=>UpdateFrame();markers.SizeChanged+=(s,e)=>DrawMarkers();
            play.Click+=(s,e)=>{if(timer.IsEnabled)Pause();else if(cursor.Maximum>0){if(cursor.Value>=cursor.Maximum)cursor.Value=0;timer.Start();play.Content="Ⅱ Pause";}};
            timer.Tick+=(s,e)=>{cursor.Value=Math.Min(cursor.Maximum,cursor.Value+(int)(speed.SelectedItem??5)*timer.Interval.TotalSeconds);if(cursor.Value>=cursor.Maximum)Pause();};
            Unloaded+=(s,e)=>Pause();
        }
        public void SetPlan(PlanData value){Pause();plan=value;counts=plan.Beams.Select(b=>b.ControlPoints.Count).ToArray();selecting=true;beams.ItemsSource=plan.Beams;selecting=false;cursor.Maximum=Math.Max(0,counts.Sum()-1);cursor.Value=0;DrawMarkers();UpdateFrame();}
        private void DrawMarkers(){markers.Children.Clear();int offset=0;foreach(int count in counts){offset+=count;if(count==0)continue;var dot=new System.Windows.Shapes.Ellipse{Width=5,Height=5,Fill=Theme.Accent,ToolTip="Beam end · CP "+offset};Canvas.SetLeft(dot,Math.Max(0,markers.ActualWidth)*(offset-1)/Math.Max(1,cursor.Maximum)-2.5);Canvas.SetTop(dot,3);markers.Children.Add(dot);}}
        private void Pause(){timer.Stop();play.Content="▶ Play";}
        public void Dispose(){Pause();}
        private void UpdateFrame()
        {
            int bi;double local;MlcTimeline.Locate(counts,cursor.Value,out bi,out local);
            if(plan==null||bi<0){beam=null;aperture.Set(null,null,0);details.Text="No control points";return;}
            beam=plan.Beams[bi];selecting=true;beams.SelectedIndex=bi;selecting=false;
            int i=(int)local,j=Math.Min(i+1,beam.ControlPoints.Count-1);double t=local-i;var a=beam.ControlPoints[i];var b=beam.ControlPoints[j];aperture.Set(a,b,t);
            double weight=a.MetersetWeight+(b.MetersetWeight-a.MetersetWeight)*t;
            details.Text=$"Beam {bi+1}/{plan.Beams.Count} · CP {local+1:0.0}/{beam.ControlPoints.Count} · Plan {cursor.Value+1:0.0}/{counts.Sum()}\nGantry {AngleText(MlcTimeline.Angle(a.Gantry,b.Gantry,t,a.GantryRotationDirection,true))} · Collimator {AngleText(MlcTimeline.Angle(a.Collimator,b.Collimator,t,a.CollimatorRotationDirection,false))}\nCouch {AngleText(MlcTimeline.Angle(a.Couch,b.Couch,t,a.CouchRotationDirection,false))} · Meterset {weight:0.0000}";
        }
        private static string AngleText(double angle)=>double.IsNaN(angle)?"n/a":angle.ToString("0.0")+"°";
    }
    internal sealed class MlcAperture : FrameworkElement
    {
        private ControlPoint first,second;private double fraction;
        public void Set(ControlPoint a,ControlPoint b,double t){first=a;second=b;fraction=t;InvalidateVisual();}
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);dc.DrawRectangle(Theme.Background,null,new Rect(0,0,ActualWidth,ActualHeight));
            if(first==null){Label(dc,"No beam",8,8);return;}
            double[] positions,bounds=first.MlcBoundaries??new double[0],xj,yj;
            try{positions=MlcTimeline.Positions(first.MlcPositions,second.MlcPositions,fraction);xj=MlcTimeline.Positions(first.XJaws,second.XJaws,fraction);yj=MlcTimeline.Positions(first.YJaws,second.YJaws,fraction);}
            catch(ArgumentException){Label(dc,"Incompatible control points",5,8);return;}
            bool mlcY=first.MlcType=="MLCY";int n=positions.Length/2;
            if(positions.Length==0&&xj.Length==0&&yj.Length==0){Label(dc,"No supported aperture data",5,8);return;}
            double extent=100;foreach(var value in bounds.Concat(positions).Concat(xj).Concat(yj))extent=Math.Max(extent,Math.Abs(value)+10);
            double scale=Math.Max(1,Math.Min(ActualWidth-24,ActualHeight-30))/(extent*2);double cx=ActualWidth/2,cy=ActualHeight/2;
            Func<double,double,Point> point=(x,y)=>new Point(cx+x*scale,cy-y*scale);
            Action<double,double,double,double,Brush> rect=(x1,y1,x2,y2,brush)=>dc.DrawRectangle(brush,null,new Rect(point(Math.Min(x1,x2),Math.Max(y1,y2)),point(Math.Max(x1,x2),Math.Min(y1,y2))));
            rect(-extent,-extent,extent,extent,Theme.Brush("#C2EADF"));
            var leaf=Theme.Brush("#416178");var leafEdge=new Pen(Theme.Brush("#A9C6D6"),.45);
            if(n>0&&positions.Length==2*n&&bounds.Length==n+1)
            {
                for(int k=0;k<n;k++)
                {
                    if(mlcY){rect(bounds[k],-extent,bounds[k+1],positions[k],leaf);rect(bounds[k],positions[k+n],bounds[k+1],extent,leaf);}
                    else{rect(-extent,bounds[k],positions[k],bounds[k+1],leaf);rect(positions[k+n],bounds[k],extent,bounds[k+1],leaf);}
                    var a=mlcY?point(bounds[k],-extent):point(-extent,bounds[k]);var b=mlcY?point(bounds[k],extent):point(extent,bounds[k]);dc.DrawLine(leafEdge,a,b);
                }
                if(mlcY){rect(-extent,-extent,bounds[0],extent,leaf);rect(bounds[n],-extent,extent,extent,leaf);}
                else{rect(-extent,-extent,extent,bounds[0],leaf);rect(-extent,bounds[n],extent,extent,leaf);}
                Label(dc,$"{first.MlcType} · {n} leaf pairs",6,4);
            }
            else Label(dc,"Jaws / no supported MLC",6,4);
            var jaw=Theme.Brush("#D927313E");
            if(xj.Length==2){rect(-extent,-extent,xj[0],extent,jaw);rect(xj[1],-extent,extent,extent,jaw);}
            if(yj.Length==2){rect(-extent,-extent,extent,yj[0],jaw);rect(-extent,yj[1],extent,extent,jaw);}
            var cross=new Pen(Theme.Brush("#EE8068"),1);dc.DrawLine(cross,point(-6,0),point(6,0));dc.DrawLine(cross,point(0,-6),point(0,6));
            Label(dc,"+Y",cx+3,18);Label(dc,"+X",Math.Max(0,ActualWidth-25),cy+3);Label(dc,$"±{extent:0} mm",6,Math.Max(0,ActualHeight-18));
        }
        private void Label(DrawingContext dc,string text,double x,double y)
        {dc.DrawText(new FormattedText(text,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),10,Theme.Foreground,VisualTreeHelper.GetDpi(this).PixelsPerDip),new Point(x,y));}
    }
}
