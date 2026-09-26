using System;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Media3D;
using QuickLook.DicomRT;
internal static class LinacOrientationScenarios
{
    public static void Run(Action<bool,string> check)
    {
        var playback=new MlcPlaybackControl();var type=typeof(MlcPlaybackControl);
        var widget=(FrameworkElement)type.GetField("orientation",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(playback);
        var modelType=widget.GetType();modelType.GetMethod("Set").Invoke(widget,new object[]{90d,80d});
        var g=(AxisAngleRotation3D)modelType.GetField("gantryRotation",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(widget);
        var c=(AxisAngleRotation3D)modelType.GetField("couchRotation",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(widget);
        var source=new RotateTransform3D(g).Transform(new Point3D(0,0,1));var support=new RotateTransform3D(c).Transform(new Point3D(0,1,0));var expected=MlcTimeline.CouchDirection(80);
        check(Math.Abs(source.X-1)<1e-8&&Math.Abs(source.Y)<1e-8&&Math.Abs(source.Z)<1e-8,"3D mini gantry transforms IEC source correctly");
        check(Math.Abs(support.X-expected[0])<1e-8&&Math.Abs(support.Y-expected[1])<1e-8,"3D mini couch rotates around isocenter in IEC fixed frame");
        check(widget.HorizontalAlignment==HorizontalAlignment.Right&&widget.VerticalAlignment==VerticalAlignment.Bottom&&widget.Width<=280,"3D mini occupies the aperture right corner");
        modelType.GetMethod("Set").Invoke(widget,new object[]{90d,0d});source=new RotateTransform3D(g).Transform(new Point3D(0,0,1));check(Math.Abs(source.X-1)<1e-8,"Couch change does not rotate the gantry source");
        var plan=new PlanData();plan.Beams.Add(new PlanBeam{PatientPosition="HFS",Name="A",ControlPoints={new ControlPoint{Couch=0},new ControlPoint{Couch=0}}});plan.Beams.Add(new PlanBeam{PatientPosition="HFS",Name="B",ControlPoints={new ControlPoint{Couch=45}}});playback.SetPlan(plan);
        var patient=(Model3DGroup)modelType.GetField("patientHost",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(widget);var before=patient.Children[0].Transform.Transform(new Point3D(0,0,.55));
        var cursor=(System.Windows.Controls.Slider)type.GetField("cursor",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(playback);var same=patient.Children[0];cursor.Value=.5;check(ReferenceEquals(same,patient.Children[0]),"MLC playback reuses patient geometry between control points");cursor.Value=2;var after=patient.Children[0].Transform.Transform(new Point3D(0,0,.55));
        check((before-new Point3D()).Length<1e-9&&(after-before).Length<1e-9,"Mixed-couch plan keeps the same assumed head isocenter across beam boundaries");
        playback.Dispose();
    }
}
