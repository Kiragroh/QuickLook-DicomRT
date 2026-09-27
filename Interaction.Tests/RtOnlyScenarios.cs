using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Dicom;
using Dicom.Imaging;
using Dicom.IO.Buffer;
using QuickLook.DicomRT;

internal static class RtOnlyScenarios
{
    static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);
    static void Invoke(object target,string name,params object[] arguments)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,arguments);
    static void Pump(Task task)
    {
        var frame=new DispatcherFrame();var limit=DateTime.UtcNow.AddSeconds(40);
        var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(10)};
        timer.Tick+=(s,e)=>{if(task.IsCompleted||DateTime.UtcNow>limit)frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);timer.Stop();
        if(!task.IsCompleted)throw new TimeoutException("RT-only scenario timed out");task.GetAwaiter().GetResult();
    }
    static string FixtureFolder(string root)
    {
        string folder=Path.Combine(root,"fixtures");Directory.CreateDirectory(folder);
        const string frame="1.2.826.0.1.3680043.10.543.1",planUid="1.2.826.0.1.3680043.10.543.2",structureUid="1.2.826.0.1.3680043.10.543.3";
        Func<DicomUID,string,string,DicomDataset> header=(sop,uid,modality)=>new DicomDataset().Add(DicomTag.SOPClassUID,sop).Add(DicomTag.SOPInstanceUID,uid)
            .Add(DicomTag.Modality,modality).Add(DicomTag.PatientID,"SYNTHETIC_RT_ONLY_TEST").Add(DicomTag.StudyInstanceUID,"1.2.826.0.1.3680043.10.543.4")
            .Add(DicomTag.SeriesInstanceUID,uid+".1").Add(DicomTag.FrameOfReferenceUID,frame);
        Func<string,DicomDataset> leaf=type=>new DicomDataset().Add(DicomTag.RTBeamLimitingDeviceType,type).Add(DicomTag.NumberOfLeafJawPairs,2).Add(DicomTag.LeafPositionBoundaries,-5d,0d,5d);
        Func<string,DicomDataset> position=type=>new DicomDataset().Add(DicomTag.RTBeamLimitingDeviceType,type).Add(DicomTag.LeafJawPositions,-2d,-2d,2d,2d);
        var cp=new DicomDataset().Add(DicomTag.ControlPointIndex,0).Add(DicomTag.GantryAngle,0d).Add(DicomTag.BeamLimitingDeviceAngle,0d).Add(DicomTag.PatientSupportAngle,0d)
            .Add(DicomTag.IsocenterPosition,2d,2d,2d).Add(DicomTag.CumulativeMetersetWeight,0d).Add(new DicomSequence(DicomTag.BeamLimitingDevicePositionSequence,position("MLCX1"),position("MLCX2")));
        var plan=header(DicomUID.RTPlanStorage,planUid,"RTPLAN").Add(DicomTag.RTPlanLabel,"SYNTHETIC")
            .Add(new DicomSequence(DicomTag.ReferencedStructureSetSequence,new DicomDataset().Add(DicomTag.ReferencedSOPInstanceUID,structureUid)))
            .Add(new DicomSequence(DicomTag.BeamSequence,new DicomDataset().Add(DicomTag.BeamNumber,1).Add(DicomTag.BeamName,"Synthetic beam")
                .Add(new DicomSequence(DicomTag.BeamLimitingDeviceSequence,leaf("MLCX1"),leaf("MLCX2"))).Add(new DicomSequence(DicomTag.ControlPointSequence,cp))));
        Func<double,DicomDataset> contour=z=>new DicomDataset().Add(DicomTag.ContourGeometricType,"CLOSED_PLANAR").Add(DicomTag.NumberOfContourPoints,4)
            .Add(DicomTag.ContourData,1d,1d,z,3d,1d,z,3d,3d,z,1d,3d,z);
        var structures=header(DicomUID.RTStructureSetStorage,structureUid,"RTSTRUCT")
            .Add(new DicomSequence(DicomTag.StructureSetROISequence,new DicomDataset().Add(DicomTag.ROINumber,1).Add(DicomTag.ROIName,"Synthetic PTV").Add(DicomTag.ReferencedFrameOfReferenceUID,frame)))
            .Add(new DicomSequence(DicomTag.RTROIObservationsSequence,new DicomDataset().Add(DicomTag.ReferencedROINumber,1).Add(DicomTag.RTROIInterpretedType,"PTV")))
            .Add(new DicomSequence(DicomTag.ROIContourSequence,new DicomDataset().Add(DicomTag.ReferencedROINumber,1).Add(DicomTag.ROIDisplayColor,255,80,80).Add(new DicomSequence(DicomTag.ContourSequence,contour(1),contour(3)))));
        var dose=header(DicomUID.RTDoseStorage,"1.2.826.0.1.3680043.10.543.5","RTDOSE").Add(DicomTag.Rows,(ushort)5).Add(DicomTag.Columns,(ushort)5)
            .Add(DicomTag.BitsAllocated,(ushort)16).Add(DicomTag.BitsStored,(ushort)16).Add(DicomTag.HighBit,(ushort)15).Add(DicomTag.PixelRepresentation,(ushort)0)
            .Add(DicomTag.SamplesPerPixel,(ushort)1).Add(DicomTag.PhotometricInterpretation,"MONOCHROME2").Add(DicomTag.ImagePositionPatient,0d,0d,0d)
            .Add(DicomTag.ImageOrientationPatient,1d,0d,0d,0d,1d,0d).Add(DicomTag.PixelSpacing,1d,1d).Add(DicomTag.GridFrameOffsetVector,0d,2d,4d)
            .Add(DicomTag.DoseGridScaling,.01d).Add(DicomTag.DoseUnits,"GY").Add(DicomTag.DoseType,"PHYSICAL").Add(DicomTag.DoseSummationType,"PLAN")
            .Add(new DicomSequence(DicomTag.ReferencedRTPlanSequence,new DicomDataset().Add(DicomTag.ReferencedSOPInstanceUID,planUid)));
        var pixels=DicomPixelData.Create(dose,true);for(int z=0;z<3;z++){var bytes=new byte[50];for(int i=0;i<25;i++)Buffer.BlockCopy(BitConverter.GetBytes((ushort)100),0,bytes,i*2,2);pixels.AddFrame(new MemoryByteBuffer(bytes));}
        new DicomFile(plan).Save(Path.Combine(folder,"RTPLAN.dcm"));new DicomFile(structures).Save(Path.Combine(folder,"RTSTRUCT.dcm"));new DicomFile(dose).Save(Path.Combine(folder,"RTDOSE.dcm"));return folder;
    }
    static ViewerControl Open(string path)
    {
        var viewer=new ViewerControl();viewer.Measure(new Size(1100,800));viewer.Arrange(new Rect(0,0,1100,800));viewer.Open(path);Pump(viewer.LoadCompletion);return viewer;
    }
    public static void Run(Action<bool,string> check)
    {
        var previous=SynchronizationContext.Current;SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        string root=Path.Combine(Path.GetTempPath(),"QuickLook-RT-only-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            string source=FixtureFolder(root);
            Action<string,string[]> stage=(name,files)=>{var folder=Path.Combine(root,name);Directory.CreateDirectory(folder);foreach(var file in files)File.Copy(Path.Combine(source,file),Path.Combine(folder,file));};
            var split=Path.Combine(root,"split");var imageFolder=Path.Combine(split,"CT");var rtFolder=Path.Combine(split,"RT");Directory.CreateDirectory(imageFolder);Directory.CreateDirectory(rtFolder);
            foreach(var name in new[]{"RTPLAN.dcm","RTDOSE.dcm","RTSTRUCT.dcm"})File.Copy(Path.Combine(source,name),Path.Combine(rtFolder,name));
            var image=DicomFile.Open(Path.Combine(source,"RTDOSE.dcm")).Dataset;image.AddOrUpdate(DicomTag.SOPClassUID,DicomUID.CTImageStorage);image.AddOrUpdate(DicomTag.SOPInstanceUID,"1.2.826.0.1.3680043.10.543.91");image.AddOrUpdate(DicomTag.SeriesInstanceUID,"1.2.826.0.1.3680043.10.543.92");image.AddOrUpdate(DicomTag.Modality,"CT");var imagePath=Path.Combine(imageFolder,"image.dcm");new DicomFile(image).Save(imagePath);
            var foreign=DicomFile.Open(Path.Combine(source,"RTPLAN.dcm")).Dataset;foreign.AddOrUpdate(DicomTag.PatientID,"OTHER_TEST_PATIENT");foreign.AddOrUpdate(DicomTag.SOPInstanceUID,"1.2.826.0.1.3680043.10.543.93");new DicomFile(foreign).Save(Path.Combine(rtFolder,"foreign.dcm"));
            using(var viewer=Open(imagePath)){
                check(Field<Button>(viewer,"searchSubfolders").Visibility==Visibility.Visible,"image-only discovery shows bottom-right subfolder search");var before=Field<DicomEntry>(viewer,"currentEntry");
                Pump((Task)typeof(ViewerControl).GetMethod("SearchFolderAsync",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(viewer,new object[]{split}));
                check(Field<List<PlanData>>(viewer,"planData").Count==1&&Field<List<DoseGrid>>(viewer,"doses").Count==1&&viewer.StructureCount>0,"recursive UI search adds matching RT and rejects other patient");
                check(Field<DicomEntry>(viewer,"currentEntry").SopUid==before.SopUid&&viewer.HasImage,"recursive RT discovery preserves the displayed image");
                check(Field<bool>(viewer,"layersVisible")&&Field<Button>(viewer,"searchSubfolders").Content.ToString()=="Search subfolders…","RT panel opens and search remains repeatable");
            }
            var multi=Path.Combine(root,"multi-frame");Directory.CreateDirectory(multi);
            var imageA=DicomFile.Open(imagePath).Dataset;new DicomFile(imageA).Save(Path.Combine(multi,"image-A.dcm"));
            const string frameB="1.2.826.0.1.3680043.10.543.100";
            var imageB=imageA.Clone();imageB.AddOrUpdate(DicomTag.FrameOfReferenceUID,frameB);imageB.AddOrUpdate(DicomTag.SeriesInstanceUID,"1.2.826.0.1.3680043.10.543.101");imageB.AddOrUpdate(DicomTag.SOPInstanceUID,"1.2.826.0.1.3680043.10.543.102");new DicomFile(imageB).Save(Path.Combine(multi,"image-B.dcm"));
            var otherPlan=DicomFile.Open(Path.Combine(source,"RTPLAN.dcm")).Dataset;otherPlan.AddOrUpdate(DicomTag.FrameOfReferenceUID,frameB);otherPlan.AddOrUpdate(DicomTag.SOPInstanceUID,"1.2.826.0.1.3680043.10.543.103");otherPlan.AddOrUpdate(DicomTag.RTPlanLabel,"OTHER FRAME");otherPlan.Remove(DicomTag.ReferencedStructureSetSequence);new DicomFile(otherPlan).Save(Path.Combine(multi,"000-first-plan.dcm"));
            foreach(var name in new[]{"RTPLAN.dcm","RTDOSE.dcm","RTSTRUCT.dcm"})File.Copy(Path.Combine(source,name),Path.Combine(multi,name));
            using(var viewer=Open(Path.Combine(multi,"image-A.dcm"))){
                var cat=Field<DicomCatalog>(viewer,"catalog");check(cat.Stacks.Count==1&&cat.DeferredImages.Any(e=>e.FrameUid==frameB),"initial CT opening fully indexes only its own series and defers other image metadata");
                check(Field<PlanData>(viewer,"selectedPlan").FrameUid==imageA.GetSingleValue<string>(DicomTag.FrameOfReferenceUID),"matching plan wins over first discovered unrelated plan");
                var picker=Field<ComboBox>(viewer,"plans");Func<string,object> planChoice=frame=>picker.Items.Cast<object>().First(c=>(c.GetType().GetField("Plan").GetValue(c) as PlanData)?.FrameUid==frame);
                var choice=planChoice(frameB);check(choice.ToString().Contains("images not loaded"),"plan labels distinguish known deferred images");
                Action<object> select=c=>{typeof(ViewerControl).GetField("changing",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(viewer,true);picker.SelectedItem=c;typeof(ViewerControl).GetField("changing",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(viewer,false);Pump((Task)typeof(ViewerControl).GetMethod("SelectPlanChoiceAsync",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(viewer,null));};
                select(choice);check(!viewer.HasImage&&Field<RenderScene>(viewer,"latestScene").Volume==null&&Field<RenderScene>(viewer,"latestScene").Native==null,"unloaded-plan selection removes unrelated CT from scene");check(Field<string>(viewer,"workspaceMode")=="MLC"&&Field<MlcPlaybackControl>(viewer,"centralPlayback")!=null,"unloaded plan remains usable in MLC");
                var deferred=cat.DeferredImages.Where(e=>e.FrameUid==frameB).Select(e=>e.Path).ToArray();Pump((Task)typeof(ViewerControl).GetMethod("SearchImagesAsync",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(viewer,new object[]{multi,deferred}));
                check(viewer.HasImage&&Field<DicomEntry>(viewer,"currentEntry").FrameUid==frameB,"targeted image search loads and attaches the selected plan's frame");check(planChoice(frameB).ToString().Contains("images available"),"plan image status refreshes after loading");
                select(planChoice(imageA.GetSingleValue<string>(DicomTag.FrameOfReferenceUID)));check(viewer.HasImage&&Field<DicomEntry>(viewer,"currentEntry").FrameUid!=frameB,"returning to the original plan restores its own image frame");
                var doseChoice=picker.Items.Cast<object>().First(c=>c.GetType().GetField("Dose").GetValue(c)!=null);select(doseChoice);check(Field<PlanData>(viewer,"selectedPlan")==null&&Field<DoseGrid>(viewer,"selectedDose")!=null,"individual dose is selectable independently of its available plan");
                var structChoice=picker.Items.Cast<object>().First(c=>c.GetType().GetField("Structure").GetValue(c)!=null);select(structChoice);check(Field<PlanData>(viewer,"selectedPlan")==null&&Field<StructureSet>(viewer,"selectedStructure")!=null,"individual structure set is selectable independently of plans");
            }
            stage("plan",new[]{"RTPLAN.dcm"});
            using(var viewer=Open(Path.Combine(root,"plan","RTPLAN.dcm")))
            {
                check(!viewer.HasImage && viewer.StackCount==0,"RTPLAN-only opens without an image");
                check(Field<Button>(viewer,"searchSubfolders").Visibility==Visibility.Visible,"subfolder search stays available after RT data is already found");
                check(Field<string>(viewer,"workspaceMode")=="MLC","RTPLAN-only opens directly in MLC workspace");
                var plan=Field<PlanData>(viewer,"centralPlan");check(plan!=null && plan.Beams.Any(b=>b.ControlPoints.Any(c=>c.MlcLayers.Count>0)),"RTPLAN-only populates MLC layers");
                check(Field<MlcPlaybackControl>(viewer,"centralPlayback")?.Visibility==Visibility.Visible,"RTPLAN-only MLC control visible");
                check(Field<List<DoseGrid>>(viewer,"doses").Count==0 && !Field<bool>(viewer,"sumMode"),"Plan without dose usable and not a sum");
            }
            stage("structures",new[]{"RTSTRUCT.dcm"});
            using(var viewer=Open(Path.Combine(root,"structures","RTSTRUCT.dcm")))
            {
                check(!viewer.HasImage && viewer.StructureCount>0,"RTSTRUCT-only parses contours without CT");
                check(Field<string>(viewer,"workspaceMode")=="3D","RTSTRUCT-only selects 3D workspace");
                var scene=Field<RenderScene>(viewer,"latestScene");check(scene.Structures.Count>0 && scene.Volume==null && scene.Native==null,"RTSTRUCT-only 3D scene contains mapped ROIs without image");
            }
            stage("dose_structures",new[]{"RTDOSE.dcm","RTSTRUCT.dcm"});
            using(var viewer=Open(Path.Combine(root,"dose_structures","RTDOSE.dcm")))
            {
                check(!viewer.HasImage && Field<List<PlanData>>(viewer,"planData").Count==0,"Dose and structures load without plan or image");
                check(!Field<bool>(viewer,"sumMode") && Field<ComboBox>(viewer,"plans").SelectedItem!=null&&Field<PlanData>(viewer,"selectedPlan")==null,"Dose-only scan selects the actual standalone dose, never a plan sum");
                Invoke(viewer,"SetWorkspace","DVH");Invoke(viewer,"BuildRoiList");
                var dvh=Field<DvhControl>(viewer,"dvhView");Pump(dvh.Completion);
                check(dvh.CalculationCount>0 && !dvh.StatusText.Contains("Unable") && !dvh.StatusText.StartsWith("No visible"),"Dose plus structures computes DVH without a plan or CT");
                var roi=Field<List<StructureSet>>(viewer,"structures")[0].Rois[0];
                var row=Field<StackPanel>(viewer,"roiList").Children.OfType<DockPanel>().First();var jump=row.Children.OfType<Button>().Single();var toggle=row.Children.OfType<CheckBox>().Single();
                check(jump.IsEnabled && row.LastChildFill,"Sidebar ROI text button fills area outside checkbox in DVH");
                bool visible=roi.Visible;jump.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(ReferenceEquals(dvh.FocusedStructure,roi) && roi.Visible==visible,"Sidebar ROI click focuses DVH without toggling visibility");
                jump.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(dvh.FocusedStructure==null,"Second sidebar ROI click clears DVH focus");
                toggle.IsChecked=!visible;Pump(dvh.Completion);check(roi.Visible!=visible,"Sidebar ROI checkbox changes visibility independently");
            }
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
            var full=Path.GetFullPath(root);var temp=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            if(full.StartsWith(temp,StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(full).StartsWith("QuickLook-RT-only-test-",StringComparison.Ordinal))Directory.Delete(full,true);
        }
    }
}
