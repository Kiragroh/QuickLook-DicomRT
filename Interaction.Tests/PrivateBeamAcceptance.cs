using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using QuickLook.DicomRT;
internal static class PrivateBeamAcceptance
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static T Get<T>(object o,string n)=>(T)o.GetType().GetField(n,Flags).GetValue(o);
    static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,Flags).Invoke(o,args);
    public static int Run(string folder)
    {
        int result=0;var app=new Application();var viewer=new ViewerControl();
        var window=new Window{Content=viewer,Width=1200,Height=850,Left=-30000,Top=-30000,ShowActivated=false,ShowInTaskbar=false,Title="Local beam verification"};
        window.Loaded+=async(s,e)=>{
            try{
                var catalog=await Task.Run(()=>DicomCatalog.Scan(Directory.EnumerateFiles(folder).First(),CancellationToken.None));
                viewer.Open(catalog.Files.First(f=>f.Modality=="RTPLAN").Path);await viewer.LoadCompletion;
                var choices=Get<ComboBox>(viewer,"plans");var entries=choices.Items.Cast<object>().Where(c=>c.GetType().GetField("Plan").GetValue(c)!=null).ToArray();int tested=0;
                foreach(var choice in entries){
                    choices.SelectedItem=choice;Call(viewer,"SetWorkspace","MLC");Get<CheckBox>(Get<MlcPlaybackControl>(viewer,"centralPlayback"),"showDrr").IsChecked=true;
                    var start=DateTime.UtcNow;object frame=null;
                    while(true){await Task.Delay(25);var mlc=Get<MlcPlaybackControl>(viewer,"centralPlayback");frame=Get<object>(Get<object>(mlc,"aperture"),"projection");
                        if(!Get<bool>(mlc,"projectionBusy")&&!Get<DispatcherTimer>(mlc,"projectionDelay").IsEnabled&&frame!=null&&frame.GetType().GetField("Drr").GetValue(frame)!=null)break;
                        if((DateTime.UtcNow-start).TotalSeconds>60){Console.WriteLine("DIAGNOSTIC busy="+Get<bool>(mlc,"projectionBusy")+" suspended="+Get<bool>(mlc,"projectionSuspended")+" delayed="+Get<DispatcherTimer>(mlc,"projectionDelay").IsEnabled+" status="+Get<TextBlock>(mlc,"projectionStatus").Text);throw new TimeoutException();}
                    }
                    if(frame.GetType().GetField("Drr").GetValue(frame)==null)throw new InvalidOperationException("No DRR");
                    if(Get<TextBlock>(viewer,"patientIdentity").Text.Contains("unavailable"))throw new InvalidOperationException("Missing identity");tested++;
                    Console.WriteLine("PRIVATE_BEV plan_index="+tested+" drr=True ready_ms="+(DateTime.UtcNow-start).TotalMilliseconds.ToString("0"));
                }
                if(tested<2)throw new InvalidOperationException("Multi-plan test requires two plans");
                Console.WriteLine("PASS private multi-plan UI: "+tested+" plans, DRR and current identity; no images or identifiers exported");
            }catch(Exception ex){result=1;Console.WriteLine("FAIL private multi-plan UI: "+ex.GetType().Name);}finally{viewer.Dispose();window.Close();}
        };app.Run(window);return result;
    }
}
