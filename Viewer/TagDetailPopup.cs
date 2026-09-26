using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace QuickLook.DicomRT
{
    internal sealed class TagDetailPopup : Window
    {
        private readonly TextBlock status = Theme.Text("Select text or copy a whole field.", 11, Theme.Muted);

        public TagDetailPopup(TagRow row)
        {
            Title = "DICOM tag"; Width = 540; Height = 370; MinWidth = 370; MinHeight = 300;
            Background = Theme.Background; Foreground = Theme.Foreground;
            ShowInTaskbar = false; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.CanResizeWithGrip;
            Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/QuickLook.DicomRT.Viewer;component/Theme.xaml", UriKind.Relative) });
            var content = new Grid { Margin = new Thickness(18) };
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition());
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            AddField(content, 0, "Tag number", row.Tag ?? "", false);
            AddField(content, 1, "Label", (row.Name ?? "").Trim(), false);
            // The reader already caps displayed values and omits binary payloads.
            AddField(content, 2, "Value", row.Value ?? "", true);
            Grid.SetRow(status, 3); status.Margin = new Thickness(0, 10, 0, 0); content.Children.Add(status);
            var shell = new DockPanel();
            var heading = new DockPanel { Margin = new Thickness(18, 10, 10, 0) };
            var close = Theme.Button("×"); close.ToolTip = "Close · Esc"; close.Click += (s, e) => Close();
            DockPanel.SetDock(close, Dock.Right); heading.Children.Add(close);
            var title = Theme.Text("DICOM TAG", 13, Theme.Accent); title.VerticalAlignment = VerticalAlignment.Center;
            title.MouseLeftButtonDown += (s, e) => DragMove(); heading.Children.Add(title);
            DockPanel.SetDock(heading, Dock.Top); shell.Children.Add(heading); shell.Children.Add(content);
            Content = new Border { BorderBrush = Theme.Brush("#345F86"), BorderThickness = new Thickness(1), Child = shell };
            PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
        }

        private void AddField(Grid content, int index, string label, string value, bool multiline)
        {
            var field = new DockPanel { Margin = new Thickness(0, 0, 0, 9) };
            var heading = new DockPanel();
            var copy = Theme.Button("Copy"); copy.FontSize = 11; copy.Padding = new Thickness(8, 3, 8, 3);
            copy.ToolTip = "Copy " + label.ToLowerInvariant(); copy.Tag = value;
            copy.Click += (s, e) => CopyField((string)copy.Tag, Clipboard.SetText);
            DockPanel.SetDock(copy, Dock.Right); heading.Children.Add(copy);
            heading.Children.Add(Theme.Text(label, 12, Theme.Accent));
            DockPanel.SetDock(heading, Dock.Top); field.Children.Add(heading);
            field.Children.Add(new TextBox
            {
                Text = value, IsReadOnly = true, Padding = new Thickness(8, 6, 8, 6),
                TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
                VerticalScrollBarVisibility = multiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled,
                HorizontalScrollBarVisibility = multiline ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto,
                VerticalContentAlignment = VerticalAlignment.Top
            });
            Grid.SetRow(field, index); content.Children.Add(field);
        }

        internal void CopyField(string value, Action<string> copy)
        {
            try { copy(value); status.Text = "Copied."; }
            catch (ExternalException) { status.Text = "Clipboard is busy. Try Copy again, or select the text and press Ctrl+C."; }
        }
    }
}
