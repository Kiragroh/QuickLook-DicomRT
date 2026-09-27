using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QuickLook.DicomRT
{
    internal static class ViewerSnapshot
    {
        static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject
        {for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T item)yield return item;foreach(var nested in Descendants<T>(child))yield return nested;}}
        public static void AttachMenu(FrameworkElement view,Func<FrameworkElement> target=null,string name="View")
        {
            var menu=view.ContextMenu??new ContextMenu{Background=Theme.Panel,Foreground=Theme.Foreground};
            var save=new MenuItem{Header="Save view as image...",ToolTip="Save only this view as PNG; use the top PNG button for the entire viewer"};
            save.Click+=(s,e)=>{var identity=ExportIdentity.GetContext(view);var dialog=new Microsoft.Win32.SaveFileDialog{Filter="PNG image|*.png",FileName=identity.FileName(name,".png"),Title="Save view as image"};if(dialog.ShowDialog(Window.GetWindow(view))!=true)return;try{Save(identity.Stamp(Capture(target?.Invoke()??view)),dialog.FileName);}catch(Exception){MessageBox.Show("Unable to save this view.","Save image");}};
            menu.Items.Add(save);view.ContextMenu=menu;
        }
        public static BitmapSource Capture(FrameworkElement root)
        {
            var restore=new List<Action>();
            try{
                foreach(var three in (root is ThreeDControl single?new[]{single}:Descendants<ThreeDControl>(root)).Where(t=>t.IsVisible)){
                    var gpu=three.CaptureSurface;if(gpu==null||!(gpu.View.Parent is Panel parent))continue;
                    var image=new Image{Source=gpu.Capture(),Stretch=Stretch.Fill,IsHitTestVisible=false};parent.Children.Insert(parent.Children.IndexOf(gpu.View)+1,image);var previous=gpu.View.Visibility;gpu.View.Visibility=Visibility.Hidden;restore.Add(()=>{parent.Children.Remove(image);gpu.View.Visibility=previous;});
                }
                root.UpdateLayout();var bitmap=new RenderTargetBitmap(Math.Max(1,(int)Math.Ceiling(root.ActualWidth)),Math.Max(1,(int)Math.Ceiling(root.ActualHeight)),96,96,PixelFormats.Pbgra32);bitmap.Render(root);bitmap.Freeze();return bitmap;
            }finally{foreach(var action in restore)action();}
        }
        public static void Save(BitmapSource image,string path)
        {var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var file=File.Create(path))encoder.Save(file);}
    }
}
