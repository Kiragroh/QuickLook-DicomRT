using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using QuickLook.DicomRT;

internal static class NavigationExportScenarios
{
    const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance;
    static T Get<T>(object o,string n)=>(T)o.GetType().GetField(n,Flags).GetValue(o);
    static void Set(object o,string n,object v)=>o.GetType().GetField(n,Flags).SetValue(o,v);
    static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,Flags).Invoke(o,args);
    public static void Run(Action<bool,string> check)
    {
        using(var viewer=new ViewerControl()){
            check(Get<Dictionary<string,Button>>(viewer,"imageModeButtons").Count==6,"six icon view buttons include standalone 3D");
            check(Get<ComboBox>(viewer,"planes").Parent==null,"image plane dropdown removed from visible UI");
            check(Get<string>(viewer,"windowPreset")=="DICOM"&&Get<StackPanel>(viewer,"customWindow").Visibility==Visibility.Collapsed,"DICOM is default and custom sliders hidden");
            Set(viewer,"currentEntry",new DicomEntry{Modality="CT",WindowCenter=900,WindowWidth=8000});Call(viewer,"SetInitialWindow");
            check(Get<string>(viewer,"windowPreset")=="DICOM"&&Get<double>(viewer,"windowCenter")==900&&Get<double>(viewer,"windowWidth")==8000,"CT opens with its stored DICOM center and width");
            Set(viewer,"currentEntry",new DicomEntry{Modality="MR",WindowCenter=120,WindowWidth=250});Call(viewer,"SetInitialWindow");
            check(Get<string>(viewer,"windowPreset")=="DICOM"&&Get<double>(viewer,"windowCenter")==120&&Get<double>(viewer,"windowWidth")==250,"MR uses its own stored DICOM window too");
            Set(viewer,"currentEntry",new DicomEntry{Modality="CT"});Call(viewer,"SetInitialWindow");
            check(Get<string>(viewer,"windowPreset")=="DICOM"&&Get<double>(viewer,"windowCenter")==500&&Get<double>(viewer,"windowWidth")==1000,"Missing DICOM window uses automatic fallback without a CT-specific preset");
            Call(viewer,"ApplyWindowPreset","Auto");check(Get<string>(viewer,"windowPreset")=="Auto","Auto remains manually selectable");
            Call(viewer,"ApplyWindowPreset","Lung");check(Get<double>(viewer,"windowCenter")==-600&&Get<double>(viewer,"windowWidth")==1500,"lung preset width and level");
            Call(viewer,"ApplyWindowPreset","Custom");check(Get<StackPanel>(viewer,"customWindow").Visibility==Visibility.Visible&&Get<double>(viewer,"windowCenter")==-600,"custom exposes sliders and keeps current values");
            var bar=Get<object>(viewer,"windowRange");var element=(FrameworkElement)bar;element.Measure(new Size(142,184));element.Arrange(new Rect(0,0,142,184));Call(bar,"Change",50d);check(Get<double>(viewer,"windowWidth")>=1,"histogram window handle keeps a positive range");
            var plan=new PlanData();plan.Beams.Add(new PlanBeam{Number=1,ControlPoints={new ControlPoint()}});Set(viewer,"selectedPlan",plan);Call(viewer,"Redraw");Get<CheckBox>(viewer,"showFields").IsChecked=true;Get<ComboBox>(viewer,"fieldPicker").SelectedIndex=0;
            check(Get<RenderScene>(viewer,"latestScene").ActiveBeam==null,"neutral fields mode has no highlighted beam");Call(viewer,"Redraw");check(Get<RenderScene>(viewer,"latestScene").ActiveBeam==null,"neutral selection survives redraw");
            check(Get<Slider>(viewer,"fieldCursor").Visibility==Visibility.Collapsed,"neutral fields hide irrelevant CP controls");
            plan.FrameUid="frame-new";plan.StructureSopUid="structure-new";
            var set=new StructureSet{Entry=new DicomEntry{SopUid="structure-new"},ReferencedSeries={"series-new"}};Get<List<StructureSet>>(viewer,"structures").Add(set);
            var wanted=new ImageStack{FrameUid="frame-new",Entries={new DicomEntry{Modality="CT",SeriesUid="series-new",FrameUid="frame-new"}}};
            var unrelated=new ImageStack{FrameUid="frame-old",Entries={new DicomEntry{Modality="CT",SeriesUid="series-old",FrameUid="frame-old"}}};
            var catalog=new DicomCatalog{Stacks={unrelated,wanted}};Set(viewer,"catalog",catalog);
            check(ReferenceEquals(Call(viewer,"MatchingPlanStack"),wanted),"plan image switch follows referenced series and matching frame");
            catalog.Stacks.Add(new ImageStack{FrameUid="frame-new",Entries={new DicomEntry{Modality="CT",SeriesUid="series-new",FrameUid="frame-new"}}});
            check(Call(viewer,"MatchingPlanStack")==null,"ambiguous referenced stacks are not chosen arbitrarily");

        }
        using(var dvh=new DvhControl()){
            var a=new StructureRoi{Name="Enabled, \"one\"",InterpretedType="PTV"};var b=new StructureRoi{Name="Disabled",InterpretedType="ORGAN"};var pending=new StructureRoi{Name="Pending",InterpretedType="GTV"};
            Set(dvh,"exportRois",new[]{a,b,pending});Get<Dictionary<StructureRoi,bool>>(dvh,"visibility")[b]=false;
            ExportIdentity.SetContext(dvh,new ExportIdentity{PatientId="DEMO/ID",PlanId="Plan:One"});
            var file=ExportIdentity.GetContext(dvh).FileName("DVH",".csv");
            check(file.StartsWith("Patient_DEMO_ID_Plan_Plan_One_DVH_")&&file.EndsWith(".csv"),"identifiers included and filesystem-safe in export filenames");
            var plot=Get<object>(dvh,"plot");var curves=(IList)Get<object>(plot,"Curves");var curveType=curves.GetType().GetGenericArguments()[0];
            foreach(var roi in new[]{a,b}){var result=new DvhResult();foreach(var property in new[]{Tuple.Create("DoseValues",(object)new[]{0d,10.12345678d}),Tuple.Create("CumulativeVolumePercent",(object)new[]{100d,50d}),Tuple.Create("DoseUnits",(object)"GY")})typeof(DvhResult).GetProperty(property.Item1).SetValue(result,property.Item2);var curve=Activator.CreateInstance(curveType);Set(curve,"Roi",roi);Set(curve,"Result",result);Set(curve,"Color",Brushes.Red);Set(curve,"Visible",roi==a);curves.Add(curve);}
            Set(plot,"FocusedStructure",b);var csv=dvh.ExportCsv();
            check(csv.StartsWith("PatientID,PlanID,")&&csv.Contains("\"DEMO/ID\",\"Plan:One\"")&&csv.Contains("10.1235")&&!csv.Contains("10.123456"),"detailed CSV carries IDs and rounds to at most four decimals");
            var simple=dvh.ExportCsv(true);check(simple.StartsWith("Structure,Dose,DoseUnit,VolumePercent\r\n")&&!simple.Contains("Dmean")&&!simple.Contains("PatientID")&&!simple.Contains("Pending")&&!simple.Contains("Disabled")&&simple.Contains("10.1235"),"simple CSV has only four importable columns and enabled calculated curves");check(csv.Contains("\"Enabled, \"\"one\"\"\"")&&!csv.Contains("Disabled"),"DVH CSV escapes names and exports enabled structures regardless of focus");
            check(csv.Contains("Pending")&&csv.Contains("NotCalculated"),"DVH export identifies unavailable active curves instead of silently omitting them");
            check(csv.Contains("Dmean,Dmedian,Dmax,Dmin,D98,D2"),"CSV includes all requested summary metrics");
            var image=dvh.ExportChart();check(image.PixelWidth==1500&&image.PixelHeight>=900,"DVH chart exports a full chart with legend");
            check(dvh.ContextMenu.Items.Count==4,"DVH right-click offers CSV and PNG");
        }
    }
}
