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
                var pen=new Pen(Theme.Accent,1.2);
                if(mode=="MPR + 3D")foreach(int x in new[]{1,10})foreach(int y in new[]{1,10})dc.DrawRectangle(null,pen,new Rect(x,y,6,6));
                else if(mode=="3D"){var shape=Geometry.Parse("M 9,1 L 17,5 17,13 9,17 1,13 1,5 Z M 1,5 L 9,9 17,5 M 9,9 L 9,17 M 9,1 L 9,9");dc.DrawGeometry(null,pen,shape);}
                else{dc.DrawRoundedRectangle(null,pen,new Rect(2,2,14,14),3,3);if(mode=="Axial")dc.DrawLine(new Pen(Theme.Accent,3),new Point(3,9),new Point(15,9));else if(mode=="Coronal")dc.DrawRectangle(Theme.Accent,null,new Rect(5,4,8,10));else if(mode=="Sagittal")dc.DrawLine(new Pen(Theme.Accent,3),new Point(9,3),new Point(9,15));else dc.DrawLine(pen,new Point(4,13),new Point(14,5));}
            }drawing.Freeze();row.Children.Add(new Image{Source=new DrawingImage(drawing),Width=18,Height=18,Margin=new Thickness(0,0,6,0)});if(mode=="MPR + 3D"||mode=="3D")row.Children.Add(new TextBlock{Text=mode=="MPR + 3D"?"2 x 2":mode,VerticalAlignment=VerticalAlignment.Center});button.Content=row;button.Tag=mode;
            button.ToolTip=mode=="MPR + 3D"?"Linked axial, coronal, sagittal and 3D overview (Ctrl+5)":mode=="3D"?"Standalone 3D view (Ctrl+6)":"Show "+mode+" image plane";return button;
        }
    }
    public sealed partial class ViewerControl
    {
        readonly Dictionary<string,Button> imageModeButtons=new Dictionary<string,Button>();
        WindowRangeControl windowRange;
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
            if(mode!="Native"&&volume==null){status.Text="This view needs a loaded image volume.";return;}
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
