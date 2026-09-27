using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace QuickLook.DicomRT
{
    internal static class Theme
    {
        public static readonly Brush Background = Brush("#111314"), Panel = Brush("#191C1E"), Foreground = Brush("#EDF3F9"), Muted = Brush("#A9B5BC"), Accent = Brush("#64B5F6");
        public static SolidColorBrush Brush(string color) { var b = (SolidColorBrush)new BrushConverter().ConvertFromString(color); b.Freeze(); return b; }
        public static TextBlock Text(string text, double size = 12, Brush color = null) => new TextBlock { Text = text, FontSize = size, Foreground = color ?? Foreground, Margin = new Thickness(0, 3, 0, 3), TextWrapping = TextWrapping.Wrap };
        public static Button Button(string text) => new Button { Content = text, ToolTip=text=="All"?"Show all structures":text=="None"?"Hide all structures":text=="MLC"?"Inspect field apertures, control points and beam projections":text=="DVH"?"Show cumulative dose-volume histograms":text=="3D"?"Open the standalone 3D scene":text=="Image"?"Return to image inspection":text, Padding = new Thickness(9, 5, 9, 5), Margin = new Thickness(3) };
        public static ComboBox Combo(double width = double.NaN) => new ComboBox { Width = width, MinHeight = 28, Margin = new Thickness(3), VerticalContentAlignment = VerticalAlignment.Center };
        public static Border Box(UIElement child, Thickness? padding = null) => new Border { Background = Panel, Padding = padding ?? new Thickness(12), Child = child };
    }
}
