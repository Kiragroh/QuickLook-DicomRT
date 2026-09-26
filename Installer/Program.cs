using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QuickLook.DicomRT.Installer
{
    internal static class Program
    {
        [STAThread] public static int Main(string[] args)
        {
            try
            {
                var assembly=Assembly.GetExecutingAssembly();VerifiedPayload payload;
                using(var hash=assembly.GetManifestResourceStream("Payload.sha256"))using(var archive=assembly.GetManifestResourceStream("Payload.zip"))
                {
                    if(hash==null||archive==null)throw new InvalidDataException("This installer has no embedded plugin package.");
                    using(var text=new StreamReader(hash))payload=Payload.Verify(archive,text.ReadToEnd().Trim());
                }
                if(args.Length==1&&args[0]=="--verify-payload")
                {Console.WriteLine("PASS: embedded payload verified; "+payload.Files.Count+" files; SHA256 "+payload.ArchiveHash);return 0;}
                if(args.Length!=0){Console.Error.WriteLine("Usage: QuickLook-DicomRT-Setup.exe [--verify-payload]");return 2;}
                var app=new Application();return app.Run(new SetupWindow(payload));
            }
            catch(Exception ex)
            {
                if(args.Length>0)Console.Error.WriteLine("Payload verification failed: "+ex.Message);
                else MessageBox.Show("The installer could not verify its plugin package.\n\n"+ex.Message,"DICOM RT setup",MessageBoxButton.OK,MessageBoxImage.Error);
                return 1;
            }
        }
    }
    internal sealed class SetupWindow : Window
    {
        static readonly Brush Ink=new SolidColorBrush(Color.FromRgb(234,243,246)),Muted=new SolidColorBrush(Color.FromRgb(163,185,192)),Accent=new SolidColorBrush(Color.FromRgb(108,212,190));
        readonly TextBlock status;readonly Button install,refresh;readonly VerifiedPayload payload;string executable;bool busy;
        public SetupWindow(VerifiedPayload package)
        {
            payload=package;Title="DICOM RT for QuickLook · Setup";Width=740;Height=540;MinWidth=680;MinHeight=520;WindowStartupLocation=WindowStartupLocation.CenterScreen;Background=new SolidColorBrush(Color.FromRgb(13,19,24));Foreground=Ink;FontFamily=new FontFamily("Segoe UI");
            var icon=new BitmapImage(new Uri("pack://application:,,,/QuickLook-DicomRT-Setup;component/Assets/icon.png"));Icon=icon;
            var root=new Grid{Margin=new Thickness(34)};root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
            var heading=new StackPanel{Orientation=Orientation.Horizontal};heading.Children.Add(new Image{Source=icon,Width=68,Height=68,Margin=new Thickness(0,0,20,0)});var names=new StackPanel();names.Children.Add(Text("DICOM RT",30,Ink));names.Children.Add(Text("QuickLook plugin · version "+Payload.Version,14,Muted));heading.Children.Add(names);root.Children.Add(heading);
            var body=new StackPanel{Margin=new Thickness(0,24,0,20)};body.Children.Add(Text("CT and MR, MPR, DICOM tags, RT structures, dose, MLC, DVH and 3D previews.",16,Ink));
            body.Children.Add(Text("Install for your Windows account. No administrator rights required.",13,Muted,14));
            body.Children.Add(Text("QuickLook will restart when files change. The existing DICOM RT folder is backed up; other plugins remain installed.",13,Muted,12));
            body.Children.Add(Text("Install location",11,Accent,18));body.Children.Add(Text(InstallEngine.TargetDirectory,12,Muted,5));
            status=Text("Checking QuickLook …",13,Accent,20);body.Children.Add(status);Grid.SetRow(body,1);root.Children.Add(body);
            var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
            var get=Button("Get QuickLook");get.Click+=(s,e)=>{try{Process.Start(new ProcessStartInfo("https://github.com/QL-Win/QuickLook/releases"){UseShellExecute=true});}catch(Exception){status.Text="Open github.com/QL-Win/QuickLook/releases to install QuickLook.";}};buttons.Children.Add(get);
            refresh=Button("Check again");refresh.Click+=(s,e)=>FindHost();buttons.Children.Add(refresh);
            install=Button("Install / Update");install.Background=Accent;install.Foreground=new SolidColorBrush(Color.FromRgb(13,19,24));install.FontWeight=FontWeights.SemiBold;install.Click+=async(s,e)=>await InstallAsync();buttons.Children.Add(install);Grid.SetRow(buttons,2);root.Children.Add(buttons);Content=root;
            Closing+=(s,e)=>{if(busy)e.Cancel=true;};Loaded+=(s,e)=>FindHost();
        }
        static TextBlock Text(string value,double size,Brush brush,double top=0)=>new TextBlock{Text=value,FontSize=size,Foreground=brush,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,top,0,0)};
        static Button Button(string label)=>new Button{Content=label,Foreground=Ink,Background=new SolidColorBrush(Color.FromRgb(29,44,52)),BorderBrush=new SolidColorBrush(Color.FromRgb(57,80,89)),Padding=new Thickness(16,10,16,10),Margin=new Thickness(8,0,0,0),FontSize=13,MinHeight=40};
        void FindHost()
        {
            executable=InstallEngine.FindQuickLook();var unsupported=InstallEngine.UnsupportedHostReason(executable);
            install.IsEnabled=executable!=null&&unsupported==null;
            status.Text=unsupported??(executable==null?"Install QuickLook first, then click Check again.":"QuickLook found. Embedded plugin package verified.");
        }
        async Task InstallAsync()
        {
            if(busy)return;busy=true;install.IsEnabled=false;refresh.IsEnabled=false;
            try
            {
                var result=await Task.Run(()=>InstallEngine.Install(payload,executable,message=>Dispatcher.Invoke(new Action(()=>status.Text=message))));
                status.Text=result.RestartWarning??("Ready. "+result.Updated+" files updated; "+result.Unchanged+" unchanged. Select a DICOM file in Explorer and press Space.");
                if(result.BackupPath!=null)status.Text+="\nBackup: "+result.BackupPath;
                install.Content="Installed";
            }
            catch(Exception ex){status.Text="Installation did not complete. "+ex.Message;install.IsEnabled=true;}
            finally{busy=false;refresh.IsEnabled=true;}
        }
    }
}
