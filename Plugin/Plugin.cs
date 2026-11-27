using System;
using System.IO;
using System.Windows;
using QuickLook.Common.Plugin;

namespace QuickLook.Plugin.DicomRT
{
    public sealed class Plugin : IViewer
    {
        private global::QuickLook.DicomRT.ViewerControl control;
        private Window host;
        private System.Windows.Media.ImageSource previousIcon;
        private bool previousTaskbar;
        public int Priority => 110;
        public void Init() { }
        public bool CanHandle(string path)
        {
            if (!File.Exists(path)) return false;
            var ext = Path.GetExtension(path);
            if (ext.Equals(".dcm", StringComparison.OrdinalIgnoreCase) || ext.Equals(".dicom", StringComparison.OrdinalIgnoreCase)) return true;
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (stream.Length < 132) return false;
                    stream.Position = 128;
                    return stream.ReadByte() == 68 && stream.ReadByte() == 73 && stream.ReadByte() == 67 && stream.ReadByte() == 77;
                }
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
        public void Prepare(string path, ContextObject context)
        {
            context.PreferredSize = new Size(1240, 860);
            context.Theme = Themes.Dark;
        }
        public void View(string path, ContextObject context)
        {
            Cleanup();
            control = new global::QuickLook.DicomRT.ViewerControl();
            control.Loaded += PreviewLoaded;
            control.SetKeepOpenAction(()=>KeepOpen(context));
            context.Title = "DICOM RT · v"+typeof(global::QuickLook.DicomRT.ViewerControl).Assembly.GetName().Version.ToString(3)+" · Preview";
            context.ViewerContent = control;
            context.IsBusy = false;
            control.Open(path);
        }
        private void PreviewLoaded(object sender,RoutedEventArgs e)
        {
            if(sender!=control)return;
            var window=Window.GetWindow(control);if(window==null||window==host)return;
            RestoreHost();host=window;previousIcon=host.Icon;previousTaskbar=host.ShowInTaskbar;
            global::QuickLook.DicomRT.AppIcon.Apply(host);host.ShowInTaskbar=true;
        }
        private void RestoreHost(){if(host!=null){host.Icon=previousIcon;host.ShowInTaskbar=previousTaskbar;host=null;}}
        private void KeepOpen(ContextObject context)
        {
            var kept=control;if(kept==null)return;
            var origin=Window.GetWindow(kept);
            var window=new Window{
                Title="DICOM RT · v"+typeof(global::QuickLook.DicomRT.ViewerControl).Assembly.GetName().Version.ToString(3)+" · Kept open",
                Width=origin?.ActualWidth>0?origin.ActualWidth:1240,Height=origin?.ActualHeight>0?origin.ActualHeight:860,
                MinWidth=640,MinHeight=480,ResizeMode=ResizeMode.CanResize,WindowStyle=WindowStyle.SingleBorderWindow,
                ShowInTaskbar=true,ShowActivated=origin?.IsActive??true,Background=kept.Background,Foreground=kept.Foreground
            };
            if(origin!=null){window.Left=origin.Left;window.Top=origin.Top;if(origin.WindowState==WindowState.Maximized)window.WindowState=WindowState.Maximized;}
            window.SourceInitialized+=(s,e)=>global::QuickLook.DicomRT.AppIcon.Apply(window);
            EventHandler closed=(s,e)=>{window.Content=null;kept.Dispose();};window.Closed+=closed;
            kept.Loaded-=PreviewLoaded;kept.SetKeepOpenAction(null);
            // Transfer lifetime ownership before notifying the host: its content change
            // may synchronously trigger Cleanup or the next preview.
            control=null;RestoreHost();
            var notice=new System.Windows.Controls.TextBlock{Text="This case is kept in a separate DICOM RT window.\nSelect another file and press Space to preview it.",Foreground=System.Windows.Media.Brushes.White,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(24)};context.ViewerContent=notice;
            // Give the host's content binding a chance to release the old logical parent.
            kept.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,new Action(()=>{
                try{window.Content=kept;window.Show();}
                catch(Exception){
                    window.Closed-=closed;window.Content=null;window.Close();
                    if(control==null&&ReferenceEquals(context.ViewerContent,notice)){
                        control=kept;kept.Loaded+=PreviewLoaded;kept.SetKeepOpenAction(()=>KeepOpen(context));context.ViewerContent=kept;
                    }else kept.Dispose();
                    MessageBox.Show("Unable to open a separate window. The original preview is retained when still available.","DICOM RT");
                }
            }));
        }
        public void Cleanup() { if(control!=null){control.Loaded-=PreviewLoaded;control.Dispose();control=null;}RestoreHost(); }
    }
}
