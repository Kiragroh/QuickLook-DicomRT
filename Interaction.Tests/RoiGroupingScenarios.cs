using System;
using System.Reflection;
using System.Windows.Controls;
using QuickLook.DicomRT;
internal static class RoiGroupingScenarios
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    public static void Run(Action<bool,string> check)
    {
        var target=new StructureRoi{Number=1,StructureSopUid="1.2.3",Name="Volume 1"};
        var organ=new StructureRoi{Number=2,StructureSopUid="1.2.3",Name="Parotis_L"};
        var other=new StructureRoi{Number=3,StructureSopUid="1.2.3",Name="Ring_Parotis"};
        var plan=new PlanData{StructureSopUid="1.2.3"};plan.TargetRoiNumbers.Add(1);
        using(var control=new MlcPlaybackControl()){
            control.SetPlan(plan);
            check(control.IsOutlineEnabled(target)&&!control.IsOutlineEnabled(organ)&&!control.IsOutlineEnabled(other),"MLC defaults to plan-referenced targets, independent of DICOM type");
            var group=(CheckBox)typeof(MlcPlaybackControl).GetField("showOrgans",F).GetValue(control);group.IsChecked=true;
            check(control.IsOutlineEnabled(organ)&&!control.IsOutlineEnabled(other),"Organ names enabled without admitting optimization helpers");
            target.Visible=false;check(!control.IsOutlineEnabled(target),"Individual visibility still overrides target group");target.Visible=true;
            var newPlan=new PlanData{StructureSopUid="1.2.3"};newPlan.TargetRoiNumbers.Add(3);control.SetPlan(newPlan);
            check(!control.IsOutlineEnabled(target)&&control.IsOutlineEnabled(other),"Switching plan updates outline classification without modifying ROI data");
        }
        check(ThreeDGeometry.DefaultRoi(target,plan)&&!ThreeDGeometry.DefaultRoi(target),"3D and MLC share plan-scoped target classification");
        var scene=new RenderScene{Plan=plan,Structures={new RoiOverlay{Roi=target,ClassificationPlan=plan},new RoiOverlay{Roi=organ,ClassificationPlan=plan}}};
        var key=typeof(ThreeDControl).GetMethod("SceneKey",BindingFlags.Static|BindingFlags.NonPublic);
        var before=(string)key.Invoke(null,new object[]{scene,0d});scene.Plan=null;
        check(before!=(string)key.Invoke(null,new object[]{scene,0d}),"Changing target plan invalidates displayed category selection while mesh geometry can be reused");
    }
}
