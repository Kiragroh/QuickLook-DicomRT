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
        playback.Dispose();
    }
}
