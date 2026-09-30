using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using QuickLook.DicomRT;

internal static class LoadingNavigationScenarios
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static T Get<T>(object o,string name)=>(T)o.GetType().GetField(name,F).GetValue(o);
    static object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,F).Invoke(o,args);
    public static void Run(Action<bool,string> check)
    {
        using(var viewer=new ViewerControl())
        {
            var entry=new DicomEntry{Modality="REG",SopUid="navigation-test",PatientKey="test|test",Path="navigation-test.dcm"};
            viewer.GetType().GetField("initialEntry",F).SetValue(viewer,entry);
            var submitted=Task.Run(()=>Call(viewer,"OnEntryFound",entry));
            // The scanner must return even while the dispatcher is busy handling input.
            bool returned=submitted.Wait(1000);
            PumpUntil(()=>submitted.IsCompleted);
            check(returned,"RT discovery never waits for UI publication before scanning more images");
            Call(viewer,"OnEntryFound",entry);
            var completion=(Task)viewer.GetType().GetProperty("RtLoadsCompletion",F).GetValue(viewer);
            var input=Dispatcher.CurrentDispatcher.InvokeAsync(()=>Call(viewer,"SelectImageMode","Coronal"),DispatcherPriority.Input);
            PumpUntil(()=>completion.IsCompleted&&input.Task.IsCompleted);completion.GetAwaiter().GetResult();
            check(Get<int>(viewer,"rtQueued")==1&&Get<int>(viewer,"rtCompleted")==1,"duplicate discoveries decode and publish once");
            check((string)Get<ComboBox>(viewer,"planes").SelectedItem=="Coronal","publishing RT preserves the view selected during loading");
            Call(viewer,"UpdateBackgroundIndicator");
            check(Get<Border>(viewer,"backgroundIndicator").Visibility==Visibility.Collapsed,"RT loading indicator clears after publication");
        }
        using(var viewer=new ViewerControl())
        {
            var entry=new DicomEntry{Modality="REG",SopUid="cancel-test",PatientKey="test|test",Path="cancel-test.dcm"};
            viewer.GetType().GetField("initialEntry",F).SetValue(viewer,entry);
            Call(viewer,"OnEntryFound",entry);
            var completion=(Task)viewer.GetType().GetProperty("RtLoadsCompletion",F).GetValue(viewer);
            viewer.Dispose();PumpUntil(()=>completion.IsCompleted);
            check(completion.IsCanceled&&Get<HashSet<string>>(viewer,"loadedRt").Count==0,"closing during RT loading cancels publication without waiting for the worker");
        }
        using(var viewer=new ViewerControl())
        {
            var modes=Get<Dictionary<string,Button>>(viewer,"imageModeButtons");
            foreach(var mode in new[]{"Axial","Coronal","Sagittal","MPR + 3D","Native"})
            {
                modes[mode].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                check((string)Get<ComboBox>(viewer,"planes").SelectedItem==mode,"image mode can be selected before volume arrives: "+mode);
            }
            Call(viewer,"RefreshPlanChoices");
            var views=Get<Dictionary<string,Button>>(viewer,"viewButtons");
            foreach(var mode in new[]{"MLC","DVH","3D","Bild"})
            {
                check(views[mode].IsEnabled,"view remains enabled while RT is missing: "+mode);
                views[mode].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                check(Get<string>(viewer,"workspaceMode")==mode,"RT loading permits view switch: "+mode);
            }
            Call(viewer,"OpenMpr");
            check((string)Get<ComboBox>(viewer,"planes").SelectedItem=="MPR + 3D","return to overview works before volume arrives");
        }
        using(var pane=new SlicePane())
        {
            pane.Scene=new RenderScene{Plane="Coronal",Native=new PixelPlane{Width=2,Height=2,Values=new float[4]}};
            check(Get<object>(pane,"frame")==null&&Get<string>(pane,"status").Contains("volume"),"a missing MPR volume is a placeholder, not a mislabeled native image");
        }
    }
    static void PumpUntil(Func<bool> done)
    {
        var clock=System.Diagnostics.Stopwatch.StartNew();
        while(!done()&&clock.ElapsedMilliseconds<5000)
        {
            var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);Thread.Sleep(1);
        }
        if(!done())throw new Exception("Timed out waiting for background RT work");
    }
}
