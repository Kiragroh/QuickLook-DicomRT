using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace QuickLook.DicomRT
{
 public sealed class ThreeDControl : UserControl,IDisposable
 {
  sealed class Part {public MeshGeometry3D Mesh,InteractionMesh;public StructureRoi Roi;public string Kind,RoiType;public Color Color;public Vec3 Center;public bool Fallback,Reduced;}
  sealed class Prepared {public List<Part> Parts=new List<Part>();public int Fallbacks,Skipped,Reduced,CacheHits;}
  readonly Viewport3D viewport=new Viewport3D();readonly ModelVisual3D visual=new ModelVisual3D();readonly PerspectiveCamera camera=new PerspectiveCamera();
  Direct3DSurface gpu;internal Direct3DSurface CaptureSurface=>gpu;
  readonly PatientOrientationBadge orientationBadge=new PatientOrientationBadge();
  readonly IsocenterOverlay isocenterOverlay=new IsocenterOverlay();
  readonly ModelVisual3D sliceVisual=new ModelVisual3D();bool compact;VolumeData sliceVolume;Vec3 sliceFocus;bool cameraAdjusted;
  readonly CheckBox bone,skin,structures,organs,support,external,dose,allRois;readonly Slider opacity,skinOpacity;readonly ComboBox doseLevel;readonly TextBlock status;
  readonly StackPanel footer;bool backgroundPreparation,gpuDirty;
  RoiSurfaceTypes SelectedTypes=>(structures?.IsChecked==true?RoiSurfaceTypes.Ptv:0)|(organs?.IsChecked==true?RoiSurfaceTypes.Organ:0)|(allRois?.IsChecked==true?RoiSurfaceTypes.Other:0);
  readonly Dictionary<string,Part> cache=new Dictionary<string,Part>();
  readonly DispatcherTimer interactionIdle=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(180)};
  Model3DGroup qualityModels,interactionModels;bool interacting;StructureRoi focusedRoi;
  sealed class DoseChoice {public double Value;public bool Absolute;public override string ToString()=>Value.ToString("0.##",CultureInfo.InvariantCulture)+(Absolute?" Gy":" % max.");}
  bool updatingDoseChoices;string doseChoicesKey;
  double DisplayDoseLevel=>doseLevel.SelectedItem is DoseChoice item?item.Value:new[]{20d,50d,80d,95d}[Math.Max(0,Math.Min(3,doseLevel.SelectedIndex))];
  void ConfigureDoseChoices()
  {
   var levels=SliceRaster.IsodoseLevels(scene);if(levels.Length==0)levels=scene.AbsoluteIsodoses?new[]{1d}:new[]{20d,50d,80d,95d};
   string next=scene.AbsoluteIsodoses+":"+string.Join(";",levels.Select(x=>x.ToString("R",CultureInfo.InvariantCulture)));if(next==doseChoicesKey)return;
   double desired=doseLevel.SelectedItem is DoseChoice previous&&previous.Absolute==scene.AbsoluteIsodoses?previous.Value:scene.AbsoluteIsodoses?scene.IsoColorMaximum*.5:50;
   updatingDoseChoices=true;try{doseChoicesKey=next;var choices=levels.Select(x=>new DoseChoice{Value=x,Absolute=scene.AbsoluteIsodoses}).ToArray();doseLevel.ItemsSource=choices;doseLevel.SelectedItem=choices.OrderBy(x=>Math.Abs(x.Value-desired)).First();doseLevel.ToolTip=scene.AbsoluteIsodoses?"Absolute isodose surface in Gy; edit levels in the Dose panel":"Relative isodose; no physical Gy unit available";}finally{updatingDoseChoices=false;}
  }
  readonly TextBlock interactionHint=Theme.Text("Interaction preview",11,Theme.Muted);
  sealed class Identity {public readonly int Value=Interlocked.Increment(ref identitySequence);}
  static int identitySequence;static readonly ConditionalWeakTable<object,Identity> identities=new ConditionalWeakTable<object,Identity>();
  static int Id(object value)=>value==null?0:identities.GetValue(value,x=>new Identity()).Value;
  static string TransformKey(Matrix4 transform)=>string.Join(",",transform.Values.Select(x=>x.ToString("R",CultureInfo.InvariantCulture)));
  RenderScene scene;Prepared prepared;CancellationTokenSource pending;int generation;bool disposed,doseDefaultInitialized;string key,preparedDoseKey;double yaw=-1.7,pitch=.25,distance=500,radius=250;Vec3 target;Point mouse;bool dragging;
  public event Action MprRequested;
  public ThreeDControl(bool compact=false)
  {
   ViewerSnapshot.AttachMenu(this,null,"3D");
   this.compact=compact;
   Background=Theme.Background;var root=new Grid();root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
   var controls=new WrapPanel{Margin=new Thickness(8,4,8,4)};
   bone=Toggle("Bone",false);skin=Toggle("Skin",true);structures=Toggle("PTV",true);organs=Toggle("Organs",false);support=Toggle("Support",false);external=Toggle("External",false);dose=Toggle("Dose",false);allRois=Toggle("Other",false);allRois.ToolTip="Additional ROI types, such as CTV or AVOIDANCE; excludes SUPPORT and EXTERNAL";
   support.Visibility=external.Visibility=Visibility.Collapsed;support.IsEnabled=external.IsEnabled=false;
   controls.Children.Add(structures);controls.Children.Add(organs);controls.Children.Add(allRois);controls.Children.Add(bone);controls.Children.Add(skin);controls.Children.Add(dose);
   controls.Children.Add(Theme.Text("ROI opacity",11));opacity=new Slider{Minimum=.1,Maximum=1,Value=.7,Width=95,Margin=new Thickness(6),ToolTip="ROI opacity; large enclosing organs are automatically more transparent. Skin has its own control."};controls.Children.Add(opacity);opacity.ValueChanged+=(s,e)=>ApplyModels();
   var skinOpacityLabel=Theme.Text("Skin opacity",11);controls.Children.Add(skinOpacityLabel);skinOpacity=new Slider{Minimum=.01,Maximum=.25,Value=.06,Width=80,Margin=new Thickness(6),ToolTip="CT skin opacity (1–25%), independent of ROI opacity; 6% by default"};controls.Children.Add(skinOpacity);skinOpacity.ValueChanged+=(s,e)=>ApplyModels();
   doseLevel=new ComboBox{Width=100,Margin=new Thickness(5),ItemsSource=new[]{"20 % max.","50 % max.","80 % max.","95 % max."},SelectedIndex=1,ToolTip="Isodose surface"};controls.Children.Add(doseLevel);doseLevel.SelectionChanged+=(s,e)=>{if(!updatingDoseChoices)StartBuild();};
   var quad=ViewButtons.Create("MPR + 3D");quad.Click+=(s,e)=>MprRequested?.Invoke();controls.Children.Add(quad);
   var reset=Theme.Button("Reset view");reset.Click+=(s,e)=>{cameraAdjusted=false;ResetCamera();};controls.Children.Add(reset);
   root.Children.Add(controls);
   var viewportHost=new Grid();viewportHost.Children.Add(viewport);
   try{gpu=new Direct3DSurface();viewportHost.Children.Add(gpu.View);viewport.Visibility=Visibility.Hidden;gpu.View.RenderExceptionOccurred+=(s,e)=>{e.Handled=true;var failed=gpu;gpu=null;failed.View.Visibility=Visibility.Collapsed;Dispatcher.BeginInvoke(new Action(()=>failed.Dispose()));viewport.Visibility=Visibility.Visible;interactionHint.Text="Direct3D unavailable · WPF fallback";interactionHint.Visibility=Visibility.Visible;};}catch(Exception){gpu?.Dispose();gpu=null;interactionHint.Text="Direct3D unavailable · WPF fallback";}
   viewportHost.Children.Add(isocenterOverlay);
   orientationBadge.HorizontalAlignment=HorizontalAlignment.Left;orientationBadge.VerticalAlignment=VerticalAlignment.Bottom;orientationBadge.Margin=new Thickness(5);viewportHost.Children.Add(orientationBadge);
   interactionHint.HorizontalAlignment=HorizontalAlignment.Right;interactionHint.VerticalAlignment=VerticalAlignment.Top;interactionHint.Margin=new Thickness(8);interactionHint.Visibility=gpu==null?Visibility.Visible:Visibility.Collapsed;interactionHint.IsHitTestVisible=false;viewportHost.Children.Add(interactionHint);
   var host=new Border{Background=Theme.Background,Child=viewportHost,ClipToBounds=true};Grid.SetRow(host,1);root.Children.Add(host);host.SizeChanged+=(s,e)=>{if(!cameraAdjusted)ResetCamera();};
   viewport.Camera=camera;viewport.IsHitTestVisible=false;viewport.ClipToBounds=false;viewport.Children.Add(visual);viewport.Children.Add(sliceVisual);camera.FieldOfView=42;
   interactionIdle.Tick+=(s,e)=>EndInteraction();
   host.MouseLeftButtonDown+=(s,e)=>{dragging=true;mouse=e.GetPosition(host);host.CaptureMouse();e.Handled=true;};
   host.MouseLeftButtonUp+=(s,e)=>{dragging=false;host.ReleaseMouseCapture();QueueQualityRestore();e.Handled=true;};host.LostMouseCapture+=(s,e)=>{dragging=false;QueueQualityRestore();};
   host.MouseMove+=(s,e)=>{if(!dragging)return;BeginInteraction();cameraAdjusted=true;var p=e.GetPosition(host);yaw+=(p.X-mouse.X)*.01;pitch=Math.Max(-1.45,Math.Min(1.45,pitch+(p.Y-mouse.Y)*.01));mouse=p;UpdateCamera();QueueQualityRestore();e.Handled=true;};
   host.MouseWheel+=(s,e)=>{BeginInteraction();cameraAdjusted=true;distance=Math.Max(radius*.15,Math.Min(radius*20,distance*Math.Pow(1.15,-e.Delta/120.0)));UpdateCamera();QueueQualityRestore();e.Handled=true;};
   footer=new StackPanel{Margin=new Thickness(10,4,10,7)};status=Theme.Text("3D: select spatial DICOM objects",11);footer.Children.Add(status);
   footer.Children.Add(Theme.Text("Drag to rotate · mouse wheel to zoom · LPS patient coordinates. Skin/bone surfaces use CT thresholds; ROI surfaces are smoothed, bounded approximations of voxelized contours (≤1 mm smoothing displacement). Transparency is approximate; 3D provides an overview.",10,Theme.Muted));Grid.SetRow(footer,2);root.Children.Add(footer);if(compact)footer.Visibility=Visibility.Collapsed;Content=root;
   IsVisibleChanged+=(s,e)=>{if(IsVisible){if(gpuDirty)ApplyModels();StartBuild();}else{EndInteraction();if(!backgroundPreparation&&pending!=null){++generation;pending.Cancel();pending=null;key=null;}}};
  }
  // One control moves between full and MPR layouts; cache, camera and settings stay intact.
  public void SetCompact(bool value){compact=value;footer.Visibility=value?Visibility.Collapsed:Visibility.Visible;if(!value)SetSlicePlanes(new Vec3(),null);}
  public void PreloadScene(RenderScene value){backgroundPreparation=true;SetScene(value);}
  CheckBox Toggle(string label,bool initial)
  {
   var box=new CheckBox{Content=label,IsChecked=initial,Foreground=Theme.Foreground,Margin=new Thickness(5,7,12,7),VerticalAlignment=VerticalAlignment.Center};box.Checked+=(s,e)=>{ApplyModels();StartBuild();};box.Unchecked+=(s,e)=>{ApplyModels();StartBuild();};return box;
  }
  public void SetScene(RenderScene value)
  {
   if(disposed)return;bool changed=scene==null||scene.Volume!=value?.Volume||(scene.Volume==null&&scene.Entry!=value?.Entry&&scene.Entry?.SeriesUid!=value?.Entry?.SeriesUid);bool remapped=scene!=null&&value!=null&&(scene.Structures??new List<RoiOverlay>()).Any(old=>(value.Structures??new List<RoiOverlay>()).Any(next=>next.Roi==old.Roi&&TransformKey(next.RoiToImage)!=TransformKey(old.RoiToImage)));changed|=remapped;bool paletteChanged=scene!=null&&value!=null&&!scene.IsoColors.OrderBy(x=>x.Key).SequenceEqual(value.IsoColors.OrderBy(x=>x.Key));scene=value;isocenterOverlay.Set(scene?.Isocenters,camera);if(scene!=null)ConfigureDoseChoices();if(paletteChanged)ApplyModels();
   if(changed){EndInteraction();key=null;prepared=null;focusedRoi=null;cameraAdjusted=false;ResetCamera();ApplyModels();}
   if(focusedRoi!=null&&!(scene?.Structures?.Any(r=>r?.Roi==focusedRoi)??false))focusedRoi=null;
   if(!doseDefaultInitialized&&scene!=null)
   {
    bool hasRoi=(scene.Structures??new List<RoiOverlay>()).Any(r=>r?.Roi!=null&&r.Roi.Visible&&ThreeDGeometry.DefaultRoi(r.Roi));bool hasDose=(scene.Doses??new List<DoseOverlay>()).Any(d=>d?.Dose!=null&&d.Dose.Visible);
    if(hasRoi||hasDose||scene.Volume!=null){doseDefaultInitialized=true;if(!hasRoi&&hasDose&&scene.Volume==null&&(scene.Entry==null||scene.Entry.Modality=="RTDOSE"))dose.IsChecked=true;}
   }
   if(scene==null){pending?.Cancel();++generation;prepared=null;key=null;ApplyModels();return;}
   if(IsVisible||backgroundPreparation)StartBuild();
  }
  // Explicit selection also admits a single nondefault ROI without widening the
  // default filter. Center is the RT contour representative point in image space.
  public void FocusStructure(StructureRoi roi)
  {
   if(disposed||roi==null||(ThreeDGeometry.SurfaceType(roi)&(RoiSurfaceTypes.External|RoiSurfaceTypes.Support))!=0)return;
   var overlay=scene?.Structures?.FirstOrDefault(r=>r?.Roi==roi);if(overlay==null)return;
   if(focusedRoi==roi){focusedRoi=null;ApplyModels();StartBuild();return;}
   var point=overlay.RoiToImage.Transform(roi.Center);
   if(!Finite(point)||!roi.Contours.Any(c=>c.Points.Count>0))return;
   focusedRoi=roi;cameraAdjusted=true;target=point;EndInteraction();
   // Keep the current useful magnification and orbit; expand only if the selected
   // extent cannot fit after centering, including portrait compact panes.
   double aspect=viewport.ActualHeight>0?Math.Max(.1,viewport.ActualWidth/viewport.ActualHeight):1;
   double tangent=Math.Tan(camera.FieldOfView*Math.PI/360)*.8;
   var view=new Vec3(-Math.Cos(pitch)*Math.Cos(yaw),-Math.Cos(pitch)*Math.Sin(yaw),-Math.Sin(pitch));var right=view.Cross(new Vec3(0,0,1)).Normalized();var up=right.Cross(view);
   foreach(var contour in roi.Contours)foreach(var source in contour.Points){var delta=overlay.RoiToImage.Transform(source)-target;distance=Math.Max(distance,Math.Max(Math.Abs(delta.Dot(right))/tangent,Math.Abs(delta.Dot(up))*aspect/tangent)-delta.Dot(view));}
   UpdateCamera();ApplyModels();StartBuild();
  }
  static bool Finite(Vec3 value)=>!double.IsNaN(value.X)&&!double.IsNaN(value.Y)&&!double.IsNaN(value.Z)&&!double.IsInfinity(value.X)&&!double.IsInfinity(value.Y)&&!double.IsInfinity(value.Z);
  void BeginInteraction(){} // Retain full geometry and transparency throughout camera motion.
  void QueueQualityRestore(){}
  void EndInteraction(){interactionIdle.Stop();if(interacting)interacting=false;}
  // Cheap orientation overlay, separate from ROI/dose preparation and camera state.
  // Polygon vertices are intersections of patient-coordinate slice planes and the
  // oriented image voxel envelope, not an axis-aligned approximation to that envelope.
  public void SetSlicePlanes(Vec3 focus,VolumeData volume)
  {
   if(disposed)return;if(volume==null){sliceVolume=null;sliceVisual.Content=null;gpu?.SetGuides(null);return;}
   if(volume==sliceVolume&&(focus-sliceFocus).Length<1e-7)return;
   if(volume==sliceVolume){BeginInteraction();QueueQualityRestore();}sliceVolume=volume;sliceFocus=focus;
   if(volume.Width<1||volume.Height<1||volume.Depth<1||double.IsNaN(focus.X)||double.IsNaN(focus.Y)||double.IsNaN(focus.Z)||double.IsInfinity(focus.X)||double.IsInfinity(focus.Y)||double.IsInfinity(focus.Z)){sliceVisual.Content=null;gpu?.SetGuides(null);return;}
   var group=new Model3DGroup();var normals=new[]{new Vec3(0,0,1),new Vec3(0,1,0),new Vec3(1,0,0)};var colors=new[]{Color.FromRgb(70,150,255),Color.FromRgb(75,210,140),Color.FromRgb(245,105,105)};
   double span=(volume.WorldAt(volume.Width-.5,volume.Height-.5,volume.Depth-.5)-volume.WorldAt(-.5,-.5,-.5)).Length;
   double thickness=Math.Max(.025,span*.0008);
   for(int plane=0;plane<3;plane++)
   {
    var polygon=SlicePlanePolygon(volume,focus,normals[plane]);if(polygon.Length<3)continue;
    var surface=new MeshGeometry3D();foreach(var p in polygon)surface.Positions.Add(new Point3D(p.X,p.Y,p.Z));for(int i=1;i<polygon.Length-1;i++){surface.TriangleIndices.Add(0);surface.TriangleIndices.Add(i);surface.TriangleIndices.Add(i+1);}
    AddGuide(group,surface,colors[plane],.09);
    var lines=new MeshGeometry3D();for(int i=0;i<polygon.Length;i++)GuideLine(lines,polygon[i],polygon[(i+1)%polygon.Length],thickness);AddGuide(group,lines,colors[plane],.8);
   }
   // Three short patient-coordinate axes cross exactly at the displayed focus.
   var axes=new[]{new Vec3(1,0,0),new Vec3(0,1,0),new Vec3(0,0,1)};
   for(int i=0;i<3;i++)
   {
    double low=-span*.055,high=span*.055;if(!ClipGuide(volume,focus,axes[i],ref low,ref high))continue;
    var line=new MeshGeometry3D();GuideLine(line,focus+axes[i]*low,focus+axes[i]*high,thickness*1.6);AddGuide(group,line,colors[2-i],1);
   }
   group.Freeze();sliceVisual.Content=group;gpu?.SetGuides(group);
  }
  static void AddGuide(Model3DGroup group,MeshGeometry3D mesh,Color color,double opacity)
  {
   mesh.Freeze();var brush=new SolidColorBrush(color){Opacity=opacity};brush.Freeze();var material=new EmissiveMaterial(brush);material.Freeze();group.Children.Add(new GeometryModel3D(mesh,material){BackMaterial=material});
  }
  static void GuideLine(MeshGeometry3D mesh,Vec3 a,Vec3 b,double thickness)
  {
   var delta=b-a;if(delta.Length<1e-7)return;var direction=delta.Normalized();var seed=Math.Abs(direction.Z)<.9?new Vec3(0,0,1):new Vec3(0,1,0);var right=direction.Cross(seed).Normalized()*thickness;var up=direction.Cross(right);
   foreach(var offset in new[]{right,up}){int k=mesh.Positions.Count;foreach(var point in new[]{a-offset,b-offset,b+offset,a+offset})mesh.Positions.Add(new Point3D(point.X,point.Y,point.Z));foreach(int index in new[]{0,1,2,0,2,3})mesh.TriangleIndices.Add(k+index);}
  }
  static bool ClipGuide(VolumeData volume,Vec3 focus,Vec3 direction,ref double low,ref double high)
  {
   var axes=new[]{volume.AxisX,volume.AxisY,volume.AxisZ};var spacing=new[]{volume.SpacingX,volume.SpacingY,volume.SpacingZ};var size=new[]{volume.Width,volume.Height,volume.Depth};var delta=focus-volume.Origin;
   for(int i=0;i<3;i++)
   {
    double position=delta.Dot(axes[i]),rate=direction.Dot(axes[i]),minimum=-.5*spacing[i],maximum=(size[i]-.5)*spacing[i];
    if(Math.Abs(rate)<1e-12){if(position<minimum||position>maximum)return false;continue;}double a=(minimum-position)/rate,b=(maximum-position)/rate;if(a>b){double temp=a;a=b;b=temp;}low=Math.Max(low,a);high=Math.Min(high,b);if(high<=low)return false;
   }
   return true;
  }
  internal static Vec3[] SlicePlanePolygon(VolumeData volume,Vec3 focus,Vec3 normal)
  {
   var corners=new Vec3[8];for(int i=0;i<8;i++)corners[i]=volume.WorldAt((i&1)==0?-.5:volume.Width-.5,(i&2)==0?-.5:volume.Height-.5,(i&4)==0?-.5:volume.Depth-.5);
   var points=new List<Vec3>();Action<Vec3> add=p=>{if(!points.Any(q=>(q-p).Length<1e-7))points.Add(p);};
   for(int i=0;i<8;i++)for(int axis=0;axis<3;axis++)
   {
    int j=i^(1<<axis);if(j<i)continue;var a=corners[i];var b=corners[j];double da=(a-focus).Dot(normal),db=(b-focus).Dot(normal);
    if(Math.Abs(da)<1e-8)add(a);if(Math.Abs(db)<1e-8)add(b);if((da<0&&db>0)||(da>0&&db<0))add(a+(b-a)*(da/(da-db)));
   }
   if(points.Count<3)return new Vec3[0];var center=new Vec3();foreach(var p in points)center+=p;center/=points.Count;var right=Math.Abs(normal.Z)<.9?normal.Cross(new Vec3(0,0,1)).Normalized():new Vec3(1,0,0);var up=normal.Cross(right);
   return points.OrderBy(p=>Math.Atan2((p-center).Dot(up),(p-center).Dot(right))).ToArray();
  }
  static string SceneKey(RenderScene s,double level)
  {
   string matrices(IEnumerable<double> values)=>string.Join(",",values.Select(x=>x.ToString("R",CultureInfo.InvariantCulture)));
   var parts=new List<string>{(s.Volume==null?0:Id(s.Volume)).ToString(),s.Entry?.Modality??"",level.ToString("R",CultureInfo.InvariantCulture),(level>0&&s.AbsoluteIsodoses).ToString()};
   foreach(var r in s.Structures??new List<RoiOverlay>())if(r?.Roi!=null&&r.Roi.Visible)parts.Add("R"+Id(r.Roi)+":"+matrices(r.RoiToImage.Values));
   foreach(var d in s.Doses??new List<DoseOverlay>())if(d?.Dose!=null&&d.Dose.Visible)parts.Add("D"+Id(d.Dose)+":"+matrices(d.ImageToDose.Values));
   foreach(var p in s.Isocenters??new Vec3[0])parts.Add("I"+matrices(new[]{p.X,p.Y,p.Z}));return string.Join("|",parts);
  }
  async void StartBuild()
  {
   if(disposed||(!IsVisible&&!backgroundPreparation)||scene==null||doseLevel==null)return;
   if(focusedRoi!=null&&(ThreeDGeometry.SurfaceType(focusedRoi)&(RoiSurfaceTypes.External|RoiSurfaceTypes.Support))!=0)focusedRoi=null;
   double level=DisplayDoseLevel/(scene.AbsoluteIsodoses?1:100);bool includeBone=bone.IsChecked==true,includeSkin=skin.IsChecked==true,includeDose=dose.IsChecked==true;var types=SelectedTypes;var selected=focusedRoi;string nextKey=SceneKey(scene,includeDose?level:0)+"|"+includeBone+includeSkin+includeDose+types+"|F"+(selected!=null&&(!selected.Visible||!ThreeDGeometry.DisplayRoi(selected,types))?Id(selected):0);if(key==nextKey)return;key=nextKey;
   string nextDoseKey=(includeDose?level.ToString("R",CultureInfo.InvariantCulture):"off")+scene.AbsoluteIsodoses+string.Join(";",scene.Doses.Select(d=>Id(d.Dose)+TransformKey(d.ImageToDose)));
   if(preparedDoseKey!=nextDoseKey&&prepared!=null){prepared=new Prepared{Parts=prepared.Parts.Where(p=>p.Kind!="Dose").ToList()};ApplyModels();}preparedDoseKey=nextDoseKey;
   var copy=scene.Snapshot();int mine=++generation;pending?.Cancel();var cancel=new CancellationTokenSource();pending=cancel;
   bool ct=copy.Volume!=null&&copy.Entry?.Modality=="CT";bone.IsEnabled=skin.IsEnabled=skinOpacity.IsEnabled=ct;bone.ToolTip=skin.ToolTip=ct?"Surface derived from a CT threshold; not an anatomical segmentation":"Bone/skin thresholds require CT values in HU";
   if(prepared==null&&!cameraAdjusted)ResetCamera();
   status.Text=prepared==null?"Building 3D surfaces in the background …":"Adding missing surfaces · current scene retained …";
   try
   {
    if(!IsVisible)await Task.Delay(120,cancel.Token);
    bool progressDone=false;var next=await Task.Run(()=>PrepareFilteredProgress(copy,ct,level,cancel.Token,cache,includeBone,includeSkin,includeDose,types,selected,partial=>{
     Dispatcher.BeginInvoke(new Action(()=>{if(disposed||mine!=generation||progressDone)return;var merged=new Prepared();merged.Parts.AddRange(partial.Parts);if(prepared!=null)merged.Parts.AddRange(prepared.Parts.Where(p=>!merged.Parts.Any(n=>n.Mesh==p.Mesh||(n.Kind==p.Kind&&n.Roi==p.Roi))));prepared=merged;ApplyModels();}),DispatcherPriority.Background);
    }),cancel.Token);
    await Dispatcher.InvokeAsync(()=>{if(disposed||mine!=generation)return;progressDone=true;prepared=next;if(!cameraAdjusted&&changedCamera())ResetCamera();ApplyModels();
    int triangles=next.Parts.Sum(x=>x.Mesh.TriangleIndices.Count/3);
    status.Text=next.Parts.Count(p=>p.Kind=="Roi")+" selected ROI surfaces · "+string.Format(CultureInfo.InvariantCulture,"{0} surfaces · {1:N0} triangles · high-detail contour surfaces{2}{3}",next.Parts.Count,triangles,next.Fallbacks>0?" · "+next.Fallbacks+" ROIs shown as contour lines only":"",next.Skipped>0?" · "+next.Skipped+" objects unavailable":next.Reduced>0?" · detail level adjusted automatically":"");});
   }
   catch(OperationCanceledException){}
   catch(Exception){if(!Dispatcher.HasShutdownStarted)await Dispatcher.InvokeAsync(()=>{if(!disposed&&mine==generation){key=null;status.Text="3D display unavailable for these objects.";}});}
   finally{if(pending==cancel)pending=null;cancel.Dispose();}
  }
  const int SceneTriangleLimit=8000000;
  static ThreeDMeshData AdaptiveMesh(VolumeData bounds,Func<Vec3,float> sample,double level,int budget,CancellationToken token,out bool reduced)
  {
   reduced=false;ThreeDMeshData best=null;
   foreach(int resolution in new[]{160,128,96,80,64})
   {
    token.ThrowIfCancellationRequested();ThreeDMeshData mesh;
    try{mesh=ThreeDGeometry.Isosurface(bounds,sample,level,resolution,token);}catch(InvalidOperationException){reduced=true;continue;}
    if(mesh.Indices.Count==0){if(best!=null)return best;continue;}
    best=mesh;if(mesh.Indices.Count/3<=budget)return mesh;reduced=true;
   }
   return best??new ThreeDMeshData();
  }
  static Prepared Prepare(RenderScene s,bool ct,double level,CancellationToken token)=>PrepareCore(s,ct,level,token,new Dictionary<string,Part>(),true,true,true,false);
  static Prepared PrepareCore(RenderScene s,bool ct,double level,CancellationToken token,Dictionary<string,Part> cache,bool includeBone,bool includeSkin,bool includeDose,bool includeAll)
   =>PrepareFocused(s,ct,level,token,cache,includeBone,includeSkin,includeDose,includeAll,null);
  static Prepared PrepareFocused(RenderScene s,bool ct,double level,CancellationToken token,Dictionary<string,Part> cache,bool includeBone,bool includeSkin,bool includeDose,bool includeAll,StructureRoi focused)
   =>PrepareFiltered(s,ct,level,token,cache,includeBone,includeSkin,includeDose,includeAll?RoiSurfaceTypes.Ptv|RoiSurfaceTypes.Organ|RoiSurfaceTypes.Support|RoiSurfaceTypes.Other:RoiSurfaceTypes.Ptv,focused);
  static Prepared PrepareFiltered(RenderScene s,bool ct,double level,CancellationToken token,Dictionary<string,Part> cache,bool includeBone,bool includeSkin,bool includeDose,RoiSurfaceTypes types,StructureRoi focused)
   =>PrepareFilteredProgress(s,ct,level,token,cache,includeBone,includeSkin,includeDose,types,focused,null);
  static Prepared PrepareFilteredProgress(RenderScene s,bool ct,double level,CancellationToken token,Dictionary<string,Part> cache,bool includeBone,bool includeSkin,bool includeDose,RoiSurfaceTypes types,StructureRoi focused,Action<Prepared> progress)
  {
   var result=new Prepared();int totalTriangles=0;
   var rois=(s.Structures??new List<RoiOverlay>()).Where(x=>x?.Roi!=null&&(!ThreeDGeometry.ExternalRoi(x.Roi)||(types&RoiSurfaceTypes.External)!=0)&&(x.Roi==focused||x.Roi.Visible&&ThreeDGeometry.DisplayRoi(x.Roi,types))).OrderByDescending(x=>x.Roi==focused).Take(64).ToArray();
   var doses=(s.Doses??new List<DoseOverlay>()).Where(x=>includeDose&&x?.Dose!=null&&x.Dose.Visible&&x.Dose.Maximum>0&&(!s.AbsoluteIsodoses||(IsodoseConfiguration.IsPhysicalGy(x.Dose)&&level<=x.Dose.Maximum))).Take(8).ToArray();
   int contextBudget=ct&&(includeBone||includeSkin)?1000000:0,doseTotalBudget=doses.Length==0?0:Math.Min(1000000,500000*doses.Length);
   int roiBudget=1000000;
   string objectKey=null,objectRoiType=null;StructureRoi objectRoi=null;ThreeDMeshData interactionMesh=null;bool objectFallback=false,objectReduced=false;
   Func<string,bool> reuse=k=>{objectKey=k;interactionMesh=null;objectFallback=objectReduced=false;lock(cache){Part part;if(!cache.TryGetValue(k,out part))return false;token.ThrowIfCancellationRequested();if(totalTriangles+part.Mesh.TriangleIndices.Count/3>SceneTriangleLimit)return false;result.Parts.Add(part);totalTriangles+=part.Mesh.TriangleIndices.Count/3;result.CacheHits++;if(part.Fallback)result.Fallbacks++;if(part.Reduced)result.Reduced++;return true;}};
   Action<ThreeDMeshData,string,Color> add=(data,kind,color)=>
   {
    if(data==null||data.Indices.Count==0){result.Skipped++;return;}
    token.ThrowIfCancellationRequested();if(result.Parts.Count>=80||totalTriangles+data.Indices.Count/3>SceneTriangleLimit){result.Skipped++;return;}
    totalTriangles+=data.Indices.Count/3;var geometry=new MeshGeometry3D();var center=new Vec3();
    for(int i=0;i<data.Points.Count;i++){if((i&4095)==0)token.ThrowIfCancellationRequested();var p=data.Points[i];geometry.Positions.Add(new Point3D(p.X,p.Y,p.Z));center+=p;}
    foreach(var i in data.Indices)geometry.TriangleIndices.Add(i);foreach(var normal in data.Normals)geometry.Normals.Add(new Vector3D(normal.X,normal.Y,normal.Z));geometry.Freeze();
    var part=new Part{Mesh=geometry,InteractionMesh=interactionMesh==null||interactionMesh.Indices.Count==0?geometry:FreezeMesh(interactionMesh),Roi=objectRoi,Kind=kind,RoiType=objectRoiType,Color=color,Center=center/data.Points.Count,Fallback=objectFallback,Reduced=objectReduced};result.Parts.Add(part);interactionMesh=null;
    if(objectKey!=null)lock(cache){token.ThrowIfCancellationRequested();if(cache.Count>=96||cache.Values.Sum(p=>p.Mesh.TriangleIndices.Count/3)+data.Indices.Count/3>10000000)cache.Clear();cache[objectKey]=part;}
   };
   // Reserve independent fair shares for CT context, dose and every ROI. Earlier large
   // objects cannot consume later objects' budget; resolution adapts before publication.
   if(ct&&s.Volume!=null)foreach(double threshold in new[]{300d,-350d}.Where(t=>t>0?includeBone:includeSkin))
   {
    if(reuse("C"+Id(s.Volume)+":"+threshold))continue;
    bool reduced;var mesh=AdaptiveMesh(s.Volume,s.Volume.Sample,threshold,contextBudget/2,token,out reduced);if(reduced)result.Reduced++;

    objectReduced=reduced;mesh=ThreeDGeometry.Smooth(mesh,.5,token);add(mesh,threshold>0?"Bone":"Skin",threshold>0?Color.FromRgb(236,229,211):Color.FromRgb(205,172,151));
   }
   result.Skipped+=Math.Max(0,(s.Doses?.Count??0)-8);
   foreach(var overlay in doses)
   {
    token.ThrowIfCancellationRequested();if(reuse("D"+Id(overlay.Dose)+":"+TransformKey(overlay.ImageToDose)+":"+s.AbsoluteIsodoses+":"+level+":"+doseTotalBudget/Math.Max(1,doses.Length)))continue;bool reduced;ThreeDMeshData mesh;
    var doseBounds=overlay.Dose.SamplingBounds;
    if(doseBounds==null||doseBounds.Depth<2){result.Skipped++;continue;}
    mesh=AdaptiveMesh(doseBounds,overlay.Dose.Sample,(s.AbsoluteIsodoses?level:overlay.Dose.Maximum*level),doseTotalBudget/Math.Max(1,doses.Length),token,out reduced);
    var inverse=overlay.ImageToDose.Inverse();for(int i=0;i<mesh.Points.Count;i++){if((i&1023)==0)token.ThrowIfCancellationRequested();mesh.Points[i]=inverse.Transform(mesh.Points[i]);}

    objectReduced=reduced;mesh=ThreeDGeometry.Smooth(mesh,0,token);if(reduced)result.Reduced++;add(mesh,"Dose",Color.FromRgb(255,150,65));
   }
   result.Skipped+=Math.Max(0,(s.Structures?.Count??0)-64);
   // Build at most one pair at a time. Workers only own scalar/mesh data; cache,
   // WPF freezing, scene budgets and draw order remain on this preparation thread.
   int roiWorkers=Environment.Is64BitProcess?2:1;
   for(int offset=0;offset<rois.Length;offset+=roiWorkers)
   {
    token.ThrowIfCancellationRequested();int count=Math.Min(roiWorkers,rois.Length-offset);var batch=new RoiOverlay[count];var keys=new string[count];var cached=new bool[count];
    var meshes=new ThreeDMeshData[count];var reasons=new string[count];var built=new bool[count];var failed=new bool[count];var fallback=new bool[count];var reduced=new bool[count];
    for(int i=0;i<count;i++)
    {
     var overlay=batch[i]=rois[offset+i];keys[i]="R"+Id(overlay.Roi)+":"+TransformKey(overlay.RoiToImage)+":"+roiBudget+":"+overlay.Roi.InterpretedType+":"+overlay.Roi.Red+","+overlay.Roi.Green+","+overlay.Roi.Blue;
     lock(cache)cached[i]=cache.ContainsKey(keys[i]);
    }
    Action<int> buildRoi=i=>
    {
     token.ThrowIfCancellationRequested();var overlay=batch[i];built[i]=true;
     try
     {
      meshes[i]=ThreeDGeometry.BuildRoiSurface(overlay.Roi,overlay.RoiToImage,token,out reasons[i]);fallback[i]=meshes[i]==null;reduced[i]=reasons[i]!=null&&reasons[i].IndexOf("bounded",StringComparison.OrdinalIgnoreCase)>=0;
      if(fallback[i])meshes[i]=ThreeDGeometry.ContourLines(overlay.Roi,overlay.RoiToImage,token);
     }
     catch(InvalidOperationException){failed[i]=true;}
    };
    Parallel.For(0,count,new ParallelOptions{CancellationToken=token,MaxDegreeOfParallelism=roiWorkers},i=>{if(!cached[i])buildRoi(i);});
    for(int i=0;i<count;i++)
    {
     token.ThrowIfCancellationRequested();var overlay=batch[i];objectRoi=overlay.Roi;objectRoiType=overlay.Roi.InterpretedType;
     if(reuse(keys[i])){meshes[i]=null;continue;}
     // A preceding publication may evict a cache entry. Rebuild it locally only
     // after the pair has joined, preserving the original cache/budget behavior.
     if(!built[i])buildRoi(i);
     if(failed[i]){result.Skipped++;meshes[i]=null;continue;}
     objectFallback=fallback[i];objectReduced=reduced[i];if(fallback[i])result.Fallbacks++;if(reduced[i])result.Reduced++;
     try{add(meshes[i],"Roi",Color.FromRgb(overlay.Roi.Red,overlay.Roi.Green,overlay.Roi.Blue));}
     catch(InvalidOperationException){result.Skipped++;}
     finally{meshes[i]=null;}
    }
    if(progress!=null)progress(new Prepared{Parts=new List<Part>(result.Parts)});
   }
   foreach(var p in s.Isocenters??new Vec3[0])
   {
    objectKey=null;objectRoi=null;objectRoiType=null;interactionMesh=null;objectFallback=objectReduced=false;var cross=new StructureRoi();foreach(var axis in new[]{new Vec3(1,0,0),new Vec3(0,1,0),new Vec3(0,0,1)})cross.Contours.Add(new Contour{GeometricType="OPEN_PLANAR",Points=new List<Vec3>{p-axis*5,p+axis*5}});
    add(ThreeDGeometry.ContourLines(cross,Matrix4.Identity,token),"Isocenter",Colors.Cyan);
   }
   return result;
  }
  static MeshGeometry3D FreezeMesh(ThreeDMeshData data)
  {
   var mesh=new MeshGeometry3D();foreach(var p in data.Points)mesh.Positions.Add(new Point3D(p.X,p.Y,p.Z));foreach(int i in data.Indices)mesh.TriangleIndices.Add(i);foreach(var n in data.Normals)mesh.Normals.Add(new Vector3D(n.X,n.Y,n.Z));mesh.Freeze();return mesh;
  }
  void ApplyModels()
  {
   if(disposed||visual==null)return;qualityModels=CreateModels(false);interactionModels=qualityModels;visual.Content=qualityModels;if(IsVisible){gpu?.SetSurfaces(qualityModels);gpuDirty=false;}else gpuDirty=true;
  }
  Model3DGroup CreateModels(bool interactive)
  {
   var group=new Model3DGroup();group.Children.Add(new AmbientLight(Color.FromRgb(105,105,105)));
   group.Children.Add(new DirectionalLight(Colors.White,new Vector3D(-.8,-.5,-1)));group.Children.Add(new DirectionalLight(Color.FromRgb(100,115,130),new Vector3D(.5,1,.3)));
   if(prepared!=null)
   {
    var visible=prepared.Parts.Where(p=>p.Kind=="Bone"?bone.IsChecked==true&&bone.IsEnabled:p.Kind=="Skin"?skin.IsChecked==true&&skin.IsEnabled:p.Kind=="Roi"?(scene?.Structures.Any(r=>r.Roi==p.Roi)??false)&&(p.Roi.Visible&&ThreeDGeometry.DisplayRoi(p.Roi,SelectedTypes)||p.Roi==focusedRoi):p.Kind=="Isocenter"?(scene?.Isocenters?.Any(i=>(p.Center-i).Length<.01)??false):dose.IsChecked==true).ToArray();
    bool hasInterior=visible.Any(p=>p.Kind=="Bone"||p.Kind=="Roi"||p.Kind=="Dose");
    var eye=new Vec3(camera.Position.X,camera.Position.Y,camera.Position.Z);
    foreach(var part in visible.OrderBy(p=>p.Kind=="Bone"?0:1).ThenByDescending(p=>(p.Center-eye).Length))
    {
     bool highlighted=focusedRoi!=null&&part.Roi==focusedRoi;
     // Transparent enclosing shells create expensive overdraw and can mask the
     // selected interior during orbit. Omit those context shells only in preview.
     if(interactive&&!highlighted&&(part.Kind=="Skin"&&hasInterior||part.Kind=="Roi"&&OrganOpacityScale(part)<1&&visible.Any(p=>p.Kind=="Roi"&&p!=part&&(p.Roi==focusedRoi||OrganOpacityScale(p)==1))))continue;
     double alpha=part.Kind=="Bone"||part.Kind=="Isocenter"?1:part.Kind=="Dose"?.25:part.Kind=="Skin"||ThreeDGeometry.ExternalRoi(part.Roi)?skinOpacity.Value:(opacity?.Value??.7)*OrganOpacityScale(part);
     if(interactive&&part.Kind!="Dose")alpha=1;
     var color=part.Color;if(part.Kind=="Dose"){double red,green,blue;SliceRaster.IsodoseColor(scene,DisplayDoseLevel,out red,out green,out blue);color=Color.FromRgb((byte)red,(byte)green,(byte)blue);}byte strongest=Math.Max(color.R,Math.Max(color.G,color.B));if(part.Kind=="Roi"&&strongest>0&&strongest<120){double boost=120d/strongest;color=Color.FromRgb((byte)(color.R*boost),(byte)(color.G*boost),(byte)(color.B*boost));}
     if(highlighted){alpha=.95;color=Color.FromRgb((byte)Math.Min(255,color.R+50),(byte)Math.Min(255,color.G+50),(byte)Math.Min(255,color.B+50));}
     var brush=new SolidColorBrush(color){Opacity=alpha};brush.Freeze();Material material=part.Kind=="Isocenter"?(Material)new EmissiveMaterial(brush):new DiffuseMaterial(brush);
     if(highlighted){var glow=new MaterialGroup();glow.Children.Add(material);var light=new SolidColorBrush(color){Opacity=.45};light.Freeze();glow.Children.Add(new EmissiveMaterial(light));material=glow;}material.Freeze();
     var mesh=interactive&&!highlighted?part.InteractionMesh:part.Mesh;bool back=part.Kind!="Roi"||part.Fallback;if(interactive&&!part.Fallback)back=false;
     group.Children.Add(new GeometryModel3D(mesh,material){BackMaterial=back?material:null});
    }
   }
   group.Freeze();return group;
  }
  double OrganOpacityScale(Part part)
  {
   if(!string.Equals(part.RoiType,"ORGAN",StringComparison.OrdinalIgnoreCase))return 1;
   var bounds=part.Mesh.Bounds;var extent=new Vec3(bounds.SizeX,bounds.SizeY,bounds.SizeZ);double scale=1;
   foreach(var targetPart in prepared.Parts.Where(p=>p.Kind=="Roi"&&string.Equals(p.RoiType,"PTV",StringComparison.OrdinalIgnoreCase)))
   {
    var targetBounds=targetPart.Mesh.Bounds;if(bounds.Contains(targetBounds))scale=Math.Min(scale,ThreeDGeometry.RoiOpacityScale(part.RoiType,extent,new Vec3(targetBounds.SizeX,targetBounds.SizeY,targetBounds.SizeZ),true));
   }
   if(scene?.Volume!=null)
   {
    var volume=scene.Volume;var context=Rect3D.Empty;for(int i=0;i<8;i++){var p=volume.WorldAt((i&1)==0?-.5:volume.Width-.5,(i&2)==0?-.5:volume.Height-.5,(i&4)==0?-.5:volume.Depth-.5);context.Union(new Point3D(p.X,p.Y,p.Z));}
    scale=Math.Min(scale,ThreeDGeometry.RoiOpacityScale(part.RoiType,extent,new Vec3(context.SizeX,context.SizeY,context.SizeZ),false));
   }
   return scale;
  }
  bool changedCamera()=>scene?.Volume==null&&distance==500;
  void ResetCamera()
  {
   if(scene==null)return;
   var points=(compact&&scene.Volume!=null?new List<RoiOverlay>():scene.Structures??new List<RoiOverlay>()).Where(r=>r?.Roi!=null&&ThreeDGeometry.DefaultRoi(r.Roi)).SelectMany(r=>r.Roi.Contours.SelectMany(c=>c.Points).Select(p=>r.RoiToImage.Transform(p))).ToList();
   bool showContext=compact||scene.Entry?.Modality=="CT"&&(skin.IsChecked==true||bone.IsChecked==true);
   if(scene.Volume!=null&&(points.Count==0||showContext)){var v=scene.Volume;foreach(double x in new[]{-.5,v.Width-.5})foreach(double y in new[]{-.5,v.Height-.5})foreach(double z in new[]{-.5,v.Depth-.5})points.Add(v.WorldAt(x,y,z));}
   if(points.Count==0&&prepared!=null)foreach(var part in prepared.Parts)foreach(var p in part.Mesh.Positions)points.Add(new Vec3(p.X,p.Y,p.Z));
   if(points.Count==0)points.AddRange(scene.Isocenters??new Vec3[0]);if(points.Count==0)return;
   var low=new Vec3(points.Min(p=>p.X),points.Min(p=>p.Y),points.Min(p=>p.Z));var high=new Vec3(points.Max(p=>p.X),points.Max(p=>p.Y),points.Max(p=>p.Z));target=(low+high)/2;radius=Math.Max(10,(high-low).Length*.55);double aspect=viewport.ActualHeight>0?Math.Max(.1,viewport.ActualWidth/viewport.ActualHeight):1;
   yaw=-1.7;pitch=.25;
   var view=new Vec3(-Math.Cos(pitch)*Math.Cos(yaw),-Math.Cos(pitch)*Math.Sin(yaw),-Math.Sin(pitch));var right=view.Cross(new Vec3(0,0,1)).Normalized();var up=right.Cross(view);double horizontal=Math.Tan(camera.FieldOfView*Math.PI/360)*.9,vertical=horizontal/aspect;
   distance=radius*.15;foreach(var point in points){var delta=point-target;double depth=delta.Dot(view);distance=Math.Max(distance,Math.Max(Math.Abs(delta.Dot(right))/horizontal-depth,Math.Abs(delta.Dot(up))/vertical-depth));}
   UpdateCamera();
  }
  void UpdateCamera()
  {
   var offset=new Vec3(Math.Cos(pitch)*Math.Cos(yaw),Math.Cos(pitch)*Math.Sin(yaw),Math.Sin(pitch))*distance;var p=target+offset;
   camera.Position=new Point3D(p.X,p.Y,p.Z);camera.LookDirection=new Vector3D(-offset.X,-offset.Y,-offset.Z);camera.UpDirection=new Vector3D(0,0,1);camera.NearPlaneDistance=Math.Max(.1,radius*.002);camera.FarPlaneDistance=radius*100;
   orientationBadge.SetDirection(camera.LookDirection,camera.UpDirection);gpu?.SetCamera(camera);isocenterOverlay.Set(scene?.Isocenters,camera);
  }
  public void Dispose(){if(disposed)return;gpu?.Dispose();gpu=null;EndInteraction();disposed=true;++generation;pending?.Cancel();pending=null;prepared=null;scene=null;focusedRoi=null;qualityModels=interactionModels=null;lock(cache)cache.Clear();visual.Content=null;sliceVisual.Content=null;sliceVolume=null;}
 }
}
