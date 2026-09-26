using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using QuickLook.DicomRT;

internal static class AbsoluteIsodoseScenarios
{
    static DoseGrid Dose(float maximum)
    {
        var volume = new VolumeData { Width = 29, Height = 3, Depth = 2, SpacingX = 1, SpacingY = 1, SpacingZ = 1, AxisX = new Vec3(1, 0, 0), AxisY = new Vec3(0, 1, 0), AxisZ = new Vec3(0, 0, 1), Max = maximum };
        volume.Values = Enumerable.Range(0, 29 * 3 * 2).Select(i => maximum * (i % 29) / 28).ToArray();
        return (DoseGrid)typeof(DoseGrid).GetMethod("FromDerivedVolume", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { volume, "synthetic", "Synthetic ramp" });
    }
    public static void Run(Action<bool, string> check)
    {
        var levels = IsodoseConfiguration.AutomaticLevels(27.699, true);
        check(levels.SequenceEqual(Enumerable.Range(1, 13).Select(i => i * 2d)), "27.699 Gy maximum generates 2, 4, ..., 26 Gy integer thresholds");
        check(IsodoseConfiguration.AutomaticLevels(68, true).SequenceEqual(Enumerable.Range(1, 13).Select(i => i * 5d)), "68 Gy maximum generates 5, 10, ..., 65 Gy thresholds");
        check(IsodoseConfiguration.AutomaticLevels(.8, true).Length == 0, "Sub-Gy maximum never fabricates a positive whole-Gy contour");
        double[] parsed; string error;
        check(IsodoseConfiguration.TryParse("2; 4.25; 6.50", true, out parsed, out error) && parsed.SequenceEqual(new[] { 2d, 4.25d, 6.5d }), "Manual levels preserve up to two decimals exactly");
        check(!IsodoseConfiguration.TryParse("4.251", true, out parsed, out error), "Three decimal places are rejected instead of silently rounded");
        check(!IsodoseConfiguration.TryParse("4,25", true, out parsed, out error), "Ambiguous decimal comma is rejected rather than split into two thresholds");
        check(IsodoseConfiguration.TryParse(string.Join(";", Enumerable.Range(1, 24)), true, out parsed, out error) && parsed.Length == 24, "24 user levels are accepted");
        check(!IsodoseConfiguration.TryParse(string.Join(";", Enumerable.Range(1, 25)), true, out parsed, out error), "More than 24 user levels are rejected");
        check(!IsodoseConfiguration.IsPhysicalGy(new DoseGrid { Units = "RELATIVE" }) && !IsodoseConfiguration.IsPhysicalGy(new DoseGrid { Units = "GY", DoseType = "EFFECTIVE" }), "Relative and nonphysical doses do not claim physical Gy isodoses");
        check(IsodoseConfiguration.AutomaticLevels(27.699, false).SequenceEqual(Enumerable.Range(1, 10).Select(i => i * 10d)), "Nonphysical dose defaults explicitly remain ten percentage levels");
        var low = Dose(27.699f);
        var scene = new RenderScene { AbsoluteIsodoses = true, IsoColorMaximum = low.Maximum, Isodoses = true, DoseWash = false, IsoLevels = levels, Native = new PixelPlane { Width = 29, Height = 3, Values = new float[87] }, Entry = new DicomEntry { HasGeometry = true, SpacingX = 1, SpacingY = 1, AxisX = new Vec3(1, 0, 0), AxisY = new Vec3(0, 1, 0) } };
        scene.Doses.Add(new DoseOverlay { Dose = low });
        var raster = SliceRaster.Render(scene, 29, 3, CancellationToken.None);
        check(raster.Isolines.Select(l => l.DoseLevel).Distinct().OrderBy(x => x).SequenceEqual(levels), "Raster contains every generated absolute Gy level");
        check(raster.Isolines.All(l => Math.Abs(l.A.X - l.DoseLevel * 28 / low.Maximum) < .00001 && Math.Abs(l.B.X - l.DoseLevel * 28 / low.Maximum) < .00001), "Actual contour positions use integer Gy thresholds, not rounded percentage labels");
        scene.IsoLevels = new[] { 4.25d, 30d };
        var high = Dose(55.398f); scene.Doses.Add(new DoseOverlay { Dose = high });
        raster = SliceRaster.Render(scene, 29, 3, CancellationToken.None);
        var decimalLines = raster.Isolines.Where(l => l.DoseLevel == 4.25).ToArray();
        check(decimalLines.Any(l => Math.Abs(l.A.X - 4.25 * 28 / low.Maximum) < .00001) && decimalLines.Any(l => Math.Abs(l.A.X - 4.25 * 28 / high.Maximum) < .00001), "Two visible doses share the same 4.25 Gy threshold despite different maxima");
        var highOnly = raster.Isolines.Where(l => l.DoseLevel == 30).ToArray();
        check(highOnly.Length > 0 && highOnly.All(l => Math.Abs(l.A.X - 30 * 28 / high.Maximum) < .00001), "Levels above a grid maximum are skipped only for that grid");
        scene.IsoColors[4.25] = 0x123456; double r, g, b; SliceRaster.IsodoseColor(scene, 4.25, out r, out g, out b);
        check(r == 0x12 && g == 0x34 && b == 0x56, "Absolute decimal Gy level has one shared custom color");
        scene.AbsoluteIsodoses = false; scene.IsoLevels = new[] { 10d }; scene.Doses.Clear(); low.Units = "RELATIVE"; scene.Doses.Add(new DoseOverlay { Dose = low });
        raster = SliceRaster.Render(scene, 29, 3, CancellationToken.None);
        check(raster.Isolines.Count > 0 && raster.Isolines.All(l => Math.Abs(l.A.X - 2.8) < .00001), "Relative fallback contour remains ten percent of its grid maximum");
    }
}
