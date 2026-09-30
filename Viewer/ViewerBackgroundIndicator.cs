using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace QuickLook.DicomRT {
 public sealed partial class ViewerControl {
  readonly DispatcherTimer backgroundIndicatorTimer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(300)};
  readonly TextBlock backgroundIndicatorText=Theme.Text("",11,Theme.Accent);
  readonly RotateTransform backgroundGearRotation=new RotateTransform();
  Border backgroundIndicator;int backgroundImageLoads,backgroundOverlayLoads,backgroundDoseSums;
  UIElement BuildBackgroundIndicator(){
   var row=new StackPanel{Orientation=Orientation.Horizontal};
   var gear=new System.Windows.Shapes.Path{Data=GearGeometry(),Fill=Theme.Accent,Width=18,Height=18,Stretch=Stretch.Fill,Margin=new Thickness(0,0,8,0),VerticalAlignment=VerticalAlignment.Center,RenderTransformOrigin=new Point(.5,.5),RenderTransform=backgroundGearRotation};row.Children.Add(gear);backgroundIndicatorText.MaxWidth=390;row.Children.Add(backgroundIndicatorText);
   backgroundIndicator=new Border{Background=Theme.Brush("#E0182029"),CornerRadius=new CornerRadius(5),Padding=new Thickness(9,6,9,6),Margin=new Thickness(8),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,IsHitTestVisible=false,Visibility=Visibility.Collapsed,Child=row};
   backgroundIndicatorTimer.Tick+=(s,e)=>UpdateBackgroundIndicator();return backgroundIndicator;
  }
  static Geometry GearGeometry(){
   var geometry=new StreamGeometry{FillRule=FillRule.EvenOdd};using(var context=geometry.Open()){
    var points=new List<Point>();for(int i=0;i<48;i++){double angle=i*Math.PI/24,r=i%6<3?9:7;points.Add(new Point(10+Math.Cos(angle)*r,10+Math.Sin(angle)*r));}
    context.BeginFigure(points[0],true,true);context.PolyLineTo(points.GetRange(1,points.Count-1),true,false);
    context.BeginFigure(new Point(13,10),true,true);context.ArcTo(new Point(7,10),new Size(3,3),0,false,SweepDirection.Clockwise,true,false);context.ArcTo(new Point(13,10),new Size(3,3),0,false,SweepDirection.Clockwise,true,false);
   }geometry.Freeze();return geometry;
  }
  void UpdateBackgroundIndicator(){
   if(backgroundIndicator==null||disposed)return;var jobs=new List<string>();
   if(elapsed.IsRunning&&!LoadCompletion.IsCompleted)jobs.Add(!scanComplete?"Finding RT / images":"Loading image context");
   if(folderSearch!=null||imageSearch!=null)jobs.Add("Searching folders");
   if(RtLoadsPending)jobs.Add($"RT objects {System.Threading.Volatile.Read(ref rtCompleted)}/{System.Threading.Volatile.Read(ref rtQueued)} · navigation available");
   if(backgroundOverlayLoads>0)jobs.Add("Image fusion");
   if(backgroundDoseSums>0)jobs.Add("Dose sum");
   if(backgroundImageLoads>0)jobs.Add("CT / MR slices");
   if(threeDView?.IsPreparing==true||mprThreeD?.IsPreparing==true)jobs.Add("3D surfaces");
   if(centralPlayback?.IsPreparing==true)jobs.Add(centralPlayback.PreparationStatus);
   backgroundIndicatorText.Text=string.Join(" · ",jobs);
   var visibility=jobs.Count>0?Visibility.Visible:Visibility.Collapsed;
   if(backgroundIndicator.Visibility!=visibility){backgroundIndicator.Visibility=visibility;backgroundGearRotation.BeginAnimation(RotateTransform.AngleProperty,visibility==Visibility.Visible?new DoubleAnimation(0,360,TimeSpan.FromSeconds(2)){RepeatBehavior=RepeatBehavior.Forever}:null);}
  }
 }
}
