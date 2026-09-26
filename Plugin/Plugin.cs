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
            control.Loaded += (s,e) => { host = Window.GetWindow(control); if(host != null) { previousIcon = host.Icon; previousTaskbar = host.ShowInTaskbar; global::QuickLook.DicomRT.AppIcon.Apply(host); host.ShowInTaskbar = true; } };
            context.Title = "DICOM RT · Preview";
            context.ViewerContent = control;
            context.IsBusy = false;
            control.Open(path);
        }
        public void Cleanup() { control?.Dispose(); control = null; if(host != null) { host.Icon=previousIcon;host.ShowInTaskbar=previousTaskbar;host=null; } }
    }
}
