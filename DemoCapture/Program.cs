using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using QuickLook.DicomRT;

internal static partial class Program
{
 const int Width=1600,Height=900,Frames=90;
 const BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
 static Application app;static Window window;static ViewerControl viewer;static string output;static string sourceKind;
 static readonly List<object> artifacts=new List<object>();static readonly List<object> availability=new List<object>();static readonly List<string> omissions=new List<string>();
 static T Get<T>(object value,string name){var f=value.GetType().GetField(name,Fields);if(f==null)throw new InvalidOperationException("Capture field unavailable: "+name);return (T)f.GetValue(value);}
 static void Set(object value,string name,object data)=>value.GetType().GetField(name,Fields).SetValue(value,data);
 static object Invoke(object value,string name,params object[] args)=>value.GetType().GetMethod(name,Fields).Invoke(value,args);
 static async Task Call(object value,string name,params object[] args){var t=Invoke(value,name,args)as Task;if(t!=null)await t;}
 static async Task Wait(Func<bool> predicate,string purpose,int seconds=60)
 {
  var sw=Stopwatch.StartNew();while(!predicate()){if(sw.Elapsed.TotalSeconds>seconds)throw new TimeoutException(purpose);await Task.Delay(20);}await viewer.Dispatcher.InvokeAsync(()=>viewer.UpdateLayout(),DispatcherPriority.Render);await Task.Delay(25);
 }
 static async Task Settle()
 {
  viewer.UpdateLayout();await Task.Delay(30);
  string mode=Get<string>(viewer,"workspaceMode");
  if(mode=="Bild"){await Wait(()=>Get<List<SlicePane>>(viewer,"panes").All(p=>Get<object>(p,"pending")==null&&Get<object>(p,"frame")!=null),"Image settle");if((string)Get<ComboBox>(viewer,"planes").SelectedItem=="MPR + 3D"){var three=Get<ThreeDControl>(viewer,"mprThreeD");await Wait(()=>three!=null&&Get<object>(three,"pending")==null&&Get<object>(three,"prepared")!=null,"MPR 3D settle");}}
  else if(mode=="DVH"){var dvh=Get<DvhControl>(viewer,"dvhView");if(dvh!=null)await dvh.Completion;}
  else if(mode=="3D"){var three=Get<ThreeDControl>(viewer,"threeDView");await Wait(()=>three!=null&&Get<object>(three,"pending")==null&&Get<object>(three,"prepared")!=null,"3D settle");}
  foreach(var three in Descendants<ThreeDControl>(viewer).Where(t=>t.IsVisible).ToArray())await Get<Task>(three,"gpuInitialization");
  await viewer.Dispatcher.InvokeAsync(()=>viewer.UpdateLayout(),DispatcherPriority.Render);await Task.Delay(35);
 }
 static async Task Save(string name,string caption,bool record=true)
 {
  await Settle();string path=Path.Combine(output,name);Directory.CreateDirectory(Path.GetDirectoryName(path));
  // Offscreen WPF capture does not reliably include a D3DImage. Insert the actual
  // GPU readback at the same visual position below the orientation badge for capture.
  var overlays=new List<Action>();
  foreach(var three in Descendants<ThreeDControl>(viewer).Where(t=>t.IsVisible).ToArray())
  {
   var bridge=Get<object>(three,"gpu");if(bridge==null)continue;var view=Get<FrameworkElement>(bridge,"View");var parent=view.Parent as Panel;if(parent==null)continue;
   var image=new Image{Source=(BitmapSource)Invoke(bridge,"Capture"),Stretch=Stretch.Fill,IsHitTestVisible=false};parent.Children.Insert(parent.Children.IndexOf(view)+1,image);var previous=view.Visibility;view.Visibility=Visibility.Hidden;overlays.Add(()=>{parent.Children.Remove(image);view.Visibility=previous;});
  }
  try{viewer.UpdateLayout();var bmp=new RenderTargetBitmap(Width,Height,96,96,PixelFormats.Pbgra32);bmp.Render(viewer);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bmp));using(var file=File.Create(path))png.Save(file);}finally{foreach(var restore in overlays)restore();}
  if(record){artifacts.Add(new{file=name.Replace('\\','/'),source=sourceKind,uiLanguage="en",caption,width=Width,height=Height,method="Actual ViewerControl RenderTargetBitmap; no desktop capture or generated UI"});Console.WriteLine("CAPTURE "+name);WriteManifest();}
 }
 static void WriteManifest()
 {
  var data=new{version=1,width=Width,height=Height,framesPerSequence=Frames,effectiveFps=15,sequenceDurationSeconds=6,tagRedaction="In-memory display rows only: person/patient identifiers, UID values, dates/times, institution/provider identifiers. DICOM source files remain unchanged.",artifacts,availability,omissions};
  File.WriteAllText(Path.Combine(output,"manifest.json"),new JavaScriptSerializer{MaxJsonLength=int.MaxValue}.Serialize(data));
 }
 static void Panels(bool rt,bool tags)
 {Set(viewer,"layersVisible",rt);Set(viewer,"tagsVisible",tags);Invoke(viewer,"UpdatePanels");}
 static void Mode(string mode)=>Invoke(viewer,"SetWorkspace",mode);
 static void DoseTab()
 {var panel=Get<UIElement>(viewer,"leftPanel");foreach(var tab in Descendants<TabControl>(panel))foreach(TabItem item in tab.Items)if(item.Header as string=="Doses")tab.SelectedItem=item;}
 static IEnumerable<T> Descendants<T>(DependencyObject root)where T:DependencyObject
 {for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var c=VisualTreeHelper.GetChild(root,i);if(c is T item)yield return item;foreach(var x in Descendants<T>(c))yield return x;}}
 static async Task Load(string folder,string kind,bool openImage=false)
 {
  sourceKind=kind;viewer?.Dispose();viewer=new ViewerControl{Width=Width,Height=Height};window.Content=viewer;window.Show();viewer.UpdateLayout();
  var cat=await Task.Run(()=>DicomCatalog.Scan(Directory.EnumerateFiles(folder).First(),CancellationToken.None));
  var chosen=openImage?cat.Files.First(e=>e.Modality=="CT"):(cat.Files.FirstOrDefault(e=>e.Modality=="RTPLAN")??cat.Files.First(e=>e.Modality=="CT"));
  availability.Add(new{source=kind,files=cat.Files.Count,ct=cat.Files.Count(e=>e.Modality=="CT"),mr=cat.Files.Count(e=>e.Modality=="MR"),reg=cat.Files.Count(e=>e.Modality=="REG"),plans=cat.Files.Count(e=>e.Modality=="RTPLAN"),doses=cat.Files.Count(e=>e.Modality=="RTDOSE"),structures=cat.Files.Count(e=>e.Modality=="RTSTRUCT")});
  viewer.Open(chosen.Path);await viewer.LoadCompletion;if(!viewer.HasImage)throw new InvalidOperationException("No image after load");
  var plan=Get<PlanData>(viewer,"selectedPlan");var volume=Get<VolumeData>(viewer,"volume");
  if(plan?.Beams.Count>0&&!double.IsNaN(plan.Beams[0].Isocenter.X)&&volume!=null&&!float.IsNaN(volume.Sample(plan.Beams[0].Isocenter)))await Call(viewer,"MoveFocusAsync",plan.Beams[0].Isocenter);
  await Settle();
 }
 static void Layers(bool rois,bool doses)
 {
  foreach(var roi in Get<List<StructureSet>>(viewer,"structures").SelectMany(s=>s.Rois))roi.Visible=rois;
  foreach(var dose in Get<List<DoseGrid>>(viewer,"doses"))dose.Visible=doses;Invoke(viewer,"RefreshRt");
 }
 static void ScrubTagRows()
 {
  var rows=Get<List<TagRow>>(viewer,"tags");var safe=rows.Select(r=>new TagRow{Path=r.Path,Tag=r.Tag,Name=r.Name,VR=r.VR,Value=Sensitive(r)?"[redacted for demonstration]":r.Value}).ToList();
  Set(viewer,"tags",safe);var tree=Get<object>(viewer,"tagTree");Invoke(tree,"SetRows",safe,true);Invoke(viewer,"FilterTags");
 }
 static bool Sensitive(TagRow r)
 {
  string name=(r.Name??"").ToLowerInvariant();return new[]{"PN","UI","DA","DT","TM"}.Contains(r.VR)||new[]{"patient","institution","physician","operator","station","accession","study id","device serial","address","telephone","birth"}.Any(name.Contains);
 }
 static void Expand(IEnumerable<TagNode> nodes,bool value){foreach(var n in nodes){n.IsExpanded=value;Expand(n.Children,value);}}
 static async Task Tags()
 {
  Panels(false,true);var sources=Get<ComboBox>(viewer,"tagSource");var plan=Get<PlanData>(viewer,"selectedPlan");foreach(var item in sources.Items){var e=item.GetType().GetProperty("Entry")?.GetValue(item,null)as DicomEntry;if(e==plan?.Entry){sources.SelectedItem=item;break;}}
  ScrubTagRows();var search=Get<TextBox>(viewer,"tagSearch");search.Text="";Get<DispatcherTimer>(viewer,"tagTimer").Stop();Invoke(viewer,"FilterTags");var tree=Get<TreeView>(viewer,"tagTree");
  Expand(tree.ItemsSource.Cast<TagNode>(),false);viewer.UpdateLayout();var beam=tree.ItemsSource.Cast<TagNode>().FirstOrDefault(n=>n.Row.Name.IndexOf("Beam Sequence",StringComparison.OrdinalIgnoreCase)>=0);if(beam!=null){var scroll=Descendants<ScrollViewer>(tree).FirstOrDefault();scroll?.ScrollToVerticalOffset(Math.Max(0,tree.Items.IndexOf(beam)-4));viewer.UpdateLayout();(tree.ItemContainerGenerator.ContainerFromItem(beam) as TreeViewItem)?.BringIntoView();}await Save("tags-collapsed.png","RTPLAN: full DICOM hierarchy with collapsed sequences; identifying display values redacted.");
  search.Text="Beam";Get<DispatcherTimer>(viewer,"tagTimer").Stop();Invoke(viewer,"FilterTags");Expand(tree.ItemsSource.Cast<TagNode>(),true);await Save("tags-expanded.png","RTPLAN: nested Beam sequences expanded; identifying display values redacted.");
  search.Text="Leaf";Get<DispatcherTimer>(viewer,"tagTimer").Stop();Invoke(viewer,"FilterTags");await Save("tags-search.png","Search across all loaded RTPLAN attributes, including nested Leaf positions.");Panels(true,false);
 }
 static async Task Sequence(string folder,string caption,Func<int,Task> update)
 {
  for(int i=0;i<Frames;i++){await update(i);await Save(folder+"/frame-"+i.ToString("000")+".png",caption,false);if(i%15==0)Console.WriteLine("SEQUENCE "+folder+" "+i+"/"+Frames);}
  artifacts.Add(new{file=folder+"/frame-%03d.png",source=sourceKind,uiLanguage="en",caption,width=Width,height=Height,frames=Frames,effectiveFps=15,durationSeconds=6,method="90 actual settled ViewerControl states; presentation timeline, not a live performance benchmark"});WriteManifest();
 }
 static async Task StructureJump()
 {
  var oldFocus=Get<Vec3>(viewer,"focus");var roi=Get<List<StructureSet>>(viewer,"structures").SelectMany(x=>x.Rois).FirstOrDefault(r=>r.InterpretedType=="PTV");if(roi==null)return;
  var map=Invoke(viewer,"TransformToImage",roi.FrameUid) as Matrix4;if(map==null)return;
  Get<TextBox>(viewer,"roiSearch").Text=roi.Name;await Call(viewer,"MoveFocusAsync",map.Transform(roi.Center));await Save("structures-jump.png","Structure search and navigation to an actual target centre; ROI display and dose remain physically aligned.");Get<TextBox>(viewer,"roiSearch").Text="";await Call(viewer,"MoveFocusAsync",oldFocus);
 }
 static async Task Primary(string folder)
 {
  await Load(folder,"user-supplied public nonpatient benchmark");Mode("Bild");Panels(false,false);Layers(false,false);await Save("image-clean.png","Original CT from the supplied public nonpatient benchmark, with side panels closed.");
  Layers(true,true);Panels(true,false);await Save("rt-overview.png","RT tools on the left: plan, structures and dose on the actual benchmark CT.");await StructureJump();await Tags();
  Get<ComboBox>(viewer,"planes").SelectedItem="MPR + 3D";await Save("mpr-three.png","Axial, coronal and sagittal reconstructions of the same CT volume.");Get<ComboBox>(viewer,"planes").SelectedItem="Axial";
  DoseTab();Get<CheckBox>(viewer,"wash").IsChecked=true;Get<CheckBox>(viewer,"iso").IsChecked=false;Get<Slider>(viewer,"opacity").Value=.42;await Save("dose-wash.png","Dose colorwash with adjustable opacity and thresholds relative to dose maximum.");
  Get<CheckBox>(viewer,"wash").IsChecked=false;Get<CheckBox>(viewer,"iso").IsChecked=true;await Save("dose-isodoses.png","Actual RTDOSE isodose lines, with colorwash disabled.");
  Get<CheckBox>(viewer,"wash").IsChecked=true;await Save("dose-combined.png","Colorwash and isodose lines together, with dose controls on the left.");
  Panels(false,false);Get<ComboBox>(viewer,"planes").SelectedItem="Native";var stack=Get<ImageStack>(viewer,"currentStack");int center=Get<int>(viewer,"sliceIndex"),start=Math.Max(0,center-35),end=Math.Min(stack.Entries.Count-1,center+35);
  await Sequence("scroll","Slice navigation through the actual benchmark CT with RT overlays.",async i=>await Call(viewer,"ShowSliceAsync",(int)Math.Round(start+(end-start)*i/(Frames-1.0)),true));await Call(viewer,"ShowSliceAsync",center,true);
  Panels(true,false);DoseTab();Get<ComboBox>(viewer,"planes").SelectedItem="Axial";await Sequence("dose","Opacity and isodose settings change in the actual dose view.",i=>{Get<Slider>(viewer,"opacity").Value=.12+.55*(.5-.5*Math.Cos(i/(Frames-1.0)*Math.PI*2));Get<CheckBox>(viewer,"iso").IsChecked=i>=Frames/3;return Task.CompletedTask;});
  await CaptureMlc();
  Mode("DVH");await Save("dvh.png","Large approximate DVH preview for all 55 selected structures; contour and RTDOSE computation fully awaited.");
  var dvh=Get<DvhControl>(viewer,"dvhView");var candidates=Get<List<StructureSet>>(viewer,"structures").SelectMany(x=>x.Rois).Where(r=>r.Visible).Take(5).ToArray();
  await Sequence("dvh","Actual DVH focus interaction: selected curve bold, other curves faded; repeat click restores all.",i=>{StructureRoi next=i<10||i>79?null:candidates[(i/15)%candidates.Length];if(dvh.FocusedStructure!=next){if(dvh.FocusedStructure!=null)dvh.FocusStructure(dvh.FocusedStructure);if(next!=null)dvh.FocusStructure(next);}return Task.CompletedTask;});
  if(candidates.Length>0)dvh.FocusStructure(candidates[0]);await Save("dvh-focused.png","A focused DVH curve is drawn thick; other curves stay visible with reduced opacity.");if(dvh.FocusedStructure!=null)dvh.FocusStructure(dvh.FocusedStructure);
  await ThreeD();
 }
 static async Task CaptureMlc()
 {
  Mode("MLC");Panels(false,false);var mlc=Get<MlcPlaybackControl>(viewer,"centralPlayback");if(mlc!=null){var cursor=Get<Slider>(mlc,"cursor");cursor.Value=cursor.Maximum*.25;await Save("mlc.png","Large MLC view with global plan timeline and field boundaries.");await Sequence("mlc","Scrubbing actual control points and field boundaries of the benchmark plan.",i=>{cursor.Value=cursor.Maximum*i/(Frames-1.0);return Task.CompletedTask;});}
 }
 static async Task ThreeD()
 {
  Mode("3D");await Settle();var fullScene=Get<ThreeDControl>(viewer,"threeDView");Get<CheckBox>(fullScene,"bone").IsChecked=false;Get<CheckBox>(fullScene,"skin").IsChecked=true;Get<CheckBox>(fullScene,"dose").IsChecked=true;Get<Slider>(fullScene,"opacity").Value=.7;await Save("three-d-all.png","Default PTV/ORGAN metadata selection with dose enabled; external and other ROI types excluded.");
  // All ROI/dose geometry is independently verified by SceneBudget. Select a small
  // explicit subset for legibility in the presentation, using the actual ROI toggles.
  var allRois=Get<List<StructureSet>>(viewer,"structures").SelectMany(x=>x.Rois).ToList();
  var chosenRois=allRois.Where(r=>r.InterpretedType=="PTV").OrderBy(r=>r.Center.Z).ToList();
  var selected=new List<StructureRoi>();if(chosenRois.Count>0)for(int i=0;i<Math.Min(4,chosenRois.Count);i++)selected.Add(chosenRois[(int)Math.Round(i*(chosenRois.Count-1)/(double)Math.Max(1,Math.Min(4,chosenRois.Count)-1))]);
  selected.AddRange(allRois.Where(r=>r.Name.IndexOf("brainstem",StringComparison.OrdinalIgnoreCase)>=0||r.Name.IndexOf("chiasm",StringComparison.OrdinalIgnoreCase)>=0).Take(2));
  if(selected.Count==0)selected.AddRange(allRois.Take(6));foreach(var r in allRois)r.Visible=selected.Contains(r);Invoke(viewer,"RefreshRt");
  Mode("3D");await Settle();var three=Get<ThreeDControl>(viewer,"threeDView");Get<CheckBox>(three,"allRois").IsChecked=true;
  Get<CheckBox>(three,"bone").IsChecked=true;Get<CheckBox>(three,"skin").IsChecked=false;Get<CheckBox>(three,"structures").IsChecked=true;Get<CheckBox>(three,"dose").IsChecked=false;Invoke(three,"ResetCamera");
  string subset=selected.Distinct().Count()+" explicitly selected target/OAR structures for legibility; source contains "+allRois.Count+" structures.";
  await Save("three-d-bone.png","CT-derived bone surface and contour-derived target/OAR surfaces. "+subset);
  Get<CheckBox>(three,"bone").IsChecked=false;Get<CheckBox>(three,"skin").IsChecked=true;Get<Slider>(three,"opacity").Value=.65;await Save("three-d-skin.png","Transparent CT-derived skin surface. "+subset);
  Get<CheckBox>(three,"skin").IsChecked=false;Get<Slider>(three,"opacity").Value=.65;await Save("three-d-roi.png","Contour-derived target/OAR surfaces, with CT threshold surfaces hidden. "+subset);
  Get<CheckBox>(three,"skin").IsChecked=true;Get<CheckBox>(three,"dose").IsChecked=true;Get<Slider>(three,"opacity").Value=.7;await Save("three-d-dose.png","Actual 3D isodose and selected ROI surfaces in the same physical coordinate frame; very transparent CT skin retained for orientation. "+subset);
  double yaw=Get<double>(three,"yaw");await Sequence("orbit","Orbit around actual CT/ROI/dose geometry; bounded derived 3D preview. "+subset,i=>{Set(three,"yaw",yaw+i/(Frames-1.0)*Math.PI*2);Set(three,"pitch",.2+.12*Math.Sin(i/(Frames-1.0)*Math.PI*2));Invoke(three,"UpdateCamera");return Task.CompletedTask;});
 }
 static async Task CaptureQuad(string folder)
 {
  var serializer=new JavaScriptSerializer{MaxJsonLength=int.MaxValue};var old=(Dictionary<string,object>)serializer.DeserializeObject(File.ReadAllText(Path.Combine(output,"manifest.json")));
  foreach(var a in (object[])old["artifacts"]){string file=(string)((Dictionary<string,object>)a)["file"];if(file!="mpr-three.png"&&!file.StartsWith("mpr/"))artifacts.Add(a);}foreach(var a in (object[])old["availability"])availability.Add(a);
  await Load(folder,"user-supplied public nonpatient benchmark");Panels(false,false);Mode("Bild");Get<ComboBox>(viewer,"planes").SelectedItem="MPR + 3D";
  var three=Get<ThreeDControl>(viewer,"mprThreeD");await Wait(()=>Get<object>(three,"pending")==null&&Get<object>(three,"prepared")!=null,"Quad 3D settle");
  await Save("mpr-three.png","Synchronized 2 × 2 layout: axial, coronal, sagittal and 3D slice-coordinate planes.");
  var initial=Get<Vec3>(viewer,"focus");await Sequence("mpr","Scrolling axial slices moves the 3D coordinate planes in the same LPS frame.",i=>{Set(viewer,"focus",initial+new Vec3(0,0,24*Math.Sin(i/(Frames-1.0)*Math.PI*2)));Invoke(viewer,"Redraw");return Task.CompletedTask;});
  Console.WriteLine("MPR_COMPLETE");
 }
 static async Task UiReview(string folder)
 {
  await Load(folder,"user-supplied public nonpatient benchmark");Panels(true,false);Mode("Bild");Layers(true,true);DoseTab();Get<CheckBox>(viewer,"iso").IsChecked=true;await Save("dose-legend-iso.png","Editable dose legend, blue accents and registered ISO navigation.");
  Get<ComboBox>(viewer,"isodoseMode").SelectedIndex=1;await Save("relative-isodoses.png","Percentage presets apply globally; Default restores the standard percentage levels.");Get<ComboBox>(viewer,"isodoseMode").SelectedIndex=0;
  var three=Get<ThreeDControl>(viewer,"threeDView");bool preloaded=three!=null&&Get<object>(three,"prepared")!=null;var switchTimer=Stopwatch.StartNew();Mode("3D");await Settle();three=Get<ThreeDControl>(viewer,"threeDView");Console.WriteLine("PRELOAD_REVIEW ready_before_first_3d="+preloaded+" first_switch_settle_ms="+switchTimer.ElapsedMilliseconds);await Save("gpu-skin.png","PTV-only default with transparent CT skin and independent structure-type switches.");
  var roi=Get<List<StructureSet>>(viewer,"structures").SelectMany(s=>s.Rois).FirstOrDefault(r=>r.Name.IndexOf("brainstem",StringComparison.OrdinalIgnoreCase)>=0);if(roi!=null){three.FocusStructure(roi);await Save("gpu-focused.png","Focused ROI retains full geometry and translucent context.");}
  Mode("Bild");Get<ComboBox>(viewer,"planes").SelectedItem="MPR + 3D";Panels(false,false);await Save("gpu-mpr.png","Linked MPR and Direct3D with transparent colored coordinate planes.");
  await Tags();var row=Get<List<TagRow>>(viewer,"tags").First(r=>r.Name.IndexOf("Leaf/Jaw Positions",StringComparison.OrdinalIgnoreCase)>=0);
  var popup=(Window)Activator.CreateInstance(typeof(ViewerControl).Assembly.GetType("QuickLook.DicomRT.TagDetailPopup"),new object[]{row});popup.Owner=window;popup.WindowStartupLocation=WindowStartupLocation.Manual;popup.Left=-30000;popup.Top=-30000;popup.ShowActivated=false;popup.Show();popup.UpdateLayout();
  var bitmap=new RenderTargetBitmap((int)popup.ActualWidth,(int)popup.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(popup);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(output,"tag-details.png")))encoder.Save(file);popup.Close();Console.WriteLine("UI_REVIEW_COMPLETE");
 }
 [STAThread] static int Main(string[] args)
 {
  if(args.Length==3 && args[0]=="--approved-public-tour-mlc")return RunPublicTour(args[1],args[2],false,false,true);
  if(args.Length==3 && args[0]=="--approved-public-tour-orbit")return RunPublicTour(args[1],args[2],true,true);
  if(args.Length==3 && args[0]=="--approved-public-tour-refresh")return RunPublicTour(args[1],args[2],true);
  if(args.Length==3 && args[0]=="--approved-public-tour")return RunPublicTour(args[1],args[2]);
  if(args.Length==3 && args[0]=="--mlc-only")return MlcOnlyCapture.Run(args[1],args[2]);
  if(args.Length!=3||(args[0]!="--approved-public-early-fields"&&args[0]!="--approved-public-loading"&&args[0]!="--approved-public-3d-cp"&&args[0]!="--approved-public-bev-review"&&args[0]!="--approved-public-ui-review"&&args[0]!="--approved-public-demo"&&args[0]!="--approved-public-metrics"&&args[0]!="--approved-public-orbit"&&args[0]!="--approved-public-metrics-ct"&&args[0]!="--approved-public-mlc"&&args[0]!="--approved-public-mpr")){Console.WriteLine("Usage: --approved-public-demo <public-source> <output>");return 2;}
  output=Path.GetFullPath(args[2]);Directory.CreateDirectory(output);app=new Application();window=new Window{Width=Width,Height=Height,Left=-30000,Top=-30000,ShowInTaskbar=false,ShowActivated=false,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,Title="DICOM RT demonstration renderer"};int result=0;
  window.Loaded+=async(s,e)=>{try{if(args[0]=="--approved-public-early-fields"){await EarlyFieldsReview(args[1]);}else if(args[0]=="--approved-public-loading"){await LoadingReview(args[1]);}else if(args[0]=="--approved-public-3d-cp"){await ThreeDCpReview(args[1]);}else if(args[0]=="--approved-public-bev-review"){await BeamReview(args[1]);}else if(args[0]=="--approved-public-ui-review"){await UiReview(args[1]);}else if(args[0]=="--approved-public-mpr"){await CaptureQuad(args[1]);}else if(args[0]=="--approved-public-mlc"){var serializer=new JavaScriptSerializer{MaxJsonLength=int.MaxValue};var old=(Dictionary<string,object>)serializer.DeserializeObject(File.ReadAllText(Path.Combine(output,"manifest.json")));foreach(var a in (object[])old["artifacts"]){string file=(string)((Dictionary<string,object>)a)["file"];if(file!="mlc.png"&&!file.StartsWith("mlc/"))artifacts.Add(a);}foreach(var a in (object[])old["availability"])availability.Add(a);await Load(args[1],"user-supplied public nonpatient benchmark");await CaptureMlc();WriteManifest();Console.WriteLine("MLC_COMPLETE");}else if(args[0]=="--approved-public-orbit"){var serializer=new JavaScriptSerializer{MaxJsonLength=int.MaxValue};var old=(Dictionary<string,object>)serializer.DeserializeObject(File.ReadAllText(Path.Combine(output,"manifest.json")));foreach(var a in (object[])old["artifacts"]){string file=(string)((Dictionary<string,object>)a)["file"];if(!file.StartsWith("three-d-")&&!file.StartsWith("orbit/"))artifacts.Add(a);}foreach(var a in (object[])old["availability"])availability.Add(a);await Load(args[1],"user-supplied public nonpatient benchmark");Panels(false,false);Layers(true,true);await ThreeD();var revised=(Dictionary<string,object>)serializer.DeserializeObject(File.ReadAllText(Path.Combine(output,"manifest.json")));if(old.ContainsKey("publicPerformance"))revised["publicPerformance"]=old["publicPerformance"];File.WriteAllText(Path.Combine(output,"manifest.json"),serializer.Serialize(revised));Console.WriteLine("ORBIT_COMPLETE");}else if(args[0].StartsWith("--approved-public-metrics")){bool ct=args[0].EndsWith("-ct");await Load(args[1],"user-supplied public nonpatient benchmark",ct);var serializer=new JavaScriptSerializer{MaxJsonLength=int.MaxValue};var manifest=(Dictionary<string,object>)serializer.DeserializeObject(File.ReadAllText(Path.Combine(output,"manifest.json")));var metrics=new{source=sourceKind,openedModality=ct?"CT":"RTPLAN",firstImageMs=Math.Round(viewer.FirstImageMilliseconds),firstRtMs=Math.Round(viewer.FirstRtMilliseconds),firstPlanMs=Math.Round(viewer.FirstPlanMilliseconds),indexMs=Math.Round(viewer.IndexMilliseconds),method="Fresh ViewerControl instance; warm OS file cache after demo capture; component loading milestones, not cold-start or video playback latency."};manifest[ct?"publicPerformanceCt":"publicPerformance"]=metrics;File.WriteAllText(Path.Combine(output,"manifest.json"),serializer.Serialize(manifest));Console.WriteLine("PUBLIC_PERFORMANCE "+serializer.Serialize(metrics));}else{await Primary(args[1]);WriteManifest();Console.WriteLine("CAPTURE_COMPLETE artifacts="+artifacts.Count);}}catch(Exception ex){result=1;omissions.Add("Capture interrupted: "+ex.GetType().Name);if(args[0]=="--approved-public-demo")WriteManifest();Console.WriteLine("CAPTURE_FAILED "+ex.GetType().Name+(args[0]=="--approved-public-bev-review"?" "+ex.Message:""));}finally{viewer?.Dispose();window.Close();}};
  app.Run(window);return result;
 }
}
