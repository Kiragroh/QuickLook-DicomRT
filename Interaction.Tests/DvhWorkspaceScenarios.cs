using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dicom;
using QuickLook.DicomRT;
static class DvhWorkspaceScenarios
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static object Get(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
    static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,F).Invoke(o,a);
    public static void Run(Action<bool,string> check)
    {
        var refs=new[]{new DicomDataset{{DicomTag.DoseReferenceType,"TARGET"},{DicomTag.TargetPrescriptionDose,40d}},new DicomDataset{{DicomTag.DoseReferenceType,"TARGET"},{DicomTag.TargetPrescriptionDose,50d}},new DicomDataset{{DicomTag.DoseReferenceType,"ORGAN_AT_RISK"},{DicomTag.TargetPrescriptionDose,99d}}};
        var dataset=new DicomDataset();dataset.Add(new DicomSequence(DicomTag.DoseReferenceSequence,refs));
        var plan=PlanData.Load(new DicomEntry{SopUid="test-plan",Dataset=dataset});
        check(plan.PrescriptionDosesGy.SequenceEqual(new[]{40d,50d}),"Prescription reads TARGET doses only, including multiple levels without ROI references");
        using(var viewer=new ViewerControl())
        {
            ((List<PlanData>)Get(viewer,"planData")).Add(plan);var dose=new DoseGrid{Entry=new DicomEntry(),PlanUid="test-plan",Units="GY",SummationType="PLAN"};((List<DoseGrid>)Get(viewer,"doses")).Add(dose);
            check(((double[])Call(viewer,"DvhPrescriptions",dose)).Length==2,"Prescription follows exact dose-to-plan reference");
            dose.PlanUid="other";check(((double[])Call(viewer,"DvhPrescriptions",dose)).Length==0,"Unmatched plan never supplies prescription");dose.PlanUid="test-plan";
            dose.Units="RELATIVE";check(((double[])Call(viewer,"DvhPrescriptions",dose)).Length==0,"Relative dose does not receive a Gy prescription");dose.Units="GY";
            dose.SummationType="FRACTION";check(((double[])Call(viewer,"DvhPrescriptions",dose)).Length==0,"Fraction dose does not receive total-plan prescription");dose.SummationType="PLAN";
            check(((double[])Call(viewer,"DvhPrescriptions",new DoseGrid{PlanUid=dose.PlanUid,Units="GY",SummationType="PLAN"})).Length==0,"Generated dose never impersonates a source plan dose");
            ((List<DoseGrid>)Get(viewer,"doses")).Clear();((List<PlanData>)Get(viewer,"planData")).Clear();
            ((ComboBox)Get(viewer,"planes")).SelectedItem="MPR + 3D";var three=Get(viewer,"threeDView");
            Call(viewer,"SetQuadDvh",true);var dvh=(DvhControl)Get(viewer,"dvhView");var holder=(Grid)Get(viewer,"dvhContainer");
            check(ReferenceEquals(holder.Parent,Get(viewer,"quadBody"))&&holder.Visibility==Visibility.Visible,"Fourth pane displays the shared DVH control");
            int count=dvh.CalculationCount;Call(viewer,"Redraw");Call(viewer,"Redraw");check(dvh.CalculationCount==count,"Slice redraw does not recalculate DVHs");
            Call(viewer,"SetWorkspace","DVH");check(ReferenceEquals(holder.Parent,Get(viewer,"workspace")),"Full DVH reparents its existing control");
            Call(viewer,"SetWorkspace","Bild");check(ReferenceEquals(holder.Parent,Get(viewer,"quadBody")),"Returning to quad preserves DVH pane choice");
            Call(viewer,"SetQuadDvh",false);check(ReferenceEquals(three,Get(viewer,"threeDView"))&&((UIElement)three).Visibility==Visibility.Visible,"Returning to 3D retains its control and cached scene");
            Call(viewer,"SetQuadDvh",true);check(ReferenceEquals(dvh,Get(viewer,"dvhView")),"Repeated 3D/DVH switching reuses DVH caches");
            viewer.Measure(new Size(1300,900));viewer.Arrange(new Rect(0,0,1300,900));viewer.UpdateLayout();
            var bitmap=new RenderTargetBitmap(1300,900,96,96,PixelFormats.Pbgra32);bitmap.Render(viewer);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=System.IO.File.Create(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"quad-dvh-review.png")))encoder.Save(file);
        }
        using(var view=new DvhControl())
        {
            var plot=Get(view,"plot");plot.GetType().GetField("Units").SetValue(plot,"GY");view.SetPrescriptions(new[]{40d,50d,double.NaN,-1d});
            var curveType=plot.GetType().GetNestedType("Curve",BindingFlags.Public);var empty=Array.CreateInstance(curveType,0);
            check((double)Call(plot,"AxisMaximum",new object[]{empty})==50,"Axis includes prescription values beyond currently calculated curves");
            check(((double[])Call(plot,"DoseTicks",50d)).SequenceEqual(Enumerable.Range(0,11).Select(i=>i*5d)),"Absolute DVH ticks remain 5 Gy apart, with 10 Gy major marks");
            plot.GetType().GetField("Units").SetValue(plot,"RELATIVE");check((double)Call(plot,"AxisMaximum",new object[]{empty})==1,"Relative axes ignore Gy prescription extents");
        }
        var roi=new StructureRoi{Name="ROI name",Description="Full description"};
        var labels=typeof(ViewerControl).Assembly.GetType("QuickLook.DicomRT.RoiLabels");
        foreach(RoiLabelMode mode in Enum.GetValues(typeof(RoiLabelMode))){roi.LabelMode=mode;var detail=(string)labels.GetMethod("Details").Invoke(null,new object[]{roi});check(detail.Contains(roi.Name)&&detail.Contains(roi.Description),"Hover retains both labels in every display mode");}
    }
}
