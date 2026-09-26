using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace QuickLook.DicomRT
{
    public sealed partial class ViewerControl
    {
        private System.Windows.Controls.Primitives.Popup fusionPopup;
        private readonly Grid workspace = new Grid();
        private readonly ComboBox overlaySeries = Theme.Combo(), dvhDose = Theme.Combo();
        private readonly Slider blend = new Slider { Minimum = 0, Maximum = 1, Value = .5, Width = 100, ToolTip = "Overlay image contribution" };
        private readonly CheckBox wash = new CheckBox { Content = "Colorwash", IsChecked = true, Margin = new Thickness(4,8,4,4) };
        private readonly Slider doseMin = new Slider { Minimum = 0, Maximum = 99, Value = 5 }, doseMax = new Slider { Minimum = 1, Maximum = 100, Value = 100 };
        private readonly TextBlock doseRange = Theme.Text("",11,Theme.Muted), fusionStatus = Theme.Text("",10,Theme.Muted);
        private readonly TextBox isoLevels = new TextBox { Text = "10; 20; 30; 40; 50; 60; 70; 80; 90; 100", Padding = new Thickness(6), Margin = new Thickness(0,3,0,3) };
        private double[] displayedIsoLevels = { 10,20,30,40,50,60,70,80,90,100 };
        private VolumeData overlayVolume;
        private ImageStack overlayStack;
        private CancellationTokenSource overlayLoad;
        private int overlayGeneration;
        private bool updatingOverlay;
        private string workspaceMode = "Bild";
        private RenderScene latestScene;
        private MlcPlaybackControl centralPlayback;
        private PlanData centralPlan;
        private DvhControl dvhView;
        private ThreeDControl threeDView;
        private ThreeDControl mprThreeD;
        private UIElement imageHeader, imageFooter;
        private readonly Dictionary<string,Button> viewButtons = new Dictionary<string,Button>();

        private UIElement BuildViewButtons()
        {
            var row = new WrapPanel { Margin = new Thickness(0,3,0,3) };
            foreach (string name in new[] { "Bild", "MLC", "DVH", "3D" })
            {
                var button = Theme.Button(name=="Bild"?"Image":name);button.MinWidth=52;button.Click+=(s,e)=>SetWorkspace(name);viewButtons[name]=button;row.Children.Add(button);
            }
            viewButtons["Bild"].Foreground=Theme.Accent;
            return row;
        }
        private UIElement BuildFusionButton()
        {
            var button=Theme.Button("");button.ToolTip="Image fusion · select an overlay";button.Width=36;
            button.Content=new System.Windows.Shapes.Path {Data=System.Windows.Media.Geometry.Parse("M 1,1 L 12,1 12,12 1,12 Z M 6,6 L 17,6 17,17 6,17 Z"),Stroke=Theme.Accent,StrokeThickness=1.4,Width=18,Height=18};
            var content=new StackPanel();content.Children.Add(Theme.Text("IMAGE FUSION",12,Theme.Accent));content.Children.Add(Theme.Text("The main series is the base image. Select a registered image here to overlay it.",11,Theme.Muted));content.Children.Add(BuildFusionTools());
            var popup=new System.Windows.Controls.Primitives.Popup {PlacementTarget=button,Placement=System.Windows.Controls.Primitives.PlacementMode.Bottom,StaysOpen=false,AllowsTransparency=true,Child=Theme.Box(content)};((FrameworkElement)popup.Child).Width=480;popup.Resources.MergedDictionaries.Add(Resources.MergedDictionaries[0]);
            fusionPopup=popup;button.Click+=(s,e)=>popup.IsOpen=!popup.IsOpen;return button;
        }
        private UIElement BuildFusionTools()
        {
            var panel = new StackPanel();
            var row = new DockPanel(); var label = Theme.Text("Overlay",11,Theme.Muted);label.VerticalAlignment=VerticalAlignment.Center;DockPanel.SetDock(label,Dock.Left);row.Children.Add(label);
            var opacityLabel=Theme.Text("Blend",10,Theme.Muted);DockPanel.SetDock(blend,Dock.Right);row.Children.Add(blend);DockPanel.SetDock(opacityLabel,Dock.Right);row.Children.Add(opacityLabel);row.Children.Add(overlaySeries);panel.Children.Add(row);panel.Children.Add(fusionStatus);
            overlaySeries.SelectionChanged+=async(s,e)=>{if(!updatingOverlay)await LoadOverlayAsync((overlaySeries.SelectedItem as OverlayChoice)?.Stack);};
            blend.ValueChanged+=(s,e)=>Redraw();
            return panel;
        }
        private sealed class OverlayChoice
        {
            public ImageStack Stack;public string Label;public override string ToString()=>Label;
        }
        private void UpdateOverlayChoices()
        {
            if (catalog==null || currentEntry==null)return;
            var previous=(overlaySeries.SelectedItem as OverlayChoice)?.Stack;
            var choices=new List<OverlayChoice>{new OverlayChoice{Label="No overlay"}};
            foreach(var stack in catalog.Stacks.Where(s=>s!=currentStack&&s.CanMpr))
            {
                if(RegistrationReader.Resolve(registrations,currentEntry.FrameUid,stack.FrameUid)==null)continue;
                choices.Add(new OverlayChoice{Stack=stack,Label=stack+ (stack.FrameUid==currentEntry.FrameUid?" · same frame":" · REG")});
            }
            updatingOverlay=true;overlaySeries.ItemsSource=choices;overlaySeries.SelectedItem=choices.FirstOrDefault(c=>c.Stack==previous)??choices[0];updatingOverlay=false;
            var chosen=(overlaySeries.SelectedItem as OverlayChoice)?.Stack;
            if(chosen!=overlayStack)_=LoadOverlayAsync(chosen);
            fusionStatus.Text=choices.Count>1?"Base image: select the series above · Overlay: select a registered series here":"No additional unambiguously associated series suitable for MPR.";
        }
        private async Task LoadOverlayAsync(ImageStack stack)
        {
            overlayLoad?.Cancel();overlayLoad?.Dispose();overlayLoad=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);var token=overlayLoad.Token;int generation=++overlayGeneration;
            overlayVolume=null;overlayStack=stack;Redraw();if(stack==null){fusionStatus.Text="Overlay off";return;}
            fusionStatus.Text="Loading overlay …";
            try
            {
                var loaded=await Task.Run(()=>VolumeData.Load(stack,token),token);
                if(disposed||token.IsCancellationRequested||generation!=overlayGeneration)return;
                overlayVolume=loaded;fusionStatus.Text=stack.FrameUid==currentEntry.FrameUid?"Image fusion active · same frame":"Image fusion active · spatial REG applied";Redraw();
            }
            catch(OperationCanceledException){}
            catch(Exception){if(!token.IsCancellationRequested)fusionStatus.Text="This image series cannot be overlaid as a volume.";}
        }
        private UIElement BuildDoseTools()
        {
            var panel=new StackPanel();panel.Children.Add(wash);panel.Children.Add(Theme.Text("Opacity",10,Theme.Muted));opacity.Width=double.NaN;panel.Children.Add(opacity);
            panel.Children.Add(doseRange);panel.Children.Add(Theme.Text("Lower threshold",10,Theme.Muted));panel.Children.Add(doseMin);panel.Children.Add(Theme.Text("Upper threshold",10,Theme.Muted));panel.Children.Add(doseMax);
            panel.Children.Add(iso);panel.Children.Add(Theme.Text("Isodose units",10,Theme.Muted));
            isodoseMode.Items.Add(new ComboBoxItem{Content="Gy"});isodoseMode.Items.Add(new ComboBoxItem{Content="%"});
            isodoseMode.ToolTip="Gy: local to this dose selection. %: global percentages of each dose grid maximum.";
            isodoseMode.SelectionChanged+=(s,e)=>ChangeIsodoseMode();panel.Children.Add(isodoseMode);
            panel.Children.Add(isodoseUnitsLabel);panel.Children.Add(isoLevels);var actions=new WrapPanel();actions.Children.Add(applyIsodosesButton);actions.Children.Add(defaultIsodosesButton);panel.Children.Add(actions);panel.Children.Add(isodoseLegend);
            applyIsodosesButton.Click+=(s,e)=>ApplyIsodoseLevels();defaultIsodosesButton.Click+=(s,e)=>DefaultIsodoseLevels();
            wash.Checked+=(s,e)=>Redraw();wash.Unchecked+=(s,e)=>Redraw();
            doseMin.ValueChanged+=(s,e)=>{if(doseMin.Value>=doseMax.Value)doseMax.Value=doseMin.Value+1;UpdateDoseRange();Redraw();};
            doseMax.ValueChanged+=(s,e)=>{if(doseMax.Value<=doseMin.Value)doseMin.Value=doseMax.Value-1;UpdateDoseRange();Redraw();};UpdateDoseRange();
            return panel;
        }
        private void UpdateDoseRange(){doseRange.Text=$"Colorwash {doseMin.Value:0}–{doseMax.Value:0} % · relative to each dose maximum";}
        private void SetWorkspace(string mode)
        {
            if(disposed)return;CloseDosePopups();if(fusionPopup!=null)fusionPopup.IsOpen=false;workspaceMode=mode;
            foreach(var pair in viewButtons)pair.Value.Foreground=pair.Key==mode?Theme.Accent:Theme.Foreground;
            foreach(UIElement child in workspace.Children)child.Visibility=Visibility.Collapsed;
            if(imageHeader!=null)imageHeader.Visibility=mode=="Bild"?Visibility.Visible:Visibility.Collapsed;
            if(imageFooter!=null)imageFooter.Visibility=mode=="Bild"?Visibility.Visible:Visibility.Collapsed;
            if(mode!="MLC")centralPlayback?.Dispose();
            if(mode!="DVH")dvhView?.Cancel();
            if(mode=="Bild")imageGrid.Visibility=Visibility.Visible;
            else if(mode=="MLC")
            {
                if(centralPlayback==null){centralPlayback=new MlcPlaybackControl();centralPlayback.IsocenterSelected+=async point=>{var map=TransformToImage(selectedPlan?.FrameUid);if(map!=null){SetWorkspace(HasImage?"Bild":"3D");await MoveFocusAsync(map.Transform(point));}};workspace.Children.Add(centralPlayback);}
                if(selectedPlan!=null&&centralPlan!=selectedPlan){centralPlayback.SetPlan(selectedPlan);centralPlan=selectedPlan;}
                centralPlayback.Visibility=Visibility.Visible;if(selectedPlan==null)status.Text="Select an RTPLAN on the left to open the MLC view.";
            }
            else if(mode=="DVH")
            {
                if(dvhView==null){dvhView=new DvhControl();var dock=new DockPanel();var header=new StackPanel();header.Children.Add(Theme.Text("DVH · select dose",11,Theme.Accent));header.Children.Add(dvhDose);DockPanel.SetDock(header,Dock.Top);dock.Children.Add(header);dock.Children.Add(dvhView);var holder=new Grid();holder.Children.Add(dock);dvhContainer=holder;workspace.Children.Add(holder);dvhDose.SelectionChanged+=(s,e)=>{if(!changing)UpdateDvh();};}
                dvhContainer.Visibility=Visibility.Visible;UpdateDvhChoices();UpdateDvh();
            }
            else if(mode=="3D")
            {
                AttachThreeD(false);
            }
            Redraw();
        }
        private Grid dvhContainer;
        private sealed class DoseChoice { public DoseGrid Dose; public override string ToString()=>Dose.Label; }
        private void UpdateDvhChoices()
        {
            var old=(dvhDose.SelectedItem as DoseChoice)?.Dose;var choices=SelectedDoses.Select(d=>new DoseChoice{Dose=d}).ToList();bool prior=changing;changing=true;dvhDose.ItemsSource=choices;dvhDose.SelectedItem=choices.FirstOrDefault(c=>c.Dose==old)??choices.FirstOrDefault();changing=prior;
        }
        private void UpdateDvh()
        {
            if(workspaceMode!="DVH"||dvhView==null)return;var dose=(dvhDose.SelectedItem as DoseChoice)?.Dose;
            dvhView.SetData(SelectedStructures.SelectMany(s=>s.Rois).Where(r=>r.Visible).ToList(),dose,r=>RegistrationReader.Resolve(registrations,r.FrameUid,dose?.FrameUid));
        }
    }
}
