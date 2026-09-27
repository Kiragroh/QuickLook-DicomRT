using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
namespace QuickLook.DicomRT {
 public sealed partial class MlcPlaybackControl {
  UIElement BuildDrrSettings(){
   var button=Theme.Button("DRR ▾");button.ToolTip="DRR contrast: automatic, soft, high contrast or manual window; no new projection required";
   var panel=new StackPanel{Width=240};panel.Children.Add(Theme.Text("DRR CONTRAST",12,Theme.Accent));
   var preset=Theme.Combo();preset.ItemsSource=new[]{"Auto","Soft","High contrast","Original","Custom"};preset.SelectedIndex=0;panel.Children.Add(preset);
   var custom=new StackPanel{Visibility=Visibility.Collapsed};panel.Children.Add(custom);
   var level=new Slider{Minimum=0,Maximum=1,Value=.5,Margin=new Thickness(4),ToolTip="DRR brightness / normalized level"};
   var width=new Slider{Minimum=.02,Maximum=2,Value=1,Margin=new Thickness(4),ToolTip="DRR contrast / normalized window width"};
   custom.Children.Add(Theme.Text("Brightness / level",11));custom.Children.Add(level);custom.Children.Add(Theme.Text("Contrast / width",11));custom.Children.Add(width);
   System.Action update=()=>{custom.Visibility=(string)preset.SelectedItem=="Custom"?Visibility.Visible:Visibility.Collapsed;aperture.SetDrrWindow((string)preset.SelectedItem,level.Value,width.Value);};
   preset.SelectionChanged+=(s,e)=>update();level.ValueChanged+=(s,e)=>update();width.ValueChanged+=(s,e)=>update();
   var popup=new Popup{PlacementTarget=button,Placement=PlacementMode.Bottom,StaysOpen=false,AllowsTransparency=true,Child=Theme.Box(panel)};
   button.Click+=(s,e)=>popup.IsOpen=!popup.IsOpen;Unloaded+=(s,e)=>popup.IsOpen=false;IsVisibleChanged+=(s,e)=>{if(!IsVisible)popup.IsOpen=false;};return button;
  }
 }
}