using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace QuickLook.DicomRT
{
    public sealed partial class ViewerControl
    {
        Button infoButton;Popup infoPopup;
        UIElement BuildInfoButton()
        {
            infoButton=Theme.Button("i");infoButton.ToolTip="About, developer links and keyboard shortcuts (F1)";infoButton.Click+=(s,e)=>ShowAbout();return infoButton;
        }
        Button BuildScreenshotButton()
        {
            var button=Theme.Button("PNG");button.ToolTip="Save the entire viewer, including open RT and Tags panels (Ctrl+Shift+S)";button.Click+=(s,e)=>SaveScreenshot();return button;
        }
        void ShowAbout()
        {
            if(infoPopup?.IsOpen==true){infoPopup.IsOpen=false;return;}
            var panel=new StackPanel{Width=Math.Min(500,Math.Max(280,SystemParameters.WorkArea.Width-64))};panel.Children.Add(Theme.Text("DICOM RT for QuickLook",19,Theme.Accent));
            panel.Children.Add(Theme.Text("The Space-key preview workflow, extended to DICOM images and radiotherapy objects on Windows. Inspect local plans, structures and dose without importing them into a TPS.",12));
            panel.Children.Add(Theme.Text("Developed by Maximilian Grohmann",14));panel.Children.Add(Theme.Text("Medical physicist and medical informatics specialist, Leipzig. Built with AI-assisted development. Research and inspection preview; not a clinically validated TPS.",11,Theme.Muted));
            var links=new WrapPanel();foreach(var link in new[]{Tuple.Create("GitHub / downloads","https://github.com/Kiragroh/QuickLook-DicomRT"),Tuple.Create("What is new / changelog","https://github.com/Kiragroh/QuickLook-DicomRT/blob/main/CHANGELOG.md"),Tuple.Create("Developer profile","https://kiragroh.github.io/"),Tuple.Create("QuickLook for Windows","https://github.com/QL-Win/QuickLook")}){
                var button=Theme.Button(link.Item1);button.ToolTip=link.Item2;button.Click+=(s,e)=>{try{Process.Start(new ProcessStartInfo(link.Item2){UseShellExecute=true});}catch(Exception){status.Text="Unable to open browser.";}};links.Children.Add(button);
            }panel.Children.Add(links);panel.Children.Add(Theme.Text("Keyboard & mouse",14,Theme.Accent));
            panel.Children.Add(ShortcutSection("OPEN & NAVIGATE",new[]{
                new[]{"Space","Open preview in Explorer"},
                new[]{"Ctrl + I","Go to isocenter"},
                new[]{"Ctrl + F","Search DICOM tags"},
                new[]{"Home","Fit image"},
                new[]{"F1","Open this help"}}));
            panel.Children.Add(ShortcutSection("SWITCH VIEWS",new[]{
                new[]{"Ctrl + 1","Native image"},new[]{"Ctrl + 2","Axial"},
                new[]{"Ctrl + 3","Coronal"},new[]{"Ctrl + 4","Sagittal"},
                new[]{"Ctrl + 5","Linked 2 × 2"},new[]{"Ctrl + 6","Standalone 3D"},
                new[]{"Alt + I","Image workspace"},new[]{"Alt + M","MLC workspace"},
                new[]{"Alt + D","DVH workspace"},new[]{"Alt + V","3D workspace"}}));
            panel.Children.Add(ShortcutSection("IMAGE & MLC CONTROLS",new[]{
                new[]{"Left drag","Move linked crosshair"},
                new[]{"Right drag","Custom window / level"},
                new[]{"Ctrl + wheel","Zoom image"},
                new[]{"Wheel","MLC: move by 1 control point"},
                new[]{"Shift + wheel","MLC: move by 0.1 control point"}}));
            panel.Children.Add(ShortcutSection("SAVE & EXPORT",new[]{
                new[]{"Ctrl + Shift + S","Save entire viewer as PNG, including sidebars"},
                new[]{"Right-click view","Save only that view as PNG"},
                new[]{"Right-click DVH","Export active curves, with optional metrics"}}));
            var shot=Theme.Button("Save entire viewer");shot.ToolTip="Save all visible panels, toolbars and displayed identifiers (Ctrl+Shift+S)";shot.Click+=(s,e)=>{infoPopup.IsOpen=false;SaveScreenshot();};panel.Children.Add(shot);
            infoPopup=DarkPopup(infoButton.IsVisible?(UIElement)infoButton:this,new ScrollViewer{Content=panel,MaxHeight=Math.Min(720,Math.Max(240,SystemParameters.WorkArea.Height-100)),HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});if(!infoButton.IsVisible)infoPopup.Placement=PlacementMode.Center;infoPopup.IsOpen=true;
        }
        static UIElement ShortcutSection(string title,string[][] shortcuts)
        {
            var section=new StackPanel{Margin=new Thickness(0,10,0,2)};
            var heading=Theme.Text(title,10,Theme.Muted);heading.FontWeight=FontWeights.SemiBold;heading.Margin=new Thickness(2,0,0,6);section.Children.Add(heading);
            var table=new Grid();table.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(174)});table.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
            for(int i=0;i<shortcuts.Length;i++)
            {
                table.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
                var background=new Border{Background=i%2==0?Theme.Brush("#171E24"):Theme.Brush("#141A20"),BorderBrush=Theme.Brush("#28323B"),BorderThickness=new Thickness(0,0,0,i==shortcuts.Length-1?0:1)};Grid.SetRow(background,i);Grid.SetColumnSpan(background,2);table.Children.Add(background);
                var keys=new WrapPanel{Margin=new Thickness(9,6,6,6),VerticalAlignment=VerticalAlignment.Center};
                var tokens=shortcuts[i][0].Split(new[]{" + "},StringSplitOptions.None);
                for(int k=0;k<tokens.Length;k++)
                {
                    if(k>0){var plus=Theme.Text("+",10,Theme.Muted);plus.Margin=new Thickness(3,2,3,0);keys.Children.Add(plus);}
                    var text=Theme.Text(tokens[k],11,Theme.Foreground);text.Margin=new Thickness(0);text.FontWeight=FontWeights.SemiBold;
                    keys.Children.Add(new Border{Child=text,Padding=new Thickness(6,2,6,3),CornerRadius=new CornerRadius(4),Background=Theme.Brush("#263440"),BorderBrush=Theme.Brush("#466075"),BorderThickness=new Thickness(1)});
                }
                Grid.SetRow(keys,i);table.Children.Add(keys);
                var description=Theme.Text(shortcuts[i][1],12);description.Margin=new Thickness(6,7,10,7);description.VerticalAlignment=VerticalAlignment.Center;Grid.SetRow(description,i);Grid.SetColumn(description,1);table.Children.Add(description);
            }
            section.Children.Add(new Border{Child=table,BorderBrush=Theme.Brush("#33414D"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(5),Padding=new Thickness(1)});return section;
        }
        void InitializeShortcuts()
        {
            Focusable=true;Loaded+=(s,e)=>{if(!IsKeyboardFocusWithin)Focus();};
            PreviewKeyDown+=(s,e)=>{
                var modifiers=Keyboard.Modifiers;
                if(e.Key==Key.F1){ShowAbout();e.Handled=true;return;}
                if(modifiers==(ModifierKeys.Control|ModifierKeys.Shift)&&e.Key==Key.S){SaveScreenshot();e.Handled=true;return;}
                if(modifiers==ModifierKeys.Control&&e.Key==Key.F){tagsVisible=true;UpdatePanels();UpdateTags();tagSearch.Focus();tagSearch.SelectAll();e.Handled=true;return;}
                if(e.OriginalSource is TextBoxBase||e.OriginalSource is ComboBox)return;
                if(modifiers==ModifierKeys.Control&&e.Key>=Key.D1&&e.Key<=Key.D6){SelectImageMode(new[]{"Native","Axial","Coronal","Sagittal","MPR + 3D","3D"}[e.Key-Key.D1]);e.Handled=true;}
                else if(modifiers==ModifierKeys.Control&&e.Key==Key.I){if(isocenterButton.Visibility==Visibility.Visible)isocenterButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));e.Handled=true;}
                else if(modifiers==ModifierKeys.Alt){var key=e.SystemKey==Key.None?e.Key:e.SystemKey;if(key==Key.I||key==Key.M||key==Key.D||key==Key.V){SetWorkspace(key==Key.I?"Bild":key==Key.M?"MLC":key==Key.D?"DVH":"3D");e.Handled=true;}}
                else if(modifiers==ModifierKeys.None&&e.Key==Key.Home){zoom=1;viewportCenter=null;Redraw();e.Handled=true;}
            };
        }
        void SaveScreenshot()
        {
            var identity=CurrentExportIdentity(workspaceMode=="DVH");var dialog=new Microsoft.Win32.SaveFileDialog{Filter="PNG image|*.png",FileName=identity.FileName("Viewer_"+(workspaceMode=="Bild"?(string)planes.SelectedItem:workspaceMode),".png"),Title="Save entire viewer (including sidebars and displayed identifiers)"};
            if(dialog.ShowDialog(Window.GetWindow(this))!=true)return;
            try{ViewerSnapshot.Save(identity.Stamp(ViewerSnapshot.Capture(this)),dialog.FileName);status.Text="Screenshot saved.";}catch(Exception){status.Text="Unable to save screenshot.";}
        }
    }
}
