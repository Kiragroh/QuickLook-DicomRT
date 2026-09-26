using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace QuickLook.DicomRT
{
    public static class AppIcon
    {
        public static ImageSource Image => BitmapFrame.Create(new Uri("pack://application:,,,/QuickLook.DicomRT.Viewer;component/Assets/dicom-rt.ico"));
        public static void Apply(Window window)
        {
            if(window==null)return;
            window.Icon=Image;
            var handle=new WindowInteropHelper(window).Handle;
            if(handle!=IntPtr.Zero){int enabled=1;try{DwmSetWindowAttribute(handle,20,ref enabled,sizeof(int));}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}}
        }
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
    }
}
