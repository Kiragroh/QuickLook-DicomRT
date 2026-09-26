using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using QuickLook.DicomRT;

internal static partial class Program
{
    static async Task BeamResponsiveness(MlcPlaybackControl playback)
    {
        var slider=Get<Slider>(playback,"cursor");var drr=Get<CheckBox>(playback,"showDrr");var ptv=Get<CheckBox>(playback,"showPtv");var organs=Get<CheckBox>(playback,"showOrgans");
        drr.IsChecked=true;ptv.IsChecked=true;organs.IsChecked=true;
        var dispatch=new double[60];var painted=new double[60];var watch=new Stopwatch();
        for(int i=0;i<60;i++){
            watch.Restart();slider.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,0,-120){RoutedEvent=Mouse.PreviewMouseWheelEvent});dispatch[i]=watch.Elapsed.TotalMilliseconds;
            await viewer.Dispatcher.InvokeAsync(()=>viewer.UpdateLayout(),DispatcherPriority.Render);await Task.Delay(16);painted[i]=watch.Elapsed.TotalMilliseconds;
        }
        if(dispatch.OrderBy(x=>x).ElementAt(57)>100)throw new Exception("MLC wheel handler blocks interaction");
        Console.WriteLine($"MLC_RESPONSIVENESS samples=60 drr=True ptv=True organs=True dispatch_median_ms={dispatch.OrderBy(x=>x).ElementAt(30):0.00} dispatch_p95_ms={dispatch.OrderBy(x=>x).ElementAt(57):0.00} frame_with_16ms_delay_p95_ms={painted.OrderBy(x=>x).ElementAt(57):0.00}");
        if(Math.Abs(slider.Value-Math.Round(slider.Value,1))>1e-7)throw new Exception("Wheel did not retain fine tenths");
        Mode("Bild");Get<CheckBox>(viewer,"showFields").IsChecked=true;
        var selector=Get<ComboBox>(viewer,"fieldPicker");var cursor=Get<Slider>(viewer,"fieldCursor");if(selector.Items.Count<2)throw new Exception("Missing field choices");
        selector.SelectedIndex=1;cursor.Value=Math.Min(15.3,cursor.Maximum);await Call(viewer,"MoveFocusAsync",Get<ControlPoint>(viewer,"activeFieldPoint").Isocenter);Get<ComboBox>(viewer,"planes").SelectedItem="Axial";await Settle();
        var active=Get<PlanBeam>(viewer,"activeField");if(active!=selector.SelectedItem||Math.Abs(playback.LocalPosition-cursor.Value)>1e-8)throw new Exception("Fields and MLC timeline differ");
        await Save("fields-mlc-aperture.png","Selected field with actual jaw and multi-layer MLC aperture intersections; linked fine CP navigation.");
        Invoke(viewer,"OpenMpr");if(Get<string>(viewer,"workspaceMode")!="Bild"||(string)Get<ComboBox>(viewer,"planes").SelectedItem!="MPR + 3D")throw new Exception("Direct MPR navigation failed");
        Get<CheckBox>(viewer,"showFields").IsChecked=false;Mode("MLC");organs.IsChecked=false;
    }
}
