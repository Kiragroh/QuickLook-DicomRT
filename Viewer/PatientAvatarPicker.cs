using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Media3D;
namespace QuickLook.DicomRT {
 internal sealed partial class PatientOrientationBadge {
  Popup avatarPicker;readonly Dictionary<string,Button> avatarButtons=new Dictionary<string,Button>();TextBlock avatarStatus;
  void OpenAvatarPicker(){
   if(avatarPicker==null){
    var panel=new StackPanel{Width=270};panel.Children.Add(Theme.Text("ORIENTATION CHARACTER",12,Theme.Accent));
    panel.Children.Add(Theme.Text("Shared by slices, 3D and LINAC",10,Theme.Muted));
    for(int i=0;i<OrientationAvatarPreferences.Ids.Length;i++){
     string id=OrientationAvatarPreferences.Ids[i];var row=new DockPanel();var preview=new Viewport3D{Width=56,Height=61,IsHitTestVisible=false,Camera=new OrthographicCamera(new Point3D(2,-5,1),new Vector3D(-2,5,-1.12),new Vector3D(0,0,1),1.95)};
     var group=new Model3DGroup();group.Children.Add(new AmbientLight(Color.FromRgb(145,145,145)));group.Children.Add(new DirectionalLight(Colors.White,new Vector3D(-1,2,-3)));group.Children.Add(PatientOrientationGlyph.Create(id));preview.Children.Add(new ModelVisual3D{Content=group});DockPanel.SetDock(preview,Dock.Left);row.Children.Add(preview);
     var label=Theme.Text(OrientationAvatarPreferences.Labels[i],12);label.VerticalAlignment=VerticalAlignment.Center;label.Margin=new Thickness(9,0,0,0);row.Children.Add(label);
     var button=Theme.Button("");button.Content=row;button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.Padding=new Thickness(3);button.ToolTip="Use "+OrientationAvatarPreferences.Labels[i]+" as the schematic orientation character";
     button.Click+=(s,e)=>{if(avatarPreferences.Select(id))avatarPicker.IsOpen=false;else avatarStatus.Text="Could not save the preference.";};avatarButtons[id]=button;panel.Children.Add(button);
    }
    avatarStatus=Theme.Text("L / R colors and orientation stay unchanged",10,Theme.Muted);panel.Children.Add(avatarStatus);
    avatarPicker=new Popup{PlacementTarget=this,Placement=PlacementMode.Left,StaysOpen=false,AllowsTransparency=true,Child=Theme.Box(panel)};
    Unloaded+=(s,e)=>avatarPicker.IsOpen=false;IsVisibleChanged+=(s,e)=>{if(!IsVisible)avatarPicker.IsOpen=false;};
   }
   foreach(var item in avatarButtons){bool selected=item.Key==avatarPreferences.Selected;item.Value.Background=selected?Theme.Brush("#24384C"):Theme.Panel;item.Value.BorderBrush=selected?Theme.Accent:Theme.Brush("#394148");}
   avatarPicker.IsOpen=true;
  }
 }
}