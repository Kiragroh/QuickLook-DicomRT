using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Dicom;
using QuickLook.DicomRT;

internal static class BeamInteractionScenarios
{
    const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance;
    static void Set(object o,string n,object v)=>o.GetType().GetField(n,Flags).SetValue(o,v);
    static T Get<T>(object o,string n)=>(T)o.GetType().GetField(n,Flags).GetValue(o);
    static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,Flags).Invoke(o,args);
    public static void Run(Action<bool,string> check)
    {
        using(var mlc=new MlcPlaybackControl()){
            var plan=new PlanData();plan.Beams.Add(new PlanBeam{ControlPoints={new ControlPoint(),new ControlPoint()}});plan.Beams.Add(new PlanBeam{ControlPoints={new ControlPoint(),new ControlPoint()}});mlc.SetPlan(plan);
            check(Get<CheckBox>(mlc,"showDrr").IsChecked==false,"DRR defaults off for immediate MLC interaction");
            var slider=Get<Slider>(mlc,"cursor");slider.Value=1.95;var aperture=Get<FrameworkElement>(mlc,"aperture");
            var wheel=new MouseWheelEventArgs(Mouse.PrimaryDevice,0,-120){RoutedEvent=Mouse.PreviewMouseWheelEvent};aperture.RaiseEvent(wheel);
            check(wheel.Handled&&Math.Abs(slider.Value-2.0)<1e-8&&Get<ComboBox>(mlc,"beams").SelectedIndex==1,"wheel over MLC crosses field boundary");
            slider.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,0,120){RoutedEvent=Mouse.PreviewMouseWheelEvent});check(Math.Abs(slider.Value-1.0)<1e-8,"wheel over timeline navigates same CP sequence");
        }
        int rem=0;check(MlcTimeline.RecordedWheelStep(2,10,-60,ref rem)==2&&MlcTimeline.RecordedWheelStep(2,10,-60,ref rem)==3,"high resolution deltas accumulate to a cached recorded control point");
        using(var pane=new SlicePane()){
            var volume=new VolumeData{Width=11,Height=11,Depth=11,SpacingX=1,SpacingY=1,SpacingZ=1,AxisX=new Vec3(1,0,0),AxisY=new Vec3(0,1,0),AxisZ=new Vec3(0,0,1)};
            var geometry=SliceGeometry.Create(new RenderScene{Volume=volume,Plane="Axial",Focus=new Vec3(5,5,5)});
            Set(pane,"pickGeometry",geometry);Set(pane,"pickRect",new Rect(10,20,110,110));int picks=0;Vec3 last=new Vec3();pane.Picked+=(p,v)=>{picks++;last=v;};
            Call(pane,"PickAt",new Point(65,75));check((last-new Vec3(5,5,5)).Length<1e-8,"drag uses displayed plane geometry");
            Call(pane,"PickAt",new Point(200,-10));check(picks==2&&(last-geometry.WorldAt(1,0)).Length<1e-8,"drag continues with clamping outside image bounds");
        }
        using(var viewer=new ViewerControl()){
            var data=new DicomDataset();data.Add(DicomTag.PatientName,"Example^One");data.Add(DicomTag.PatientID,"DEMO-ONE");Set(viewer,"currentEntry",new DicomEntry{Dataset=data});Call(viewer,"Redraw");
            check(Get<TextBlock>(viewer,"patientIdentity").Text=="Example One  ·  ID: DEMO-ONE","patient name and ID from current dataset");
            Set(viewer,"currentEntry",null);Call(viewer,"Redraw");check(!Get<TextBlock>(viewer,"patientIdentity").Text.Contains("DEMO-ONE"),"old identity clears when source unavailable");
        }
        // The projected mesh is an L, not its convex hull; internal triangle edges must disappear.
        var mesh=new ThreeDMeshData();mesh.Points.AddRange(new[]{new Vec3(-20,0,-20),new Vec3(20,0,-20),new Vec3(20,0,0),new Vec3(0,0,0),new Vec3(0,0,20),new Vec3(-20,0,20)});
        mesh.Indices.AddRange(new[]{0,1,2,0,2,3,0,3,4,0,4,5});string reason;var projection=BeamProjection.Create(new PlanBeam{PatientPosition="HFS",SourceAxisDistance=1000},new ControlPoint(),Matrix4.Identity,out reason);
        var renderer=typeof(ViewerControl).Assembly.GetType("QuickLook.DicomRT.MlcProjectionRenderer");
        var outline=(Geometry)renderer.GetMethod("Silhouette",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{mesh,Matrix4.Identity,projection,50d,500,System.Threading.CancellationToken.None});
        check(outline.StrokeContains(new Pen(Brushes.White,.005),new Point(.5,.4),.0001,ToleranceType.Absolute),"concave projected boundary retained");
        check(!outline.StrokeContains(new Pen(Brushes.White,.005),new Point(.4,.6),.0001,ToleranceType.Absolute),"internal mesh diagonals absent from silhouette");
        check(!outline.StrokeContains(new Pen(Brushes.White,.005),new Point(.6,.4),.0001,ToleranceType.Absolute),"convex hull shortcut not used");
    }
}
