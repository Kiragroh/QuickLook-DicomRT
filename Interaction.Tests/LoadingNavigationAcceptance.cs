using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using QuickLook.DicomRT;

internal static class LoadingNavigationAcceptance
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static T Get<T>(object o,string n)=>(T)o.GetType().GetField(n,F).GetValue(o);
    static void Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,F).Invoke(o,args);
    public static int Run(string folder)
    {
        int result=0;var app=new Application();ViewerControl viewer=null;
        var window=new Window{Width=1200,Height=850,Left=-30000,Top=-30000,ShowActivated=false,ShowInTaskbar=false};
        window.Loaded+=async(s,e)=>
        {
            try
            {
                var catalog=await Task.Run(()=>DicomCatalog.Scan(Directory.EnumerateFiles(folder).First(),CancellationToken.None));
                foreach(var entry in catalog.Files.Where(x=>x.Modality=="CT").Take(1).Concat(catalog.Files.Where(x=>x.Modality=="RTPLAN").Take(1)))
                {
                    viewer?.Dispose();viewer=new ViewerControl();window.Content=viewer;
                    var clock=Stopwatch.StartNew();double last=0,maxGap=0,maxSwitch=0;int switches=0;string wanted="Bild",plane="Coronal";
                    var heartbeat=new DispatcherTimer(DispatcherPriority.Input){Interval=TimeSpan.FromMilliseconds(40)};
                    heartbeat.Tick+=(a,b)=>{var now=clock.Elapsed.TotalMilliseconds;maxGap=Math.Max(maxGap,now-last);last=now;};heartbeat.Start();
                    try
                    {
                        viewer.Open(entry.Path);Call(viewer,"SelectImageMode",plane);
                        var modes=new[]{"MLC","DVH","3D","Bild"};
                        while(!viewer.LoadCompletion.IsCompleted)
                        {
                            var switchClock=Stopwatch.StartNew();wanted=modes[switches++%modes.Length];
                            Get<Dictionary<string,Button>>(viewer,"viewButtons")[wanted].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            maxSwitch=Math.Max(maxSwitch,switchClock.Elapsed.TotalMilliseconds);
                            if(Get<string>(viewer,"workspaceMode")!=wanted)throw new Exception("View switch rejected during loading");
                            if(clock.Elapsed.TotalSeconds>90)throw new Exception("Loading timed out");
                            await Task.WhenAny(viewer.LoadCompletion,Task.Delay(160));
                        }
                        await viewer.LoadCompletion;
                        if(Get<string>(viewer,"workspaceMode")!=wanted||(string)Get<ComboBox>(viewer,"planes").SelectedItem!=plane)throw new Exception("Late loading changed the selected view");
                        if(Get<VolumeData>(viewer,"volume")==null||Get<PlanData>(viewer,"selectedPlan")==null)throw new Exception("Matching CT / plan not ready at completion");
                        if(Get<int>(viewer,"rtQueued")!=Get<int>(viewer,"rtCompleted"))throw new Exception("Completion reported with pending RT work");
                        Console.WriteLine($"PASS loading navigation: {entry.Modality}, {switches} switches, load {clock.Elapsed.TotalSeconds:0.00}s, max switch {maxSwitch:0}ms, max input gap {maxGap:0}ms; requested view retained; CT and RT complete");
                    }
                    finally{heartbeat.Stop();}
                }
            }
            catch(Exception ex){result=1;Console.WriteLine("FAIL loading navigation: "+ex.GetType().Name+" · "+ex.Message);}
            finally{viewer?.Dispose();window.Close();}
        };
        app.Run(window);return result;
    }
}
