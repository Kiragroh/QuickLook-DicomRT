using System;
using System.IO;
using System.Windows;
using QuickLook.DicomRT;
class Program
{
    [STAThread] static int Main(string[] args)
    {
        if(args.Length<1){Console.WriteLine("Usage: DicomRT.Harness.exe <DICOM path> [--verify]");return 2;}
        var app=new Application();var control=new ViewerControl();
        var window=new Window {Title="DICOM RT · Entwicklungsvorschau",Width=1480,Height=920,MinWidth=1000,MinHeight=650,Content=control};
        bool verify=args.Length>1 && args[1]=="--verify";int exitCode=0;if(verify){window.Left=-30000;window.Top=-30000;window.ShowInTaskbar=false;window.ShowActivated=false;}
        window.Loaded+=async (s,e)=>
        {
            AppIcon.Apply(window);
            try{control.Open(args[0]);await control.LoadCompletion;Console.WriteLine($"UI_LOADED image={control.HasImage} selectedFileVisible={control.SelectedFileVisible} stacks={control.StackCount} structures={control.StructureCount} firstImageMs={control.FirstImageMilliseconds:0} firstRtMs={control.FirstRtMilliseconds:0} firstPlanMs={control.FirstPlanMilliseconds:0} indexMs={control.IndexMilliseconds:0} referencedPlanSelected={control.ReferencedPlanSelected}");if(verify){exitCode=control.HasImage&&control.StackCount>0&&control.ReferencedPlanSelected?0:1;window.Close();}}
            catch(Exception ex){Console.WriteLine("UI_FAILED "+ex.GetType().Name);exitCode=1;if(verify)window.Close();}
        };
        window.Closed+=(s,e)=>control.Dispose();app.Run(window);return exitCode;
    }
}
