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
  sealed class Part {public MeshGeometry3D Mesh;public string Kind;public Color Color;public Vec3 Center;}
  sealed class Prepared {public List<Part> Parts=new List<Part>();public int Fallbacks,Skipped,Reduced;}
  readonly Viewport3D viewport=new Viewport3D();readonly ModelVisual3D visual=new ModelVisual3D();readonly PerspectiveCamera camera=new PerspectiveCamera();
  readonly CheckBox bone,skin,structures,dose;readonly Slider opacity;readonly ComboBox doseLevel;readonly TextBlock status;
  RenderScene scene;Prepared prepared;CancellationTokenSource pending;int generation;bool disposed;string key;double yaw=-1.7,pitch=.25,distance=500,radius=250;Vec3 target;Point mouse;bool dragging;
  public ThreeDControl()
  {
   Background=Theme.Background;var root=new Grid();root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
   var controls=new WrapPanel{Margin=new Thickness(8,4,8,4)};
   bone=Toggle("Bone ≥300 HU",true);skin=Toggle("Skin ≥−350 HU",false);structures=Toggle("Targets / OARs",true);dose=Toggle("Dose",false);
   controls.Children.Add(bone);controls.Children.Add(skin);controls.Children.Add(structures);controls.Children.Add(dose);
   controls.Children.Add(Theme.Text("Opacity",11));opacity=new Slider{Minimum=.1,Maximum=.8,Value=.35,Width=95,Margin=new Thickness(6),ToolTip="Opacity of ROI and skin surfaces"};controls.Children.Add(opacity);opacity.ValueChanged+=(s,e)=>ApplyModels();
   doseLevel=new ComboBox{Width=100,Margin=new Thickness(5),ItemsSource=new[]{"20 % max.","50 % max.","80 % max.","95 % max."},SelectedIndex=1,ToolTip="Isodose relative to each maximum; no automatic summation"};controls.Children.Add(doseLevel);doseLevel.SelectionChanged+=(s,e)=>{key=null;StartBuild();};
   var reset=Theme.Button("Reset view");reset.Click+=(s,e)=>ResetCamera();controls.Children.Add(reset);
   root.Children.Add(controls);
   var host=new Border{Background=Theme.Background,Child=viewport,ClipToBounds=true};Grid.SetRow(host,1);root.Children.Add(host);
   viewport.Camera=camera;viewport.Children.Add(visual);camera.FieldOfView=42;
   host.MouseLeftButtonDown+=(s,e)=>{dragging=true;mouse=e.GetPosition(host);host.CaptureMouse();e.Handled=true;};
   host.MouseLeftButtonUp+=(s,e)=>{dragging=false;host.ReleaseMouseCapture();e.Handled=true;};host.LostMouseCapture+=(s,e)=>dragging=false;
   host.MouseMove+=(s,e)=>{if(!dragging)return;var p=e.GetPosition(host);yaw+=(p.X-mouse.X)*.01;pitch=Math.Max(-1.45,Math.Min(1.45,pitch+(p.Y-mouse.Y)*.01));mouse=p;UpdateCamera();e.Handled=true;};
   host.MouseWheel+=(s,e)=>{distance=Math.Max(radius*.15,Math.Min(radius*20,distance*Math.Pow(1.15,-e.Delta/120.0)));UpdateCamera();e.Handled=true;};
   var footer=new StackPanel{Margin=new Thickness(10,4,10,7)};status=Theme.Text("3D: select an image series",11);footer.Children.Add(status);
   footer.Children.Add(Theme.Text("Drag to rotate · mouse wheel to zoom · LPS patient coordinates. Skin/bone surfaces use CT thresholds; ROI surfaces use voxelized contours. Transparency is approximate; 3D provides an overview.",10,Theme.Muted));Grid.SetRow(footer,2);root.Children.Add(footer);Content=root;
   IsVisibleChanged+=(s,e)=>{if(IsVisible)StartBuild();else if(pending!=null){++generation;pending.Cancel();pending=null;key=null;}};
  }
  CheckBox Toggle(string label,bool initial)
  {
   var box=new CheckBox{Content=label,IsChecked=initial,Foreground=Theme.Foreground,Margin=new Thickness(5,7,12,7),VerticalAlignment=VerticalAlignment.Center};box.Checked+=(s,e)=>ApplyModels();box.Unchecked+=(s,e)=>ApplyModels();return box;
  }
  public void SetScene(RenderScene value)
  {
   if(disposed)return;bool changed=scene?.Volume!=value?.Volume;scene=value;
   if(changed){key=null;prepared=null;if(scene?.Volume!=null)ResetCamera();}
   if(scene?.Volume==null){pending?.Cancel();++generation;prepared=null;key=null;ApplyModels();status.Text="A spatial image volume is required for the 3D view.";return;}
   if(IsVisible)StartBuild();
  }
  static string SceneKey(RenderScene s,double level)
  {
   string matrices(IEnumerable<double> values)=>string.Join(",",values.Select(x=>x.ToString("R",CultureInfo.InvariantCulture)));
   var parts=new List<string>{RuntimeHelpers.GetHashCode(s.Volume).ToString(),s.Entry?.Modality??"",level.ToString("R",CultureInfo.InvariantCulture)};
   foreach(var r in s.Structures??new List<RoiOverlay>())if(r?.Roi!=null&&r.Roi.Visible)parts.Add("R"+RuntimeHelpers.GetHashCode(r.Roi)+":"+matrices(r.RoiToImage.Values));
   foreach(var d in s.Doses??new List<DoseOverlay>())if(d?.Dose!=null&&d.Dose.Visible)parts.Add("D"+RuntimeHelpers.GetHashCode(d.Dose)+":"+matrices(d.ImageToDose.Values));
   return string.Join("|",parts);
  }
  async void StartBuild()
  {
   if(disposed||!IsVisible||scene?.Volume==null)return;
   double level=new[]{.2,.5,.8,.95}[Math.Max(0,doseLevel.SelectedIndex)];string nextKey=SceneKey(scene,level);if(key==nextKey)return;key=nextKey;
   var copy=scene.Snapshot();int mine=++generation;pending?.Cancel();var cancel=new CancellationTokenSource();pending=cancel;
   bool ct=copy.Entry?.Modality=="CT";bone.IsEnabled=skin.IsEnabled=ct;bone.ToolTip=skin.ToolTip=ct?"Surface derived from a CT threshold; not an anatomical segmentation":"Bone/skin thresholds require CT values in HU";
   status.Text="Building 3D surfaces in the background …";prepared=null;ApplyModels();
   try
   {
    var next=await Task.Run(()=>Prepare(copy,ct,level,cancel.Token),cancel.Token);
    if(disposed||mine!=generation)return;prepared=next;ApplyModels();
    int triangles=next.Parts.Sum(x=>x.Mesh.TriangleIndices.Count/3);
    status.Text=next.Parts.Count(p=>p.Kind=="Roi")+" selected ROI surfaces · "+string.Format(CultureInfo.InvariantCulture,"{0} surfaces · {1:N0} triangles · bounded ROI resolution{2}{3}",next.Parts.Count,triangles,next.Fallbacks>0?" · "+next.Fallbacks+" ROIs shown as contour lines only":"",next.Skipped>0?" · "+next.Skipped+" objects unavailable":next.Reduced>0?" · detail level adjusted automatically":"");
   }
   catch(OperationCanceledException){}
   catch(Exception){if(!disposed&&mine==generation){key=null;status.Text="3D display unavailable; select another image series.";}}
   finally{if(pending==cancel)pending=null;cancel.Dispose();}
  }
  const int SceneTriangleLimit=600000;
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
  static Prepared Prepare(RenderScene s,bool ct,double level,CancellationToken token)
  {
   var result=new Prepared();int totalTriangles=0;
   var rois=(s.Structures??new List<RoiOverlay>()).Where(x=>x?.Roi!=null&&x.Roi.Visible).Take(64).ToArray();
   var doses=(s.Doses??new List<DoseOverlay>()).Where(x=>x?.Dose!=null&&x.Dose.Visible&&x.Dose.Maximum>0).Take(8).ToArray();
   int contextBudget=ct?140000:0,doseTotalBudget=doses.Length==0?0:Math.Min(120000,100000*doses.Length);
   int roiBudget=Math.Min(60000,Math.Max(3000,(SceneTriangleLimit-contextBudget-doseTotalBudget)/Math.Max(1,rois.Length)));
   Action<ThreeDMeshData,string,Color> add=(data,kind,color)=>
   {
    if(data==null||data.Indices.Count==0){result.Skipped++;return;}
    token.ThrowIfCancellationRequested();if(result.Parts.Count>=80||totalTriangles+data.Indices.Count/3>SceneTriangleLimit){result.Skipped++;return;}
    totalTriangles+=data.Indices.Count/3;var geometry=new MeshGeometry3D();var center=new Vec3();
    for(int i=0;i<data.Points.Count;i++){if((i&4095)==0)token.ThrowIfCancellationRequested();var p=data.Points[i];geometry.Positions.Add(new Point3D(p.X,p.Y,p.Z));center+=p;}
    foreach(var i in data.Indices)geometry.TriangleIndices.Add(i);geometry.Freeze();
    result.Parts.Add(new Part{Mesh=geometry,Kind=kind,Color=color,Center=center/data.Points.Count});
   };
   // Reserve independent fair shares for CT context, dose and every ROI. Earlier large
   // objects cannot consume later objects' budget; resolution adapts before publication.
   if(ct)foreach(double threshold in new[]{300d,-350d})
   {
    bool reduced;var mesh=AdaptiveMesh(s.Volume,s.Volume.Sample,threshold,contextBudget/2,token,out reduced);if(reduced)result.Reduced++;
    add(mesh,threshold>0?"Bone":"Skin",threshold>0?Color.FromRgb(236,229,211):Color.FromRgb(205,172,151));
   }
   result.Skipped+=Math.Max(0,(s.Doses?.Count??0)-8);
   foreach(var overlay in doses)
   {
    token.ThrowIfCancellationRequested();bool reduced;ThreeDMeshData mesh;
    if(overlay.Dose.Volume!=null)
    {
     mesh=AdaptiveMesh(overlay.Dose.Volume,overlay.Dose.Sample,overlay.Dose.Maximum*level,doseTotalBudget/Math.Max(1,doses.Length),token,out reduced);
     var inverse=overlay.ImageToDose.Inverse();for(int i=0;i<mesh.Points.Count;i++){if((i&1023)==0)token.ThrowIfCancellationRequested();mesh.Points[i]=inverse.Transform(mesh.Points[i]);}
    }
    else mesh=AdaptiveMesh(s.Volume,p=>overlay.Dose.Sample(overlay.ImageToDose.Transform(p)),overlay.Dose.Maximum*level,doseTotalBudget/Math.Max(1,doses.Length),token,out reduced);
    if(reduced)result.Reduced++;add(mesh,"Dose",Color.FromRgb(255,150,65));
   }
   result.Skipped+=Math.Max(0,(s.Structures?.Count??0)-64);
   foreach(var overlay in rois)
   {
    token.ThrowIfCancellationRequested();
    try
    {
     ThreeDMeshData selected=null;bool fallback=false,reduced=false;
     foreach(int resolution in new[]{24,20,16,12,8})
     {
      string reason;var volume=ThreeDGeometry.VoxelizeRoi(overlay.Roi,overlay.RoiToImage,resolution,token,out reason);
      if(volume==null){fallback=true;break;}
      ThreeDMeshData mesh;
      try{mesh=ThreeDGeometry.Isosurface(volume,volume.Sample,.5,resolution,token);}catch(InvalidOperationException){reduced=true;continue;}
      if(mesh.Indices.Count==0)continue;selected=mesh;if(mesh.Indices.Count/3<=roiBudget)break;reduced=true;
     }
     if(fallback||selected==null){selected=ThreeDGeometry.ContourLines(overlay.Roi,overlay.RoiToImage,token);result.Fallbacks++;}
     if(reduced)result.Reduced++;add(selected,"Roi",Color.FromRgb(overlay.Roi.Red,overlay.Roi.Green,overlay.Roi.Blue));
    }
    catch(InvalidOperationException){result.Skipped++;}
   }
   return result;
  }
  void ApplyModels()
  {
   if(disposed||visual==null)return;var group=new Model3DGroup();group.Children.Add(new AmbientLight(Color.FromRgb(105,105,105)));
   group.Children.Add(new DirectionalLight(Colors.White,new Vector3D(-.8,-.5,-1)));group.Children.Add(new DirectionalLight(Color.FromRgb(100,115,130),new Vector3D(.5,1,.3)));
   if(prepared!=null)
   {
    var visible=prepared.Parts.Where(p=>p.Kind=="Bone"?bone.IsChecked==true&&bone.IsEnabled:p.Kind=="Skin"?skin.IsChecked==true&&skin.IsEnabled:p.Kind=="Roi"?structures.IsChecked==true:dose.IsChecked==true);
    var eye=new Vec3(camera.Position.X,camera.Position.Y,camera.Position.Z);
    foreach(var part in visible.OrderBy(p=>p.Kind=="Bone"?0:1).ThenByDescending(p=>(p.Center-eye).Length))
    {
     double alpha=part.Kind=="Bone"?1:part.Kind=="Dose"?.25:opacity?.Value??.35;
     var brush=new SolidColorBrush(part.Color){Opacity=alpha};brush.Freeze();var material=new DiffuseMaterial(brush);material.Freeze();
     group.Children.Add(new GeometryModel3D(part.Mesh,material){BackMaterial=material});
    }
   }
   visual.Content=group;
  }
  void ResetCamera()
  {
   if(scene?.Volume==null)return;var v=scene.Volume;target=v.Center;radius=Math.Max(10,(v.WorldAt(v.Width-1,v.Height-1,v.Depth-1)-v.Origin).Length*.55);distance=radius*3;yaw=-1.7;pitch=.25;UpdateCamera();
  }
  void UpdateCamera()
  {
   var offset=new Vec3(Math.Cos(pitch)*Math.Cos(yaw),Math.Cos(pitch)*Math.Sin(yaw),Math.Sin(pitch))*distance;var p=target+offset;
   camera.Position=new Point3D(p.X,p.Y,p.Z);camera.LookDirection=new Vector3D(-offset.X,-offset.Y,-offset.Z);camera.UpDirection=new Vector3D(0,0,1);camera.NearPlaneDistance=Math.Max(.1,radius*.002);camera.FarPlaneDistance=radius*100;ApplyModels();
  }
  public void Dispose(){if(disposed)return;disposed=true;++generation;pending?.Cancel();pending=null;prepared=null;scene=null;visual.Content=null;}
 }
}
