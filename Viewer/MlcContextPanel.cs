using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
namespace QuickLook.DicomRT
{
 // Fixed internal layout inside a Viewbox: resizing never restarts CT rendering.
 internal sealed class MlcContextPanel:Canvas
 {
  readonly Grid area;readonly Border card;readonly StackPanel content;bool positioned,userPlaced,clamping;
  internal MlcContextPanel(Grid host,FrameworkElement slice,FrameworkElement linac)
  {
   area=host;ClipToBounds=true;
   content=new StackPanel{Width=300};content.Children.Add(slice);content.Children.Add(linac);
   var grid=new Grid();grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
   var header=new DockPanel();var reset=Theme.Button("↺");reset.Padding=new Thickness(5,0,5,0);reset.ToolTip="Reset orientation panel size and position";reset.Click+=(s,e)=>{userPlaced=false;positioned=false;card.Width=320;Clamp();};DockPanel.SetDock(reset,Dock.Right);header.Children.Add(reset);
   var drag=new Thumb{Cursor=Cursors.SizeAll,Height=24,ToolTip="Drag to move slice and LINAC together · Ctrl + wheel or bottom-right grip to resize"};
   var caption=new FrameworkElementFactory(typeof(TextBlock));caption.SetValue(TextBlock.TextProperty,"ORIENTATION  ·  drag to move");caption.SetValue(TextBlock.ForegroundProperty,Theme.Accent);caption.SetValue(TextBlock.FontSizeProperty,10d);caption.SetValue(TextBlock.BackgroundProperty,Brushes.Transparent);caption.SetValue(TextBlock.VerticalAlignmentProperty,VerticalAlignment.Center);drag.Template=new ControlTemplate(typeof(Thumb)){VisualTree=caption};
   drag.DragDelta+=(s,e)=>MoveBy(e.HorizontalChange,e.VerticalChange);header.Children.Add(drag);grid.Children.Add(header);
   var viewbox=new Viewbox{Stretch=Stretch.Uniform,Child=content};Grid.SetRow(viewbox,1);grid.Children.Add(viewbox);
   var grip=new Thumb{Width=18,Height=18,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Cursor=Cursors.SizeNWSE,ToolTip="Resize orientation panel"};
   var mark=new FrameworkElementFactory(typeof(TextBlock));mark.SetValue(TextBlock.TextProperty,"◢");mark.SetValue(TextBlock.ForegroundProperty,Theme.Accent);mark.SetValue(TextBlock.BackgroundProperty,Brushes.Transparent);grip.Template=new ControlTemplate(typeof(Thumb)){VisualTree=mark};Grid.SetRow(grip,1);grid.Children.Add(grip);grip.DragDelta+=(s,e)=>ResizeBy(e.HorizontalChange+e.VerticalChange*.4);
   card=new Border{Width=320,Padding=new Thickness(5),CornerRadius=new CornerRadius(6),Background=Theme.Brush("#E6111314"),BorderBrush=Theme.Brush("#344D63"),BorderThickness=new Thickness(1),Child=grid};Children.Add(card);
   area.SizeChanged+=(s,e)=>Clamp();card.SizeChanged+=(s,e)=>Clamp();slice.IsVisibleChanged+=(s,e)=>Clamp();
  }
  internal void MoveBy(double x,double y){userPlaced=true;positioned=true;SetLeft(card,(double.IsNaN(GetLeft(card))?0:GetLeft(card))+x);SetTop(card,(double.IsNaN(GetTop(card))?0:GetTop(card))+y);Clamp();}
  internal void ResizeBy(double delta){card.Width=Math.Max(180,Math.Min(540,card.Width+delta));Clamp();}
  void Clamp()
  {
   if(clamping||area.ActualWidth<=0||area.ActualHeight<=0)return;clamping=true;
   try{
    double natural=0;foreach(FrameworkElement child in content.Children)if(child.Visibility!=Visibility.Collapsed)natural+=child.Height;
    double maxWidth=Math.Min(area.ActualWidth-16,Math.Max(100,area.ActualHeight-52)*300/Math.Max(1,natural)+12);
    card.Width=Math.Max(100,Math.Min(card.Width,maxWidth));
    double x=GetLeft(card),y=GetTop(card);if(!positioned||!userPlaced){x=area.ActualWidth-card.Width-8;y=8;positioned=true;}
    SetLeft(card,Math.Max(0,Math.Min(double.IsNaN(x)?0:x,Math.Max(0,area.ActualWidth-card.Width))));SetTop(card,Math.Max(0,Math.Min(double.IsNaN(y)?0:y,Math.Max(0,area.ActualHeight-card.ActualHeight))));
   }finally{clamping=false;}
  }
 }
}
