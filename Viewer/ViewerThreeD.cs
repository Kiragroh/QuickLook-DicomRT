using System.Windows;
using System.Windows.Controls;

namespace QuickLook.DicomRT
{
 public sealed partial class ViewerControl
 {
  private void EnsureThreeDView()
  {
   if(threeDView!=null)return;
   threeDView=new ThreeDControl{Visibility=Visibility.Collapsed};
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
