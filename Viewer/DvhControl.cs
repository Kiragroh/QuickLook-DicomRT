using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace QuickLook.DicomRT
{
    /// <summary>Call SetData only when opening DVH view. It cancels stale work and computes off the UI thread.</summary>
    public sealed partial class DvhControl : UserControl, IDisposable
    {
        readonly TextBlock status=Theme.Text("Select structures and an RTDOSE.",11,Theme.Muted);
        readonly StackPanel legend=new StackPanel();
        readonly DvhPlot plot=new DvhPlot();
        readonly Grid body=new Grid {Margin=new Thickness(0,14,0,10)};
        readonly ScrollViewer legendScroll=new ScrollViewer {VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        CancellationTokenSource calculation;
        bool disposed,embedded;
        StackPanel titleHeader;TextBlock calculationNote;
        public void SetCompact(bool value){if(embedded==value)return;embedded=value;titleHeader.Visibility=value?Visibility.Collapsed:Visibility.Visible;calculationNote.Visibility=value?Visibility.Collapsed:Visibility.Visible;((Grid)Content).Margin=new Thickness(value?4:16);body.Margin=new Thickness(0,value?2:14,0,value?2:10);plot.MinHeight=value?100:160;UpdateLayoutMode();}
        public void SetPrescriptions(double[] values){plot.Prescriptions=(values??new double[0]).Where(v=>!double.IsNaN(v)&&!double.IsInfinity(v)&&v>0).Distinct().OrderBy(v=>v).ToArray();plot.InvalidateVisual();}
        int requestedCount;
        DoseGrid cachedDose;
        readonly Dictionary<StructureRoi,CachedResult> cache=new Dictionary<StructureRoi,CachedResult>();
        readonly Dictionary<StructureRoi,bool> visibility=new Dictionary<StructureRoi,bool>();
        readonly Dictionary<StructureRoi,Button> legendNames=new Dictionary<StructureRoi,Button>();
        readonly Dictionary<StructureRoi,CheckBox> legendChecks=new Dictionary<StructureRoi,CheckBox>();
        public void RefreshLabels(){foreach(var item in legendNames){item.Value.Content=RoiLabels.Content(item.Key);item.Value.ToolTip=RoiLabels.Details(item.Key);}foreach(var item in legendChecks)System.Windows.Automation.AutomationProperties.SetName(item.Value,"Show "+item.Key.DisplayName);plot.HideHover();}
        readonly Dictionary<StructureRoi,Border> legendRows=new Dictionary<StructureRoi,Border>();
        public StructureRoi FocusedStructure => plot.FocusedStructure;
        public int CalculationCount { get; private set; }
        sealed class CachedResult { public double[] Transform;public DvhResult Result; }
        public void FocusStructure(StructureRoi roi)
        {
            if(disposed)return;
            plot.FocusedStructure=ReferenceEquals(plot.FocusedStructure,roi)?null:roi;
            foreach(var item in legendRows)item.Value.Background=ReferenceEquals(item.Key,plot.FocusedStructure)?Theme.Panel:Brushes.Transparent;
            Border selected;if(plot.FocusedStructure!=null&&legendRows.TryGetValue(plot.FocusedStructure,out selected))selected.BringIntoView();
            plot.InvalidateVisual();
        }
        public string StatusText => status.Text;
        public Task Completion { get; private set; } = Task.CompletedTask;
        public DvhControl()
        {
            var root=new Grid { Background=Theme.Background, Margin=new Thickness(16) };
            root.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
            root.RowDefinitions.Add(new RowDefinition {Height=new GridLength(1,GridUnitType.Star)});
            root.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
            var header=titleHeader=new StackPanel();header.Children.Add(Theme.Text("Dose–volume histogram",23));
            header.Children.Add(Theme.Text("CUMULATIVE  ·  VOLUME IN %  ·  APPROXIMATE PREVIEW",10,Theme.Accent));root.Children.Add(header);
            body.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(1,GridUnitType.Star)});
            body.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(260)});
            body.RowDefinitions.Add(new RowDefinition {Height=new GridLength(1,GridUnitType.Star)});
            body.RowDefinitions.Add(new RowDefinition {Height=new GridLength(0)});
            plot.MinHeight=160;plot.CurveSelected+=FocusStructure;body.Children.Add(plot);
            legendScroll.Content=legend;legendScroll.Margin=new Thickness(18,0,0,0);Grid.SetColumn(legendScroll,1);body.Children.Add(legendScroll);
            Grid.SetRow(body,1);root.Children.Add(body);
            var footer=new StackPanel();footer.Children.Add(status);
            calculationNote=Theme.Text("Slab volumes estimated from contour spacing; DVH may differ from the TPS. Uncovered dose remains unknown. Percentages refer to the entire estimated structure volume.",10,Theme.Muted);footer.Children.Add(calculationNote);
            Grid.SetRow(footer,2);root.Children.Add(footer);Content=root;
            // The viewer cancels on workspace exit and Dispose. Reparenting between quad
            // and standalone also raises Unloaded and must not cancel the new calculation.
            SizeChanged+=(s,e)=>UpdateLayoutMode();InitializeExport();
        }
        void UpdateLayoutMode()
        {
            bool narrow=ActualWidth<650;
            body.ColumnDefinitions[1].Width=new GridLength(narrow?0:260);
            body.RowDefinitions[1].Height=new GridLength(narrow?(embedded?90:150):0);
            Grid.SetColumn(legendScroll,narrow?0:1);Grid.SetRow(legendScroll,narrow?1:0);
            legendScroll.Margin=narrow?new Thickness(0,12,0,0):new Thickness(18,0,0,0);
        }
        public void SetData(IReadOnlyList<StructureRoi> rois,DoseGrid dose,Func<StructureRoi,Matrix4> transform)
        {
            if(disposed)return;Cancel();
            if(!ReferenceEquals(cachedDose,dose)){cache.Clear();visibility.Clear();cachedDose=dose;plot.FocusedStructure=null;}
            legend.Children.Clear();legendRows.Clear();legendNames.Clear();legendChecks.Clear();plot.HideHover();plot.Curves.Clear();plot.Units=dose?.Units??"";plot.InvalidateVisual();
            exportRois=(rois??new StructureRoi[0]).ToArray();requestedCount=rois?.Count??0;Completion=Task.CompletedTask;
            if(dose==null || rois==null || rois.Count==0){status.Text="No visible structures with a matching RTDOSE are available.";return;}
            // Snapshot mapping on the UI thread. Loaded contour/dose data are immutable.
            var selected=rois.Take(128).Select(r=>new Work {Roi=r,Transform=transform?.Invoke(r)}).ToArray();
            calculation=new CancellationTokenSource();calculation.CancelAfter(TimeSpan.FromSeconds(30));
            status.Text=$"Calculating DVH · {selected.Length} structures …";
            Completion=CalculateAsync(selected,dose,calculation,rois.Count);
        }
        sealed class Work { public StructureRoi Roi; public Matrix4 Transform; }
        async Task CalculateAsync(Work[] rois,DoseGrid dose,CancellationTokenSource owner,int totalRequested)
        {
            int complete=0,partial=0,unsupported=0;
            try
            {
                foreach(var work in rois)
                {
                    var matrix=work.Transform?.Values;
                    CachedResult saved;DvhResult result;
                    if(cache.TryGetValue(work.Roi,out saved) && (matrix==null?saved.Transform==null:saved.Transform!=null&&matrix.SequenceEqual(saved.Transform)))result=saved.Result;
                    else
                    {
                        CalculationCount++;
                        result=await Task.Run(()=>DvhCalculator.Calculate(work.Roi,dose,work.Transform,owner.Token),owner.Token);
                    }
                    if(disposed || owner!=calculation)return;
                    owner.Token.ThrowIfCancellationRequested();
                    cache[work.Roi]=new CachedResult {Transform=matrix,Result=result};
                    if(result.Status==DvhStatus.Complete)complete++;else if(result.Status==DvhStatus.PartialCoverage)partial++;else unsupported++;
                    var brush=new SolidColorBrush(Color.FromRgb(work.Roi.Red,work.Roi.Green,work.Roi.Blue));brush.Freeze();
                    bool shown; if(!visibility.TryGetValue(work.Roi,out shown))shown=true;
                    var curve=new DvhPlot.Curve {Roi=work.Roi,Result=result,Color=brush,Visible=shown};plot.Curves.Add(curve);
                    var row=new Border {Background=Brushes.Transparent,Padding=new Thickness(0,2,0,3)};
                    legendRows[work.Roi]=row;if(ReferenceEquals(work.Roi,plot.FocusedStructure))row.Background=Theme.Panel;
                    var contents=new StackPanel();row.Child=contents;
                    var heading=new DockPanel();contents.Children.Add(heading);
                    var box=new CheckBox {IsChecked=shown,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,7,0),IsEnabled=result.DoseValues.Length>0,ToolTip="Show or hide curve"};
                    System.Windows.Automation.AutomationProperties.SetName(box,"Show "+work.Roi.DisplayName);
                    DockPanel.SetDock(box,Dock.Left);heading.Children.Add(box);
                    var info=Theme.Button("i");info.Padding=new Thickness(5,0,5,0);info.Margin=new Thickness(3,0,0,0);info.ToolTip=RoiLabels.Details(work.Roi)+"\n\n"+MetricDetails(result);ToolTipService.SetShowDuration(info,60000);DockPanel.SetDock(info,Dock.Right);heading.Children.Add(info);
                    var name=new Button {Content=RoiLabels.Content(work.Roi),Foreground=brush,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(0,2,0,2),HorizontalContentAlignment=HorizontalAlignment.Left,FontWeight=FontWeights.SemiBold,ToolTip=RoiLabels.Details(work.Roi)};
                    legendNames[work.Roi]=name;legendChecks[work.Roi]=box;
                    name.Click+=(s,e)=>{FocusStructure(work.Roi);e.Handled=true;};heading.Children.Add(name);
                    box.Checked+=(s,e)=>{visibility[work.Roi]=curve.Visible=true;plot.InvalidateVisual();};
                    box.Unchecked+=(s,e)=>{visibility[work.Roi]=curve.Visible=false;plot.InvalidateVisual();};
                    row.MouseLeftButtonUp+=(s,e)=>
                    {
                        for(var source=e.OriginalSource as DependencyObject;source!=null&&source!=row;source=VisualTreeHelper.GetParent(source))
                            if(source is CheckBox || source is Button)return;
                        FocusStructure(work.Roi);e.Handled=true;
                    };
                    string metrics=result.EstimatedVolumeCc>0?$"{result.EstimatedVolumeCc:0.##} cm³":"No curve";
                    if(result.Status!=DvhStatus.Complete)metrics+=" · "+(result.Status==DvhStatus.PartialCoverage?"partial coverage":"unavailable");
                    contents.Children.Add(Theme.Text(metrics,10,result.Status==DvhStatus.Complete?Theme.Muted:Theme.Accent));
                    legend.Children.Add(row);
                    legend.Children.Add(new Border {Height=1,Background=Theme.Panel,Margin=new Thickness(0,0,0,5)});
                    plot.InvalidateVisual();status.Text=$"{plot.Curves.Count} / {rois.Length} structures calculated …";
                }
                status.Text=$"{complete} complete · {partial} with partial coverage · {unsupported} without a curve"+(totalRequested>rois.Length?$" · {totalRequested-rois.Length} not processed (limit: 128 structures).":"");
            }
            catch(OperationCanceledException){if(!disposed&&owner==calculation)status.Text=$"Time limit reached · {plot.Curves.Count} / {totalRequested} processed · {Math.Max(0,totalRequested-plot.Curves.Count)} not processed. Completed curves remain visible.";}
            catch(Exception){if(!disposed&&owner==calculation)status.Text="Unable to calculate the DVH. Check geometry and dose associations.";}
            finally { if(owner==calculation)calculation=null;owner.Dispose(); }
        }
        public void Cancel(){var active=calculation;if(active!=null){calculation=null;active.Cancel();if(!disposed)status.Text=$"Calculation canceled · {Math.Max(0,requestedCount-plot.Curves.Count)} not processed. Completed curves remain visible.";}}
        public void Dispose(){disposed=true;Cancel();plot.HideHover();cache.Clear();visibility.Clear();legendRows.Clear();legendNames.Clear();legendChecks.Clear();}

        sealed partial class DvhPlot : FrameworkElement
        {
            public sealed class Curve { public StructureRoi Roi;public DvhResult Result;public Brush Color;public bool Visible; }
            public StructureRoi FocusedStructure;
            public readonly List<Curve> Curves=new List<Curve>();
            public string Units;
            public double[] Prescriptions=new double[0];
            bool IsGy=>string.Equals(Units,"GY",StringComparison.OrdinalIgnoreCase);
            double AxisMaximum(Curve[] usable)
            {
                double max=usable.Length==0?1:usable.Max(c=>c.Result.DoseValues.Last());if((double.IsNaN(max)||double.IsInfinity(max))||max<=0)max=1;
                if(IsGy){if(Prescriptions.Length>0)max=Math.Max(max,Prescriptions.Max());max=Math.Max(10,Math.Ceiling(max/10)*10);}
                return max;
            }
            double[] DoseTicks(double max)=>IsGy?Enumerable.Range(0,(int)Math.Min(10000,Math.Floor(max/5))+1).Select(i=>i*5d).ToArray():Enumerable.Range(0,6).Select(i=>max*i/5).ToArray();
            protected override void OnRender(DrawingContext dc)
            {
                base.OnRender(dc);double w=ActualWidth,h=ActualHeight;if(w<140||h<140)return;
                var bounds=new Rect(52,22,Math.Max(1,w-70),Math.Max(1,h-74));
                dc.DrawRoundedRectangle(Theme.Panel,null,new Rect(0,0,w,h),10,10);
                var usable=Curves.Where(c=>c.Visible&&c.Result.DoseValues.Length>0).ToArray();
                double max=AxisMaximum(usable);
                var gridPen=new Pen(new SolidColorBrush(Color.FromArgb(35,180,200,225)),1);
                for(int i=0;i<=5;i++)
                {
                    double yy=bounds.Bottom-bounds.Height*i/5;dc.DrawLine(gridPen,new Point(bounds.Left,yy),new Point(bounds.Right,yy));
                    Text(dc,(i*20).ToString(CultureInfo.InvariantCulture),new Point(12,yy-7),Theme.Muted,10);
                }
                var minorPen=new Pen(new SolidColorBrush(Color.FromArgb(18,180,200,225)),1);
                foreach(double tick in DoseTicks(max))
                {
                    bool major=!IsGy||Math.Abs(tick%10)<1e-8;double xx=bounds.Left+bounds.Width*tick/max;
                    dc.DrawLine(major?gridPen:minorPen,new Point(xx,bounds.Top),new Point(xx,bounds.Bottom));
                    dc.DrawLine(new Pen(Theme.Muted,1),new Point(xx,bounds.Bottom),new Point(xx,bounds.Bottom+(major?6:3)));
                    if(major)Text(dc,tick.ToString("0.##",CultureInfo.CurrentCulture),new Point(xx-10,bounds.Bottom+9),Theme.Muted,10);
                }
                Text(dc,"Volume (%)",new Point(12,3),Theme.Muted,10);
                Text(dc,Units=="GY"?"Dose (Gy)":Units=="RELATIVE"?"Relative dose (DICOM values)":"Dose",new Point(bounds.Left+bounds.Width*.4,h-22),Theme.Muted,11);
                dc.PushClip(new RectangleGeometry(bounds));
                bool focused=usable.Any(c=>ReferenceEquals(c.Roi,FocusedStructure));
                // Draw the emphasized curve last so overlapping faint curves cannot obscure it.
                foreach(var curve in usable.OrderBy(c=>focused&&ReferenceEquals(c.Roi,FocusedStructure)?1:0))
                {
                    var r=curve.Result;var geometry=new StreamGeometry();
                    using(var context=geometry.Open())
                    {
                        context.BeginFigure(new Point(bounds.Left+r.DoseValues[0]/max*bounds.Width,bounds.Bottom-r.CumulativeVolumePercent[0]/100*bounds.Height),false,false);
                        for(int i=1;i<r.DoseValues.Length;i++)
                        {
                            double xx=bounds.Left+r.DoseValues[i]/max*bounds.Width;
                            context.LineTo(new Point(xx,bounds.Bottom-r.CumulativeVolumePercent[i]/100*bounds.Height),true,false);
                        }
                    }
                    geometry.Freeze();bool emphasized=focused&&ReferenceEquals(curve.Roi,FocusedStructure);
                    var pen=new Pen(curve.Color,emphasized?3.5:focused?1.5:2) {LineJoin=PenLineJoin.Round};
                    if(r.Status==DvhStatus.PartialCoverage)pen.DashStyle=DashStyles.Dash;
                    dc.PushOpacity(focused&&!emphasized?.18:1);dc.DrawGeometry(null,pen,geometry);dc.Pop();
                }
                if(IsGy)
                {
                    var rxPen=new Pen(Theme.Foreground,1.5){DashStyle=DashStyles.Dash};
                    foreach(double rx in Prescriptions){double x=bounds.Left+rx/max*bounds.Width;dc.DrawLine(rxPen,new Point(x,bounds.Top),new Point(x,bounds.Bottom));}
                }
                dc.Pop();
                if(IsGy&&Prescriptions.Length>0)Text(dc,"Rx "+string.Join(" / ",Prescriptions.Select(v=>v.ToString("0.##",CultureInfo.CurrentCulture)))+" Gy",new Point(bounds.Left+70,3),Theme.Foreground,10);
                if(usable.Length==0)Text(dc,"No calculated curves yet",new Point(bounds.Left+20,bounds.Top+bounds.Height*.45),Theme.Muted,13);
            }
            void Text(DrawingContext dc,string value,Point point,Brush brush,double size)=>dc.DrawText(new FormattedText(value,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),size,brush,VisualTreeHelper.GetDpi(this).PixelsPerDip),point);
        }
    }
}
