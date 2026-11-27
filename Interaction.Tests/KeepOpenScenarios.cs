using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using QuickLook.DicomRT;
using QuickLook.Common.Plugin;

static class KeepOpenScenarios
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    static object Field(object target,string name)=>target.GetType().GetField(name,F).GetValue(target);
    static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    public static void Run(Action<bool,string> check)
    {
        var plugin=new QuickLook.Plugin.DicomRT.Plugin();var context=new ContextObject();
        var presenter=new ContentControl();presenter.SetBinding(ContentControl.ContentProperty,new Binding("ViewerContent"){Source=context});
        var host=new Window{Content=presenter,Width=1100,Height=800,Left=-30000,Top=-30000,ShowActivated=false,ShowInTaskbar=false};
        var root=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"QuickLook-keep-open-test-"+Guid.NewGuid().ToString("N"));
        var fixture=RtOnlyScenarios.FixtureFolder(root);var path=System.IO.Path.Combine(fixture,"RTPLAN.dcm");
        ViewerControl retained=null;Window detached=null,secondDetached=null;
        try{
            host.Show();plugin.View(path,context);Pump();
            retained=(ViewerControl)context.ViewerContent;
            var deadline=DateTime.UtcNow.AddSeconds(30);
            while(!retained.LoadCompletion.IsCompleted&&DateTime.UtcNow<deadline){Pump();System.Threading.Thread.Sleep(5);}
            check(retained.LoadCompletion.IsCompleted&&retained.StructureCount>0,"Keep-open test starts with a fully loaded RT case");
            var plan=Field(retained,"selectedPlan");
            var button=FindButtons(retained).FirstOrDefault(b=>b.Content?.ToString()=="Keep open");
            check(button!=null&&button.IsEnabled,"QuickLook preview offers Keep open");
            typeof(ViewerControl).GetField("zoom",F).SetValue(retained,2.5d);
            var cache=Field(retained,"pixelCache");
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
            detached=Application.Current.Windows.Cast<Window>().SingleOrDefault(w=>w!=host&&ReferenceEquals(w.Content,retained));
            check(detached!=null&&detached.Owner==null&&detached.ShowInTaskbar,"Kept viewer has an independent normal taskbar window");
            check(detached.ResizeMode==ResizeMode.CanResize&&detached.WindowStyle==WindowStyle.SingleBorderWindow,"Kept window can resize and maximize");
            check((double)Field(retained,"zoom")==2.5&&ReferenceEquals(cache,Field(retained,"pixelCache")),"Keeping a preview preserves its instance, zoom and cache");
            check(context.ViewerContent!=retained&&!button.IsEnabled,"Preview releases viewer and repeated detachment is disabled");
            check(ReferenceEquals(plan,Field(retained,"selectedPlan"))&&retained.StructureCount>0,"Retained window preserves loaded RT selection and structures");
            plugin.Cleanup();check(!(bool)Field(retained,"disposed"),"QuickLook cleanup does not dispose a kept viewer");
            plugin.View(path,context);Pump();
            var next=(ViewerControl)context.ViewerContent;
            check(next!=retained&&ReferenceEquals(detached.Content,retained),"Next preview cannot replace the kept viewer");
            FindButtons(next).First(b=>b.Content?.ToString()=="Keep open").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
            secondDetached=Application.Current.Windows.Cast<Window>().Single(w=>ReferenceEquals(w.Content,next));
            host.Close();plugin.Cleanup();Pump();
            check(detached.IsVisible&&!(bool)Field(retained,"disposed")&&!(bool)Field(next,"disposed"),"Closing the preview leaves both kept viewers alive");
            detached.Close();Pump();check((bool)Field(retained,"disposed")&&!(bool)Field(next,"disposed"),"Closing one kept window releases only its viewer");
            secondDetached.Close();Pump();check((bool)Field(next,"disposed"),"Closing the second kept window releases its viewer");
        }finally{plugin.Cleanup();detached?.Close();secondDetached?.Close();host.Close();retained?.Dispose();
            var full=System.IO.Path.GetFullPath(root);var temp=System.IO.Path.GetFullPath(System.IO.Path.GetTempPath()).TrimEnd(System.IO.Path.DirectorySeparatorChar)+System.IO.Path.DirectorySeparatorChar;
            if(full.StartsWith(temp,StringComparison.OrdinalIgnoreCase)&&System.IO.Path.GetFileName(full).StartsWith("QuickLook-keep-open-test-",StringComparison.Ordinal))System.IO.Directory.Delete(full,true);
        }
    }
    static System.Collections.Generic.IEnumerable<Button> FindButtons(DependencyObject parent)
    {
        for(int i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);i++){
            var child=System.Windows.Media.VisualTreeHelper.GetChild(parent,i);if(child is Button b)yield return b;
            foreach(var nested in FindButtons(child))yield return nested;
        }
    }
}
