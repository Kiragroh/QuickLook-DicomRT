using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace QuickLook.DicomRT
{
    public sealed partial class ViewerControl : UserControl, IDisposable
    {
        private readonly ComboBox series = Theme.Combo(), planes = Theme.Combo(150), tagSource = Theme.Combo(), plans = Theme.Combo();
        private readonly TextBox tagSearch = new TextBox { Margin = new Thickness(0, 8, 0, 8), Padding = new Thickness(7), MinHeight = 30, ToolTip = "Search tag number, name, value or sequence path" };
        private readonly TextBox roiSearch = new TextBox { Margin = new Thickness(0, 6, 0, 6), Padding = new Thickness(6), MinHeight = 28, ToolTip = "Filter structures" };
        private readonly TextBlock status = Theme.Text("Opening DICOM file …"), position = Theme.Text(""), registrationStatus = Theme.Text(""), rtSummary = Theme.Text("");
        private readonly TextBlock tagStatus = Theme.Text("", 11, Theme.Muted);
        private readonly StackPanel roiList = new StackPanel(), doseList = new StackPanel();
        private readonly Grid imageGrid = new Grid();
        private ColumnDefinition leftColumn,rightColumn;
        private UIElement leftPanel,rightPanel;
        private bool layersVisible=false,tagsVisible=false;
        private readonly List<SlicePane> panes = new List<SlicePane>();
        private readonly Slider sliceSlider = new Slider { Minimum = 0, Maximum = 0, IsSnapToTickEnabled = true, TickFrequency = 1, Margin = new Thickness(8), MinWidth = 100 };
        private readonly Slider opacity = new Slider { Minimum = 0, Maximum = 0.8, Value = 0.35, Margin = new Thickness(4), Width = 90, ToolTip = "Dose opacity" };
        private readonly CheckBox iso = new CheckBox { Content = "Isodoses", Foreground = Theme.Foreground, Margin = new Thickness(8), VerticalAlignment = VerticalAlignment.Center };
        private readonly TagTreeControl tagTree = new TagTreeControl();
        private readonly DispatcherTimer tagTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        private CancellationTokenSource lifetime = new CancellationTokenSource(), seriesLoad;
        private DicomCatalog catalog;
        private DicomEntry initialEntry, currentEntry;
        private ImageStack currentStack;
        private VolumeData volume;
        private PixelPlane native;
        private Vec3 focus;
        private double windowCenter = 40, windowWidth = 400, zoom = 1;
        private int sliceIndex, pixelGeneration, seriesGeneration;
        private bool changing, disposed;
        private List<TagRow> tags = new List<TagRow>();
        private readonly List<StructureSet> structures = new List<StructureSet>();
        private readonly List<DoseGrid> doses = new List<DoseGrid>();
        private readonly List<PlanData> planData = new List<PlanData>();
        private List<RegistrationLink> registrations = new List<RegistrationLink>();
        private PlanData selectedPlan;
        private readonly Dictionary<string, PixelPlane> pixelCache = new Dictionary<string, PixelPlane>();
        private readonly Queue<string> pixelOrder = new Queue<string>();
        private readonly Stopwatch elapsed = new Stopwatch();
        public Task LoadCompletion { get; private set; } = Task.CompletedTask;
        public bool HasImage => native != null || volume != null;
        public int StackCount => catalog?.Stacks.Count ?? 0;
        public int StructureCount => structures.Sum(x => x.Rois.Count);
        public double FirstImageMilliseconds { get; private set; }
        public double FirstRtMilliseconds { get; private set; }
        public double FirstPlanMilliseconds { get; private set; }
        public double IndexMilliseconds { get; private set; }
        public bool SelectedFileVisible => SamePath(initialEntry?.Path, currentEntry?.Path);
        public bool ReferencedPlanSelected => initialEntry?.Modality!="RTDOSE" || doses.FirstOrDefault(d=>d.Entry.SopUid==initialEntry.SopUid)?.PlanUid==selectedPlan?.Entry.SopUid;

        public ViewerControl()
        {
            Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/QuickLook.DicomRT.Viewer;component/Theme.xaml", UriKind.Relative) });
            FontFamily = new FontFamily("Segoe UI"); FontSize = 12; Background = Theme.Background; Foreground = Theme.Foreground;
            var root = new Grid(); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition()); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var heading = new DockPanel { Margin = new Thickness(10, 4, 10, 4), LastChildFill=false };
            var logo = new Image { Source = AppIcon.Image, Width = 26, Height = 26, Margin = new Thickness(0, 0, 7, 0) }; DockPanel.SetDock(logo, Dock.Left); heading.Children.Add(logo);
            var title = Theme.Text("DICOM RT", 12); title.FontWeight = FontWeights.SemiBold; title.VerticalAlignment=VerticalAlignment.Center;title.Margin=new Thickness(0,0,16,0); DockPanel.SetDock(title, Dock.Left); heading.Children.Add(title);
            var tagsButton=Theme.Button("Tags");tagsButton.ToolTip="Right sidebar with searchable DICOM tags";tagsButton.Click+=(s,e)=>{tagsVisible=!tagsVisible;UpdatePanels();};DockPanel.SetDock(tagsButton,Dock.Right);heading.Children.Add(tagsButton);
            var layersButton=Theme.Button("RT");layersButton.ToolTip="Plans, structures, dose, MLC, DVH and 3D";layersButton.Click+=(s,e)=>{layersVisible=!layersVisible;UpdatePanels();};DockPanel.SetDock(layersButton,Dock.Right);heading.Children.Add(layersButton);root.Children.Add(heading);
            var body = new Grid();leftColumn=new ColumnDefinition {Width=new GridLength(0)};body.ColumnDefinitions.Add(leftColumn);body.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(4)});body.ColumnDefinitions.Add(new ColumnDefinition {MinWidth=280});body.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(4)});rightColumn=new ColumnDefinition {Width=new GridLength(0)};body.ColumnDefinitions.Add(rightColumn);Grid.SetRow(body,1);root.Children.Add(body);
            leftPanel=BuildRtPanel();body.Children.Add(leftPanel);var splitLeft=new GridSplitter {Width=4,HorizontalAlignment=HorizontalAlignment.Stretch,Background=Theme.Background};Grid.SetColumn(splitLeft,1);body.Children.Add(splitLeft);
            var middle=BuildMiddle();Grid.SetColumn(middle,2);body.Children.Add(middle);var splitter=new GridSplitter {Width=4,HorizontalAlignment=HorizontalAlignment.Stretch,Background=Theme.Background};Grid.SetColumn(splitter,3);body.Children.Add(splitter);
            rightPanel=BuildRight();Grid.SetColumn(rightPanel,4);body.Children.Add(rightPanel);UpdatePanels();
            var footer=new DockPanel {Margin=new Thickness(12,3,12,6)};activity.Margin=new Thickness(0,2,12,2);activity.MaxWidth=360;activity.TextWrapping=TextWrapping.NoWrap;activity.TextTrimming=TextTrimming.CharacterEllipsis;DockPanel.SetDock(activity,Dock.Right);footer.Children.Add(activity);status.Foreground=Theme.Muted;footer.Children.Add(status);Grid.SetRow(footer,2);root.Children.Add(footer);Content=root;
            series.SelectionChanged += async (s, e) => { if (!changing && series.SelectedItem is ImageStack stack) await SelectStackAsync(stack); };
            planes.SelectionChanged += (s, e) => { if (!changing) { if ((string)planes.SelectedItem != "Native" && volume == null) { changing = true; planes.SelectedItem = "Native"; changing = false; status.Text = "MPR becomes available once a volume with suitable geometry has loaded."; } RebuildPanes(); } };
            tagSource.SelectionChanged += (s, e) => UpdateTags();
            tagSearch.TextChanged += (s, e) => { tagTimer.Stop(); tagTimer.Start(); };
            tagTimer.Tick += (s, e) => { tagTimer.Stop(); FilterTags(); };
            roiSearch.TextChanged += (s, e) => BuildRoiList();
            plans.SelectionChanged += async (s, e) => await SelectPlanChoiceAsync();
            opacity.ValueChanged += (s, e) => Redraw(); iso.Checked += (s, e) => Redraw(); iso.Unchecked += (s, e) => Redraw();
            sliceSlider.ValueChanged += async (s, e) => { if (!changing && currentStack != null) await ShowSliceAsync((int)Math.Round(e.NewValue), true); };
            IsVisibleChanged+=(s,e)=>{if(!IsVisible&&fusionPopup!=null)fusionPopup.IsOpen=false;};
            Unloaded+=(s,e)=>{if(fusionPopup!=null)fusionPopup.IsOpen=false;};
            RebuildPanes();
        }

        private UIElement BuildRtPanel()
        {
            var outer = new DockPanel(); var top = new StackPanel();
            top.Children.Add(BuildPlanPicker());top.Children.Add(BuildViewButtons());top.Children.Add(rtSummary); top.Children.Add(registrationStatus); DockPanel.SetDock(top, Dock.Top); outer.Children.Add(top);
            var tabs = new TabControl { Background = Theme.Panel, BorderThickness = new Thickness(0), Margin = new Thickness(0, 8, 0, 0) };
            var roiPanel = new DockPanel(); var filters = new StackPanel(); filters.Children.Add(Theme.Text("Find structures", 10, Theme.Muted)); filters.Children.Add(roiSearch); var toggles = new WrapPanel();
            Button all = Theme.Button("All"), none = Theme.Button("None"); all.Click += (s, e) => SetRois(true); none.Click += (s, e) => SetRois(false); toggles.Children.Add(all); toggles.Children.Add(none); filters.Children.Add(toggles); filters.Children.Add(Theme.Text("Click a name to locate the structure", 10, Theme.Muted)); DockPanel.SetDock(filters, Dock.Top); roiPanel.Children.Add(filters); roiPanel.Children.Add(new ScrollViewer { Content = roiList, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            tabs.Items.Add(new TabItem { Header = "Structures", Content = roiPanel });
            var doseContent=new StackPanel();doseContent.Children.Add(BuildDoseTools());doseContent.Children.Add(doseList);var dosePanel=new ScrollViewer { Content=doseContent,VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
            tabs.Items.Add(new TabItem { Header = "Doses", Content = dosePanel });

            outer.Children.Add(tabs); return Theme.Box(outer);
        }

        private UIElement BuildMiddle()
        {
            var dock = new DockPanel { Margin = new Thickness(8, 0, 8, 0) };
            var top = new StackPanel();var images=new StackPanel();imageHeader=images;top.Children.Add(images);
            var sourceRow=new DockPanel();var fusion=BuildFusionButton();DockPanel.SetDock(fusion,Dock.Right);sourceRow.Children.Add(fusion);series.ToolTip="Change the base image · RT follows when a matching registration is available";sourceRow.Children.Add(series);images.Children.Add(sourceRow);
            var tools = new WrapPanel(); planes.ItemsSource = new[] { "Native", "Axial", "Coronal", "Sagittal", "3 planes" }; planes.SelectedIndex = 0; tools.Children.Add(planes);
            Button soft = Theme.Button("Soft tissue"), bone = Theme.Button("Bone"), auto = Theme.Button("Auto"), fit = Theme.Button("Fit"), zin = Theme.Button("＋"), zout = Theme.Button("−");
            soft.Click += (s, e) => SetWindow(40, 400); bone.Click += (s, e) => SetWindow(400, 1800); auto.Click += (s, e) => AutoWindow(); fit.Click += (s, e) => { zoom = 1; Redraw(); }; zin.Click += (s, e) => { zoom = Math.Min(8, zoom * 1.25); Redraw(); }; zout.Click += (s, e) => { zoom = Math.Max(.25, zoom / 1.25); Redraw(); };
            foreach (var b in new[] { soft, bone, auto, fit, zin, zout }) tools.Children.Add(b); images.Children.Add(tools);
            DockPanel.SetDock(top, Dock.Top); dock.Children.Add(top);
            var bottom = new StackPanel(); imageFooter=bottom; bottom.Children.Add(sliceSlider); bottom.Children.Add(position); DockPanel.SetDock(bottom, Dock.Bottom); dock.Children.Add(bottom); workspace.Children.Add(imageGrid); dock.Children.Add(workspace); return dock;
        }

        private UIElement BuildRight()
        {
            var dock = new DockPanel(); var top = new StackPanel(); top.Children.Add(Theme.Text("DICOM TAGS", 13, Theme.Accent)); top.Children.Add(tagSource); top.Children.Add(Theme.Text("Search · tag, name, value or sequence", 10, Theme.Muted)); top.Children.Add(tagSearch); top.Children.Add(tagStatus); DockPanel.SetDock(top, Dock.Top); dock.Children.Add(top);
            dock.Children.Add(tagTree);return Theme.Box(dock);
        }

        private void SetWindow(double center, double width) { windowCenter = center; windowWidth = Math.Max(1, width); Redraw(); }
        private void UpdatePanels(){if(rightColumn==null||leftColumn==null)return;leftColumn.Width=new GridLength(layersVisible?290:0);rightColumn.Width=new GridLength(tagsVisible?345:0);if(leftPanel!=null)leftPanel.Visibility=layersVisible?Visibility.Visible:Visibility.Collapsed;if(rightPanel!=null)rightPanel.Visibility=tagsVisible?Visibility.Visible:Visibility.Collapsed;}
        private void AutoWindow() { float min = native?.Min ?? volume?.Min ?? 0, max = native?.Max ?? volume?.Max ?? 1000; SetWindow((min + max) / 2.0, Math.Max(1, max - min)); }
        private void RebuildPanes()
        {
            foreach (var pane in panes) pane.Dispose(); panes.Clear(); imageGrid.Children.Clear(); imageGrid.ColumnDefinitions.Clear(); imageGrid.RowDefinitions.Clear();
            string selected = (string)planes.SelectedItem ?? "Native"; var views = selected == "3 planes" ? new[] { "Axial", "Coronal", "Sagittal" } : new[] { selected };
            for (int i = 0; i < views.Length; i++)
            {
                imageGrid.ColumnDefinitions.Add(new ColumnDefinition());
                var pane = new SlicePane { Margin = new Thickness(2), Tag = views[i] };
                pane.Scrolled += async (p, steps) => await ScrollAsync((string)p.Tag, steps);
                pane.Picked += (p, point) => { focus = point; Redraw(); }; pane.WindowChanged += SetWindow;
                Grid.SetColumn(pane, i); imageGrid.Children.Add(pane); panes.Add(pane);
            }
            Redraw();
        }

        public void Dispose()
        {
            if (disposed) return; if(fusionPopup!=null)fusionPopup.IsOpen=false; disposed = true; lifetime.Cancel(); seriesLoad?.Cancel(); tagTimer.Stop();
            foreach (var pane in panes) pane.Dispose(); centralPlayback?.Dispose(); dvhView?.Dispose(); threeDView?.Dispose(); sumLoad?.Cancel(); sumLoad?.Dispose(); overlayLoad?.Cancel(); overlayLoad?.Dispose(); overlayVolume=null; pixelCache.Clear(); volume = null; native = null;
        }
    }
}
