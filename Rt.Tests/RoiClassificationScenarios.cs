using System;
using Dicom;
using QuickLook.DicomRT;
internal static class RoiClassificationScenarios
{
    public static void Run(Action<bool,string> check)
    {
        var references=new DicomSequence(DicomTag.DoseReferenceSequence,
            new DicomDataset().Add(DicomTag.DoseReferenceStructureType,"VOLUME").Add(DicomTag.DoseReferenceType,"TARGET").Add(DicomTag.ReferencedROINumber,7),
            new DicomDataset().Add(DicomTag.DoseReferenceStructureType,"VOLUME").Add(DicomTag.DoseReferenceType,"ORGAN_AT_RISK").Add(DicomTag.ReferencedROINumber,9),
            new DicomDataset().Add(DicomTag.DoseReferenceStructureType,"POINT").Add(DicomTag.DoseReferenceType,"TARGET").Add(DicomTag.ReferencedROINumber,11));
        var plan=PlanData.Load(new DicomEntry{Dataset=new DicomDataset().Add(references).Add(new DicomSequence(DicomTag.ReferencedStructureSetSequence,new DicomDataset().Add(DicomTag.ReferencedSOPInstanceUID,"1.2.3")))});
        var roi=new StructureRoi{Number=7,Name="Volume 7",StructureSopUid="1.2.3"};
        check(RoiClassification.Category(roi,plan)==RoiCategory.Target,"RTPLAN TARGET reference matches exact structure SOP and ROI number");
        check(RoiClassification.Category(roi)==RoiCategory.Other,"Removing plan leaves raw ROI unmodified");
        check(roi.InterpretedType==null,"Grouping never overwrites DICOM type");
        roi.StructureSopUid="1.2.4";check(RoiClassification.Category(roi,plan)==RoiCategory.Other,"Foreign structure with reused ROI number is not a target");
        roi.StructureSopUid="";check(RoiClassification.Category(roi,plan)==RoiCategory.Other,"Missing SOP identity cannot match a plan");
        roi.StructureSopUid="1.2.3";roi.Number=9;check(RoiClassification.Category(roi,plan)==RoiCategory.Organ,"Explicit plan OAR reference works without type/name");
        roi.Number=11;check(RoiClassification.Category(roi,plan)==RoiCategory.Other,"Point dose reference cannot classify volume");
        foreach(var name in new[]{"PTV","Boost_PTV_60","old ptv 2"})check(RoiClassification.Category(new StructureRoi{Name=name})==RoiCategory.Target,"PTV name fallback: "+name);
        foreach(var type in new[]{"PTV","GTV","CTV","ITV"})check(RoiClassification.Category(new StructureRoi{InterpretedType=type})==RoiCategory.Target,"Target type: "+type);
        foreach(var name in new[]{"Brainstem","Lung_L","Parotis rechts","SpinalCord_PRV5mm","Harnblase","Glnd_Submand_R","Bowel_large","Chiasm"})
            check(RoiClassification.Category(new StructureRoi{Name=name})==RoiCategory.Organ,"Organ name fallback: "+name);
        foreach(var name in new[]{"Ring_Brainstem","Lung_minus_Target","opt_heart","UnknownVolume","Avoidance 2","LungExpanded10mm"})
            check(RoiClassification.Category(new StructureRoi{Name=name})==RoiCategory.Other,"Helper/unknown name remains Other: "+name);
        check(RoiClassification.Category(new StructureRoi{Name="Brain",InterpretedType="CONTROL"})==RoiCategory.Other,"Explicit non-organ type prevents organ guessing");
        check(RoiClassification.Category(new StructureRoi{Name="PTV",InterpretedType="EXTERNAL"})==RoiCategory.External,"External type excluded despite target name");
        foreach(var name in new[]{"Body","External","Skin"})check(RoiClassification.Category(new StructureRoi{Name=name,InterpretedType="ORGAN"})==RoiCategory.External,"Body context excluded from organs: "+name);
        foreach(var name in new[]{"Table_Interior","CouchSurface","Support"})check(RoiClassification.Category(new StructureRoi{Name=name})==RoiCategory.Support,"Support name excluded: "+name);
        var loaded=StructureSet.Load(new DicomEntry{Dataset=new DicomDataset().Add(DicomTag.SOPInstanceUID,"1.2.3").Add(new DicomSequence(DicomTag.StructureSetROISequence,new DicomDataset().Add(DicomTag.ROINumber,7).Add(DicomTag.ROIName,"Target volume")))});
        check(RoiClassification.Category(loaded.Rois[0],plan)==RoiCategory.Target,"Structure parser retains SOP provenance for plan grouping");
    }
}
