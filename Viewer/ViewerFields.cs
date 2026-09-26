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
        WrapPanel fieldControls;bool syncingFields;int fieldWheel;
        UIElement BuildFieldControls()
        {
            fieldControls=new WrapPanel{Visibility=Visibility.Collapsed};fieldControls.Children.Add(fieldPicker);fieldControls.Children.Add(fieldCursor);fieldControls.Children.Add(fieldPosition);
            fieldPicker.ToolTip="Active field · other treatment fields stay faint at their first control point";
            fieldCursor.ToolTip="Active field control point · wheel 0.1 CP · Shift + wheel 1 CP; wheel over the image still changes slices";
            fieldPicker.SelectionChanged+=(s,e)=>{if(!syncingFields&&fieldPicker.SelectedItem is PlanBeam beam){EnsurePlayback();centralPlayback.Navigate(beam,0);}};
            fieldCursor.ValueChanged+=(s,e)=>{if(!syncingFields&&activeField!=null)centralPlayback?.Navigate(activeField,e.NewValue);};
            fieldControls.PreviewMouseWheel+=(s,e)=>{fieldCursor.Value=MlcTimeline.WheelStep(fieldCursor.Value,fieldCursor.Maximum,e.Delta*((Keyboard.Modifiers&ModifierKeys.Shift)!=0?10:1),ref fieldWheel);e.Handled=true;};
            showFields.Checked+=(s,e)=>SyncFieldControls();showFields.Unchecked+=(s,e)=>SyncFieldControls();return fieldControls;
        }
        void SyncFieldControls()
        {
            if(fieldControls==null||syncingFields)return;syncingFields=true;
            try{
                fieldControls.Visibility=showFields.IsChecked==true&&selectedPlan!=null?Visibility.Visible:Visibility.Collapsed;
                var choices=MlcTimeline.PlaybackOrder(selectedPlan);
                if(!fieldPicker.Items.Cast<PlanBeam>().SequenceEqual(choices))fieldPicker.ItemsSource=choices;
                fieldPicker.SelectedItem=activeField;fieldCursor.Maximum=Math.Max(0,(activeField?.ControlPoints.Count??1)-1);
                fieldCursor.Value=centralPlayback?.LocalPosition??0;fieldPosition.Text=$"CP {fieldCursor.Value+1:0.0} / {fieldCursor.Maximum+1:0}";
            }finally{syncingFields=false;}
        }
        void UpdateFieldOverlays()
        {
            if(latestScene==null)return;latestScene.ActiveBeam=activeField;latestScene.ActiveControlPoint=activeFieldPoint;
            // Field motion does not re-rasterize the CT, structures, dose, or 3D scene.
            if(workspaceMode=="Bild"&&showFields.IsChecked==true)foreach(var pane in panes)pane.UpdateFields(latestScene);
        }
    }
}
