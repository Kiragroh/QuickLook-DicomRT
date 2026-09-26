using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using QuickLook.DicomRT;
internal static class ReviewScenarios
{
 const BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;
 static void Set(object target,string name,object value)=>target.GetType().GetField(name,Fields).SetValue(target,value);
 static T Get<T>(object target,string name)=>(T)target.GetType().GetField(name,Fields).GetValue(target);
 static object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Fields).Invoke(target,args);
 static IEnumerable<DoseGrid> SelectedDoses(ViewerControl viewer)=>(IEnumerable<DoseGrid>)typeof(ViewerControl).GetProperty("SelectedDoses",Fields).GetValue(viewer,null);
 public static void Run(Action<bool,string> check)
 {
  using(var viewer=new ViewerControl())
  {
   Set(viewer,"initialEntry",new DicomEntry{Modality="RTDOSE",SopUid="synthetic-opened"});var opened=new DoseGrid{Entry=new DicomEntry{SopUid="synthetic-opened"},PlanUid="synthetic-missing",FrameUid="synthetic"};var other=new DoseGrid{Entry=new DicomEntry{SopUid="synthetic-other-dose"},PlanUid="synthetic-other1",FrameUid="synthetic"};Get<List<DoseGrid>>(viewer,"doses").AddRange(new[]{opened,other});
   var plan=new PlanData{Entry=new DicomEntry{SopUid="synthetic-other1"}};Get<List<PlanData>>(viewer,"planData").AddRange(new[]{plan,new PlanData{Entry=new DicomEntry{SopUid="synthetic-other2"}}});Call(viewer,"RefreshPlanChoices");
   check(SelectedDoses(viewer).SequenceEqual(new[]{opened}),"Opened RTDOSE remains selected when referenced plan is absent and unrelated plans exist");check(Get<PlanData>(viewer,"selectedPlan")==null&&!Get<bool>(viewer,"sumMode"),"Missing dose-plan reference never selects an unrelated plan or sum");
   var compatible=new StructureSet{Entry=new DicomEntry{SopUid="synthetic-structure-compatible"},Rois=new List<StructureRoi>{new StructureRoi{FrameUid="synthetic"}}};var unrelated=new StructureSet{Entry=new DicomEntry{SopUid="synthetic-structure-unrelated"},Rois=new List<StructureRoi>{new StructureRoi{FrameUid="synthetic-unregistered"}}};Get<List<StructureSet>>(viewer,"structures").AddRange(new[]{compatible,unrelated});
   var available=(IEnumerable<StructureSet>)typeof(ViewerControl).GetProperty("SelectedStructures",Fields).GetValue(viewer,null);check(available.SequenceEqual(new[]{compatible}),"Explicit dose without referenced plan permits only geometrically compatible structures despite unrelated plans");
   compatible.Rois.Insert(0,new StructureRoi{FrameUid="synthetic-unregistered"});Call(viewer,"Redraw");var scene=Get<RenderScene>(viewer,"latestScene");check(scene.Doses.Count==1&&ReferenceEquals(scene.Doses[0].Dose,opened)&&scene.Structures.Count==1&&scene.Structures[0].Roi.FrameUid=="synthetic","Opened dose frame anchors mixed-frame structure sets and unmapped ROIs stay excluded");
   Set(viewer,"userSelectedPlan",true);Set(viewer,"selectedPlan",plan);check(SelectedDoses(viewer).SequenceEqual(new[]{other}),"Explicit manual plan selection supersedes opened dose selection");
  }
  using(var viewer=new ViewerControl())
  {
   Set(viewer,"initialEntry",new DicomEntry{Modality="RTSTRUCT",SopUid="synthetic-opened"});Get<List<PlanData>>(viewer,"planData").AddRange(new[]{new PlanData{Entry=new DicomEntry{SopUid="synthetic-plan1"},StructureSopUid="synthetic-opened"},new PlanData{Entry=new DicomEntry{SopUid="synthetic-plan2"},StructureSopUid="synthetic-opened"}});Call(viewer,"RefreshPlanChoices");check(Get<PlanData>(viewer,"selectedPlan")==null,"RTSTRUCT with two associated plans does not choose an arbitrary plan");
  }
  using(var viewer=new ViewerControl())
  {
   var stack=new ImageStack{FrameUid="synthetic",CanMpr=true};for(int z=0;z<3;z++)stack.Entries.Add(new DicomEntry{Path="synthetic"+z,FrameUid="synthetic",SeriesUid="synthetic-series",HasGeometry=true,Rows=4,Columns=4,Origin=new Vec3(0,0,z),AxisX=new Vec3(1,0,0),AxisY=new Vec3(0,1,0),SpacingX=1,SpacingY=1});
   Set(viewer,"currentStack",stack);Set(viewer,"currentEntry",stack.Entries[0]);Set(viewer,"initialEntry",stack.Entries[0]);Set(viewer,"volume",new VolumeData{Width=4,Height=4,Depth=3,Values=new float[48],Origin=new Vec3(),AxisX=new Vec3(1,0,0),AxisY=new Vec3(0,1,0),AxisZ=new Vec3(0,0,1),SpacingX=1,SpacingY=1,SpacingZ=1});Set(viewer,"focus",new Vec3(1,2,0));Set(viewer,"zoom",3.2);Call(viewer,"SetWindow",42d,999d);
   // Exercise the pane-to-viewer zoom event, without simulating global keyboard input.
   var pane=Get<List<SlicePane>>(viewer,"panes")[0];var zoom=(Action<double>)typeof(SlicePane).GetField("ZoomChanged",Fields).GetValue(pane);zoom(1.15);var before=Get<Vec3>(viewer,"focus");check(before.X==1&&before.Y==2&&before.Z==0,"Zoom callback retains physical focus");
   ((Task)Call(viewer,"ShowSliceAsync",1,true)).GetAwaiter().GetResult();var after=Get<Vec3>(viewer,"focus");check(Math.Abs(Get<double>(viewer,"zoom")-3.68)<1e-9&&Get<double>(viewer,"windowWidth")==999&&Get<double>(viewer,"windowCenter")==42&&before.X==after.X&&before.Y==after.Y&&after.Z==1,"Native slice navigation preserves window, zoom and in-plane focus after zoom");
   ((Task)Call(viewer,"ScrollAsync","Coronal",1)).GetAwaiter().GetResult();after=Get<Vec3>(viewer,"focus");check(after.X==1&&after.Y==3&&after.Z==1&&Get<double>(viewer,"windowWidth")==999&&Get<double>(viewer,"windowCenter")==42&&Math.Abs(Get<double>(viewer,"zoom")-3.68)<1e-9,"Reformatted scroll changes only its normal coordinate and preserves window and zoom");
   Get<System.Windows.Controls.ComboBox>(viewer,"planes").SelectedItem="MPR + 3D";var grid=Get<System.Windows.Controls.Grid>(viewer,"imageGrid");var three=Get<ThreeDControl>(viewer,"mprThreeD");
   check(grid.RowDefinitions.Count==2&&grid.ColumnDefinitions.Count==2&&Get<List<SlicePane>>(viewer,"panes").Count==3&&System.Windows.Controls.Grid.GetRow(three)==1&&System.Windows.Controls.Grid.GetColumn(three)==1,"Three-plane view is a 2 by 2 grid with 3D at bottom right");
   Get<System.Windows.Controls.ComboBox>(viewer,"planes").SelectedItem="Axial";Get<System.Windows.Controls.ComboBox>(viewer,"planes").SelectedItem="MPR + 3D";check(ReferenceEquals(three,Get<ThreeDControl>(viewer,"mprThreeD")),"Reentering quad view preserves the 3D control and its camera/cache");
  }
 }
}
