using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using QuickLook.DicomRT;

internal static class MlcZoomScenarios
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static object Get(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
    static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,F).Invoke(o,args);
    public static void Run(Action<bool,string> check)
    {
        using(var control=new MlcPlaybackControl())
        {
            var plan=new PlanData();plan.Beams.Add(new PlanBeam{ControlPoints={new ControlPoint{XJaws=new[]{-30d,20d},YJaws=new[]{-20d,20d}},new ControlPoint{XJaws=new[]{-20d,30d},YJaws=new[]{-20d,20d}}}});control.SetPlan(plan);
            var aperture=(FrameworkElement)Get(control,"aperture");aperture.Measure(new Size(600,500));aperture.Arrange(new Rect(0,0,600,500));
            var zoom=aperture.GetType().GetProperty("Zoom");var projection=aperture.GetType().GetProperty("Projection");var cached=Activator.CreateInstance(projection.PropertyType);projection.SetValue(aperture,cached);
            Point mouse=new Point(410,180),origin=new Point(aperture.ActualWidth/2,aperture.ActualHeight/2+8);Vector anchor=mouse-origin;
            Call(control,"NavigateWheel",120,ModifierKeys.Control,mouse,false);
            double value=(double)zoom.GetValue(aperture);Vector offset=(Vector)Get(aperture,"viewOffset");
            check(value>1&&control.Position==0,"Ctrl-wheel zooms without changing the control point");
            check((offset+anchor*value-anchor).Length<1e-7,"The physical point under the mouse stays anchored during zoom");
            check(ReferenceEquals(cached,projection.GetValue(aperture)),"Zoom reuses the cached projection without requesting DRR or contours");
            Call(control,"NavigateWheel",-120,ModifierKeys.None,mouse,false);check(control.Position==1&&(double)zoom.GetValue(aperture)==value,"Plain wheel changes CP and preserves MLC zoom");
            Call(aperture,"ZoomAt",100d,mouse);check((double)zoom.GetValue(aperture)==8,"MLC zoom has a bounded maximum");
            var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(600,500,96,96,PixelFormats.Pbgra32);bitmap.Render(aperture);check(aperture.ClipToBounds,"Zoomed beam drawing remains inside its pane");
            Call(aperture,"ResetView");check((double)zoom.GetValue(aperture)==1&&((Vector)Get(aperture,"viewOffset")).Length==0,"Fit resets magnification and offset");
            var top=(StackPanel)control.Children[0];var settings=(StackPanel)((ScrollViewer)top.Children[0]).Content;
            check(!settings.Children.OfType<Button>().Any(b=>Equals(b.Tag,"MPR + 3D")),"MLC settings have no duplicate 2x2 button");
        }
        using(var viewer=new ViewerControl())
        {
            viewer.GetType().GetField("layersVisible",F).SetValue(viewer,true);Call(viewer,"UpdatePanels");
            viewer.Measure(new Size(1200,750));viewer.Arrange(new Rect(0,0,1200,750));viewer.UpdateLayout();
            var labels=(ComboBox)Get(viewer,"roiLabelMode");var row=labels.Parent as DockPanel;
            check(row!=null&&row.Children.OfType<Button>().Count()==2&&row.ActualHeight<45&&labels.ActualWidth>=60,"Structure label picker shares one constrained row with All and None");
            var modes=(System.Collections.Generic.Dictionary<string,Button>)Get(viewer,"imageModeButtons");check(modes.ContainsKey("MPR + 3D"),"The shared 2x2 navigation button remains available");
        }
    }
}
