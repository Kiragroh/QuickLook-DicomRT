using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace QuickLook.DicomRT
{
    public sealed class TagNode : INotifyPropertyChanged
    {
        public TagRow Row { get; set; }
        public List<TagNode> Children { get; set; } = new List<TagNode>();
        public string Label => string.IsNullOrEmpty(Row.Tag) ? Row.Name.Trim() : Row.Tag + "  " + Row.Name.Trim() + "  · " + Row.VR;
        public string Value => Row.Value;
        private bool expanded;
        public bool IsExpanded { get => expanded; set { expanded = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded))); ExpansionChanged?.Invoke(this); } }
        private bool selected;
        public bool IsSelected { get => selected; set { if (selected == value) return; selected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); } }
        public Action<TagNode> ExpansionChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        public static List<TagNode> Build(IEnumerable<TagRow> rows)
        {
            var roots = new List<TagNode>(); var index = new Dictionary<string, TagNode>();
            foreach (var row in rows)
            {
                var node = new TagNode { Row = row }; string path = row.Path;
                int split = string.IsNullOrEmpty(row.Tag) ? path.LastIndexOf('[') : path.LastIndexOf('/');
                TagNode parent;
                if (split >= 0 && index.TryGetValue(path.Substring(0, split), out parent)) parent.Children.Add(node); else roots.Add(node);
                index[path] = node;
            }
            return roots;
        }
        public static List<TagNode> Search(IEnumerable<TagNode> nodes, string query)
        {
            var result = new List<TagNode>();
            foreach (var node in nodes)
            {
                bool matches = new[] { node.Row.Tag, node.Row.Name, node.Row.Path, node.Row.Value, node.Row.VR }.Any(s => s?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
                var children = Search(node.Children, query);
                if (matches || children.Count > 0) result.Add(new TagNode { Row = node.Row, Children = children, IsExpanded = true });
            }
            return result;
        }
    }

    internal sealed class TagTreeControl : TreeView
    {
        private readonly HashSet<string> expanded = new HashSet<string>();
        private List<TagNode> roots = new List<TagNode>();
        private TagRow selectedRow;
        private bool filtering, changingItems;
        private TagDetailPopup detailPopup;
        public TagTreeControl()
        {
            Background = Theme.Panel; Foreground = Theme.Foreground; BorderThickness = new Thickness(0);
            VirtualizingStackPanel.SetIsVirtualizing(this, true); VirtualizingStackPanel.SetVirtualizationMode(this, VirtualizationMode.Recycling);
            var itemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(TagItemsPanel)));
            ItemsPanel = itemsPanel;
            var style = new Style(typeof(TreeViewItem));
            style.Setters.Add(new Setter(TreeViewItem.IsExpandedProperty, new Binding("IsExpanded") { Mode = BindingMode.TwoWay }));
            style.Setters.Add(new Setter(TreeViewItem.IsSelectedProperty, new Binding("IsSelected") { Mode = BindingMode.TwoWay }));
            style.Setters.Add(new Setter(ItemsControl.ItemsPanelProperty, itemsPanel));
            style.Setters.Add(new Setter(Control.ForegroundProperty, Theme.Foreground));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(3)));
            style.Setters.Add(new Setter(Control.TemplateProperty, System.Windows.Markup.XamlReader.Parse(@"
<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='TreeViewItem'>
 <Grid><Grid.ColumnDefinitions><ColumnDefinition Width='18'/><ColumnDefinition/></Grid.ColumnDefinitions><Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition/></Grid.RowDefinitions>
  <ToggleButton x:Name='Expander' Width='16' Height='22' VerticalAlignment='Top' Focusable='False' IsChecked='{Binding IsExpanded,RelativeSource={RelativeSource TemplatedParent},Mode=TwoWay}'>
   <ToggleButton.Template><ControlTemplate TargetType='ToggleButton'><Border Background='Transparent'><Path x:Name='Arrow' Data='M 5 6 L 10 11 L 5 16' Stroke='#A9B5BC' StrokeThickness='1.5'/></Border><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='Arrow' Property='Data' Value='M 3 8 L 8 13 L 13 8'/><Setter TargetName='Arrow' Property='Stroke' Value='#64B5F6'/></Trigger></ControlTemplate.Triggers></ControlTemplate></ToggleButton.Template>
  </ToggleButton>
  <Border x:Name='Row' Grid.Column='1' Padding='4' Background='Transparent' CornerRadius='3'><ContentPresenter x:Name='PART_Header' ContentSource='Header'/></Border>
  <ItemsPresenter x:Name='Children' Grid.Row='1' Grid.Column='1'/>
 </Grid><ControlTemplate.Triggers><Trigger Property='IsExpanded' Value='False'><Setter TargetName='Children' Property='Visibility' Value='Collapsed'/></Trigger><Trigger Property='HasItems' Value='False'><Setter TargetName='Expander' Property='Visibility' Value='Hidden'/></Trigger><Trigger Property='IsSelected' Value='True'><Setter TargetName='Row' Property='Background' Value='#233D56'/></Trigger></ControlTemplate.Triggers>
</ControlTemplate>")));
            ItemContainerStyle = style;
            var template = new HierarchicalDataTemplate(typeof(TagNode)) { ItemsSource = new Binding("Children"), ItemContainerStyle = style };
            var panel = new FrameworkElementFactory(typeof(StackPanel));
            var label = new FrameworkElementFactory(typeof(TextBlock)); label.SetBinding(TextBlock.TextProperty, new Binding("Label")); label.SetValue(TextBlock.FontSizeProperty, 11.0); label.SetValue(TextBlock.ForegroundProperty, Theme.Accent); panel.AppendChild(label);
            var value = new FrameworkElementFactory(typeof(TextBlock)); value.SetBinding(TextBlock.TextProperty, new Binding("Value")); value.SetValue(TextBlock.FontSizeProperty, 11.0); value.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis); value.SetValue(FrameworkElement.MaxWidthProperty, 650.0); panel.AppendChild(value);
            panel.SetBinding(FrameworkElement.ToolTipProperty, new Binding("Row.Path")); template.VisualTree = panel; ItemTemplate = template;
            SelectedItemChanged += (s, e) => { if (!changingItems && e.NewValue is TagNode node) selectedRow = node.Row; };
            PreviewMouseLeftButtonDown += OpenDetails;
            Unloaded += (s, e) => { detailPopup?.Close(); detailPopup = null; };
        }
        public void SetRows(IEnumerable<TagRow> rows, bool reset)
        {
            if (reset) expanded.Clear(); selectedRow = null; filtering = false; roots = TagNode.Build(rows);
            Action<TagNode> init = null;
            init = node => { node.IsExpanded = expanded.Contains(node.Row.Path); node.ExpansionChanged = n => { if (n.IsExpanded) expanded.Add(n.Row.Path); else expanded.Remove(n.Row.Path); }; foreach (var child in node.Children) init(child); };
            foreach (var root in roots) init(root);
        }
        public void Filter(string query)
        {
            bool clearSearch = filtering && string.IsNullOrEmpty(query);
            filtering = !string.IsNullOrEmpty(query);
            var visible = filtering ? TagNode.Search(roots, query) : roots;
            var branch = new List<TagNode>();
            bool found = selectedRow != null && FindBranch(visible, selectedRow, branch);
            if (clearSearch && found)
                foreach (var ancestor in branch.Take(branch.Count - 1)) ancestor.IsExpanded = true;
            changingItems = true;
            try { ItemsSource = visible; }
            finally { changingItems = false; }
            if (found && (filtering || clearSearch))
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                {
                    if (!ReferenceEquals(ItemsSource, visible) || !ReferenceEquals(selectedRow, branch.Last().Row)) return;
                    Reveal(branch);
                }));
        }

        private static bool FindBranch(IEnumerable<TagNode> nodes, TagRow row, List<TagNode> branch)
        {
            foreach (var node in nodes)
            {
                branch.Add(node);
                if (ReferenceEquals(node.Row, row) || FindBranch(node.Children, row, branch)) return true;
                branch.RemoveAt(branch.Count - 1);
            }
            return false;
        }

        private void Reveal(List<TagNode> branch)
        {
            ItemsControl owner = this;
            foreach (var node in branch)
            {
                owner.UpdateLayout();
                var item = owner.ItemContainerGenerator.ContainerFromItem(node) as TreeViewItem;
                if (item == null)
                {
                    FindItemsPanel(owner, owner)?.Reveal(owner.Items.IndexOf(node));
                    owner.UpdateLayout();
                    item = owner.ItemContainerGenerator.ContainerFromItem(node) as TreeViewItem;
                }
                if (item == null) return;
                if (node == branch.Last()) { item.IsSelected = true; item.BringIntoView(); }
                owner = item;
            }
        }

        private static TagItemsPanel FindItemsPanel(DependencyObject visual, ItemsControl owner)
        {
            if (visual is TagItemsPanel panel && ItemsControl.GetItemsOwner(panel) == owner) return panel;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(visual); i++)
            {
                var match = FindItemsPanel(VisualTreeHelper.GetChild(visual, i), owner);
                if (match != null) return match;
            }
            return null;
        }

        private void OpenDetails(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2) return;
            var visual = e.OriginalSource as DependencyObject;
            while (visual != null && !(visual is TreeViewItem))
            {
                if (visual is ToggleButton) return;
                visual = VisualTreeHelper.GetParent(visual);
            }
            if (!(visual is TreeViewItem item) || !(item.DataContext is TagNode node)) return;
            item.IsSelected = true;
            e.Handled = true;
            detailPopup?.Close();
            detailPopup = new TagDetailPopup(node.Row);
            var owner = Window.GetWindow(this);
            if (owner != null) detailPopup.Owner = owner;
            detailPopup.Show();
        }
    }

    public sealed class TagItemsPanel : VirtualizingStackPanel
    {
        public void Reveal(int index) { if (index >= 0) BringIndexIntoView(index); }
    }
}
