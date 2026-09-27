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
        check(widget.Width==300&&widget.Height==260,"LINAC has a larger dedicated viewport");
        var overlay=(FrameworkElement)type.GetField("contextOverlay",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(playback);
        var slice=(FrameworkElement)type.GetField("fieldArrangement",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(playback);
        check(ReferenceEquals(widget.Parent,slice.Parent),"LINAC and slice share the movable scaling group");
        playback.Measure(new Size(1400,1100));playback.Arrange(new Rect(0,0,1400,1100));playback.UpdateLayout();
        var card=(FrameworkElement)overlay.GetType().GetField("card",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(overlay);double x=System.Windows.Controls.Canvas.GetLeft(card),size=card.Width;
        overlay.GetType().GetMethod("MoveBy",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(overlay,new object[]{-60d,20d});
        check(System.Windows.Controls.Canvas.GetLeft(card)<x,"orientation group can move inside the aperture area");
        overlay.GetType().GetMethod("ResizeBy",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(overlay,new object[]{-40d});check(card.Width<size&&widget.Width==300&&slice.Width==300,"group resize scales both views without reflowing source render geometry");
        modelType.GetMethod("Set").Invoke(widget,new object[]{90d,0d});source=new RotateTransform3D(g).Transform(new Point3D(0,0,1));check(Math.Abs(source.X-1)<1e-8,"Couch change does not rotate the gantry source");
        var plan=new PlanData();plan.Beams.Add(new PlanBeam{PatientPosition="HFS",Name="A",ControlPoints={new ControlPoint{Couch=0},new ControlPoint{Couch=0}}});plan.Beams.Add(new PlanBeam{PatientPosition="HFS",Name="B",ControlPoints={new ControlPoint{Couch=45}}});playback.SetPlan(plan);
        var patient=(Model3DGroup)modelType.GetField("patientHost",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(widget);var before=patient.Children[0].Transform.Transform(new Point3D(0,0,.55));
        var cursor=(System.Windows.Controls.Slider)type.GetField("cursor",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(playback);var same=patient.Children[0];cursor.Value=.5;check(ReferenceEquals(same,patient.Children[0]),"MLC playback reuses patient geometry between control points");cursor.Value=2;var after=patient.Children[0].Transform.Transform(new Point3D(0,0,.55));
        check((before-new Point3D()).Length<1e-9&&(after-before).Length<1e-9,"Mixed-couch plan keeps the same assumed head isocenter across beam boundaries");
        foreach(string position in new[]{"HFS","FFS","HFP","FFP"})foreach(string region in new[]{"HEAD","CHEST"})
        {
            var context=new PlanBeam{PatientPosition=position};modelType.GetMethod("SetContext").Invoke(widget,new object[]{context,region,false});string cue;var head=PatientOrientation.ToIec(position).Transform(new Vec3(0,0,.70-PatientOrientation.SchematicAnchor(region,false,out cue)));
            var table=(Model3DGroup)modelType.GetField("couchTop",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(widget);double edge=position.StartsWith("HF")?table.Bounds.Y+table.Bounds.SizeY:table.Bounds.Y;
            check(Math.Abs(edge-head.Y)<1e-9,"Couch ends at schematic head for "+position+" "+region);
        }
        var rotating=new PlanData();rotating.Beams.Add(new PlanBeam{PatientPosition="HFS",ControlPoints={new ControlPoint{Gantry=0,Couch=0,Collimator=350,CollimatorRotationDirection="CC",GantryRotationDirection="NONE",CouchRotationDirection="NONE"},new ControlPoint{Gantry=0,Couch=0,Collimator=10}}});playback.SetPlan(rotating);cursor.Value=.5;
        var dial=modelType.GetField("collimator",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(widget);double angle=(double)dial.GetType().GetProperty("Angle").GetValue(dial);check(Math.Abs(angle)<1e-8,"Collimator dial follows directed control-point interpolation across zero");
        playback.SetPlan(new PlanData());angle=(double)dial.GetType().GetProperty("Angle").GetValue(dial);check(double.IsNaN(angle),"Empty plan clears the collimator angle");
        playback.Dispose();
    }
}
