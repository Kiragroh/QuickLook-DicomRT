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
            var panel=new StackPanel{Width=400};panel.Children.Add(Theme.Text("DICOM RT for QuickLook",19,Theme.Accent));
            panel.Children.Add(Theme.Text("The Space-key preview workflow, extended to DICOM images and radiotherapy objects on Windows. Inspect local plans, structures and dose without importing them into a TPS.",12));
            panel.Children.Add(Theme.Text("Developed by Maximilian Grohmann",14));panel.Children.Add(Theme.Text("Medical physicist and medical informatics specialist, Leipzig. Built with AI-assisted development. Research and inspection preview; not a clinically validated TPS.",11,Theme.Muted));
            var links=new WrapPanel();foreach(var link in new[]{Tuple.Create("GitHub / downloads","https://github.com/Kiragroh/QuickLook-DicomRT"),Tuple.Create("What is new / changelog","https://github.com/Kiragroh/QuickLook-DicomRT/blob/main/CHANGELOG.md"),Tuple.Create("Developer profile","https://kiragroh.github.io/"),Tuple.Create("QuickLook for Windows","https://github.com/QL-Win/QuickLook")}){
                var button=Theme.Button(link.Item1);button.ToolTip=link.Item2;button.Click+=(s,e)=>{try{Process.Start(new ProcessStartInfo(link.Item2){UseShellExecute=true});}catch(Exception){status.Text="Unable to open browser.";}};links.Children.Add(button);
            }panel.Children.Add(links);panel.Children.Add(Theme.Text("Keyboard & mouse",14,Theme.Accent));
            panel.Children.Add(Theme.Text("Space in Explorer   Open preview\nCtrl+1 / 2 / 3 / 4   Native / Axial / Coronal / Sagittal\nCtrl+5 / 6   Linked 2 x 2 / standalone 3D\nAlt+I / M / D / V   Image / MLC / DVH / 3D\nCtrl+I   Go to isocenter\nCtrl+F   Search DICOM tags\nCtrl+Shift+S   Save entire viewer (PNG)\nHome   Fit image\nF1   This help\nLeft drag   Move linked crosshair\nRight drag   Custom window / level\nCtrl+wheel   Image zoom\nMLC wheel   1 CP; Shift+wheel   0.1 CP\nRight-click view   Save only that view as PNG\nRight-click DVH   Also export active curves and metrics",11));
            var shot=Theme.Button("Save entire viewer");shot.ToolTip="Save all visible panels, toolbars and displayed identifiers (Ctrl+Shift+S)";shot.Click+=(s,e)=>{infoPopup.IsOpen=false;SaveScreenshot();};panel.Children.Add(shot);
            infoPopup=DarkPopup(infoButton.IsVisible?(UIElement)infoButton:this,new ScrollViewer{Content=panel,MaxHeight=620,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});if(!infoButton.IsVisible)infoPopup.Placement=PlacementMode.Center;infoPopup.IsOpen=true;
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
