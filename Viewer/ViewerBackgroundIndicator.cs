using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace QuickLook.DicomRT {
 public sealed partial class ViewerControl {
  readonly DispatcherTimer backgroundIndicatorTimer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(300)};
  readonly TextBlock backgroundIndicatorText=Theme.Text("",11,Theme.Accent);
  Border backgroundIndicator;int backgroundImageLoads,backgroundOverlayLoads,backgroundDoseSums;
  UIElement BuildBackgroundIndicator(){
   var row=new StackPanel{Orientation=Orientation.Horizontal};row.Children.Add(new ProgressBar{IsIndeterminate=true,Width=28,Height=3,Margin=new Thickness(0,0,8,0),Foreground=Theme.Accent});row.Children.Add(backgroundIndicatorText);
   backgroundIndicator=new Border{Background=Theme.Brush("#E0182029"),CornerRadius=new CornerRadius(5),Padding=new Thickness(9,6,9,6),Margin=new Thickness(8),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,IsHitTestVisible=false,Visibility=Visibility.Collapsed,Child=row};
   backgroundIndicatorTimer.Tick+=(s,e)=>UpdateBackgroundIndicator();return backgroundIndicator;
  }
  void UpdateBackgroundIndicator(){
   if(backgroundIndicator==null||disposed)return;var jobs=new List<string>();
   if(elapsed.IsRunning&&!LoadCompletion.IsCompleted)jobs.Add(!scanComplete?"Finding RT / images":"Loading image context");
   if(folderSearch!=null||imageSearch!=null)jobs.Add("Searching folders");
   if(backgroundOverlayLoads>0)jobs.Add("Image fusion");
   if(backgroundDoseSums>0)jobs.Add("Dose sum");
   if(backgroundImageLoads>0)jobs.Add("CT / MR slices");
   if(threeDView?.IsPreparing==true||mprThreeD?.IsPreparing==true)jobs.Add("3D surfaces");
   if(centralPlayback?.IsPreparing==true)jobs.Add("DRR / outlines");
   backgroundIndicatorText.Text=string.Join(" · ",jobs);
   backgroundIndicator.Visibility=jobs.Count>0?Visibility.Visible:Visibility.Collapsed;
  }
 }
}