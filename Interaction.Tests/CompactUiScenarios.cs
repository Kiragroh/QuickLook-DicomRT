using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuickLook.DicomRT;

internal static class CompactUiScenarios
{
    const BindingFlags Static=BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public,Instance=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static Type Type(string name)=>typeof(ViewerControl).Assembly.GetType("QuickLook.DicomRT."+name);
    static object Call(string type,string name,params object[] args)=>Type(type).GetMethod(name,Static).Invoke(null,args);
    static object Field(object target,string name)=>target.GetType().GetField(name,Instance).GetValue(target);
    static void Set(object target,string name,object value)=>target.GetType().GetField(name,Instance).SetValue(target,value);
    public static void Run(Action<bool,string> check)
    {
        var previousContext=System.Threading.SynchronizationContext.Current;ViewerControl early;
        try{System.Threading.SynchronizationContext.SetSynchronizationContext(null);early=new ViewerControl();}finally{System.Threading.SynchronizationContext.SetSynchronizationContext(previousContext);}
        using(early){var pending=(Task)Field(early,"rtSliceCompletion");var frame=new System.Windows.Threading.DispatcherFrame();var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(5)};var deadline=Stopwatch.StartNew();timer.Tick+=(s,e)=>{if(pending.IsCompleted||deadline.Elapsed.TotalSeconds>5)frame.Continue=false;};timer.Start();System.Windows.Threading.Dispatcher.PushFrame(frame);timer.Stop();check(pending.Status==TaskStatus.RanToCompletion,"RT-only bounds created before an application context marshal completion to the UI");}
        var plan=new PlanData{Label="None",Entry=new DicomEntry{SopUid="test-plan"}};plan.Beams.Add(new PlanBeam());
        check((string)Call("RtObjectLabels","Plan",plan,2)=="RT-Plan 2 · 1 fields","placeholder plan receives display fallback");
        check(plan.Label=="None","fallback does not rewrite source plan label");
        plan.Label="";check(((string)Call("RtObjectLabels","Plan",plan,1)).StartsWith("RT-Plan 1"),"empty plan fallback");plan.Label="Original plan";
        var a=new DoseGrid{Label="Original dose",SummationType="PLAN",Units="GY",Maximum=10,PlanUid="test-plan",Entry=new DicomEntry{SopUid="dose-a"}};
        var b=new DoseGrid{Label=a.Label,SummationType="PLAN",Units="GY",Maximum=20,PlanUid="test-plan",Entry=new DicomEntry{SopUid="dose-b"}};
        check((string)Call("RtObjectLabels","Dose",a,1)!=(string)Call("RtObjectLabels","Dose",b,2),"same-name source doses have distinct choices");
        using(var viewer=new ViewerControl()){
            ((System.Collections.Generic.List<PlanData>)Field(viewer,"planData")).Add(plan);((System.Collections.Generic.List<DoseGrid>)Field(viewer,"doses")).AddRange(new[]{a,b});
            Type("ViewerControl").GetMethod("RefreshPlanChoices",Instance).Invoke(viewer,null);
            var choices=((ComboBox)Field(viewer,"plans")).Items.Cast<object>().ToArray();
            check(choices.Any(c=>ReferenceEquals(Field(c,"Dose"),b)),"display choices retain exact source grid identity");
            Set(viewer,"changing",true);var picker=(ComboBox)Field(viewer,"plans");picker.SelectedItem=choices.First(c=>ReferenceEquals(Field(c,"Dose"),a));string first=picker.ToolTip as string;picker.SelectedItem=choices.First(c=>ReferenceEquals(Field(c,"Dose"),b));string second=picker.ToolTip as string;check(first!=second&&second.Contains("20 Gy"),"closed picker tooltip follows changed dose selection and metadata");Set(viewer,"changing",false);
            var original=(string)Type("ViewerControl").GetMethod("DosePlanLabel",Instance).Invoke(viewer,new object[]{a});
            check(original=="Original plan"&&a.Label=="Original dose","sum identity retains original plan/source labels");
            var sum=new DoseGrid{Label="Sum original identity"};var displayed=(string)Type("ViewerControl").GetMethod("DoseDisplay",Instance).Invoke(viewer,new object[]{sum});
            check(displayed=="Generated sum · Sum original identity"&&sum.Label=="Sum original identity","generated sum does not impersonate source dose ordinal");
        }
        var beam=new PlanBeam();for(int i=0;i<180;i++)beam.ControlPoints.Add(new ControlPoint{XJaws=new[]{-40d,40d},YJaws=new[]{-60d,60d},MlcLayers={new MlcLayer{Type="MLCX",Boundaries=Enumerable.Range(0,61).Select(x=>(double)x-30).ToArray(),Positions=Enumerable.Repeat(150d,120).ToArray()}}});
        var entries=new object[16];var values=new double[16];var miniatures=new double[16];Parallel.For(0,16,i=>{entries[i]=Call("BeamExtentCache","Get",beam);values[i]=(double)entries[i].GetType().GetProperty("Value",Instance).GetValue(entries[i]);miniatures[i]=(double)entries[i].GetType().GetProperty("Miniature",Instance).GetValue(entries[i]);});
        check(values.All(v=>v==values[0])&&miniatures.All(v=>v==66),"concurrent cold access calculates consistent full and miniature extents");
        check(entries.All(e=>ReferenceEquals(e,entries[0])),"concurrent extent queries share the installed weak cache entry");
        double cached=(double)entries[0].GetType().GetProperty("Value",Instance).GetValue(entries[0]);
        check(cached==(double)Call("BeamExtentCache","Calculate",beam),"extent cache preserves numerical formula");
        check((double)entries[0].GetType().GetProperty("Miniature",Instance).GetValue(entries[0])==66,"3D miniature retains jaw-based framing");
        check(!ReferenceEquals(Call("BeamExtentCache","Get",new PlanBeam()),entries[0]),"different source beam gets distinct extent cache");
        var weak=WeakBeam();for(int i=0;i<3&&weak.IsAlive;i++){GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();}check(!weak.IsAlive,"weak extent cache does not retain a closed beam");
        var watch=Stopwatch.StartNew();for(int i=0;i<200;i++)Call("BeamExtentCache","Calculate",beam);watch.Stop();long scan=watch.ElapsedTicks;watch.Restart();for(int i=0;i<200;i++)MlcPlaybackControl.BeamExtent(beam);watch.Stop();Console.WriteLine("Extent benchmark, 180 CP / 60 leaf pairs / 200 queries: scan {0:0.00} ms; cached {1:0.00} ms (informational)",scan*1000d/Stopwatch.Frequency,watch.Elapsed.TotalMilliseconds);
        Func<double[],double[],Rect> bounds=(x,y)=>(Rect)Call("MlcAperture","TipBounds",x,y,100d);
        check(bounds(new[]{10d,-10d},new[]{-20d,20d}).IsEmpty&&bounds(new[]{0d,0d},null).IsEmpty,"crossed/equal jaws stay closed for tips");
        check(bounds(null,new[]{-20d,20d}).Width==200&&bounds(new[]{-20d,20d},null).Height==200,"missing jaw axis does not invent clipping");
        check(!(bool)Call("MlcAperture","KnownJaw",new[]{double.NaN,20d})&&!(bool)Call("MlcAperture","KnownJaw",new double[0]),"absent/invalid jaws produce no guide");
        var output=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"compact-review");Directory.CreateDirectory(output);
        foreach(double scale in new[]{1d,1.5,2d}){
            var toolbar=new StackPanel{Orientation=Orientation.Horizontal,Background=new SolidColorBrush(Color.FromRgb(17,19,20))};
            foreach(string mode in new[]{"Native","Axial","Coronal","Sagittal","MPR + 3D","3D"}){var button=(Button)Call("ViewButtons","Create",mode);toolbar.Children.Add(button);check(((StackPanel)button.Content).Children.OfType<TextBlock>().Any(t=>t.Text.Length>0),"view button retains visible label");}
            toolbar.Measure(new Size(1024,70));toolbar.Arrange(new Rect(0,0,1024,toolbar.DesiredSize.Height));toolbar.UpdateLayout();
            check(toolbar.ActualHeight<=34,"compact view controls fit one 34 DIP row");Save(toolbar,1024,40,scale,Path.Combine(output,"toolbar-"+scale+".png"));
            foreach(int width in new[]{1024,1600})using(var viewer=new ViewerControl()){
                Set(viewer,"layersVisible",true);Set(viewer,"tagsVisible",true);Type("ViewerControl").GetMethod("UpdatePanels",Instance).Invoke(viewer,null);
                viewer.Measure(new Size(width,800));viewer.Arrange(new Rect(0,0,width,800));viewer.UpdateLayout();
                check(((Grid)((FrameworkElement)Field(viewer,"leftPanel")).Parent).ActualWidth<=width,"whole viewer fits both sidebars at supported DIP width");Save(viewer,width,800,scale,Path.Combine(output,"viewer-"+width+"-"+scale+".png"));
            }
        }
        var aperture=(FrameworkElement)Activator.CreateInstance(Type("MlcAperture"),true);
        var cp=new ControlPoint{XJaws=new[]{-30d,30d},YJaws=new[]{-35d,35d},MlcLayers={new MlcLayer{Type="MLCX",Key="1",Boundaries=new[]{-20d,0d,20d},Positions=new[]{0d,0d,0d,0d}},new MlcLayer{Type="MLCY",Key="2",Boundaries=new[]{-15d,15d},Positions=new[]{-8d,8d}}}};
        Type("MlcAperture").GetMethod("Set",Instance).Invoke(aperture,new object[]{cp,cp,0d});Save(aperture,640,640,1,Path.Combine(output,"closed-double-layer.png"));
        var before=cp.XJaws.ToArray();Type("MlcAperture").GetProperty("ShowJawGuides").SetValue(aperture,false);Save(aperture,640,640,1,Path.Combine(output,"jaw-guides-off.png"));check(cp.XJaws.SequenceEqual(before),"guide switch never alters source jaw blocking coordinates");
    }
    [MethodImpl(MethodImplOptions.NoInlining)] static WeakReference WeakBeam(){var beam=new PlanBeam();Call("BeamExtentCache","Get",beam);return new WeakReference(beam);}
    static void Save(FrameworkElement element,int width,int height,double scale,string path){element.Measure(new Size(width,height));element.Arrange(new Rect(0,0,width,height));element.UpdateLayout();var bitmap=new RenderTargetBitmap((int)(width*scale),(int)(height*scale),96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(element);using(var file=File.Create(path)){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));encoder.Save(file);}}
}
