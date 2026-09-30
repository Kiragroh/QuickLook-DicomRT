using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using QuickLook.DicomRT;
internal static class PreloadWorkspaceTests
{
 const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static T Get<T>(object target,string name)=>(T)target.GetType().GetField(name,Flags).GetValue(target);
 static void Set(object target,string name,object value)=>target.GetType().GetField(name,Flags).SetValue(target,value);
 static void Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Flags).Invoke(target,args);
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);}
 static StructureRoi[] VisibleRois(ThreeDControl control){var prepared=Get<object>(control,"prepared");return ((IEnumerable)prepared.GetType().GetField("Parts").GetValue(prepared)).Cast<object>().Select(p=>(StructureRoi)p.GetType().GetField("Roi").GetValue(p)).Where(r=>r!=null).ToArray();}
 static void Ready(ThreeDControl control)=>FrameBenchmark.Pump(()=>Get<object>(control,"prepared")!=null&&Get<object>(control,"pending")==null,20);
 public static void Run()
 {
  SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
  using(var early=new ThreeDControl()){early.PreloadScene(new RenderScene{Entry=new DicomEntry{Modality="RTPLAN"},Doses=new List<DoseOverlay>{new DoseOverlay{Dose=new DoseGrid{Maximum=20,Units="GY"}}}});Check(Get<CheckBox>(early,"dose").IsChecked==false,"incremental RTPLAN dose discovery does not enable a dose surface before images/structures arrive");}
  var rois=new[]{"PTV","ORGAN","SUPPORT","EXTERNAL","AVOIDANCE"}.Select(t=>{var r=QualityTests.Sphere();r.InterpretedType=t;r.FrameUid="test";return r;}).ToArray();
  using(var viewer=new ViewerControl())
  {
   Set(viewer,"volume",new VolumeData{Width=24,Height=24,Depth=24,Origin=new Vec3(-12,-12,-12),AxisX=new Vec3(1,0,0),AxisY=new Vec3(0,1,0),AxisZ=new Vec3(0,0,1),SpacingX=1,SpacingY=1,SpacingZ=1,Values=new float[24*24*24]});
   Set(viewer,"currentEntry",new DicomEntry{Modality="MR",FrameUid="test",SeriesUid="test",HasGeometry=true});
   Get<List<StructureSet>>(viewer,"structures").Add(new StructureSet{Entry=new DicomEntry{SopUid="test"},Rois=rois.ToList()});
   Call(viewer,"Redraw");var control=Get<ThreeDControl>(viewer,"threeDView");
   Check(control!=null&&!control.IsVisible,"3D preparation starts before the 3D workspace is opened");Ready(control);
   Check(VisibleRois(control).SequenceEqual(new[]{rois[0]}),"only PTV surfaces initially prepared");
   foreach(string field in new[]{"organs","support","external","allRois"})Check(Get<CheckBox>(control,field).IsChecked==false,field+" defaults off");
   var prepared=Get<object>(control,"prepared");int generation=Get<int>(control,"generation");
   var window=new Window{Width=1300,Height=900,Left=-30000,Top=-30000,ShowActivated=false,ShowInTaskbar=false,Content=viewer};
   try
   {
    window.Show();Call(viewer,"SetWorkspace","3D");Ready(control);
    Check(ReferenceEquals(prepared,Get<object>(control,"prepared"))&&generation==Get<int>(control,"generation"),"first 3D activation reuses preloaded mesh");
    foreach(var pair in new[]{Tuple.Create("organs",1),Tuple.Create("allRois",4)})
    {
     Get<CheckBox>(control,pair.Item1).IsChecked=true;Check(Get<object>(control,"prepared")!=null&&VisibleRois(control).Contains(rois[0]),"existing PTV remains visible while missing surfaces build");Ready(control);Check(VisibleRois(control).Contains(rois[pair.Item2]),pair.Item1+" independently renders its own DICOM type");
     Get<CheckBox>(control,pair.Item1).IsChecked=false;Ready(control);Check(!VisibleRois(control).Contains(rois[pair.Item2]),pair.Item1+" independently hides its DICOM type");
    }
    foreach(string field in new[]{"support","external"})Check(!Get<CheckBox>(control,field).IsVisible&&!Get<CheckBox>(control,field).IsEnabled,field+" removed from visible UI");
    Get<CheckBox>(control,"allRois").IsChecked=true;Ready(control);Check(!VisibleRois(control).Contains(rois[2])&&!VisibleRois(control).Contains(rois[3]),"Other does not admit SUPPORT or EXTERNAL");
    control.FocusStructure(rois[2]);Check(Get<StructureRoi>(control,"focusedRoi")==null,"SUPPORT cannot bypass filter through selection");
    Get<CheckBox>(control,"allRois").IsChecked=false;Ready(control);
    Get<CheckBox>(control,"organs").IsChecked=true;Get<Slider>(control,"opacity").Value=.43;Get<Slider>(control,"skinOpacity").Value=.12;
    Get<ComboBox>(control,"doseLevel").SelectedIndex=2;Set(control,"cameraAdjusted",true);Set(control,"yaw",.73);Set(control,"distance",234d);Call(control,"UpdateCamera");Ready(control);
    prepared=Get<object>(control,"prepared");generation=Get<int>(control,"generation");
    Call(viewer,"SetWorkspace","Bild");Get<ComboBox>(viewer,"planes").SelectedItem="MPR + 3D";window.UpdateLayout();Ready(control);
    Check(ReferenceEquals(control,Get<ThreeDControl>(viewer,"mprThreeD")),"MPR and full layouts use the same control instance");
    Check(ReferenceEquals(prepared,Get<object>(control,"prepared"))&&generation==Get<int>(control,"generation"),"layout change retains exact prepared geometry without rebuild");
    Check(Get<CheckBox>(control,"organs").IsChecked==true&&Math.Abs(Get<Slider>(control,"opacity").Value-.43)<1e-8&&Get<ComboBox>(control,"doseLevel").SelectedIndex==2,"MPR retains filter, opacity and dose level");
    Check(Get<double>(control,"yaw")==.73&&Get<double>(control,"distance")==234,"layout change retains manual camera");
    var settingsPopup=Get<System.Windows.Controls.Primitives.Popup>(control,"compactSettings");Check(!settingsPopup.IsOpen,"MPR settings start collapsed");settingsPopup.IsOpen=true;window.UpdateLayout();
    foreach(string field in new[]{"structures","organs","allRois","bone","skin","dose"})Check(Get<CheckBox>(control,field).IsVisible,"same "+field+" control available in MPR settings popup");
    foreach(string field in new[]{"support","external"})Check(!Get<CheckBox>(control,field).IsVisible,field+" remains absent in MPR");
    settingsPopup.IsOpen=false;
    Call(viewer,"SetWorkspace","3D");Ready(control);Check(ReferenceEquals(prepared,Get<object>(control,"prepared"))&&generation==Get<int>(control,"generation"),"return to full view retains meshes");
    Call(viewer,"SetWorkspace","Bild");Get<ComboBox>(viewer,"planes").SelectedItem="Native";Call(viewer,"SetWorkspace","3D");Ready(control);Check(ReferenceEquals(prepared,Get<object>(control,"prepared")),"leaving MPR for native image and returning retains meshes");
   }
   finally{window.Close();}
  }
  using(var control=new ThreeDControl()){control.PreloadScene(new RenderScene{Structures=rois.Select(r=>new RoiOverlay{Roi=r}).ToList()});control.Dispose();FrameBenchmark.Pump(()=>Get<object>(control,"pending")==null,2);Check(Get<object>(control,"prepared")==null,"closing cancels preloading without stale publication");}
  Console.WriteLine("PASS hidden preloading, independent ROI type filters, identical full/MPR settings, exact mesh/camera retention and cancellation");
 }
}
