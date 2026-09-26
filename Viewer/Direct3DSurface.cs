using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using H = HelixToolkit.Wpf.SharpDX;
using D = SharpDX;

namespace QuickLook.DicomRT
{
 // GPU buffers survive camera motion. Transparency is resolved per pixel rather
 // than by the centre of an enclosing skin/organ mesh.
 internal sealed class Direct3DSurface : IDisposable
 {
  internal readonly H.Viewport3DX View;
  readonly H.DefaultEffectsManager effects;
  readonly H.PerspectiveCamera camera=new H.PerspectiveCamera();
  PerspectiveCamera sourceCamera;
  readonly H.GroupModel3D surfaces=new H.GroupModel3D(), guides=new H.GroupModel3D();
  readonly Dictionary<MeshGeometry3D,H.MeshGeometry3D> meshes=new Dictionary<MeshGeometry3D,H.MeshGeometry3D>();
  readonly Dictionary<MeshGeometry3D,H.MeshGeometryModel3D> nodes=new Dictionary<MeshGeometry3D,H.MeshGeometryModel3D>();
  internal Direct3DSurface()
  {
   effects=new H.DefaultEffectsManager();
   try{View=new H.Viewport3DX{EffectsManager=effects,Camera=camera,BackgroundColor=Color.FromRgb(16,19,20),IsHitTestVisible=false,
    ShowViewCube=false,ShowCoordinateSystem=false,IsInertiaEnabled=false,EnableMouseButtonHitTest=false,EnableD2DRendering=false,
    OITRenderMode=H.OITRenderType.DepthPeeling,FXAALevel=H.FXAALevel.Medium};}catch{effects.Dispose();throw;}
   View.SizeChanged+=(s,e)=>{if(sourceCamera!=null)SetCamera(sourceCamera);};
   View.Items.Add(new H.AmbientLight3D{Color=Color.FromRgb(110,110,110)});
   View.Items.Add(new H.DirectionalLight3D{Color=Colors.White,Direction=new Vector3D(-.8,-.5,-1)});
   View.Items.Add(new H.DirectionalLight3D{Color=Color.FromRgb(110,120,140),Direction=new Vector3D(.5,1,.3)});
   View.Items.Add(surfaces);View.Items.Add(guides);
  }
  internal void SetCamera(PerspectiveCamera source)
  {
   sourceCamera=source;
   camera.Position=source.Position;camera.LookDirection=source.LookDirection;camera.UpDirection=source.UpDirection;
   // WPF specifies a horizontal FOV; Helix's Direct3D camera uses a vertical FOV.
   double aspect=View.ActualHeight>0?Math.Max(.01,View.ActualWidth/View.ActualHeight):1;
   camera.FieldOfView=2*Math.Atan(Math.Tan(source.FieldOfView*Math.PI/360)/aspect)*180/Math.PI;
   camera.NearPlaneDistance=source.NearPlaneDistance;camera.FarPlaneDistance=source.FarPlaneDistance;
  }
  internal void SetSurfaces(Model3DGroup source)
  {
   var retained=new HashSet<MeshGeometry3D>();
   if(source!=null)foreach(var model in source.Children.OfType<GeometryModel3D>())
   {
    var mesh=model.Geometry as MeshGeometry3D;if(mesh==null)continue;retained.Add(mesh);
    H.MeshGeometry3D geometry;if(!meshes.TryGetValue(mesh,out geometry))meshes[mesh]=geometry=ConvertMesh(mesh);
    H.MeshGeometryModel3D node;var appearance=ConvertModel(model,geometry);
    if(!nodes.TryGetValue(mesh,out node)){nodes[mesh]=node=appearance;}else{node.Material=appearance.Material;node.IsTransparent=appearance.IsTransparent;node.CullMode=appearance.CullMode;}
    if(!surfaces.Children.Contains(node))surfaces.Children.Add(node);
   }
   foreach(var pair in nodes.Where(k=>!retained.Contains(k.Key)).ToArray())surfaces.Children.Remove(pair.Value);
   // Retain detached nodes for quick type toggles, bounded independently of the CPU cache.
   if(nodes.Count>96||meshes.Keys.Sum(m=>(long)m.TriangleIndices.Count)>24000000)foreach(var key in nodes.Keys.Where(k=>!retained.Contains(k)).ToArray()){nodes.Remove(key);meshes.Remove(key);}
  }
  internal void SetGuides(Model3DGroup source)
  {
   guides.Children.Clear();if(source!=null)foreach(var model in source.Children.OfType<GeometryModel3D>())
   {var mesh=model.Geometry as MeshGeometry3D;if(mesh!=null)guides.Children.Add(ConvertModel(model,ConvertMesh(mesh)));}
  }
  static H.MeshGeometry3D ConvertMesh(MeshGeometry3D mesh)=>new H.MeshGeometry3D{
   Positions=new H.Vector3Collection(mesh.Positions.Select(p=>new D.Vector3((float)p.X,(float)p.Y,(float)p.Z))),
   Indices=new H.IntCollection(mesh.TriangleIndices),Normals=new H.Vector3Collection(mesh.Normals.Select(n=>new D.Vector3((float)n.X,(float)n.Y,(float)n.Z)))};
  static H.MeshGeometryModel3D ConvertModel(GeometryModel3D model,H.MeshGeometry3D geometry)
  {
   var materials=model.Material is MaterialGroup group?group.Children.ToArray():new[]{model.Material};
   var diffuse=materials.OfType<DiffuseMaterial>().Select(m=>m.Brush).OfType<SolidColorBrush>().FirstOrDefault();
   var glow=materials.OfType<EmissiveMaterial>().Select(m=>m.Brush).OfType<SolidColorBrush>().FirstOrDefault();
   var brush=diffuse??glow;var color=brush?.Color??Colors.White;
   float alpha=(float)((brush?.Opacity??1)*color.A/255d);
   var material=new H.PhongMaterial{DiffuseColor=new D.Color4(color.R/255f,color.G/255f,color.B/255f,alpha),
    AmbientColor=new D.Color4(.35f,.35f,.35f,1),SpecularColor=new D.Color4(.12f,.12f,.12f,1),SpecularShininess=28,
    EmissiveColor=glow==null?new D.Color4(0,0,0,0):new D.Color4(glow.Color.R/255f*.3f,glow.Color.G/255f*.3f,glow.Color.B/255f*.3f,0)};
   if(diffuse==null&&glow!=null){material.DiffuseColor=new D.Color4(0,0,0,alpha);material.SpecularColor=new D.Color4(0,0,0,0);material.EmissiveColor=new D.Color4(color.R/255f,color.G/255f,color.B/255f,0);}
   return new H.MeshGeometryModel3D{Geometry=geometry,Material=material,IsTransparent=alpha<.999f,
    CullMode=model.BackMaterial==null?D.Direct3D11.CullMode.Back:D.Direct3D11.CullMode.None,IsHitTestVisible=false};
  }
  internal BitmapSource Capture()=>H.ViewportExtensions.RenderBitmap(View);
  public void Dispose(){View.Items.Clear();View.Dispose();effects.Dispose();meshes.Clear();nodes.Clear();}
 }
}
