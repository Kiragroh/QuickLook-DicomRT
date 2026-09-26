using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using QuickLook.DicomRT;
internal static class PopupSmoke
{
 static IEnumerable<T> Children<T>(DependencyObject parent)where T:DependencyObject{for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);if(child is T item)yield return item;foreach(var nested in Children<T>(child))yield return nested;}}
 public static void Run()
 {
  using(var viewer=new ViewerControl())
  {
   var window=new Window{Content=viewer,Width=900,Height=700,Left=-30000,Top=-30000,ShowInTaskbar=false,ShowActivated=false};
   try
   {
    window.Show();viewer.UpdateLayout();var button=Children<Button>(viewer).Single(b=>(b.ToolTip as string)?.StartsWith("Image fusion")==true);button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ContextIdle);
    var combo=(ComboBox)typeof(ViewerControl).GetField("overlaySeries",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(viewer);var slider=(Slider)typeof(ViewerControl).GetField("blend",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(viewer);
    if(PresentationSource.FromVisual(combo)==null)throw new Exception("Fusion popup did not open");
    if(combo.TryFindResource(typeof(ComboBox))==null||combo.Style==null||slider.Style==null)throw new Exception("Fusion popup missing viewer-local dark theme resources");
    window.Hide();Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ContextIdle);if(combo.IsVisible)throw new Exception("Fusion popup remains visible after viewer window hides");
   }
   finally{window.Close();}
  }
 }
}
