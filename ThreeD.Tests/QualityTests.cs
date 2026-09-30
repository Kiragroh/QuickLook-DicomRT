using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Media.Media3D;
using QuickLook.DicomRT;
internal static class QualityTests
{
 static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
 static readonly BindingFlags PrivateStatic=BindingFlags.Static|BindingFlags.NonPublic;
 public static StructureRoi Sphere()
 {
  var roi=new StructureRoi{InterpretedType="ORGAN",Name="Synthetic",Red=150,Green=200,Blue=255};
  for(int z=-8;z<=8;z+=2){var contour=new Contour{GeometricType="CLOSED_PLANAR"};double radius=Math.Sqrt(100-z*z);for(int n=0;n<80;n++){double a=n*Math.PI/40;contour.Points.Add(new Vec3(radius*Math.Cos(a),radius*Math.Sin(a),z));}roi.Contours.Add(contour);}return roi;
 }
 public static void Run()
 {
  var roi=Sphere();Check(!ThreeDGeometry.DefaultRoi(roi),"ORGAN off by default");roi.InterpretedType="PTV";roi.Name="No hint";Check(ThreeDGeometry.DefaultRoi(roi),"PTV metadata default");roi.InterpretedType="EXTERNAL";roi.Name="PTV Brainstem Organ";Check(!ThreeDGeometry.DefaultRoi(roi),"EXTERNAL never selected by name");roi.InterpretedType=null;Check(ThreeDGeometry.DefaultRoi(roi),"PTV name fallback selected without type");roi.InterpretedType="ORGAN";
  string reason;var v=ThreeDGeometry.VoxelizeRoi(roi,Matrix4.Identity,40,CancellationToken.None,out reason);var raw=ThreeDGeometry.Isosurface(v,v.Sample,.5,40,CancellationToken.None);var welded=ThreeDGeometry.Smooth(raw,0,CancellationToken.None);var smooth=ThreeDGeometry.Smooth(raw,1,CancellationToken.None);
  Check(smooth.Points.Count<raw.Points.Count/2,"shared surface vertices");Check(smooth.Normals.Count==smooth.Points.Count,"explicit smooth normals");Check(smooth.Indices.Count<=raw.Indices.Count,"smoothing retains triangle budget");double displacement=0;
  for(int i=0;i<smooth.Points.Count;i++){double d=(smooth.Points[i]-welded.Points[i]).Length;displacement=Math.Max(displacement,d);Check(d<=1.000001,"bounded smoothing displacement");Check(Math.Abs(smooth.Normals[i].Length-1)<1e-6,"finite unit normals");}
  Check(displacement>.02,"geometry actually smoothed");
  var edges=new Dictionary<long,int>();var directed=new Dictionary<long,int>();for(int i=0;i<smooth.Indices.Count;i+=3)for(int j=0;j<3;j++){int a=smooth.Indices[i+j],b=smooth.Indices[i+(j+1)%3];long key=((long)Math.Min(a,b)<<32)|(uint)Math.Max(a,b);if(!edges.ContainsKey(key)){edges[key]=0;directed[key]=0;}edges[key]++;directed[key]+=a<b?1:-1;}Check(edges.Values.All(n=>n==2)&&directed.Values.All(n=>n==0),"closed manifold without winding cracks");
  double roughness(ThreeDMeshData mesh){double sum=0;for(int i=0;i<mesh.Indices.Count;i+=3){int a=mesh.Indices[i],b=mesh.Indices[i+1],c=mesh.Indices[i+2];sum+=(mesh.Normals[a]-mesh.Normals[b]).Length+(mesh.Normals[b]-mesh.Normals[c]).Length+(mesh.Normals[c]-mesh.Normals[a]).Length;}return sum/mesh.Indices.Count;}
  for(int i=0;i<smooth.Indices.Count;i+=3){int a=smooth.Indices[i],b=smooth.Indices[i+1],c=smooth.Indices[i+2];var original=(welded.Points[b]-welded.Points[a]).Cross(welded.Points[c]-welded.Points[a]);var normal=(smooth.Points[b]-smooth.Points[a]).Cross(smooth.Points[c]-smooth.Points[a]);Check(original.Length<1e-10||normal.Dot(original)>0,"smoothing preserves face orientation");}
  var field=ThreeDGeometry.VoxelizeRoi(roi,Matrix4.Identity,40,CancellationToken.None,out reason,true);Check(field.Sample(new Vec3(0,0,0))>.5,"display field retains interior");Check(field.Sample(new Vec3(10,10,0))<.5,"display field excludes exterior");
  Check(roughness(smooth)<roughness(welded),"lower normal variation after geometry smoothing");
  roi.InterpretedType="PTV";var scene=new RenderScene{Structures=new List<RoiOverlay>{new RoiOverlay{Roi=roi},new RoiOverlay{Roi=new StructureRoi{InterpretedType="EXTERNAL",Contours=roi.Contours}}},Isocenters=new[]{new Vec3(1,2,3)}};
  using(var control=new ThreeDControl())
  {
   var cache=typeof(ThreeDControl).GetField("cache",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(control);var prepare=typeof(ThreeDControl).GetMethod("PrepareCore",PrivateStatic);object[] args={scene,false,.5,CancellationToken.None,cache,false,false,false,false};var first=prepare.Invoke(null,args);var second=prepare.Invoke(null,args);
   var parts=((IEnumerable)second.GetType().GetField("Parts").GetValue(second)).Cast<object>().ToArray();Check(parts.Length==2,"ROI and isocenter without CT; EXTERNAL excluded");Check((int)second.GetType().GetField("CacheHits").GetValue(second)==1,"ROI reused from bounded cache");
   var firstRoi=((IEnumerable)first.GetType().GetField("Parts").GetValue(first)).Cast<object>().First();Check(ReferenceEquals(firstRoi,parts[0]),"cached frozen WPF geometry reused");
   var cross=(MeshGeometry3D)parts[1].GetType().GetField("Mesh").GetValue(parts[1]);Check(Math.Abs(cross.Positions.Average(p=>p.X)-1)<1e-6&&Math.Abs(cross.Positions.Average(p=>p.Y)-2)<1e-6&&Math.Abs(cross.Positions.Average(p=>p.Z)-3)<1e-6,"isocenter in scene coordinates");
   var dose=(DoseGrid)typeof(DoseGrid).GetMethod("FromDerivedVolume",PrivateStatic).Invoke(null,new object[]{v,"synthetic","synthetic"});var doseScene=new RenderScene{Doses=new List<DoseOverlay>{new DoseOverlay{Dose=dose}}};control.SetScene(doseScene);var checkbox=(System.Windows.Controls.CheckBox)typeof(ThreeDControl).GetField("dose",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(control);Check(checkbox.IsChecked==true,"dose-only scene enables initial dose");checkbox.IsChecked=false;control.SetScene(doseScene);Check(checkbox.IsChecked==false,"manual dose-off persists");
   var doseOnly=prepare.Invoke(null,new object[]{doseScene,false,.5,CancellationToken.None,cache,false,false,true,false});Check(((IEnumerable)doseOnly.GetType().GetField("Parts").GetValue(doseOnly)).Cast<object>().Count()==1,"dose mesh requires no image volume");
   args[8]=true;var all=prepare.Invoke(null,args);Check(((IEnumerable)all.GetType().GetField("Parts").GetValue(all)).Cast<object>().Count()==2,"EXTERNAL excluded even with all types enabled");
   scene.Structures.Add(new RoiOverlay{Roi=new StructureRoi{InterpretedType="CTV",Contours=roi.Contours}});all=prepare.Invoke(null,args);Check(((IEnumerable)all.GetType().GetField("Parts").GetValue(all)).Cast<object>().Count()==3,"other non-external types opt in");
  }
  Console.WriteLine("PASS smooth geometry, normals, deformation bound, metadata defaults, no-CT scene, cache and isocenter checks");
 }
}
