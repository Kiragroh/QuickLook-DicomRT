using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace QuickLook.DicomRT
{
 public sealed partial class ThreeDControl
 {
  readonly StackPanel beamNavigation=new StackPanel{Orientation=Orientation.Horizontal};
  readonly ComboBox activeBeamPicker=Theme.Combo(165);
  readonly Slider beamCursor=new Slider{Minimum=0,Width=155,SmallChange=.1,LargeChange=1,Margin=new Thickness(5),VerticalAlignment=VerticalAlignment.Center};
  readonly Button beamPlay=Theme.Button("▶");
  public event Action PlaybackRequested;
  public void SetPlaying(bool playing){beamPlay.Content=playing?"Ⅱ":"▶";beamPlay.ToolTip=playing?"Pause shared plan preview":"Play plan in a continuous loop · speed follows MLC CP/s";}
  readonly TextBlock beamPosition=Theme.Text("",10,Theme.Muted);
  bool syncingBeamNavigation;int beamWheel;
  const string NeutralFields="All fields · no highlight";
  public event Action<PlanBeam,double> ControlPointRequested;
  public event Action<bool> FieldsVisibilityChanged;
  public void SetFieldsVisible(bool enabled){showBeamFields.IsChecked=enabled;}
  void BuildBeamNavigation(Panel controls)
  {
   beamPosition.Margin=new Thickness(4,0,12,0);beamPosition.VerticalAlignment=VerticalAlignment.Center;beamNavigation.Children.Add(activeBeamPicker);beamNavigation.Children.Add(beamPlay);beamNavigation.Children.Add(beamCursor);beamNavigation.Children.Add(beamPosition);controls.Children.Add(beamNavigation);
   beamPlay.Click+=(s,e)=>PlaybackRequested?.Invoke();SetPlaying(false);
   activeBeamPicker.ToolTip="Select the highlighted field and its moving MLC preview. Other treatment tracks remain visible.";
   beamCursor.ToolTip="Control point · wheel: 1 CP · Shift + wheel: 0.1 CP. Shared with the MLC and image views.";
   activeBeamPicker.SelectionChanged+=(s,e)=>{if(!syncingBeamNavigation)ControlPointRequested?.Invoke(activeBeamPicker.SelectedItem as PlanBeam,0);};
   beamCursor.ValueChanged+=(s,e)=>{if(!syncingBeamNavigation&&activeBeamPicker.SelectedItem is PlanBeam beam)ControlPointRequested?.Invoke(beam,e.NewValue);};
   beamCursor.PreviewMouseWheel+=(s,e)=>{beamCursor.Value=(Keyboard.Modifiers&ModifierKeys.Shift)!=0?MlcTimeline.WheelStep(beamCursor.Value,beamCursor.Maximum,e.Delta,ref beamWheel):MlcTimeline.RecordedWheelStep(beamCursor.Value,beamCursor.Maximum,e.Delta,ref beamWheel);e.Handled=true;};
   SyncBeamNavigation();
  }
  void SyncBeamNavigation()
  {
   if(syncingBeamNavigation)return;syncingBeamNavigation=true;
   try{
    beamNavigation.Visibility=showBeamFields.IsChecked==true&&scene?.Plan!=null?Visibility.Visible:Visibility.Collapsed;
    var choices=new object[]{NeutralFields}.Concat(MlcTimeline.PlaybackOrder(scene?.Plan)).ToArray();if(!activeBeamPicker.Items.Cast<object>().SequenceEqual(choices))activeBeamPicker.ItemsSource=choices;
    activeBeamPicker.SelectedItem=(object)scene?.ActiveBeam??NeutralFields;
    beamCursor.Maximum=Math.Max(0,(scene?.ActiveBeam?.ControlPoints.Count??1)-1);beamCursor.Value=Math.Max(0,Math.Min(beamCursor.Maximum,scene?.ActiveControlPointIndex??0));
    beamPlay.IsEnabled=scene?.ActiveBeam!=null;
    beamCursor.Visibility=beamPosition.Visibility=scene?.ActiveBeam==null?Visibility.Collapsed:Visibility.Visible;
    beamPosition.Text=$"CP {beamCursor.Value+1:0.0} / {beamCursor.Maximum+1:0}"+(scene?.ActiveControlPoint!=null?$" · Coll {scene.ActiveControlPoint.Collimator:0.#}°":scene?.ActiveBeam!=null?" · geometry unavailable":"");
    var mu=BeamMetersetInfo.At(scene?.ActiveBeam,beamCursor.Value);if(scene?.ActiveBeam!=null)beamPosition.Text+=" · "+mu.Text;beamPosition.ToolTip=mu.Detail;
   }finally{syncingBeamNavigation=false;}
  }
  // CP navigation changes only the lightweight guide, never anatomy or surface jobs.
  public void UpdateFields(RenderScene value)
  {
   if(disposed||scene==null||value==null||scene.Plan!=value.Plan)return;
   scene.ActiveBeam=value.ActiveBeam;scene.ActiveControlPoint=value.ActiveControlPoint;scene.ActiveControlPointIndex=value.ActiveControlPointIndex;UpdateBeamFields();
  }
 }
}
