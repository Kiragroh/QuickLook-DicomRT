using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace QuickLook.DicomRT
{
    public sealed partial class ViewerControl
    {
        readonly ComboBox fieldPicker=Theme.Combo(210);
        readonly Slider fieldCursor=new Slider{Minimum=0,SmallChange=.1,LargeChange=1,Width=180,Margin=new Thickness(8,4,8,4),VerticalAlignment=VerticalAlignment.Center};
        readonly TextBlock fieldPosition=Theme.Text("",10,Theme.Muted);
        const string AllFields="All fields - no highlight";
        bool neutralFields;WrapPanel fieldControls;bool syncingFields;int fieldWheel;
        UIElement BuildFieldControls()
        {
            fieldControls=new WrapPanel{Visibility=Visibility.Collapsed};fieldControls.Children.Add(fieldPicker);fieldControls.Children.Add(fieldCursor);fieldControls.Children.Add(fieldPosition);
            fieldPicker.ToolTip="Active field · other treatment fields stay faint at their first control point";
            fieldCursor.ToolTip="Active field control point · wheel 1 CP · Shift + wheel 0.1 CP; wheel over the image still changes slices";
            fieldPicker.SelectionChanged+=(s,e)=>{if(syncingFields)return;neutralFields=fieldPicker.SelectedItem is string;if(neutralFields){SyncFieldControls();UpdateFieldOverlays();}else if(fieldPicker.SelectedItem is PlanBeam beam){EnsurePlayback();centralPlayback.Navigate(beam,0);UpdateFieldOverlays();}};
            fieldCursor.ValueChanged+=(s,e)=>{if(!syncingFields&&activeField!=null)centralPlayback?.Navigate(activeField,e.NewValue);};
            fieldControls.PreviewMouseWheel+=(s,e)=>{fieldCursor.Value=((Keyboard.Modifiers&ModifierKeys.Shift)!=0?MlcTimeline.WheelStep(fieldCursor.Value,fieldCursor.Maximum,e.Delta,ref fieldWheel):MlcTimeline.RecordedWheelStep(fieldCursor.Value,fieldCursor.Maximum,e.Delta,ref fieldWheel));e.Handled=true;};
            showFields.Checked+=(s,e)=>SyncFieldControls();showFields.Unchecked+=(s,e)=>SyncFieldControls();return fieldControls;
        }
        void SyncFieldControls()
        {
            if(fieldControls==null||syncingFields)return;syncingFields=true;
            try{
                fieldControls.Visibility=showFields.IsChecked==true&&selectedPlan!=null?Visibility.Visible:Visibility.Collapsed;
                var choices=new object[]{AllFields}.Concat(MlcTimeline.PlaybackOrder(selectedPlan)).ToArray();
                if(!fieldPicker.Items.Cast<object>().SequenceEqual(choices))fieldPicker.ItemsSource=choices;
                fieldPicker.SelectedItem=neutralFields?(object)AllFields:activeField;fieldCursor.Visibility=fieldPosition.Visibility=neutralFields?Visibility.Collapsed:Visibility.Visible;fieldCursor.Maximum=Math.Max(0,(activeField?.ControlPoints.Count??1)-1);
                fieldCursor.Value=centralPlayback?.LocalPosition??0;fieldPosition.Text=$"CP {fieldCursor.Value+1:0.0} / {fieldCursor.Maximum+1:0}";
            }finally{syncingFields=false;}
        }
        void UpdateFieldOverlays()
        {
            if(latestScene==null)return;latestScene.ActiveBeam=neutralFields?null:activeField;latestScene.ActiveControlPoint=neutralFields?null:activeFieldPoint;
            // Field motion does not re-rasterize the CT, structures, dose, or 3D scene.
            if(workspaceMode=="Bild"&&showFields.IsChecked==true)foreach(var pane in panes)pane.UpdateFields(latestScene);
        }
    }
}
