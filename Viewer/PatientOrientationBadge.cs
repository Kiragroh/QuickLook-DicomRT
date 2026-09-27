using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
namespace QuickLook.DicomRT
{
 // Orientation widget only: normalized LPS anatomy, never a patient segmentation.
 internal sealed partial class PatientOrientationBadge : Border
 {
  readonly ModelVisual3D avatar=new ModelVisual3D();
  readonly OrientationAvatarPreferences avatarPreferences=OrientationAvatarPreferences.Current;
  readonly OrthographicCamera camera=new OrthographicCamera();
  public PatientOrientationBadge()
  {
   Width=88;Height=106;IsHitTestVisible=true;Cursor=System.Windows.Input.Cursors.Hand;Focusable=true;Background=Theme.Brush("#85101314");CornerRadius=new CornerRadius(5);Margin=new Thickness(6);
   ToolTip="Choose orientation character · left turquoise / right orange";
   var grid=new Grid();grid.RowDefinitions.Add(new RowDefinition());grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});Child=grid;
   var viewport=new Viewport3D{Camera=camera};grid.Children.Add(viewport);var group=new Model3DGroup();group.Children.Add(new AmbientLight(Color.FromRgb(150,150,150)));group.Children.Add(new DirectionalLight(Colors.White,new Vector3D(-1,-2,-3)));viewport.Children.Add(new ModelVisual3D{Content=group});avatar.Content=PatientOrientationGlyph.Create();viewport.Children.Add(avatar);
   System.ComponentModel.PropertyChangedEventManager.AddHandler(avatarPreferences,AvatarChanged,"Selected");
   MouseLeftButtonDown+=(s,e)=>{e.Handled=true;OpenAvatarPicker();};MouseLeftButtonUp+=(s,e)=>e.Handled=true;
   KeyDown+=(s,e)=>{if(e.Key==System.Windows.Input.Key.Enter){e.Handled=true;OpenAvatarPicker();}};
   camera.Width=1.85;camera.NearPlaneDistance=.01;camera.FarPlaneDistance=20;
   var legend=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center};legend.Children.Add(Theme.Text("L",9,Theme.Brush(PatientOrientationGlyph.LeftColor)));legend.Children.Add(Theme.Text(" / ",9,Theme.Muted));legend.Children.Add(Theme.Text("R",9,Theme.Brush(PatientOrientationGlyph.RightColor)));Grid.SetRow(legend,1);grid.Children.Add(legend);
   SetDirection(new Vector3D(0,1,0),new Vector3D(0,0,1));
  }
  void AvatarChanged(object sender,System.ComponentModel.PropertyChangedEventArgs e){avatar.Content=PatientOrientationGlyph.Create();}
  public void SetPlane(SliceGeometry geometry)=>SetDirection(new Vector3D(geometry.Normal.X,geometry.Normal.Y,geometry.Normal.Z),new Vector3D(-geometry.Down.X,-geometry.Down.Y,-geometry.Down.Z));
  public void SetDirection(Vector3D look,Vector3D up)
  {
   if(look.Length<.001||up.Length<.001)return;look.Normalize();up.Normalize();camera.Position=new Point3D(-look.X*4,-look.Y*4,-.15-look.Z*4);camera.LookDirection=look;camera.UpDirection=up;
  }
 }
}
