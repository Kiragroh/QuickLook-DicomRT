using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using QuickLook.DicomRT;

static class FieldPlaybackCacheScenarios
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
    static object Get(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
    static Task<Dictionary<PlanBeam,Geometry>> Work(object o,string n)=>(Task<Dictionary<PlanBeam,Geometry>>)Get(o,n);
    static ControlPoint Point(double aperture)=>new ControlPoint {Isocenter=new Vec3(10,10,10),Gantry=90,XJaws=new[]{-aperture,aperture},YJaws=new[]{-4d,4d}};
    public static void Run(Action<bool,string> check)
    {
        var a=new PlanBeam{Number=1,PatientPosition="HFS",SourceAxisDistance=1000,ControlPoints={Point(3),Point(7)}};
        var b=new PlanBeam{Number=2,PatientPosition="HFS",SourceAxisDistance=1000,ControlPoints={Point(5),Point(8)}};
        var arc=new PlanBeam{Number=3,PatientPosition="HFS",SourceAxisDistance=1000,ControlPoints={Point(3),Point(7)}};arc.ControlPoints[1].Gantry=110;
        var scene=new RenderScene{Plane="Axial",Focus=new Vec3(10,10,10),Plan=new PlanData{Beams={a,b,arc}},PlanToImage=Matrix4.Identity,ActiveBeam=a,ActiveControlPoint=a.ControlPoints[0],
            Volume=new VolumeData{Width=21,Height=21,Depth=21,SpacingX=1,SpacingY=1,SpacingZ=1,AxisX=new Vec3(1,0,0),AxisY=new Vec3(0,1,0),AxisZ=new Vec3(0,0,1)}};
        var type=typeof(ViewerControl).Assembly.GetType("QuickLook.DicomRT.FieldArrangementDrawing");var method=type.GetMethod("PrepareShapes",BindingFlags.Static|BindingFlags.NonPublic);
        Func<object> prepare=()=>method.Invoke(null,new object[]{scene,SliceGeometry.Create(scene),new Rect(0,0,1,1),null});
        var state=prepare();var background=Work(state,"Background");var shapes=background.GetAwaiter().GetResult();var gray=shapes[b];
        check(shapes.Count==2&&gray.IsFrozen,"Fixed first-CP field outlines are prepared and frozen together");
        check(ReferenceEquals(Work(state,"Active"),background),"First active CP reuses the already prepared reference aperture");
        scene.ActiveControlPoint=a.ControlPoints[1];state=prepare();var active=Work(state,"Active");active.GetAwaiter().GetResult();
        check(ReferenceEquals(background,Work(state,"Background"))&&ReferenceEquals(gray,Work(state,"Background").Result[b]),"Playback retains the exact ready gray geometry when active CP changes");
        check(!ReferenceEquals(active,background)&&active.Result.Count==1&&active.Result.ContainsKey(a),"Only the active fixed-field aperture is recalculated");
        scene.ActiveBeam=b;scene.ActiveControlPoint=b.ControlPoints[1];state=prepare();Work(state,"Active").GetAwaiter().GetResult();
        check(ReferenceEquals(background,Work(state,"Background")),"Switching active fields preserves background cache");
        scene.ActiveBeam=arc;scene.ActiveControlPoint=arc.ControlPoints[0];state=prepare();scene.ActiveControlPoint=arc.ControlPoints[1];state=prepare();
        check(ReferenceEquals(background,Work(state,"Background"))&&Work(state,"Active")==null,"Arc CP playback leaves waiting fields ready without scheduling static aperture work");
        scene.ActiveBeam=null;scene.ActiveControlPoint=null;state=prepare();
        check(ReferenceEquals(background,Work(state,"Background"))&&Work(state,"Active")==null,"Neutral all-fields mode retains outlines without foreground work");
        scene.Focus=new Vec3(10,10,11);state=prepare();var moved=Work(state,"Background");moved.GetAwaiter().GetResult();
        check(!ReferenceEquals(background,moved),"Changing slice plane invalidates geometry rather than retaining stale outlines");
        scene.PlanToImage=new Matrix4((double[])Matrix4.Identity.Values.Clone());state=prepare();
        check(ReferenceEquals(moved,Work(state,"Background")),"Equivalent transform instances do not discard prepared background fields");
        scene.Zoom=2;state=prepare();Work(state,"Background").GetAwaiter().GetResult();
        check(!ReferenceEquals(moved,Work(state,"Background")),"Zoomed slice extent gets correctly recomputed outlines");
    }
}
