using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using QuickLook.DicomRT;

internal static class TagDetailScenarios
{
    private static readonly Assembly Viewer = typeof(ViewerControl).Assembly;
    private static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
    private static void Layout(FrameworkElement element)
    {
        element.ApplyTemplate();
        element.Measure(new Size(330, 240)); element.Arrange(new Rect(0, 0, 330, 240)); element.UpdateLayout();
        var frame = new DispatcherFrame();
        element.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame); element.UpdateLayout();
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
    private static TreeView Tree(IEnumerable<TagRow> rows)
    {
        var tree = (TreeView)Activator.CreateInstance(Viewer.GetType("QuickLook.DicomRT.TagTreeControl"), true);
        Call(tree, "SetRows", rows, true); Call(tree, "Filter", ""); Layout(tree); return tree;
    }
    public static void Run(Action<bool, string> check)
    {
        var rows = new List<TagRow>();
        for (int i = 0; i < 80; i++) rows.Add(new TagRow { Path = "root" + i, Tag = "root" + i, Name = "Synthetic header", Value = "value" });
        foreach (string prefix in new[] { "manual", "target", "closed" })
        {
            rows.Add(new TagRow { Path = prefix, Tag = prefix, Name = "Synthetic sequence", VR = "SQ" });
            for (int i = 0; i < 2; i++)
            {
                rows.Add(new TagRow { Path = prefix + "[" + i + "]", Tag = "", Name = "Item " + i });
                rows.Add(new TagRow { Path = prefix + "[" + i + "]/(0008,1155)", Tag = "(0008,1155)", Name = "Synthetic label", Value = "needle" });
            }
        }
        var tree = Tree(rows);
        var host = new Window { Content = tree, Width = 330, Height = 240, Left = -10000, Top = -10000, ShowInTaskbar = false, ShowActivated = false, WindowStyle = WindowStyle.None };
        host.Show(); Layout(tree);
        var original = ((IEnumerable<TagNode>)tree.ItemsSource).ToList();
        check(original.All(n => !n.IsExpanded), "Tag tree starts fully collapsed");
        var manual = original.Single(n => n.Row.Path == "manual"); manual.IsExpanded = true;
        Call(tree, "Filter", "needle"); Layout(tree); Call(tree, "Filter", ""); Layout(tree);
        check(manual.IsExpanded && original.Where(n => n != manual).All(n => !n.IsExpanded), "Clearing search without a selection restores manual expansion only");

        Call(tree, "Filter", "needle"); Layout(tree);
        var target = ((IEnumerable<TagNode>)tree.ItemsSource).Single(n => n.Row.Path == "target").Children[1].Children[0];
        // Materialize the search result, then select a real WPF item (including duplicate tag numbers).
        var filteredRoot = ((IEnumerable<TagNode>)tree.ItemsSource).Single(n => n.Row.Path == "target");
        var rootPanel = Descendants<TagItemsPanel>(tree).FirstOrDefault(p => ItemsControl.GetItemsOwner(p) == tree);
        check(rootPanel != null, "Tree uses a virtualizing tag panel (panels: " + Descendants<Panel>(tree).Count() + ", tag panels: " + Descendants<TagItemsPanel>(tree).Count() + ")");
        rootPanel.Reveal(tree.Items.IndexOf(filteredRoot)); Layout(tree);
        var rootItem = (TreeViewItem)tree.ItemContainerGenerator.ContainerFromItem(filteredRoot);
        check(rootItem != null, "Search target sequence container is materialized");
        rootItem.BringIntoView(); Layout(tree);
        var itemNode = filteredRoot.Children[1];
        Descendants<TagItemsPanel>(rootItem).Single(p => ItemsControl.GetItemsOwner(p) == rootItem).Reveal(rootItem.Items.IndexOf(itemNode)); Layout(tree);
        var item = (TreeViewItem)rootItem.ItemContainerGenerator.ContainerFromItem(itemNode);
        check(item != null, "Search sequence item container is materialized");
        item.BringIntoView(); Layout(tree);
        var leaf = (TreeViewItem)item.ItemContainerGenerator.ContainerFromItem(target);
        check(leaf != null, "Search leaf container is materialized");
        leaf.IsSelected = true; leaf.BringIntoView(); Layout(tree);
        check(ReferenceEquals(((TagNode)tree.SelectedItem).Row, target.Row), "Real search-result container selects the intended duplicate tag occurrence");
        Call(tree, "Filter", ""); Layout(tree);
        var selected = tree.SelectedItem as TagNode;
        var restoredRoot = original.Single(n => n.Row.Path == "target");
        check(selected != null && ReferenceEquals(selected.Row, target.Row) && ReferenceEquals(selected, restoredRoot.Children[1].Children[0]), "Clearing search selects the same row in the original tree");
        check(restoredRoot.IsExpanded && restoredRoot.Children[1].IsExpanded && !restoredRoot.Children[0].IsExpanded, "Clearing search opens only the selected ancestor branch");
        check(manual.IsExpanded && !original.Single(n => n.Row.Path == "closed").IsExpanded, "Search selection preserves unrelated manual expansion and collapse");
        var restoredItem = Descendants<TreeViewItem>(tree).FirstOrDefault(i => i.IsSelected);
        check(restoredItem != null && restoredItem.TransformToAncestor(tree).Transform(new Point()).Y >= 0 && restoredItem.TransformToAncestor(tree).Transform(new Point()).Y < tree.ActualHeight, "Selected tag remains scrolled into view after restoring a virtualized tree");
        host.Close();

        var popupType = Viewer.GetType("QuickLook.DicomRT.TagDetailPopup");
        string capped = new string('x', 8192) + " … [truncated]";
        foreach (string value in new[] { "synthetic value\nsecond line", capped, "[16384 bytes; binary payload omitted]" })
        {
            var row = new TagRow { Tag = "(0008,1155)", Name = " Synthetic label ", Value = value };
            var popup = (Window)Activator.CreateInstance(popupType, new object[] { row });
            var content = (FrameworkElement)popup.Content;
            content.Measure(new Size(540, 370)); content.Arrange(new Rect(0, 0, 540, 370)); content.UpdateLayout();
            var inputs = Descendants<TextBox>(content).ToList();
            var buttons = Descendants<Button>(content).Where(b => b.Tag is string).ToList();
            check(inputs.Count == 3 && inputs.All(t => t.IsReadOnly), "Tag popup exposes three selectable read-only fields");
            var expected = new[] { row.Tag, row.Name.Trim(), row.Value };
            check(inputs.Select(t => t.Text).SequenceEqual(expected) && buttons.Select(b => (string)b.Tag).SequenceEqual(expected), "Each copy button carries exactly its field text, preserving reader limits and binary omission");
            foreach (var button in buttons)
            {
                string copied = null;
                Call(popup, "CopyField", button.Tag, new Action<string>(text => copied = text));
                check(copied == (string)button.Tag, "Copy dispatch passes the exact field value without clipboard automation");
            }
            Call(popup, "CopyField", value, new Action<string>(text => { throw new ExternalException("synthetic clipboard busy"); }));
            check(Descendants<TextBlock>(content).Any(t => t.Text.StartsWith("Clipboard is busy.")), "Busy clipboard displays a useful retry instruction");
            popup.Close();
        }
    }
}
