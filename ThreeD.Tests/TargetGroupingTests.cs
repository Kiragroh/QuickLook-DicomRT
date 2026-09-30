using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Threading;
using QuickLook.DicomRT;
internal static class TargetGroupingTests
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Get<T>(object o,string n)=>(T)o.GetType().GetField(n,F).GetValue(o);
    static object[] Parts(ThreeDControl c)=>((IEnumerable)Get<object>(c,"prepared").GetType().GetField("Parts").GetValue(Get<object>(c,"prepared"))).Cast<object>().Where(p=>p.GetType().GetField("Roi").GetValue(p)!=null).ToArray();
    static void Ready(ThreeDControl c)=>FrameBenchmark.Pump(()=>Get<object>(c,"prepared")!=null&&Get<object>(c,"pending")==null,20);
    public static void Run()
    {
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        var target=QualityTests.Sphere();target.Name="Volume 7";target.InterpretedType=null;target.Number=7;target.StructureSopUid="1.2.3";
        var organ=QualityTests.Sphere();organ.Name="Parotis_L";organ.InterpretedType=null;
        var plan=new PlanData{StructureSopUid="1.2.3"};plan.TargetRoiNumbers.Add(7);
        var scene=new RenderScene{Plan=plan,Structures={new RoiOverlay{Roi=target,ClassificationPlan=plan},new RoiOverlay{Roi=organ,ClassificationPlan=plan}}};
        using(var c=new ThreeDControl()){
            c.PreloadScene(scene);Ready(c);var original=Parts(c).Single();
            if(original.GetType().GetField("Roi").GetValue(original)!=target)throw new Exception("Plan target did not build as default surface");
            var changed=new RenderScene{Structures=scene.Structures};c.PreloadScene(changed);Ready(c);
            if(Parts(c).Length!=0)throw new Exception("Old plan target leaked into new plan grouping");
            Get<CheckBox>(c,"organs").IsChecked=true;Ready(c);
            if(Parts(c).Single().GetType().GetField("Roi").GetValue(Parts(c).Single())!=organ)throw new Exception("Organ name fallback did not produce surface");
            c.PreloadScene(scene);Ready(c);
            if(!Parts(c).Any(p=>ReferenceEquals(p,original)))throw new Exception("Returning to plan did not reuse original target mesh");
        }
        Console.WriteLine("PASS plan-scoped target surfaces, named organ toggle and mesh reuse across plan changes");
    }
}
