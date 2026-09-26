using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
namespace QuickLook.DicomRT
{
 // Orientation widget only: normalized LPS anatomy, never a patient segmentation.
 internal sealed class PatientOrientationBadge : Border
 {
  readonly OrthographicCamera camera=new OrthographicCamera();
  public PatientOrientationBadge()
  {
   Width=88;Height=106;IsHitTestVisible=false;Background=Theme.Brush("#85101314");CornerRadius=new CornerRadius(5);Margin=new Thickness(6);
   ToolTip="Patient orientation · left turquoise / right orange";
   var grid=new Grid();grid.RowDefinitions.Add(new RowDefinition());grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});Child=grid;
   var viewport=new Viewport3D{Camera=camera};grid.Children.Add(viewport);var group=new Model3DGroup();group.Children.Add(new AmbientLight(Color.FromRgb(150,150,150)));group.Children.Add(new DirectionalLight(Colors.White,new Vector3D(-1,-2,-3)));group.Children.Add(PatientOrientationGlyph.Create());viewport.Children.Add(new ModelVisual3D{Content=group});
   camera.Width=1.65;camera.NearPlaneDistance=.01;camera.FarPlaneDistance=20;
   var legend=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center};legend.Children.Add(Theme.Text("L",9,Theme.Brush(PatientOrientationGlyph.LeftColor)));legend.Children.Add(Theme.Text(" / ",9,Theme.Muted));legend.Children.Add(Theme.Text("R",9,Theme.Brush(PatientOrientationGlyph.RightColor)));Grid.SetRow(legend,1);grid.Children.Add(legend);
   SetDirection(new Vector3D(0,1,0),new Vector3D(0,0,1));
  }
  public void SetPlane(SliceGeometry geometry)=>SetDirection(new Vector3D(geometry.Normal.X,geometry.Normal.Y,geometry.Normal.Z),new Vector3D(-geometry.Down.X,-geometry.Down.Y,-geometry.Down.Z));
  public void SetDirection(Vector3D look,Vector3D up)
  {
   if(look.Length<.001||up.Length<.001)return;look.Normalize();up.Normalize();camera.Position=new Point3D(-look.X*4,-look.Y*4,-.15-look.Z*4);camera.LookDirection=look;camera.UpDirection=up;
  }
 }
}
