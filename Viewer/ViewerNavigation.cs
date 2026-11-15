using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace QuickLook.DicomRT
{
    internal static class ViewButtons
    {
        public static Button Create(string mode)
        {
            var button=Theme.Button(mode);var row=new StackPanel{Orientation=Orientation.Horizontal};
            var drawing=new DrawingGroup();using(var dc=drawing.Open()){
                dc.DrawRectangle(Brushes.Transparent,null,new Rect(0,0,24,24));
                var pen=new Pen(Theme.Accent,1.2){LineJoin=PenLineJoin.Round};
                if(mode=="MPR + 3D")foreach(int x in new[]{3,13})foreach(int y in new[]{3,13})dc.DrawRectangle(null,pen,new Rect(x,y,8,8));
                else if(mode=="Native"){dc.DrawRoundedRectangle(null,pen,new Rect(4,4,16,16),2,2);dc.DrawLine(pen,new Point(6,18),new Point(18,6));}
                else{
                    string plane=mode=="Axial"?"M12 7 21 12 12 17 3 12Z":mode=="Coronal"?"M7.5 4.5 16.5 9.5 16.5 19.5 7.5 14.5Z":mode=="Sagittal"?"M7.5 9.5 16.5 4.5 16.5 14.5 7.5 19.5Z":null;
                    dc.DrawGeometry(null,pen,Geometry.Parse("M3 7 12 2 21 7 21 17 12 22 3 17Z M3 7 12 12 21 7 M12 12V22"));
                    if(plane!=null)dc.DrawGeometry(Theme.Brush("#5564B5F6"),pen,Geometry.Parse(plane));
                }
            }drawing.Freeze();row.Children.Add(new Image{Source=new DrawingImage(drawing),Width=20,Height=20,Margin=new Thickness(0,0,5,0)});row.Children.Add(new TextBlock{Text=mode=="MPR + 3D"?"2 x 2":mode,VerticalAlignment=VerticalAlignment.Center});button.Content=row;button.Tag=mode;
            button.ToolTip=mode=="MPR + 3D"?"Linked axial, coronal, sagittal and 3D or DVH overview":mode=="3D"?"Standalone 3D view":"Show "+mode+" image plane";System.Windows.Automation.AutomationProperties.SetName(button,mode=="MPR + 3D"?"Linked 2 x 2 views":mode);return button;
        }
    }
    public sealed partial class ViewerControl
    {
        readonly Dictionary<string,Button> imageModeButtons=new Dictionary<string,Button>();
        WindowRangeControl windowRange;UIElement windowControls;
        string windowPreset="DICOM";ComboBox windowPresets;StackPanel customWindow;bool applyingPreset;
        UIElement BuildImageModes()
        {
            var row=new WrapPanel();int key=1;
            foreach(var mode in new[]{"Native","Axial","Coronal","Sagittal","MPR + 3D","3D"}){
                var button=ViewButtons.Create(mode);button.ToolTip+=" (Ctrl+"+key+++")";button.Click+=(s,e)=>SelectImageMode(mode);row.Children.Add(button);imageModeButtons[mode]=button;
            }
            return row;
        }
        async void SelectImageMode(string mode)
        {
            if(mode=="3D"){SetWorkspace("3D");return;}
            if(mode!="Native"&&NavigationBounds==null)status.Text="Waiting for a matching image volume · navigation remains available.";
            planes.SelectedItem=mode;SetWorkspace("Bild");if(mode=="Native"&&currentStack!=null)await MoveFocusAsync(focus);
        }
        void UpdateImageModeButtons()
        {
            foreach(var pair in imageModeButtons){bool selected=workspaceMode=="3D"?pair.Key=="3D":workspaceMode=="Bild"&&pair.Key==(string)planes.SelectedItem;pair.Value.Foreground=selected?Theme.Accent:Theme.Foreground;pair.Value.Background=selected?Theme.Brush("#24384C"):Theme.Panel;}
        }
        UIElement BuildWindowControls()
        {
            var panel=new StackPanel();windowPresets=Theme.Combo(180);windowPresets.ToolTip="Window / level preset. Custom exposes width and level sliders.";
            windowPresets.ItemsSource=new[]{"DICOM","Auto","Soft tissue","Lung","Bone","Brain","Liver","Custom"};windowPresets.SelectedItem=windowPreset;panel.Children.Add(windowPresets);
            customWindow=new StackPanel{Visibility=windowPreset=="Custom"?Visibility.Visible:Visibility.Collapsed};panel.Children.Add(customWindow);
            miniWidth=new Slider{Minimum=1,Maximum=Math.Max(5000,windowWidth),Value=windowWidth,Width=145};miniLevel=new Slider{Minimum=Math.Min(-1500,windowCenter),Maximum=Math.Max(3500,windowCenter),Value=windowCenter,Width=145};
            foreach(var item in new[]{Tuple.Create("W",miniWidth),Tuple.Create("L",miniLevel)}){var row=new StackPanel{Orientation=Orientation.Horizontal};var label=Theme.Text(item.Item1,10,Theme.Muted);label.Width=20;row.Children.Add(label);row.Children.Add(item.Item2);customWindow.Children.Add(row);}
            foreach(UIElement child in customWindow.Children)child.Visibility=Visibility.Collapsed;windowRange=new WindowRangeControl();windowRange.SetWindow(windowCenter,windowWidth);windowRange.WindowChanged+=SetWindow;customWindow.Children.Add(windowRange);
            miniWidth.ToolTip="Window width / contrast";miniLevel.ToolTip="Window level / brightness";
            miniWidth.ValueChanged+=(s,e)=>{if(!updatingWindowControls)SetWindow(windowCenter,e.NewValue);};miniLevel.ValueChanged+=(s,e)=>{if(!updatingWindowControls)SetWindow(e.NewValue,windowWidth);};
            windowPresets.SelectionChanged+=(s,e)=>{if(!applyingPreset&&windowPresets.SelectedItem is string preset)ApplyWindowPreset(preset);};
            return new Border{Child=panel,Padding=new Thickness(6),CornerRadius=new CornerRadius(5),Background=Theme.Brush("#DF11171D"),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,12,28)};
        }
        void ApplyWindowPreset(string preset)
        {
            applyingPreset=true;windowPreset=preset;
            try{
                if(windowPresets!=null)windowPresets.SelectedItem=preset;if(customWindow!=null)customWindow.Visibility=preset=="Custom"?Visibility.Visible:Visibility.Collapsed;
                switch(preset){case "Auto":AutoWindow();break;case "DICOM":if(currentEntry?.WindowWidth>0)SetWindow(currentEntry.WindowCenter,currentEntry.WindowWidth);else AutoWindow();break;case "Soft tissue":SetWindow(40,400);break;case "Lung":SetWindow(-600,1500);break;case "Bone":SetWindow(400,1800);break;case "Brain":SetWindow(40,80);break;case "Liver":SetWindow(60,150);break;}
            }finally{applyingPreset=false;}
        }
        void MarkWindowCustom()
        {if(applyingPreset)return;windowPreset="Custom";applyingPreset=true;try{if(windowPresets!=null)windowPresets.SelectedItem=windowPreset;if(customWindow!=null)customWindow.Visibility=Visibility.Visible;}finally{applyingPreset=false;}}
    }
}
