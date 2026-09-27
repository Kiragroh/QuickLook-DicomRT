using System.Windows;
using System.Windows.Controls;

namespace QuickLook.DicomRT
{
 public sealed partial class ViewerControl
 {
  private void OpenMpr(){if(volume==null){status.Text="MPR + 3D requires a loaded image volume.";return;}planes.SelectedItem="MPR + 3D";SetWorkspace("Bild");}
  private void EnsureThreeDView()
  {
   if(threeDView!=null)return;
   threeDView=new ThreeDControl{Visibility=Visibility.Collapsed};
   threeDView.MprRequested+=OpenMpr;
   threeDView.ControlPointRequested+=(beam,at)=>{EnsurePlayback();neutralFields=beam==null;if(beam!=null)centralPlayback.Navigate(beam,at);SyncFieldControls();UpdateFieldOverlays();};
   mprThreeD=threeDView;workspace.Children.Add(threeDView);
  }
  private void AttachThreeD(bool quad)
  {
   EnsureThreeDView();Panel parent=quad?(Panel)imageGrid:workspace;
   if(threeDView.Parent!=parent){(threeDView.Parent as Panel)?.Children.Remove(threeDView);parent.Children.Add(threeDView);}
   Grid.SetColumn(threeDView,quad?1:0);Grid.SetRow(threeDView,quad?1:0);
   threeDView.SetCompact(quad);threeDView.Visibility=Visibility.Visible;
  }
 }
}
