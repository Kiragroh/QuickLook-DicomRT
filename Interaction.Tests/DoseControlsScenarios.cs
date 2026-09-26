using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using QuickLook.DicomRT;

internal static class DoseControlsScenarios
{
    static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    static void Invoke(object target, string name, params object[] values) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, values);
    static string LegendText(StackPanel panel) => string.Join(" ", panel.Children.OfType<TextBlock>().Select(t => t.Text).Concat(panel.Children.OfType<DockPanel>().SelectMany(r => r.Children.OfType<TextBlock>()).Select(t => t.Text)));
    public static void Run(Action<bool, string> check)
    {
        var scene = new RenderScene { IsoLevels = new[] { 80d, 20d, 80d, double.NaN }, IsoColors = new Dictionary<double, int> { [80] = 0x123456, [20] = 0xabcdef } };
        check(SliceRaster.IsodoseLevels(scene).SequenceEqual(new[] { 20d, 80d }), "Isodose levels normalized independently of keyed colors");
        double r, g, b; SliceRaster.IsodoseColor(scene, 80, out r, out g, out b);
        check(r == 0x12 && g == 0x34 && b == 0x56, "Custom isodose color remains associated with its percentage after level sorting");
        var snapshot = (RenderScene)typeof(RenderScene).GetMethod("Snapshot", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(scene, null);
        scene.IsoColors[80] = 0; SliceRaster.IsodoseColor(snapshot, 80, out r, out g, out b);
        check(r == 0x12 && b == 0x56, "Pending render retains an immutable isodose palette snapshot");
        double dr, dg, db; SliceRaster.DoseColor(50, out dr, out dg, out db); SliceRaster.IsodoseColor(scene, 50, out r, out g, out b);
        check(r == dr && g == dg && b == db, "New isodose level uses the matching default dose color");
        var plan = new PlanData { FrameUid = "synthetic-frame" };
        plan.Beams.Add(new PlanBeam { ControlPoints = { new ControlPoint { Isocenter = new Vec3(1, 2, 3) }, new ControlPoint { Isocenter = new Vec3(1, 2, 3) } } });
        plan.Beams.Add(new PlanBeam { ControlPoints = { new ControlPoint { Isocenter = new Vec3(10, 20, 30) }, new ControlPoint { Isocenter = new Vec3(double.NaN, 0, 0) } } });
        var type = typeof(ViewerControl).Assembly.GetType("QuickLook.DicomRT.IsocenterNavigation");
        var mapMethod = type.GetMethod("MappedPoints", BindingFlags.Static | BindingFlags.NonPublic);
        Func<Matrix4, List<Vec3>> mapped = matrix => (List<Vec3>)mapMethod.Invoke(null, new object[] { plan, matrix });
        check(mapped(null).Count == 0, "No registration never offers an isocenter target");
        var transform = new Matrix4(new double[] { 1, 0, 0, 100, 0, 1, 0, 200, 0, 0, 1, 300, 0, 0, 0, 1 });
        var points = mapped(transform);
        check(points.Count == 2 && (points[0] - new Vec3(101, 202, 303)).Length < .001, "Isocenter points use the registration, deduplicate and discard non-finite values");
        check((points[1] - new Vec3(110, 220, 330)).Length < .001, "Multiple beam isocenters remain separate explicit navigation choices");
        using (var viewer = new ViewerControl())
        {
            var legend = Field<StackPanel>(viewer, "isodoseLegend");
            var doses = Field<List<DoseGrid>>(viewer, "doses");
            doses.Add(new DoseGrid { Maximum = 27.699f, Units = "GY" });
            Invoke(viewer, "UpdateIsodoseContext");
            var colors = Field<Dictionary<double, int>>(viewer, "isodoseColors"); colors[2] = 0x123456;
            Invoke(viewer, "UpdateIsodoseLegend", new List<DoseOverlay> { new DoseOverlay { Dose = new DoseGrid { Maximum = 10, Units = "GY" } } });
            check(LegendText(legend).Contains("2 Gy") && !LegendText(legend).Contains(" %"), "Physical dose legend displays absolute Gy rather than rounded percentages");
            var swatch = (Border)legend.Children.OfType<DockPanel>().First().Children.OfType<Button>().Single().Content;
            check(((SolidColorBrush)swatch.Background).Color == Color.FromRgb(0x12, 0x34, 0x56), "Legend swatch matches the renderer custom palette");
            Set(viewer, "displayedIsoLevels", new[] { 2.25d, 4.50d });
            Invoke(viewer, "UpdateIsodoseContext");
            check(Field<double[]>(viewer, "displayedIsoLevels").SequenceEqual(new[] { 2.25d, 4.50d }), "Repeated redraw preserves manually edited decimal Gy levels");
            doses.Add(new DoseGrid { Maximum = 20, Units = "RELATIVE" });
            Invoke(viewer, "UpdateIsodoseContext");
            check(!Field<bool>(viewer, "absoluteIsodoses") && Field<double[]>(viewer, "displayedIsoLevels").Length == 10, "Mixed units switch to explicit percent defaults");
            Invoke(viewer, "UpdateIsodoseLegend", new List<DoseOverlay> { new DoseOverlay { Dose = doses[0] }, new DoseOverlay { Dose = doses[1] } });
            check(LegendText(legend).Contains("absolute levels differ") && !LegendText(legend).Contains("2 GY"), "Multi-dose legend never invents a shared absolute dose scale");
            doses.Clear(); doses.Add(new DoseGrid { Maximum = 68, Units = "GY", DoseType = "PHYSICAL" });
            Invoke(viewer, "UpdateIsodoseContext");
            check(Field<double[]>(viewer, "displayedIsoLevels").SequenceEqual(Enumerable.Range(1, 13).Select(i => i * 5d)), "A new dose context regenerates appropriate whole-Gy levels");
            Invoke(viewer, "UpdateIsocenterButton", points); var button = Field<Button>(viewer, "isocenterButton");
            check(button.Visibility == Visibility.Collapsed, "Isocenter image jump stays hidden without image geometry");
            Set(viewer, "native", new PixelPlane { Width = 1, Height = 1, Values = new float[] { 0 } });
            Set(viewer, "currentEntry", new DicomEntry { HasGeometry = true }); Invoke(viewer, "UpdateIsocenterButton", points);
            check(button.Visibility == Visibility.Visible && (string)button.Content == "ISO ▾", "Multiple mapped isocenters expose a choice button beside fusion");
            Invoke(viewer, "UpdateIsocenterButton", new List<Vec3>());
            check(button.Visibility == Visibility.Collapsed, "Isocenter button hides again after association is unavailable");
            var accent = (SolidColorBrush)viewer.Resources["Accent"];
            check(accent.Color.B > accent.Color.G && accent.Color.G > accent.Color.R, "Viewer accent is blue");
        }
    }
}
