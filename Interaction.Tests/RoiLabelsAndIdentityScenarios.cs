using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Controls;
using Dicom;
using QuickLook.DicomRT;

internal static class RoiLabelsAndIdentityScenarios
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static object Get(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
    static void Set(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
    static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,F).Invoke(o,args);
    public static void Run(Action<bool,string> check)
    {
        var ct=new DicomEntry{PatientKey="TEST|HOSPITAL",StudyUid="study",SeriesUid="1.2.3.4",FrameUid="frame",Modality="CT",SopUid="ct"};
        var rt=new DicomEntry{PatientKey="TEST|",StudyUid="study",FrameUid="frame",Modality="RTPLAN",SopUid="plan"};
        check(DicomPatientScope.Matches(ct,rt)&&DicomPatientScope.Matches(rt,ct),"Missing issuer accepted symmetrically within the same study and patient ID");
        rt.StudyUid="other";check(!DicomPatientScope.Matches(ct,rt),"Missing issuer does not cross studies");rt.StudyUid="study";
        rt.PatientKey="TEST|OTHER";check(!DicomPatientScope.Matches(ct,rt),"Two explicit issuers stay separated even within one study");
        rt.PatientKey="OTHER|";check(!DicomPatientScope.Matches(ct,rt),"Different patient IDs stay separated");rt.PatientKey="TEST|";
        var item=new DicomDataset();item.Add(DicomTag.ROINumber,1);item.Add(DicomTag.ROIName,"PTV");item.Add(DicomTag.ROIDescription,"Target description");
        var data=new DicomDataset();data.Add(new DicomSequence(DicomTag.StructureSetROISequence,item));
        var set=StructureSet.Load(new DicomEntry{Dataset=data,SopUid="set"});var roi=set.Rois.Single();
        check(roi.Name=="PTV"&&roi.Description=="Target description","Both DICOM label fields are retained separately");
        check(roi.DisplayName=="PTV","Name is the initial label");roi.LabelMode=RoiLabelMode.Description;
        check(roi.DisplayName=="Target description","Description mode uses ROI Description");roi.LabelMode=RoiLabelMode.Both;
        check(roi.DisplayName=="PTV · Target description"&&roi.Name=="PTV","Both mode does not mutate the canonical name");
        check(roi.MatchesLabel("target")&&roi.MatchesLabel("ptv"),"Search matches both fields independently of mode");
        var fallback=new StructureRoi{Name="Brain",LabelMode=RoiLabelMode.Description};check(fallback.DisplayName=="Brain","Missing description falls back to name");
        fallback.Name="";fallback.Description="Brain description";fallback.LabelMode=RoiLabelMode.Name;check(fallback.DisplayName=="Brain description","Missing name falls back to description");
        fallback.Name=fallback.Description;fallback.LabelMode=RoiLabelMode.Both;check(fallback.DisplayName==fallback.Name,"Identical fields are shown once");
        using(var viewer=new ViewerControl())
        {
            Set(viewer,"initialEntry",ct);
            var patientImage=(Func<DicomEntry,bool>)Call(viewer,"PatientImagePredicate");
            var image=new DicomEntry{PatientKey="TEST|",StudyUid="study",Modality="CT"};check(patientImage(image),"Image discovery uses the same missing-issuer rule");
            ((List<StructureSet>)Get(viewer,"structures")).Add(set);roi.Visible=false;
            Call(viewer,"BuildRoiList");var modes=(ComboBox)Get(viewer,"roiLabelMode");modes.SelectedIndex=2;
            var rows=(IList)Get(viewer,"roiOutlineRows");var label=(StackPanel)((Button)Get(rows[0],"Name")).Content;
            check(((TextBlock)label.Children[0]).Text=="PTV"&&((TextBlock)label.Children[1]).Text=="Target description"&&!roi.Visible,"Label selector updates the row without changing visibility");
            ((TextBox)Get(viewer,"roiSearch")).Text="description";modes.SelectedIndex=0;
            check(((IList)Get(viewer,"roiOutlineRows")).Count==1,"Description search still finds name-only rows");
            ((TextBox)Get(viewer,"roiSearch")).Text="";
            var organA=new StructureRoi{Name="Alpha",Description="Zulu",InterpretedType="ORGAN"};
            var organZ=new StructureRoi{Name="Zulu",Description="Alpha",InterpretedType="ORGAN"};
            var targetZ=new StructureRoi{Name="PTV_Z",Description="A target"};
            var helper=new StructureRoi{Name="AAA helper"};set.Rois.AddRange(new[]{organZ,helper,targetZ,organA});
            modes.SelectedIndex=2;
            Func<StructureRoi[]> order=()=>((IList)Get(viewer,"roiOutlineRows")).Cast<object>().Select(r=>(StructureRoi)Get(r,"Roi")).ToArray();
            check(order().SequenceEqual(new[]{roi,targetZ,organA,organZ,helper}),"Name-first sorting: targets, organs, other; each group alphabetical");
            modes.SelectedIndex=3;
            check(order().SequenceEqual(new[]{targetZ,roi,organZ,organA,helper}),"Description-first change immediately resorts within the same clinical display groups");
            var secondRow=((IList)Get(viewer,"roiOutlineRows"))[1];var pair=(StackPanel)((Button)Get(secondRow,"Name")).Content;
            var primary=(TextBlock)pair.Children[0];var secondary=(TextBlock)pair.Children[1];
            check(primary.Text==roi.Description&&secondary.Text==roi.Name&&primary.FontSize>secondary.FontSize,"Description-first both mode has large description above smaller name");
            check(!roi.Visible,"Resorting preserves individual structure visibility");
            using(var dvh=new DvhControl())
            {
                var button=new Button{Content=roi.Name};((Dictionary<StructureRoi,Button>)Get(dvh,"legendNames"))[roi]=button;
                dvh.FocusStructure(roi);int calculations=dvh.CalculationCount;roi.LabelMode=RoiLabelMode.Description;dvh.RefreshLabels();
                check(((TextBlock)button.Content).Text==roi.Description&&ReferenceEquals(dvh.FocusedStructure,roi)&&dvh.CalculationCount==calculations,"DVH relabeling retains focus and never recalculates curves");
            }
        }
        var rsEntry=new DicomEntry{PatientKey=rt.PatientKey,StudyUid="study",SopUid="set",Modality="RTSTRUCT",Dataset=new DicomDataset()};
        var series=new DicomDataset();series.Add(DicomTag.SeriesInstanceUID,"1.2.3.4");var study=new DicomDataset();study.Add(new DicomSequence(DicomTag.RTReferencedSeriesSequence,series));var frame=new DicomDataset();frame.Add(new DicomSequence(DicomTag.RTReferencedStudySequence,study));rsEntry.Dataset.Add(new DicomSequence(DicomTag.ReferencedFrameOfReferenceSequence,frame));
        rt.Dataset=new DicomDataset();var plan=new PlanData{Entry=rt,StructureSopUid="set"};
        var resolved=PlanDicomExport.Resolve(plan,new[]{ct,rt,rsEntry});check(resolved.Parts["CT"].Length==1&&resolved.Parts["RS"].Length==1,"Plan ZIP retains explicitly referenced CT with a missing RT issuer");
    }
}
