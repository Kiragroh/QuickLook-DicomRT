using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using QuickLook.DicomRT;
internal static class OutlineIndicatorScenarios
{
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static object Get(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
 static void Set(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
 static void Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,F).Invoke(o,args);
 public static void Run(Action<bool,string> check)
 {
  using(var viewer=new ViewerControl()){
   var ptv=new StructureRoi{Name="Target",FrameUid="test-frame",InterpretedType="PTV",Red=255,Green=80,Blue=140,Visible=true};
   var organ=new StructureRoi{Name="Organ",FrameUid="test-frame",InterpretedType="ORGAN",Visible=true};
   var other=new StructureRoi{Name="Helper",FrameUid="test-frame",InterpretedType="AVOIDANCE",Visible=true};
   var unmatched=new StructureRoi{Name="Unmatched",FrameUid="other-frame",InterpretedType="PTV",Visible=true};
   ((List<StructureSet>)Get(viewer,"structures")).Add(new StructureSet{Entry=new DicomEntry{SopUid="test-set"},Rois=new List<StructureRoi>{ptv,organ,other,unmatched}});
   Set(viewer,"selectedPlan",new PlanData{FrameUid="test-frame",StructureSopUid="test-set",Entry=new DicomEntry{SopUid="test-plan"}});
   Call(viewer,"SetWorkspace","MLC");Call(viewer,"BuildRoiList");
   var rows=(IList)Get(viewer,"roiOutlineRows");Func<int,int> state=i=>(int)Get(rows[i],"State");var first=rows[0];
   check(state(0)==4&&state(1)==3&&state(2)==3&&state(3)==2,"MLC list marks enabled PTV, disabled groups and unregistered ROI distinctly");
   check(((Button)Get(rows[0],"Name")).FontWeight==FontWeights.SemiBold&&((TextBlock)Get(rows[0],"Indicator")).Text=="Outline","Active outline has bold colored name and explicit label");
   var mlc=(MlcPlaybackControl)Get(viewer,"centralPlayback");((CheckBox)Get(mlc,"showOrgans")).IsChecked=true;
   check(state(1)==4&&ReferenceEquals(first,rows[0]),"Category toggle updates indicators in place without rebuilding list");
   ((CheckBox)Get(mlc,"showOther")).IsChecked=true;check(state(2)==4,"Other group activates helper indicator");
   var list=(StackPanel)Get(viewer,"roiList");var toggle=((DockPanel)list.Children[0]).Children.OfType<CheckBox>().Single();toggle.IsChecked=false;
   check(!ptv.Visible&&state(0)==1&&!mlc.IsOutlineEnabled(ptv),"Individual checkbox removes both effective projection and active indicator");
   toggle.IsChecked=true;((CheckBox)Get(mlc,"showPtv")).IsChecked=false;check(state(0)==3&&ptv.Visible,"Group off preserves individual selection and explains inactivity");
   Call(viewer,"SetWorkspace","Bild");check(state(0)==0&&((TextBlock)Get(rows[0],"Indicator")).Visibility==Visibility.Collapsed,"Image view does not claim MLC category filters apply to its contours");
  }
 }
}
