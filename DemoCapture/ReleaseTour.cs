using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using QuickLook.DicomRT;
internal static partial class Program
{
 static int RunReleaseTour(string folder,string destination,bool finish=false,bool mlcOnly=false)
 {
  Thread.CurrentThread.CurrentCulture=CultureInfo.GetCultureInfo("en-US");Thread.CurrentThread.CurrentUICulture=CultureInfo.GetCultureInfo("en-US");
  System.Windows.Media.RenderOptions.ProcessRenderMode=System.Windows.Interop.RenderMode.SoftwareOnly;
  output=Path.GetFullPath(destination);Directory.CreateDirectory(output);app=new Application();
  window=new Window{Width=Width,Height=Height,Left=-30000,Top=-30000,ShowInTaskbar=false,ShowActivated=false,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize};int result=0;
  window.Loaded+=async(s,e)=>{try{if(mlcOnly)await ReleaseMlc(folder);else if(finish)await FinishReleaseTour(folder);else await ReleaseTour(folder);WriteTourNotes(true);Console.WriteLine("RELEASE_TOUR_COMPLETE");}catch(Exception ex){result=1;Console.WriteLine("RELEASE_TOUR_FAILED "+ex);WriteTourNotes(false);}finally{viewer?.Dispose();window.Close();}};app.Run(window);return result;
 }
 static async Task ReleaseMlc(string folder)
 {
  var notes=(System.Collections.Generic.Dictionary<string,object>)new System.Web.Script.Serialization.JavaScriptSerializer{MaxJsonLength=int.MaxValue}.DeserializeObject(File.ReadAllText(Path.Combine(output,"capture-notes.json")));
  foreach(var a in (object[])notes["stills"])artifacts.Add(a);foreach(var a in (object[])notes["segments"])tourSegments.Add(a);rtOnlyCaptured=true;
  await Load(folder,"approved public nonpatient benchmark");Layers(true,true);SelectTourRois();Mode("MLC");Panels(true,false);var mlc=Get<MlcPlaybackControl>(viewer,"centralPlayback");
  Get<CheckBox>(mlc,"showOther").IsChecked=true;Get<CheckBox>(mlc,"showDrr").IsChecked=true;Get<CheckBox>(viewer,"showFields").IsChecked=true;Get<Slider>(mlc,"mlcOpacity").Value=.3;
  var beam=Get<PlanData>(viewer,"selectedPlan").Beams.First(b=>b.ControlPoints.Any(c=>c.MlcLayers.Count>0));
  mlc.Navigate(beam,15);await Task.Delay(2000);await mlc.ProjectionCompletion;await Save("mlc-outlines.png","Actual mid-field aperture, CT-derived DRR and highlighted effective outlines.");
  await TourSequence("mlc",60,new[]{"Actual MLC aperture","CT-derived DRR","Target and brainstem outlines","Colored structure selection"},async i=>{mlc.Navigate(beam,5+(beam.ControlPoints.Count-11)*i/59d);await Task.Delay(170);await mlc.ProjectionCompletion;});
 }
 static async Task FinishReleaseTour(string folder)
 {
  var notes=(System.Collections.Generic.Dictionary<string,object>)new System.Web.Script.Serialization.JavaScriptSerializer{MaxJsonLength=int.MaxValue}.DeserializeObject(File.ReadAllText(Path.Combine(output,"capture-notes.json")));
  foreach(var a in (object[])notes["stills"])artifacts.Add(a);foreach(var a in (object[])notes["segments"])tourSegments.Add(a);
  await Load(folder,"approved public nonpatient benchmark");Layers(true,true);SelectTourRois();Mode("Bild");Panels(false,false);
  Get<TextBox>(viewer,"isoLevels").Text="8; 16; 24";Invoke(viewer,"ApplyIsodoseLevels");Get<Slider>(viewer,"opacity").Value=.34;
  Get<ComboBox>(viewer,"planes").SelectedItem="MPR + 3D";await Settle();var three=Get<ThreeDControl>(viewer,"mprThreeD");Get<CheckBox>(three,"allRois").IsChecked=true;
  Get<Slider>(three,"opacity").Value=1;Get<Slider>(three,"skinOpacity").Value=.02;Get<CheckBox>(viewer,"showFields").IsChecked=true;
  await Task.Delay(700);await Settle();Invoke(three,"ResetCamera");Invoke(three,"FitFieldGuides");Set(three,"cameraAdjusted",true);Set(three,"distance",Get<double>(three,"distance")*.72);Invoke(three,"UpdateCamera");
  var mlc=Get<MlcPlaybackControl>(viewer,"centralPlayback");var beam=Get<PlanData>(viewer,"selectedPlan").Beams.First(b=>b.ControlPoints.Any(c=>c.MlcLayers.Count>0));mlc.Navigate(beam,15);
  await Save("hero-quad-fields.png","Actual 0.2.20 linked MPR + 3D: active field, dose, structures, and shared isocenter.");
  await CaptureTourTags();Mode("Bild");Panels(true,false);Get<ComboBox>(viewer,"planes").SelectedItem="Axial";DoseTab();await Save("dose-controls.png","Dose maximum and compact isodose controls.");await CaptureTourRtOnly(folder);
 }
 static async Task ReleaseTour(string folder)
 {
  await Load(folder,"approved public nonpatient benchmark");Layers(true,true);SelectTourRois();Mode("Bild");Panels(false,false);
  Get<CheckBox>(viewer,"wash").IsChecked=true;Get<CheckBox>(viewer,"iso").IsChecked=true;Get<Slider>(viewer,"opacity").Value=.3;
  Get<ComboBox>(viewer,"planes").SelectedItem="MPR + 3D";await Settle();var three=Get<ThreeDControl>(viewer,"mprThreeD");
  Get<CheckBox>(three,"allRois").IsChecked=true;Get<CheckBox>(three,"organs").IsChecked=true;
  Get<CheckBox>(viewer,"showFields").IsChecked=true;await Task.Delay(700);await Settle();Invoke(three,"ResetCamera");Invoke(three,"FitFieldGuides");
  var plan=Get<PlanData>(viewer,"selectedPlan");var mlc=Get<MlcPlaybackControl>(viewer,"centralPlayback");var beam=plan.Beams.First(b=>b.ControlPoints.Any(c=>c.MlcLayers.Count>0));
  mlc.Navigate(beam,beam.ControlPoints.Count*.35);await Task.Delay(400);
  await Save("hero-quad-fields.png","Actual version 0.2.20 viewer: linked MPR + 3D, dose, targets and active field geometry.");
  var focus=Get<Vec3>(viewer,"focus");
  await TourSequence("mpr",60,new[]{"Linked MPR + 3D","Active fields","Dose and structures"},async i=>{await Call(viewer,"MoveFocusAsync",focus+new Vec3(4*Math.Sin(i/59d*Math.PI*2),3*Math.Sin(i/59d*Math.PI*2),10*Math.Sin(i/59d*Math.PI*2)));mlc.Navigate(beam,(beam.ControlPoints.Count-1)*i/59d);});
  await Call(viewer,"MoveFocusAsync",focus);Panels(true,false);await Save("rt-hierarchy.png","Individual plan, dose and structure selection with image-availability label; targeted search controls.");
  Mode("3D");Panels(false,false);await Settle();double yaw=Get<double>(three,"yaw");await Save("three-d-fields.png","Shared 3D geometry with the active field and collimator-oriented aperture.");
  await TourSequence("orbit",60,new[]{"3D orbit","Field geometry","Changing actual MLC aperture"},i=>{Set(three,"yaw",yaw+i/59d*Math.PI*.65);Invoke(three,"UpdateCamera");mlc.Navigate(beam,(beam.ControlPoints.Count-1)*i/59d);return Task.CompletedTask;});
  Mode("MLC");Panels(true,false);Get<CheckBox>(mlc,"showOther").IsChecked=true;Get<CheckBox>(mlc,"showDrr").IsChecked=true;
  mlc.Navigate(beam,0);await Task.Delay(1500);await mlc.ProjectionCompletion;await Save("mlc-outlines.png","Actual MLC with CT-derived DRR, projected outlines and colored effective-selection indicators.");
  await TourSequence("mlc",60,new[]{"Actual aperture","CT-derived DRR","Projected target and organ outlines","Colored structure indicators"},async i=>{mlc.Navigate(beam,(beam.ControlPoints.Count-1)*i/59d);await Task.Delay(160);await mlc.ProjectionCompletion;});
  Get<CheckBox>(mlc,"showDrr").IsChecked=false;
  await CaptureTourDvh();await CaptureTourTags();
  Mode("Bild");Panels(true,false);Get<ComboBox>(viewer,"planes").SelectedItem="Axial";DoseTab();await Save("dose-controls.png","Dose maximum navigation and collapsible display controls.");
  await CaptureTourRtOnly(folder);
 }
}
