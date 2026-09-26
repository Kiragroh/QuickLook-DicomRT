using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace QuickLook.DicomRT
{
    internal static class IsocenterNavigation
    {
        internal static List<Vec3> MappedPoints(PlanData plan, Matrix4 map)
        {
            var points = new List<Vec3>();
            if (plan == null || map == null) return points;
            foreach (var beam in plan.Beams)
            {
                var source = beam.ControlPoints.Count > 0 ? beam.ControlPoints.Select(cp => cp.Isocenter) : new[] { beam.Isocenter };
                foreach (var value in source)
                {
                    var point = map.Transform(value);
                    if (Finite(point.X) && Finite(point.Y) && Finite(point.Z) && !points.Any(p => (p - point).Length < .1)) points.Add(point);
                }
            }
            return points;
        }
        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    public sealed partial class ViewerControl
    {
        private readonly Dictionary<double, int> isodoseColors = new Dictionary<double, int>();
        private readonly StackPanel isodoseLegend = new StackPanel();
        private readonly Button isocenterButton = Theme.Button("ISO");
        private Popup doseColorPopup, isocenterPopup;
        private string doseLegendKey, isodoseContextSignature;
        private DoseGrid[] isodoseContext = new DoseGrid[0];
        private bool absoluteIsodoses;
        private double isodoseMaximum = 100;
        private readonly TextBlock isodoseUnitsLabel = Theme.Text("Isodose levels · % of each dose maximum", 10, Theme.Muted);
        private string IsodoseUnit => absoluteIsodoses ? "Gy" : "%";
        private readonly IsodosePreferences isodosePreferences = IsodosePreferences.Current;

        private void GlobalIsodosesChanged(bool absolute,double[] levels)
        {
            if(disposed)return;
            if(!Dispatcher.CheckAccess()){Dispatcher.BeginInvoke(new Action(()=>GlobalIsodosesChanged(absolute,levels)));return;}
            if(absolute!=absoluteIsodoses)return;
            displayedIsoLevels=(double[])levels.Clone();doseLegendKey=null;
            isoLevels.Text=string.Join("; ",levels.Select(x=>x.ToString("0.##",CultureInfo.InvariantCulture)));Redraw();
        }
        private void ApplyIsodoseLevels()
        {
            double[] levels;string error;
            if(!IsodoseConfiguration.TryParse(isoLevels.Text,absoluteIsodoses,out levels,out error)||!isodosePreferences.Save(absoluteIsodoses,levels,out error)){status.Text=error;return;}
            iso.IsChecked=true;Redraw();status.Text="Global isodose levels saved · all views and future files · "+IsodoseUnit;
        }

        private void UpdateIsodoseContext()
        {
            var context = SelectedDoses.Where(d => d.Maximum > 0 && !float.IsInfinity(d.Maximum) && !float.IsNaN(d.Maximum)).ToArray();
            var signature = string.Join(";", context.Select(d => d.Maximum.ToString("R", CultureInfo.InvariantCulture) + ":" + d.Units + ":" + d.DoseType));
            if (isodoseContext.SequenceEqual(context) && signature == isodoseContextSignature) return;
            isodoseContext = context; isodoseContextSignature = signature;
            absoluteIsodoses = context.Length > 0 && context.All(IsodoseConfiguration.IsPhysicalGy);
            isodoseMaximum = absoluteIsodoses ? context.Max(d => (double)d.Maximum) : 100;
            displayedIsoLevels = isodosePreferences.Read(absoluteIsodoses) ?? IsodoseConfiguration.AutomaticLevels(isodoseMaximum, absoluteIsodoses);
            isodoseColors.Clear(); doseLegendKey = null;
            isoLevels.Text = string.Join("; ", displayedIsoLevels.Select(v => v.ToString("0.##", CultureInfo.InvariantCulture)));
            isodoseUnitsLabel.Text = absoluteIsodoses ? "Isodose levels · Gy (up to 2 decimal places)" : "Isodose levels · % of each dose maximum (relative or nonphysical dose)";
            isoLevels.ToolTip = "1–24 levels; separate with semicolons; use a decimal point, e.g. 2; 4.25";
        }

        private UIElement BuildIsocenterButton()
        {
            isocenterButton.Visibility = Visibility.Collapsed;
            isocenterButton.ToolTip = "Go to the selected plan's isocenter";
            isocenterButton.Foreground = Theme.Accent;
            isocenterButton.Click += async (s, e) =>
            {
                var points = HasImage && currentEntry?.HasGeometry == true ? IsocenterNavigation.MappedPoints(selectedPlan, TransformToImage(selectedPlan?.FrameUid)) : new List<Vec3>();
                if (points.Count == 1) await MoveFocusAsync(points[0]);
                else if (points.Count > 1)
                {
                    if (isocenterPopup != null) isocenterPopup.IsOpen = false;
                    var choices = new StackPanel();
                    choices.Children.Add(Theme.Text("Select isocenter", 12, Theme.Accent));
                    for (int i = 0; i < points.Count; i++)
                    {
                        var point = points[i];
                        var button = Theme.Button(string.Format(CultureInfo.InvariantCulture, "ISO {0} · LPS {1:0.#}, {2:0.#}, {3:0.#} mm", i + 1, point.X, point.Y, point.Z));
                        button.Click += async (sender, args) => { isocenterPopup.IsOpen = false; await MoveFocusAsync(point); };
                        choices.Children.Add(button);
                    }
                    isocenterPopup = DarkPopup(isocenterButton, choices); isocenterPopup.IsOpen = true;
                }
            };
            return isocenterButton;
        }
        private void UpdateIsocenterButton(IReadOnlyList<Vec3> points)
        {
            bool available = HasImage && currentEntry?.HasGeometry == true && points.Count > 0;
            isocenterButton.Visibility = available ? Visibility.Visible : Visibility.Collapsed;
            isocenterButton.Content = points.Count > 1 ? "ISO ▾" : "ISO";
            isocenterButton.ToolTip = points.Count > 1 ? "Select an isocenter to navigate to" : "Go to the selected plan's isocenter";
        }
        private Popup DarkPopup(UIElement target, UIElement content)
        {
            var popup = new Popup { PlacementTarget = target, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true, Child = Theme.Box(content) };
            popup.Resources.MergedDictionaries.Add(Resources.MergedDictionaries[0]);
            return popup;
        }
        private Color IsodoseDisplayColor(double level)
        {
            double r, g, b; SliceRaster.IsodoseColor(new RenderScene { IsoColors = isodoseColors, AbsoluteIsodoses = absoluteIsodoses, IsoColorMaximum = isodoseMaximum }, level, out r, out g, out b);
            return Color.FromRgb((byte)r, (byte)g, (byte)b);
        }
        private void UpdateIsodoseLegend(List<DoseOverlay> overlays)
        {
            var visible = overlays.Where(d => d.Dose.Maximum > 0).Select(d => d.Dose).ToArray();
            var key = IsodoseUnit + "|" + string.Join(";", displayedIsoLevels.Select(p => p.ToString("R", CultureInfo.InvariantCulture) + ":" + IsodoseDisplayColor(p))) + "|" + string.Join(";", visible.Select(d => d.Maximum.ToString("R", CultureInfo.InvariantCulture) + ":" + d.Units));
            if (key == doseLegendKey) return;
            doseLegendKey = key;
            if (doseColorPopup != null) doseColorPopup.IsOpen = false;
            isodoseLegend.Children.Clear();
            isodoseLegend.Children.Add(Theme.Text("ISODOSE LEGEND · click a color", 10, Theme.Muted));
            isodoseLegend.Children.Add(Theme.Text(absoluteIsodoses ? "Absolute dose levels in Gy, shared by all visible physical doses. Click a swatch to change its line color." : "Relative or nonphysical dose: levels are percentages of each grid maximum. Click a swatch to change its line color.", 10, Theme.Muted));
            foreach (var level in displayedIsoLevels)
            {
                var row = new DockPanel();
                var swatch = Theme.Button(""); swatch.Width = 35; swatch.Height = 25; swatch.Padding = new Thickness(3);
                swatch.Content = new Border { Background = new SolidColorBrush(IsodoseDisplayColor(level)), Width = 22, Height = 10, CornerRadius = new CornerRadius(2) };
                swatch.ToolTip = "Change " + level.ToString("0.##", CultureInfo.InvariantCulture) + " " + IsodoseUnit + " isodose line color";
                swatch.Click += (s, e) => OpenIsodoseColor(swatch, level);
                DockPanel.SetDock(swatch, Dock.Left); row.Children.Add(swatch);
                string coverage = absoluteIsodoses && visible.Length > 0 && visible.All(d => d.Maximum < level) ? " · above maximum" : "";
                var label = Theme.Text(level.ToString("0.##", CultureInfo.InvariantCulture) + " " + IsodoseUnit + coverage, 11); label.VerticalAlignment = VerticalAlignment.Center;
                row.Children.Add(label); isodoseLegend.Children.Add(row);
            }
            if (visible.Length > 1) isodoseLegend.Children.Add(Theme.Text(absoluteIsodoses ? "A level is drawn only in dose grids whose maximum reaches it." : "Multiple doses: absolute levels differ; see each dose maximum below.", 10, Theme.Muted));
            if (absoluteIsodoses && displayedIsoLevels.Length == 0) isodoseLegend.Children.Add(Theme.Text("No positive whole-Gy level below this maximum. Enter decimal Gy levels above.", 10, Theme.Muted));
            if (visible.Length == 0) isodoseLegend.Children.Add(Theme.Text("No visible registered dose.", 10, Theme.Muted));
        }
        private void OpenIsodoseColor(Button target, double level)
        {
            if (doseColorPopup != null) doseColorPopup.IsOpen = false;
            var content = new StackPanel { Width = 224 };
            content.Children.Add(Theme.Text(level.ToString("0.##", CultureInfo.InvariantCulture) + " " + IsodoseUnit + " · line color", 12, Theme.Accent));
            var palette = new WrapPanel();
            Action<Color> apply = color =>
            {
                isodoseColors[level] = (color.R << 16) | (color.G << 8) | color.B;
                doseColorPopup.IsOpen = false; doseLegendKey = null; Redraw();
            };
            foreach (var hex in new[] { "#64B5F6", "#FFFFFF", "#FFD54F", "#FF7043", "#EF5350", "#AB47BC", "#26C6DA", "#66BB6A" })
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                var button = Theme.Button(""); button.Width = 48; button.Height = 26; button.Padding = new Thickness(3); button.ToolTip = hex;
                button.Content = new Border { Background = new SolidColorBrush(color), Width = 32, Height = 13 };
                button.Click += (s, e) => apply(color); palette.Children.Add(button);
            }
            content.Children.Add(palette);
            var value = new TextBox { Text = IsodoseDisplayColor(level).ToString().Replace("#FF", "#"), Margin = new Thickness(3), Padding = new Thickness(6) };
            content.Children.Add(Theme.Text("Custom RGB · #RRGGBB", 10, Theme.Muted)); content.Children.Add(value);
            var error = Theme.Text("", 10, Theme.Muted); content.Children.Add(error);
            var accept = Theme.Button("Apply color");
            accept.Click += (s, e) =>
            {
                string text = value.Text.Trim().TrimStart('#'); int rgb;
                if (text.Length != 6 || !int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rgb)) { error.Text = "Enter six hexadecimal digits, e.g. #64B5F6."; return; }
                apply(Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
            };
            content.Children.Add(accept);
            doseColorPopup = DarkPopup(target, content); doseColorPopup.IsOpen = true;
        }
        private void CloseDosePopups()
        {
            if (doseColorPopup != null) doseColorPopup.IsOpen = false;
            if (isocenterPopup != null) isocenterPopup.IsOpen = false;
        }
    }
}
