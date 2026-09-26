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

namespace QuickLook.DicomRT
{
 public sealed class ThreeDControl : UserControl,IDisposable
 {
  sealed class Part {public MeshGeometry3D Mesh;public string Kind;public Color Color;public Vec3 Center;public bool Fallback,Reduced;}
  sealed class Prepared {public List<Part> Parts=new List<Part>();public int Fallbacks,Skipped,Reduced,CacheHits;}
  readonly Viewport3D viewport=new Viewport3D();readonly ModelVisual3D visual=new ModelVisual3D();readonly PerspectiveCamera camera=new PerspectiveCamera();
  readonly ModelVisual3D sliceVisual=new ModelVisual3D();readonly bool compact;VolumeData sliceVolume;Vec3 sliceFocus;bool cameraAdjusted;
  readonly CheckBox bone,skin,structures,dose,allRois;readonly Slider opacity;readonly ComboBox doseLevel;readonly TextBlock status;
  readonly Dictionary<string,Part> cache=new Dictionary<string,Part>();
  sealed class Identity {public readonly int Value=Interlocked.Increment(ref identitySequence);}
  static int identitySequence;static readonly ConditionalWeakTable<object,Identity> identities=new ConditionalWeakTable<object,Identity>();
  static int Id(object value)=>value==null?0:identities.GetValue(value,x=>new Identity()).Value;
  static string TransformKey(Matrix4 transform)=>string.Join(",",transform.Values.Select(x=>x.ToString("R",CultureInfo.InvariantCulture)));
  RenderScene scene;Prepared prepared;CancellationTokenSource pending;int generation;bool disposed,doseDefaultInitialized;string key;double yaw=-1.7,pitch=.25,distance=500,radius=250;Vec3 target;Point mouse;bool dragging;
  public ThreeDControl(bool compact=false)
  {
   this.compact=compact;
   Background=Theme.Background;var root=new Grid();root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
   var controls=new WrapPanel{Margin=new Thickness(8,4,8,4)};
   bone=Toggle("Bone ≥300 HU",false);skin=Toggle("Skin ≥−350 HU",false);structures=Toggle("PTV / organs",true);dose=Toggle("Dose",false);allRois=Toggle("All ROI types",false);
   controls.Children.Add(bone);controls.Children.Add(skin);controls.Children.Add(structures);controls.Children.Add(dose);controls.Children.Add(allRois);
   controls.Children.Add(Theme.Text("Opacity",11));opacity=new Slider{Minimum=.1,Maximum=1,Value=.7,Width=95,Margin=new Thickness(6),ToolTip="Opacity of ROI and skin surfaces"};controls.Children.Add(opacity);opacity.ValueChanged+=(s,e)=>ApplyModels();
   doseLevel=new ComboBox{Width=100,Margin=new Thickness(5),ItemsSource=new[]{"20 % max.","50 % max.","80 % max.","95 % max."},SelectedIndex=1,ToolTip="Isodose relative to each maximum; no automatic summation"};controls.Children.Add(doseLevel);doseLevel.SelectionChanged+=(s,e)=>{StartBuild();};
   var reset=Theme.Button("Reset view");reset.Click+=(s,e)=>{cameraAdjusted=false;ResetCamera();};controls.Children.Add(reset);
   if(compact)
   {
    controls.Children.Clear();controls.Margin=new Thickness(5,0,5,0);structures.Content="ROI";structures.ToolTip="PTV and ORGAN surfaces";dose.ToolTip="50 % isodose surface";reset.Content="Reset";reset.Padding=new Thickness(6,2,6,2);reset.Margin=new Thickness(3,1,0,1);
    var label=Theme.Text("3D",11,Theme.Muted);label.ToolTip="Blue: axial · green: coronal · red: sagittal. Drag to rotate; wheel to zoom.";label.VerticalAlignment=VerticalAlignment.Center;label.Margin=new Thickness(2,0,6,0);controls.Children.Add(label);controls.Children.Add(structures);controls.Children.Add(dose);controls.Children.Add(reset);
   }
   root.Children.Add(controls);
   var host=new Border{Background=Theme.Background,Child=viewport,ClipToBounds=true};Grid.SetRow(host,1);root.Children.Add(host);host.SizeChanged+=(s,e)=>{if(compact&&!cameraAdjusted)ResetCamera();};
   viewport.Camera=camera;viewport.Children.Add(visual);viewport.Children.Add(sliceVisual);camera.FieldOfView=42;
   host.MouseLeftButtonDown+=(s,e)=>{dragging=true;mouse=e.GetPosition(host);host.CaptureMouse();e.Handled=true;};
   host.MouseLeftButtonUp+=(s,e)=>{dragging=false;host.ReleaseMouseCapture();e.Handled=true;};host.LostMouseCapture+=(s,e)=>dragging=false;
   host.MouseMove+=(s,e)=>{if(!dragging)return;cameraAdjusted=true;var p=e.GetPosition(host);yaw+=(p.X-mouse.X)*.01;pitch=Math.Max(-1.45,Math.Min(1.45,pitch+(p.Y-mouse.Y)*.01));mouse=p;UpdateCamera();e.Handled=true;};
   host.MouseWheel+=(s,e)=>{cameraAdjusted=true;distance=Math.Max(radius*.15,Math.Min(radius*20,distance*Math.Pow(1.15,-e.Delta/120.0)));UpdateCamera();e.Handled=true;};
   var footer=new StackPanel{Margin=new Thickness(10,4,10,7)};status=Theme.Text("3D: select spatial DICOM objects",11);footer.Children.Add(status);
   footer.Children.Add(Theme.Text("Drag to rotate · mouse wheel to zoom · LPS patient coordinates. Skin/bone surfaces use CT thresholds; ROI surfaces are smoothed, bounded approximations of voxelized contours (≤1 mm smoothing displacement). Transparency is approximate; 3D provides an overview.",10,Theme.Muted));Grid.SetRow(footer,2);root.Children.Add(footer);if(compact)footer.Visibility=Visibility.Collapsed;Content=root;
   IsVisibleChanged+=(s,e)=>{if(IsVisible)StartBuild();else if(pending!=null){++generation;pending.Cancel();pending=null;key=null;}};
  }
  CheckBox Toggle(string label,bool initial)
  {
   var box=new CheckBox{Content=label,IsChecked=initial,Foreground=Theme.Foreground,Margin=new Thickness(5,7,12,7),VerticalAlignment=VerticalAlignment.Center};box.Checked+=(s,e)=>{ApplyModels();StartBuild();};box.Unchecked+=(s,e)=>{ApplyModels();StartBuild();};return box;
  }
  public void SetScene(RenderScene value)
  {
   if(disposed)return;bool changed=scene==null||scene.Volume!=value?.Volume||(scene.Volume==null&&scene.Entry!=value?.Entry&&scene.Entry?.SeriesUid!=value?.Entry?.SeriesUid);scene=value;
   if(changed){key=null;prepared=null;cameraAdjusted=false;ResetCamera();}
   if(!doseDefaultInitialized&&scene!=null)
   {
    bool hasRoi=(scene.Structures??new List<RoiOverlay>()).Any(r=>r?.Roi!=null&&r.Roi.Visible&&ThreeDGeometry.DefaultRoi(r.Roi));bool hasDose=(scene.Doses??new List<DoseOverlay>()).Any(d=>d?.Dose!=null&&d.Dose.Visible);
    if(hasRoi||hasDose||scene.Volume!=null){doseDefaultInitialized=true;if(!hasRoi&&hasDose&&scene.Volume==null)dose.IsChecked=true;}
   }
   if(scene==null){pending?.Cancel();++generation;prepared=null;key=null;ApplyModels();return;}
   if(IsVisible)StartBuild();
  }
  // Cheap orientation overlay, separate from ROI/dose preparation and camera state.
  // Polygon vertices are intersections of patient-coordinate slice planes and the
  // oriented image voxel envelope, not an axis-aligned approximation to that envelope.
  public void SetSlicePlanes(Vec3 focus,VolumeData volume)
  {
   if(disposed)return;if(volume==null){sliceVolume=null;sliceVisual.Content=null;return;}
   if(volume==sliceVolume&&(focus-sliceFocus).Length<1e-7)return;sliceVolume=volume;sliceFocus=focus;
   if(volume.Width<1||volume.Height<1||volume.Depth<1||double.IsNaN(focus.X)||double.IsNaN(focus.Y)||double.IsNaN(focus.Z)||double.IsInfinity(focus.X)||double.IsInfinity(focus.Y)||double.IsInfinity(focus.Z)){sliceVisual.Content=null;return;}
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
   group.Freeze();sliceVisual.Content=group;
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
   var parts=new List<string>{(s.Volume==null?0:Id(s.Volume)).ToString(),s.Entry?.Modality??"",level.ToString("R",CultureInfo.InvariantCulture)};
   foreach(var r in s.Structures??new List<RoiOverlay>())if(r?.Roi!=null&&r.Roi.Visible)parts.Add("R"+Id(r.Roi)+":"+matrices(r.RoiToImage.Values));
   foreach(var d in s.Doses??new List<DoseOverlay>())if(d?.Dose!=null&&d.Dose.Visible)parts.Add("D"+Id(d.Dose)+":"+matrices(d.ImageToDose.Values));
   foreach(var p in s.Isocenters??new Vec3[0])parts.Add("I"+matrices(new[]{p.X,p.Y,p.Z}));return string.Join("|",parts);
  }
  async void StartBuild()
  {
   if(disposed||!IsVisible||scene==null||doseLevel==null)return;
   double level=new[]{.2,.5,.8,.95}[Math.Max(0,doseLevel.SelectedIndex)];bool includeBone=bone.IsChecked==true,includeSkin=skin.IsChecked==true,includeDose=dose.IsChecked==true,includeAll=allRois.IsChecked==true;string nextKey=SceneKey(scene,includeDose?level:0)+"|"+includeBone+includeSkin+includeDose+includeAll;if(key==nextKey)return;key=nextKey;
   var copy=scene.Snapshot();int mine=++generation;pending?.Cancel();var cancel=new CancellationTokenSource();pending=cancel;
   bool ct=copy.Volume!=null&&copy.Entry?.Modality=="CT";bone.IsEnabled=skin.IsEnabled=ct;bone.ToolTip=skin.ToolTip=ct?"Surface derived from a CT threshold; not an anatomical segmentation":"Bone/skin thresholds require CT values in HU";
   status.Text="Building 3D surfaces in the background …";prepared=null;ApplyModels();
   try
   {
    var next=await Task.Run(()=>PrepareCore(copy,ct,level,cancel.Token,cache,includeBone,includeSkin,includeDose,includeAll),cancel.Token);
    if(disposed||mine!=generation)return;prepared=next;if(changedCamera())ResetCamera();ApplyModels();
    int triangles=next.Parts.Sum(x=>x.Mesh.TriangleIndices.Count/3);
    status.Text=next.Parts.Count(p=>p.Kind=="Roi")+" selected ROI surfaces · "+string.Format(CultureInfo.InvariantCulture,"{0} surfaces · {1:N0} triangles · bounded ROI resolution{2}{3}",next.Parts.Count,triangles,next.Fallbacks>0?" · "+next.Fallbacks+" ROIs shown as contour lines only":"",next.Skipped>0?" · "+next.Skipped+" objects unavailable":next.Reduced>0?" · detail level adjusted automatically":"");
   }
   catch(OperationCanceledException){}
   catch(Exception){if(!disposed&&mine==generation){key=null;status.Text="3D display unavailable for these objects.";}}
   finally{if(pending==cancel)pending=null;cancel.Dispose();}
  }
  const int SceneTriangleLimit=400000;
  static ThreeDMeshData AdaptiveMesh(VolumeData bounds,Func<Vec3,float> sample,double level,int budget,CancellationToken token,out bool reduced)
  {
   reduced=false;ThreeDMeshData best=null;
   foreach(int resolution in new[]{56,48,40,32,24,16,12,8})
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
  {
   var result=new Prepared();int totalTriangles=0;
   var rois=(s.Structures??new List<RoiOverlay>()).Where(x=>x?.Roi!=null&&x.Roi.Visible&&(includeAll||ThreeDGeometry.DefaultRoi(x.Roi))).Take(64).ToArray();
   var doses=(s.Doses??new List<DoseOverlay>()).Where(x=>includeDose&&x?.Dose!=null&&x.Dose.Visible&&x.Dose.Maximum>0).Take(8).ToArray();
   int contextBudget=ct&&(includeBone||includeSkin)?100000:0,doseTotalBudget=doses.Length==0?0:Math.Min(100000,100000*doses.Length);
   int roiBudget=Math.Min(30000,Math.Max(1500,Math.Min(180000,SceneTriangleLimit-120-contextBudget-doseTotalBudget)/Math.Max(1,rois.Length)));
   string objectKey=null;bool objectFallback=false,objectReduced=false;
   Func<string,bool> reuse=k=>{objectKey=k;objectFallback=objectReduced=false;lock(cache){Part part;if(!cache.TryGetValue(k,out part))return false;token.ThrowIfCancellationRequested();if(totalTriangles+part.Mesh.TriangleIndices.Count/3>SceneTriangleLimit)return false;result.Parts.Add(part);totalTriangles+=part.Mesh.TriangleIndices.Count/3;result.CacheHits++;if(part.Fallback)result.Fallbacks++;if(part.Reduced)result.Reduced++;return true;}};
   Action<ThreeDMeshData,string,Color> add=(data,kind,color)=>
   {
    if(data==null||data.Indices.Count==0){result.Skipped++;return;}
    token.ThrowIfCancellationRequested();if(result.Parts.Count>=80||totalTriangles+data.Indices.Count/3>SceneTriangleLimit){result.Skipped++;return;}
    totalTriangles+=data.Indices.Count/3;var geometry=new MeshGeometry3D();var center=new Vec3();
    for(int i=0;i<data.Points.Count;i++){if((i&4095)==0)token.ThrowIfCancellationRequested();var p=data.Points[i];geometry.Positions.Add(new Point3D(p.X,p.Y,p.Z));center+=p;}
    foreach(var i in data.Indices)geometry.TriangleIndices.Add(i);foreach(var normal in data.Normals)geometry.Normals.Add(new Vector3D(normal.X,normal.Y,normal.Z));geometry.Freeze();
    var part=new Part{Mesh=geometry,Kind=kind,Color=color,Center=center/data.Points.Count,Fallback=objectFallback,Reduced=objectReduced};result.Parts.Add(part);
    if(objectKey!=null)lock(cache){token.ThrowIfCancellationRequested();if(cache.Count>=96||cache.Values.Sum(p=>p.Mesh.TriangleIndices.Count/3)+data.Indices.Count/3>550000)cache.Clear();cache[objectKey]=part;}
   };
   // Reserve independent fair shares for CT context, dose and every ROI. Earlier large
   // objects cannot consume later objects' budget; resolution adapts before publication.
   if(ct&&s.Volume!=null)foreach(double threshold in new[]{300d,-350d}.Where(t=>t>0?includeBone:includeSkin))
   {
    if(reuse("C"+Id(s.Volume)+":"+threshold))continue;
    bool reduced;var mesh=AdaptiveMesh(s.Volume,s.Volume.Sample,threshold,contextBudget/2,token,out reduced);if(reduced)result.Reduced++;
    objectReduced=reduced;mesh=ThreeDGeometry.Smooth(mesh,0,token);add(mesh,threshold>0?"Bone":"Skin",threshold>0?Color.FromRgb(236,229,211):Color.FromRgb(205,172,151));
   }
   result.Skipped+=Math.Max(0,(s.Doses?.Count??0)-8);
   foreach(var overlay in doses)
   {
    token.ThrowIfCancellationRequested();if(reuse("D"+Id(overlay.Dose)+":"+TransformKey(overlay.ImageToDose)+":"+level+":"+doseTotalBudget/Math.Max(1,doses.Length)))continue;bool reduced;ThreeDMeshData mesh;
    var doseBounds=overlay.Dose.SamplingBounds;
    if(doseBounds==null||doseBounds.Depth<2){result.Skipped++;continue;}
    mesh=AdaptiveMesh(doseBounds,overlay.Dose.Sample,overlay.Dose.Maximum*level,doseTotalBudget/Math.Max(1,doses.Length),token,out reduced);
    var inverse=overlay.ImageToDose.Inverse();for(int i=0;i<mesh.Points.Count;i++){if((i&1023)==0)token.ThrowIfCancellationRequested();mesh.Points[i]=inverse.Transform(mesh.Points[i]);}
    objectReduced=reduced;mesh=ThreeDGeometry.Smooth(mesh,0,token);if(reduced)result.Reduced++;add(mesh,"Dose",Color.FromRgb(255,150,65));
   }
   result.Skipped+=Math.Max(0,(s.Structures?.Count??0)-64);
   foreach(var overlay in rois)
   {
    token.ThrowIfCancellationRequested();if(reuse("R"+Id(overlay.Roi)+":"+TransformKey(overlay.RoiToImage)+":"+roiBudget+":"+overlay.Roi.Red+","+overlay.Roi.Green+","+overlay.Roi.Blue))continue;
    try
    {
     ThreeDMeshData selected=null;bool fallback=false,reduced=false;
     foreach(int resolution in (roiBudget<4000?new[]{16,12,8}:roiBudget<7000?new[]{20,16,12,8}:roiBudget<16000?new[]{24,20,16,12,8}:new[]{32,24,20,16,12,8}))
     {
      string reason;var volume=ThreeDGeometry.VoxelizeRoi(overlay.Roi,overlay.RoiToImage,resolution,token,out reason,true);
      if(volume==null){fallback=true;break;}
      ThreeDMeshData mesh;
      try{mesh=ThreeDGeometry.Isosurface(volume,volume.Sample,.5,resolution,token);}catch(InvalidOperationException){reduced=true;continue;}
      if(mesh.Indices.Count==0)continue;selected=mesh;if(mesh.Indices.Count/3<=roiBudget)break;reduced=true;
     }
     if(fallback||selected==null){fallback=true;selected=ThreeDGeometry.ContourLines(overlay.Roi,overlay.RoiToImage,token);result.Fallbacks++;}
     objectFallback=fallback;objectReduced=reduced;if(!fallback&&selected!=null)selected=ThreeDGeometry.Smooth(selected,1,token);if(reduced)result.Reduced++;add(selected,"Roi",Color.FromRgb(overlay.Roi.Red,overlay.Roi.Green,overlay.Roi.Blue));
    }
    catch(InvalidOperationException){result.Skipped++;}
   }
   foreach(var p in s.Isocenters??new Vec3[0])
   {
    objectKey=null;objectFallback=objectReduced=false;var cross=new StructureRoi();foreach(var axis in new[]{new Vec3(1,0,0),new Vec3(0,1,0),new Vec3(0,0,1)})cross.Contours.Add(new Contour{GeometricType="OPEN_PLANAR",Points=new List<Vec3>{p-axis*5,p+axis*5}});
    add(ThreeDGeometry.ContourLines(cross,Matrix4.Identity,token),"Isocenter",Colors.Cyan);
   }
   return result;
  }
  void ApplyModels()
  {
   if(disposed||visual==null)return;var group=new Model3DGroup();group.Children.Add(new AmbientLight(Color.FromRgb(105,105,105)));
   group.Children.Add(new DirectionalLight(Colors.White,new Vector3D(-.8,-.5,-1)));group.Children.Add(new DirectionalLight(Color.FromRgb(100,115,130),new Vector3D(.5,1,.3)));
   if(prepared!=null)
   {
    var visible=prepared.Parts.Where(p=>p.Kind=="Bone"?bone.IsChecked==true&&bone.IsEnabled:p.Kind=="Skin"?skin.IsChecked==true&&skin.IsEnabled:p.Kind=="Roi"?structures.IsChecked==true:p.Kind=="Isocenter"||dose.IsChecked==true);
    var eye=new Vec3(camera.Position.X,camera.Position.Y,camera.Position.Z);
    foreach(var part in visible.OrderBy(p=>p.Kind=="Bone"?0:1).ThenByDescending(p=>(p.Center-eye).Length))
    {
     double alpha=part.Kind=="Bone"||part.Kind=="Isocenter"?1:part.Kind=="Dose"?.25:opacity?.Value??.35;
     var color=part.Color;byte strongest=Math.Max(color.R,Math.Max(color.G,color.B));if(part.Kind=="Roi"&&strongest>0&&strongest<120){double boost=120d/strongest;color=Color.FromRgb((byte)(color.R*boost),(byte)(color.G*boost),(byte)(color.B*boost));}
     var brush=new SolidColorBrush(color){Opacity=alpha};brush.Freeze();var material=new DiffuseMaterial(brush);material.Freeze();
     group.Children.Add(new GeometryModel3D(part.Mesh,material){BackMaterial=part.Kind=="Roi"&&!part.Fallback?null:material});
    }
   }
   visual.Content=group;
  }
  bool changedCamera()=>scene?.Volume==null&&distance==500;
  void ResetCamera()
  {
   if(scene==null)return;
   var points=(compact&&scene.Volume!=null?new List<RoiOverlay>():scene.Structures??new List<RoiOverlay>()).Where(r=>r?.Roi!=null&&ThreeDGeometry.DefaultRoi(r.Roi)).SelectMany(r=>r.Roi.Contours.SelectMany(c=>c.Points).Select(p=>r.RoiToImage.Transform(p))).ToList();
   if(points.Count==0&&scene.Volume!=null){var v=scene.Volume;foreach(double x in new[]{-.5,v.Width-.5})foreach(double y in new[]{-.5,v.Height-.5})foreach(double z in new[]{-.5,v.Depth-.5})points.Add(v.WorldAt(x,y,z));}
   if(points.Count==0&&prepared!=null)foreach(var part in prepared.Parts)foreach(var p in part.Mesh.Positions)points.Add(new Vec3(p.X,p.Y,p.Z));
   if(points.Count==0)points.AddRange(scene.Isocenters??new Vec3[0]);if(points.Count==0)return;
   var low=new Vec3(points.Min(p=>p.X),points.Min(p=>p.Y),points.Min(p=>p.Z));var high=new Vec3(points.Max(p=>p.X),points.Max(p=>p.Y),points.Max(p=>p.Z));target=(low+high)/2;radius=Math.Max(10,(high-low).Length*.55);double aspect=viewport.ActualHeight>0?Math.Max(.1,viewport.ActualWidth/viewport.ActualHeight):1;
   yaw=-1.7;pitch=.25;distance=radius*3;
   if(compact)
   {
    var view=new Vec3(-Math.Cos(pitch)*Math.Cos(yaw),-Math.Cos(pitch)*Math.Sin(yaw),-Math.Sin(pitch));var right=view.Cross(new Vec3(0,0,1)).Normalized();var up=right.Cross(view);double horizontal=Math.Tan(camera.FieldOfView*Math.PI/360)*.9,vertical=horizontal/aspect;
    distance=radius*.15;foreach(var point in points){var delta=point-target;double depth=delta.Dot(view);distance=Math.Max(distance,Math.Max(Math.Abs(delta.Dot(right))/horizontal-depth,Math.Abs(delta.Dot(up))/vertical-depth));}
   }
   UpdateCamera();
  }
  void UpdateCamera()
  {
   var offset=new Vec3(Math.Cos(pitch)*Math.Cos(yaw),Math.Cos(pitch)*Math.Sin(yaw),Math.Sin(pitch))*distance;var p=target+offset;
   camera.Position=new Point3D(p.X,p.Y,p.Z);camera.LookDirection=new Vector3D(-offset.X,-offset.Y,-offset.Z);camera.UpDirection=new Vector3D(0,0,1);camera.NearPlaneDistance=Math.Max(.1,radius*.002);camera.FarPlaneDistance=radius*100;
  }
  public void Dispose(){if(disposed)return;disposed=true;++generation;pending?.Cancel();pending=null;prepared=null;scene=null;lock(cache)cache.Clear();visual.Content=null;sliceVisual.Content=null;sliceVolume=null;}
 }
}
