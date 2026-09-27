using System;
using System.Collections;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using QuickLook.DicomRT;

static class DvhCurveScenarios
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static object Get(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
    static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,F).Invoke(o,args);
    static void Property(object o,string n,object v)=>o.GetType().GetProperty(n,F).SetValue(o,v,null);
    static object Add(object plot,StructureRoi roi,double[] doses,double[] volumes)
    {
        var result=new DvhResult();Property(result,"DoseValues",doses);Property(result,"CumulativeVolumePercent",volumes);
        Property(result,"DoseUnits","GY");Property(result,"EstimatedVolumeCc",12d);Property(result,"SampledVolumeCc",12d);
        Property(result,"Status",DvhStatus.Complete);Property(result,"Dmin",0d);Property(result,"Dmax",10d);Property(result,"Dmean",5d);
        var type=plot.GetType().GetNestedType("Curve",BindingFlags.Public);var curve=Activator.CreateInstance(type);
        foreach(var pair in new[]{Tuple.Create("Roi",(object)roi),Tuple.Create("Result",(object)result),Tuple.Create("Visible",(object)true),Tuple.Create("Color",(object)Brushes.DeepSkyBlue)})type.GetField(pair.Item1).SetValue(curve,pair.Item2);
        ((IList)Get(plot,"Curves")).Add(curve);return curve;
    }
    public static void Run(Action<bool,string> check)
    {
        using(var view=new DvhControl())
        {
            var plot=(FrameworkElement)Get(view,"plot");plot.Measure(new Size(470,274));plot.Arrange(new Rect(0,0,470,274));
            var a=new StructureRoi{Name="Target"};var b=new StructureRoi{Name="Organ"};
            var curve=Add(plot,a,new[]{0d,10d},new[]{100d,0d});
            var point=new Point(252,122);
            check(ReferenceEquals(Call(plot,"FindCurve",point),curve),"DVH picks the rendered interpolated segment, not only sampled points");
            check((bool)Call(plot,"SelectAt",point)&&ReferenceEquals(view.FocusedStructure,a),"DVH curve click uses shared focus selection");
            Call(plot,"SelectAt",point);check(view.FocusedStructure==null,"Clicking focused DVH curve restores all curves");
            check(Call(plot,"FindCurve",new Point(252,150))==null&&Call(plot,"FindCurve",new Point(40,122))==null,"DVH ignores empty chart and axis margins");
            var duplicate=Add(plot,b,new[]{0d,5d,5d,10d},new[]{95d,90d,20d,0d});
            check(ReferenceEquals(Call(plot,"FindCurve",new Point(252,52)),duplicate),"DVH hit testing handles repeated doses and vertical segments");
            duplicate.GetType().GetField("Visible").SetValue(duplicate,false);
            check(Call(plot,"FindCurve",new Point(252,52))==null,"Hidden DVH curves cannot be selected");
            view.FocusStructure(a);check(ReferenceEquals(Call(plot,"FindCurve",point),curve),"Legend focus and direct curve selection share the same structure");
            plot.Arrange(new Rect(0,0,870,474));point=new Point(452,222);
            check(ReferenceEquals(Call(plot,"FindCurve",point),curve),"DVH hit testing follows resized plot bounds");
            Call(plot,"UpdateHover",point);var tooltip=(ToolTip)Get(plot,"curveTip");var stack=(StackPanel)tooltip.Content;
            check(((TextBlock)stack.Children[0]).Text=="Target","DVH hover identifies the actual hit structure");
            var metrics=((TextBlock)stack.Children[1]).Text;
            check(metrics.Contains("cm³")&&metrics.Contains("Dmean")&&metrics.Contains("Dmedian")&&metrics.Contains("Dmax")&&metrics.Contains("Dmin")&&metrics.Contains("D98")&&metrics.Contains("D2"),"DVH hover includes volume and all requested dose metrics");
            check(view.CalculationCount==0,"DVH hover, selection and resize do not recalculate curves");
            Call(plot,"HideHover");check(!tooltip.IsOpen,"DVH hover closes when leaving the plot");
            // Dense curves exercise the bounded search with a realistic maximum ROI count.
            var dense=new double[2049];var vol=new double[2049];for(int i=0;i<dense.Length;i++){dense[i]=i*10d/2048;vol[i]=100-i*100d/2048;}
            for(int i=0;i<126;i++)Add(plot,new StructureRoi{Name="ROI "+i},dense,vol);
            var watch=System.Diagnostics.Stopwatch.StartNew();for(int i=0;i<1000;i++)Call(plot,"FindCurve",point);watch.Stop();
            Console.WriteLine("DVH hit test: 128 curves / 2048 bins, 1000 probes: "+watch.ElapsedMilliseconds+" ms");
            check(ReferenceEquals(Call(plot,"FindCurve",point),curve),"Focused DVH curve wins exact overlap ties");
        }
    }
}
