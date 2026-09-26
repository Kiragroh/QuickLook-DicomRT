using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using QuickLook.DicomRT;

internal static partial class Program
{
 const int TourFrames=120;
 static readonly List<object> tourSegments=new List<object>();
 static readonly List<string> tourChecks=new List<string>();
 static bool rtOnlyCaptured;
 static int RunPublicTour(string folder,string destination,bool refresh=false,bool orbitOnly=false,bool mlcOnly=false)
 {
  Thread.CurrentThread.CurrentCulture=CultureInfo.GetCultureInfo("en-US");
  Thread.CurrentThread.CurrentUICulture=CultureInfo.GetCultureInfo("en-US");
  System.Windows.Media.RenderOptions.ProcessRenderMode=System.Windows.Interop.RenderMode.SoftwareOnly;
  output=Path.GetFullPath(destination);Directory.CreateDirectory(output);
  app=new Application();window=new Window{Width=Width,Height=Height,Left=-30000,Top=-30000,ShowInTaskbar=false,ShowActivated=false,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,Title="DICOM RT public tour renderer"};
  int result=0;
  window.Loaded+=async(s,e)=>{try{if(mlcOnly)await ResumeTourMlc(folder);else if(refresh)await RefreshTour(folder,orbitOnly);else await PublicTour(folder);WriteTourNotes(true);Console.WriteLine("PUBLIC_TOUR_COMPLETE");}catch(Exception ex){result=1;omissions.Add("Capture interrupted: "+ex.GetType().Name);WriteTourNotes(false);Console.WriteLine("PUBLIC_TOUR_FAILED "+ex);}finally{viewer?.Dispose();window.Close();}};
  app.Run(window);return result;
 }
 static void WriteTourNotes(bool complete)
 {
  var data=new{version="0.2.5",complete,width=Width,height=Height,nominalFps=15,source="Explicitly approved public nonpatient benchmark",method="Settled actual ViewerControl states; actual Direct3D GPU readback composed at its control position. Timeline is editorial and does not measure playback speed.",segments=tourSegments,checks=tourChecks,stills=artifacts,availability,limitations=new[]{"This source has no MR or registration object; fusion is not demonstrated.","This source has one plan; plan summation is not demonstrated.",rtOnlyCaptured?"RT-only capture opens an unchanged copy of the approved public RTPLAN in an isolated temporary folder without CT, structures or dose.":"RT-only entry without an image series is not part of this capture.","Percentage mode is shown without activating Apply globally or Default; no global preference write.","Tag values are redacted in display memory only. Copy controls are visible but clipboard actions are not performed.","DVH and derived surfaces are approximate previews, not clinical validation."},omissions};
  string json=new JavaScriptSerializer{MaxJsonLength=int.MaxValue}.Serialize(data);
  File.WriteAllText(Path.Combine(output,"capture-notes.json"),json);File.WriteAllText(Path.Combine(output,"manifest.json"),json);
 }
 static async Task TourSequence(string name,int count,string[] features,Func<int,Task> update)
 {
  for(int i=0;i<count;i++){await update(i);await Save(name+"/frame-"+i.ToString("000")+".png",string.Join("; ",features),false);if(i%20==0)Console.WriteLine("TOUR_SEQUENCE "+name+" "+i+"/"+count);}
  tourSegments.Add(new{folder=name,pattern="frame-%03d.png",frameCount=count,nominalFps=15,durationSeconds=count/15.0,shownFeatures=features});WriteTourNotes(false);
 }
 static List<StructureRoi> PublicTourRois()
 {
  var all=Get<List<StructureSet>>(viewer,"structures").SelectMany(s=>s.Rois).ToList();
  return all.Where(r=>r.InterpretedType=="PTV"||r.Name.IndexOf("brainstem",StringComparison.OrdinalIgnoreCase)>=0||r.Name.IndexOf("chiasm",StringComparison.OrdinalIgnoreCase)>=0).ToList();
 }
 static void SelectTourRois()
 {
  var selected=PublicTourRois();foreach(var roi in Get<List<StructureSet>>(viewer,"structures").SelectMany(s=>s.Rois))roi.Visible=selected.Contains(roi);Invoke(viewer,"RefreshRt");
 }
 static async Task RefreshTour(string folder,bool orbitOnly)
 {
  var notes=(Dictionary<string,object>)new JavaScriptSerializer{MaxJsonLength=int.MaxValue}.DeserializeObject(File.ReadAllText(Path.Combine(output,"capture-notes.json")).Replace("brainstem/chiasm","brainstem").Replace("Brainstem/chiasm","Brainstem"));
  foreach(var artifact in (object[])notes["stills"]){string name=(string)((Dictionary<string,object>)artifact)["file"];if(name=="rt-only.png")rtOnlyCaptured=true;if(!name.StartsWith("three-d-")&&(orbitOnly||name!="mpr-three.png"&&name!="mlc.png"&&name!="rt-only.png"))artifacts.Add(artifact);}
  foreach(var segment in (object[])notes["segments"]){string name=(string)((Dictionary<string,object>)segment)["folder"];if(name!="orbit"&&(orbitOnly||name!="mpr"&&name!="mlc"))tourSegments.Add(segment);}
  foreach(var check in (object[])notes["checks"])if(!check.ToString().StartsWith("3D ")&&(orbitOnly||!check.ToString().StartsWith("MPR ")&&!check.ToString().StartsWith("MLC ")))tourChecks.Add(check.ToString());
  await Load(folder,"approved public nonpatient benchmark");Layers(true,true);SelectTourRois();Get<CheckBox>(viewer,"wash").IsChecked=true;Get<CheckBox>(viewer,"iso").IsChecked=true;Get<Slider>(viewer,"opacity").Value=.38;
  if(orbitOnly){await CaptureTourOrbit();return;}await CaptureTourSpatial();await CaptureTourOrbit();await CaptureTourMlc();await CaptureTourRtOnly(folder);
 }
 static async Task ResumeTourMlc(string folder)
 {
  var notes=(Dictionary<string,object>)new JavaScriptSerializer{MaxJsonLength=int.MaxValue}.DeserializeObject(File.ReadAllText(Path.Combine(output,"capture-notes.json")));
  foreach(var artifact in (object[])notes["stills"]){string name=(string)((Dictionary<string,object>)artifact)["file"];if(name!="mlc.png"&&name!="rt-only.png")artifacts.Add(artifact);}
  foreach(var segment in (object[])notes["segments"])if((string)((Dictionary<string,object>)segment)["folder"]!="mlc")tourSegments.Add(segment);
  foreach(var check in (object[])notes["checks"])if(!check.ToString().StartsWith("MLC ")&&!check.ToString().StartsWith("Unchanged approved"))tourChecks.Add(check.ToString());
  await Load(folder,"approved public nonpatient benchmark");await CaptureTourMlc();await CaptureTourRtOnly(folder);tourChecks.Add("MLC and RT-only captures used WPF software composition of the actual unchanged components in a fresh process.");
 }
 static async Task PublicTour(string folder)
 {
  await Load(folder,"approved public nonpatient benchmark");
  Mode("Bild");Panels(false,false);Layers(false,false);await Save("image-clean.png","Original CT, approved public nonpatient benchmark.");
  Layers(true,true);SelectTourRois();Panels(true,false);Get<CheckBox>(viewer,"wash").IsChecked=true;Get<CheckBox>(viewer,"iso").IsChecked=true;Get<Slider>(viewer,"opacity").Value=.38;
  await Save("rt-overview.png","CT with all PTVs, the selected Brainstem and physical RTDOSE overlays.");await StructureJump();
  Panels(false,false);Get<ComboBox>(viewer,"planes").SelectedItem="Native";
  var stack=Get<ImageStack>(viewer,"currentStack");int center=Get<int>(viewer,"sliceIndex"),start=Math.Max(0,center-38),end=Math.Min(stack.Entries.Count-1,center+38);
  await TourSequence("scroll",TourFrames,new[]{"Native CT slice navigation","RT contours","Colorwash and isodose lines"},async i=>await Call(viewer,"ShowSliceAsync",(int)Math.Round(start+(end-start)*i/(TourFrames-1.0)),true));
  await Call(viewer,"ShowSliceAsync",center,true);await CaptureTourDose();
  await CaptureTourSpatial();await CaptureTourOrbit();await CaptureTourMlc();await CaptureTourDvh();await CaptureTourTags();await CaptureTourRtOnly(folder);
 }
 static async Task CaptureTourSpatial()
 {
  Panels(false,false);Mode("Bild");Get<ComboBox>(viewer,"planes").SelectedItem="MPR + 3D";await Settle();
  var shared=Get<ThreeDControl>(viewer,"mprThreeD");Get<CheckBox>(shared,"organs").IsChecked=true;Get<CheckBox>(shared,"allRois").IsChecked=true;Get<Slider>(shared,"skinOpacity").Value=.09;Get<Slider>(shared,"opacity").Value=.85;var initial=Get<Vec3>(viewer,"focus");await Save("mpr-three.png","Synchronized axial, coronal, sagittal and shared 3D coordinate planes.");
  await TourSequence("mpr",TourFrames,new[]{"Axial, coronal and sagittal synchronization","Moving physical 3D coordinate planes","Shared 3D control"},async i=>{double a=i/(TourFrames-1.0)*Math.PI*2;await Call(viewer,"MoveFocusAsync",initial+new Vec3(9*Math.Sin(a),7*Math.Sin(a),24*Math.Sin(a)));if(!ReferenceEquals(shared,Get<ThreeDControl>(viewer,"mprThreeD")))throw new InvalidOperationException("Shared MPR control changed");});
  await Call(viewer,"MoveFocusAsync",initial);Mode("3D");await Settle();
  if(!ReferenceEquals(shared,Get<ThreeDControl>(viewer,"threeDView")))throw new InvalidOperationException("3D workspace did not retain shared control");tourChecks.Add("MPR and full 3D use the same ThreeDControl instance throughout transitions.");
 }
 static async Task CaptureTourDose()
 {
  Panels(true,false);Mode("Bild");Get<ComboBox>(viewer,"planes").SelectedItem="Axial";DoseTab();
  var wash=Get<CheckBox>(viewer,"wash");var iso=Get<CheckBox>(viewer,"iso");var mode=Get<ComboBox>(viewer,"isodoseMode");mode.SelectedIndex=0;
  if(!Get<bool>(viewer,"absoluteIsodoses"))throw new InvalidOperationException("Local Gy controls unavailable");
  await TourSequence("dose",TourFrames,new[]{"Wash opacity adjustment","Isodoses on and off","Custom local Gy levels","Gy Default restore","Relative global controls shown without saving"},async i=>{
   if(i==0){wash.IsChecked=true;iso.IsChecked=false;await Save("dose-wash.png","RTDOSE colorwash with adjustable opacity.");}
   if(i==24){wash.IsChecked=false;iso.IsChecked=true;await Save("dose-isodoses.png","Isodose lines with colorwash hidden.");}
   if(i==44){wash.IsChecked=true;iso.IsChecked=true;await Save("dose-combined.png","Colorwash and isodose lines combined.");}
   if(i<44)Get<Slider>(viewer,"opacity").Value=.25+.2*Math.Sin(i/44.0*Math.PI);
   if(i==60){Get<TextBox>(viewer,"isoLevels").Text="4.25; 8; 12; 16; 20";Invoke(viewer,"ApplyIsodoseLevels");await Save("dose-levels-edited.png","Custom Gy levels applied only to the current dose selection and viewer session.");}
   if(i==80){Invoke(viewer,"DefaultIsodoseLevels");await Save("dose-default-gy.png","Default restores automatic whole-Gy levels for the current dose selection.");}
   if(i==100){mode.SelectedIndex=1;await Save("relative-isodoses.png","Relative percentage controls: Apply globally and Default are visible; neither is activated.");}
  });
  mode.SelectedIndex=0;Get<Slider>(viewer,"opacity").Value=.38;tourChecks.Add("Gy Apply and Gy Default were invoked only while absoluteIsodoses was true; no percentage preference write was invoked.");
 }
 static async Task CaptureTourOrbit()
 {
  Mode("3D");Panels(false,false);SelectTourRois();await Settle();var three=Get<ThreeDControl>(viewer,"threeDView");
  Get<CheckBox>(three,"structures").IsChecked=true;Get<CheckBox>(three,"organs").IsChecked=true;Get<CheckBox>(three,"support").IsChecked=false;Get<CheckBox>(three,"external").IsChecked=false;Get<CheckBox>(three,"allRois").IsChecked=true;
  Get<CheckBox>(three,"bone").IsChecked=false;Get<CheckBox>(three,"skin").IsChecked=true;Get<CheckBox>(three,"dose").IsChecked=false;Get<Slider>(three,"opacity").Value=.85;Get<Slider>(three,"skinOpacity").Value=.09;
  await Settle();Invoke(three,"ResetCamera");Set(three,"cameraAdjusted",true);Set(three,"distance",Get<double>(three,"distance")*.76);Invoke(three,"UpdateCamera");Get<CheckBox>(three,"skin").IsChecked=false;
  var selection=PublicTourRois();tourChecks.Add("3D explicitly enables PTV, Organs and Other switches (Brainstem is AVOIDANCE in the source): "+selection.Count(r=>r.InterpretedType=="PTV")+" PTVs; "+string.Join(", ",selection.Where(r=>r.InterpretedType!="PTV").Select(r=>r.Name))+".");
  await Save("three-d-roi.png","All source PTVs and the selected Brainstem; Other is enabled because the source labels Brainstem as AVOIDANCE.");
  double yaw=Get<double>(three,"yaw"),initialDistance=Get<double>(three,"distance");var initialTarget=Get<Vec3>(three,"target");var focus=selection.FirstOrDefault(r=>r.Name.IndexOf("brainstem",StringComparison.OrdinalIgnoreCase)>=0)??selection.First();
  await TourSequence("orbit",TourFrames,new[]{"All PTVs and selected brainstem organs","Full actual scene geometry during orbit","Skin and dose switches","ROI focus and restore"},async i=>{
   if(i==22){Get<CheckBox>(three,"skin").IsChecked=true;await Save("three-d-skin.png","Transparent CT-derived skin provides orientation around all selected PTV and organ surfaces.");}
   if(i==48){Get<CheckBox>(three,"dose").IsChecked=true;await Save("three-d-dose.png","Physical RTDOSE isosurface with target and organ surfaces in shared coordinates.");}
   if(i==78){Get<CheckBox>(three,"dose").IsChecked=false;three.FocusStructure(focus);await Save("three-d-focused.png","Brainstem focus highlights its actual geometry and retains translucent target context.");}
   if(i==104){three.FocusStructure(focus);Set(three,"target",initialTarget);Set(three,"distance",initialDistance);}
   Set(three,"yaw",yaw+i/(TourFrames-1.0)*Math.PI*1.65);Set(three,"pitch",.2+.12*Math.Sin(i/(TourFrames-1.0)*Math.PI*2));Invoke(three,"UpdateCamera");
  });
 }
 static async Task CaptureTourMlc()
 {
  Mode("MLC");Panels(false,false);var mlc=Get<MlcPlaybackControl>(viewer,"centralPlayback");if(mlc==null)throw new InvalidOperationException("MLC component unavailable");
  var cursor=Get<Slider>(mlc,"cursor");cursor.Value=cursor.Maximum*.25;await Save("mlc.png","Global control-point timeline with field boundaries, leaf aperture and synchronized linac/collimator orientation.");
  await TourSequence("mlc",150,new[]{"Global plan control-point traversal","Field boundaries","MLC leaves and jaws","Synchronized gantry, couch and collimator schematic"},i=>{cursor.Value=cursor.Maximum*i/149.0;return Task.CompletedTask;});
  tourChecks.Add("MLC global cursor traversed minimum to maximum across 150 settled states.");
 }
 static async Task CaptureTourRtOnly(string folder)
 {
  var catalog=await Task.Run(()=>DicomCatalog.Scan(Directory.EnumerateFiles(folder).First(),CancellationToken.None));var plan=catalog.Files.First(e=>e.Modality=="RTPLAN");
  var tempRoot=Path.GetFullPath(Path.GetTempPath());var isolated=Path.Combine(tempRoot,"DicomRT-approved-public-tour-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(isolated);
  try
  {
   string copy=Path.Combine(isolated,"approved-public-rtplan.dcm");File.Copy(plan.Path,copy);
   viewer.Dispose();viewer=new ViewerControl{Width=Width,Height=Height};window.Content=viewer;viewer.UpdateLayout();sourceKind="unchanged isolated RTPLAN from approved public nonpatient benchmark";viewer.Open(copy);await viewer.LoadCompletion;
   if(viewer.HasImage)throw new InvalidOperationException("RT-only capture unexpectedly found an image");Mode("MLC");Panels(true,false);await Save("rt-only.png","Actual RTPLAN-only entry: MLC preview remains available without an image series.");rtOnlyCaptured=true;tourChecks.Add("Unchanged approved public RTPLAN opened alone; HasImage=false and actual central MLC component captured.");
  }
  finally{viewer?.Dispose();if(Path.GetFullPath(isolated).StartsWith(tempRoot,StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(isolated).StartsWith("DicomRT-approved-public-tour-",StringComparison.Ordinal))Directory.Delete(isolated,true);}
 }
 static async Task CaptureTourDvh()
 {
  Mode("DVH");Panels(false,false);await Settle();var dvh=Get<DvhControl>(viewer,"dvhView");await dvh.Completion;
  await Save("dvh.png","Approximate cumulative DVH for all selected PTVs and brainstem organs.");
  var candidates=PublicTourRois().Where(r=>r.InterpretedType=="PTV").Take(2).Concat(PublicTourRois().Where(r=>r.Name.IndexOf("brainstem",StringComparison.OrdinalIgnoreCase)>=0)).ToArray();
  await TourSequence("dvh",TourFrames,new[]{"Cumulative DVH","Click structure names to focus curves","Focused curve emphasis","Click again to restore"},async i=>{
   if(i==20||i==50||i==80){if(dvh.FocusedStructure!=null)dvh.FocusStructure(dvh.FocusedStructure);var roi=candidates[(i-20)/30%candidates.Length];var button=Descendants<Button>(dvh).FirstOrDefault(b=>(string)b.Content==roi.Name);if(button!=null){button.BringIntoView();button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}else dvh.FocusStructure(roi);if(i==20)await Save("dvh-focused.png","Actual structure-name focus emphasizes one DVH curve and fades the remaining curves.");}
   if(i==108&&dvh.FocusedStructure!=null)dvh.FocusStructure(dvh.FocusedStructure);
  });
  tourChecks.Add("DVH calculation awaited: "+dvh.StatusText);
 }
 static IEnumerable<TagNode> AllTagNodes(IEnumerable<TagNode> nodes){foreach(var node in nodes){yield return node;foreach(var child in AllTagNodes(node.Children))yield return child;}}
 static void TourTagFilter(string query){Get<TextBox>(viewer,"tagSearch").Text=query;Get<DispatcherTimer>(viewer,"tagTimer").Stop();Invoke(viewer,"FilterTags");}
 static async Task CaptureTourTags()
 {
  Mode("Bild");Get<ComboBox>(viewer,"planes").SelectedItem="Axial";Panels(false,true);var sources=Get<ComboBox>(viewer,"tagSource");var plan=Get<PlanData>(viewer,"selectedPlan");foreach(var item in sources.Items){var e=item.GetType().GetProperty("Entry")?.GetValue(item,null)as DicomEntry;if(e==plan.Entry){sources.SelectedItem=item;break;}}
  ScrubTagRows();TourTagFilter("");var tree=Get<TreeView>(viewer,"tagTree");Expand(tree.ItemsSource.Cast<TagNode>(),false);await Save("tags-collapsed.png","RTPLAN hierarchy; identifying attribute display values are redacted.");
  TagRow selected=null;
  await TourSequence("tags",90,new[]{"Nested DICOM sequence search","Selected nested entry retained when clearing search","Actual selectable tag detail window"},async i=>{
   if(i==18){TourTagFilter("Beam");await Save("tags-expanded.png","Search reveals matching nested beam sequence attributes.");}
   if(i==38){TourTagFilter("Leaf/Jaw Positions");var node=AllTagNodes(tree.ItemsSource.Cast<TagNode>()).First(n=>n.Row.Name.IndexOf("Leaf/Jaw Positions",StringComparison.OrdinalIgnoreCase)>=0);selected=node.Row;Set(tree,"selectedRow",selected);TourTagFilter("Leaf/Jaw Positions");await viewer.Dispatcher.InvokeAsync(()=>viewer.UpdateLayout(),DispatcherPriority.ContextIdle);await Save("tags-search.png","Nested Leaf/Jaw Positions selected within the RTPLAN sequence hierarchy.");}
   if(i==65){TourTagFilter("");await viewer.Dispatcher.InvokeAsync(()=>viewer.UpdateLayout(),DispatcherPriority.ContextIdle);if(!ReferenceEquals(Get<TagRow>(tree,"selectedRow"),selected))throw new InvalidOperationException("Tag selection was not retained");await Save("tags-restored.png","Clearing search retains and reveals the selected nested Leaf/Jaw Positions entry.");}
  });
  var popup=(Window)Activator.CreateInstance(typeof(ViewerControl).Assembly.GetType("QuickLook.DicomRT.TagDetailPopup"),new object[]{selected});popup.Owner=window;popup.WindowStartupLocation=WindowStartupLocation.Manual;popup.Left=-30000;popup.Top=-30000;popup.ShowActivated=false;popup.Show();popup.UpdateLayout();
  var bitmap=new RenderTargetBitmap((int)popup.ActualWidth,(int)popup.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(popup);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(output,"tag-details.png")))encoder.Save(file);popup.Close();
  artifacts.Add(new{file="tag-details.png",source=sourceKind,uiLanguage="en",caption="Actual tag detail window with separately selectable tag number, label and value; Copy controls shown without clipboard writes.",width=bitmap.PixelWidth,height=bitmap.PixelHeight,method="Actual TagDetailPopup RenderTargetBitmap"});
  tourChecks.Add("Nested selected TagRow reference retained after clearing search; actual TagDetailPopup captured without clipboard writes.");
 }
}
