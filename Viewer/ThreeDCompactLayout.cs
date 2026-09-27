using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
namespace QuickLook.DicomRT {
 public sealed partial class ThreeDControl {
  readonly Grid toolbarHost=new Grid();readonly StackPanel compactToolbar=new StackPanel{Orientation=Orientation.Horizontal};
  WrapPanel settingsControls;Border settingsBox;Popup compactSettings;ScrollViewer compactToolbarScroll;bool? controlsCompact;
  void BuildControlLayout(Grid root,WrapPanel controls){
   settingsControls=controls;settingsBox=new Border{Child=controls,Background=Theme.Background};
   var button=Theme.Button("3D settings ▾");button.ToolTip="Structures, skin, bone, dose, fields, opacity and reset view";
   compactToolbar.Children.Add(button);compactToolbarScroll=new ScrollViewer{Content=compactToolbar,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled};
   compactSettings=new Popup{PlacementTarget=button,Placement=PlacementMode.Bottom,StaysOpen=false,AllowsTransparency=true};
   button.Click+=(s,e)=>compactSettings.IsOpen=!compactSettings.IsOpen;
   Unloaded+=(s,e)=>compactSettings.IsOpen=false;IsVisibleChanged+=(s,e)=>{if(!IsVisible)compactSettings.IsOpen=false;};
   root.Children.Add(toolbarHost);ApplyControlLayout();
  }
  void ApplyControlLayout(){
   if(settingsControls==null||controlsCompact==compact)return;controlsCompact=compact;compactSettings.IsOpen=false;
   if(beamNavigation.Parent is Panel parent)parent.Children.Remove(beamNavigation);
   compactSettings.Child=null;toolbarHost.Children.Clear();
   if(compact){
    settingsBox.Width=350;compactSettings.Child=settingsBox;compactToolbar.Children.Add(beamNavigation);toolbarHost.Children.Add(compactToolbarScroll);
   }else{
    settingsBox.Width=double.NaN;settingsControls.Children.Insert(7,beamNavigation);toolbarHost.Children.Add(settingsBox);
   }
   activeBeamPicker.Width=compact?135:165;beamCursor.Width=compact?115:155;beamPosition.Width=compact?180:285;
  }
 }
}