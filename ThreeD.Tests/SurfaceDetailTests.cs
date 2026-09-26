using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using QuickLook.DicomRT;

internal static class SurfaceDetailTests
{
 static void Check(bool value,string label){if(!value)throw new Exception(label);}
 static Contour Circle(double radius,double z,string type="CLOSED_PLANAR")
 {
  var contour=new Contour{GeometricType=type};
  for(int i=0;i<128;i++){double a=i*Math.PI/64;contour.Points.Add(new Vec3(radius*Math.Cos(a),radius*Math.Sin(a),z));}return contour;
 }
 static StructureRoi Cylinder(double radius,double low,double high,double interval)
 {
  var roi=new StructureRoi{InterpretedType="ORGAN"};for(double z=low;z<=high+.001;z+=interval)roi.Contours.Add(Circle(radius,z));return roi;
 }
 static ThreeDMeshData Build(StructureRoi roi,out string reason)=>ThreeDGeometry.BuildRoiSurface(roi,Matrix4.Identity,CancellationToken.None,out reason);
 static void ValidateMesh(ThreeDMeshData mesh)
 {
  Check(mesh!=null&&mesh.Points.Count>0,"surface exists");Check(mesh.Indices.Count/3<=ThreeDMeshData.MaximumTriangles,"hard triangle bound");Check(mesh.Points.Count==mesh.Normals.Count,"smooth normal per shared vertex");
  for(int i=0;i<mesh.Points.Count;i++){Check(!double.IsNaN(mesh.Points[i].Length)&&!double.IsInfinity(mesh.Points[i].Length),"finite positions");Check(Math.Abs(mesh.Normals[i].Length-1)<1e-6,"unit smooth normals");}
 }
 public static void Run()
 {
  var timer=Stopwatch.StartNew();string reason;
  var cylinder=Cylinder(20,-20,20,2);var before=cylinder.Contours.SelectMany(c=>c.Points).ToArray();var mesh=Build(cylinder,out reason);ValidateMesh(mesh);
  var side=mesh.Points.Select((p,i)=>new{P=p,N=mesh.Normals[i]}).Where(p=>Math.Abs(p.P.Z)<17).ToArray();double error=side.Max(p=>Math.Abs(Math.Sqrt(p.P.X*p.P.X+p.P.Y*p.P.Y)-20));
  Check(error<1.1,"1 mm surface cylinder boundary error");Check(side.All(p=>p.N.Dot(new Vec3(p.P.X,p.P.Y,0))>0),"outward side normals");Check(mesh.Indices.Count/3>10000,"detail no longer collapsed to an 8-24 sample mesh");
  var edges=new Dictionary<long,int>();for(int i=0;i<mesh.Indices.Count;i+=3)for(int j=0;j<3;j++){int a=mesh.Indices[i+j],b=mesh.Indices[i+(j+1)%3];long key=((long)Math.Min(a,b)<<32)|(uint)Math.Max(a,b);if(!edges.ContainsKey(key))edges[key]=0;edges[key]++;}Check(edges.Values.All(n=>n==2),"fine surface is closed without triangle cracks");
  var after=cylinder.Contours.SelectMany(c=>c.Points).ToArray();for(int i=0;i<before.Length;i++)Check((before[i]-after[i]).Length==0,"source contours unchanged");
  var transform=new Matrix4(new double[]{0,-1,0,100,1,0,0,50,0,0,1,40,0,0,0,1});var moved=ThreeDGeometry.BuildRoiSurface(cylinder,transform,CancellationToken.None,out reason);ValidateMesh(moved);Check(Math.Abs((moved.Points.Min(p=>p.X)+moved.Points.Max(p=>p.X))*.5-100)<.5&&Math.Abs((moved.Points.Min(p=>p.Z)+moved.Points.Max(p=>p.Z))*.5-40)<.5,"registered contour surface preserves physical frame");
  var sphere=new StructureRoi();for(int z=-28;z<=28;z+=2)sphere.Contours.Add(Circle(Math.Sqrt(900-z*z),z));var curved=Build(sphere,out reason);ValidateMesh(curved);Check(curved.Points.Where(p=>Math.Abs(p.Z)<26).All(p=>Math.Abs(p.Length-30)<1.1),"distance interpolation retains curved contour boundaries");
  var longThin=Build(Cylinder(2,-300,300,5),out reason);ValidateMesh(longThin);Check(longThin.Points.Max(p=>p.Z)-longThin.Points.Min(p=>p.Z)>599,"long thin anatomy full extent");
  Check(longThin.Points.Where(p=>Math.Abs(p.Z)<290).All(p=>Math.Abs(Math.Sqrt(p.X*p.X+p.Y*p.Y)-2)<1),"independent axis spacing preserves thin anatomy");Check(reason.Contains("256 samples"),"anisotropic extent bound reported");
  var bounded=Build(Cylinder(100,-100,100,5),out reason);ValidateMesh(bounded);Check(reason.Contains("bounded triangle budget"),"large surface uses explicit fine-spacing budgeting");Check(bounded.Points.Max(p=>p.Z)-bounded.Points.Min(p=>p.Z)>198,"budget retry retains entire object extent");
  var hollow=Cylinder(15,-10,10,2);foreach(var c in hollow.Contours)c.GeometricType="CLOSEDPLANAR_XOR";for(int z=-10;z<=10;z+=2)hollow.Contours.Add(Circle(6,z,"CLOSEDPLANAR_XOR"));var hole=Build(hollow,out reason);ValidateMesh(hole);
  var inner=hole.Points.Select((p,i)=>new{P=p,N=hole.Normals[i]}).Where(p=>Math.Abs(p.P.Z)<7&&p.P.X*p.P.X+p.P.Y*p.P.Y<100).ToArray();Check(inner.Length>0,"XOR hole surface exists");Check(inner.All(p=>Math.Sqrt(p.P.X*p.P.X+p.P.Y*p.P.Y)>4.8),"XOR hole remains open");Check(inner.All(p=>p.N.Dot(new Vec3(p.P.X,p.P.Y,0))<0),"XOR cavity normals point into hole");
  var separated=Cylinder(5,0,4,2);separated.Contours.Add(Circle(5,12));separated.Contours.Add(Circle(5,14));var gap=Build(separated,out reason);ValidateMesh(gap);Check(!gap.Points.Any(p=>p.Z>6&&p.Z<10),"absent contour interval stays empty");
  for(int i=0;i<gap.Indices.Count;i+=3){double low=double.MaxValue,high=double.MinValue;for(int j=0;j<3;j++){double z=gap.Points[gap.Indices[i+j]].Z;low=Math.Min(low,z);high=Math.Max(high,z);}Check(!(low<6&&high>10),"no triangle bridges contour gap");}
  var variableInterval=new StructureRoi();foreach(double z in new[]{0,2,4.2,6.2,8.2})variableInterval.Contours.Add(Circle(4,z));var continuous=ThreeDGeometry.VoxelizeRoi(variableInterval,Matrix4.Identity,64,CancellationToken.None,out reason,true);Check(continuous.Sample(new Vec3(0,0,3.1))>.5,"minor contour interval variation does not create false empty planes");
  var single=Cylinder(3,0,0,2);Check(Build(single,out reason)==null&&reason.Contains("Single"),"single plane has explicit line fallback");
  var open=Cylinder(3,0,4,2);open.Contours[0].GeometricType="OPEN_PLANAR";Check(Build(open,out reason)==null&&reason.Contains("Open"),"open contour fallback explained");
  var mixed=Cylinder(3,0,4,2);mixed.Contours[0].GeometricType="CLOSEDPLANAR_XOR";Check(Build(mixed,out reason)==null&&reason.Contains("Mixed"),"mixed XOR fallback explained");
  var invalid=Cylinder(3,0,4,2);invalid.Contours[0].Points[0]=new Vec3(double.NaN,0,0);Check(Build(invalid,out reason)==null&&reason.Contains("Nonfinite"),"nonfinite geometry fallback");
  bool canceled=false;try{ThreeDGeometry.BuildRoiSurface(cylinder,Matrix4.Identity,new CancellationToken(true),out reason);}catch(OperationCanceledException){canceled=true;}Check(canceled,"ROI cancellation propagated");
  var volume=new VolumeData{Width=129,Height=129,Depth=9,Origin=new Vec3(-64,-64,-4),AxisX=new Vec3(1,0,0),AxisY=new Vec3(0,1,0),AxisZ=new Vec3(0,0,1),SpacingX=1,SpacingY=1,SpacingZ=1,Values=new float[129*129*9]};
  for(int z=0;z<9;z++)for(int y=0;y<129;y++)for(int x=0;x<129;x++)volume.Values[(z*129+y)*129+x]=x-64;
  var exact=ThreeDGeometry.Isosurface(volume,0,192,CancellationToken.None);Check(exact.Points.All(p=>Math.Abs(p.X)<1e-7),"native CT scalar interpolation");Check(exact.Points.Select(p=>p.Y).Distinct().Count()>=129,"CT retains greater than 80 samples");
  Check(volume.Values[0]==-64&&volume.Values[128]==64,"native extraction does not mutate scalar payload");
  using(var cancel=new CancellationTokenSource()){int calls=0;canceled=false;try{ThreeDGeometry.Isosurface(volume,p=>{if(++calls==100)cancel.Cancel();return volume.Sample(p);},0,192,cancel.Token);}catch(OperationCanceledException){canceled=true;}Check(canceled,"in-flight extraction cancels without returning a partial mesh");}
  Console.WriteLine("PASS fine surface: boundary_error_mm="+error.ToString("0.000",System.Globalization.CultureInfo.InvariantCulture)+" cylinder_triangles="+mesh.Indices.Count/3+" thin_triangles="+longThin.Indices.Count/3+" elapsed_ms="+timer.ElapsedMilliseconds);
 }
 // This path emits aggregate counts/timings only; never labels, identifiers or anatomy.
 public static int Benchmark(string folder)
 {
  try
  {
   var catalog=DicomCatalog.Scan(Directory.EnumerateFiles(folder).First(),CancellationToken.None);var stack=catalog.Stacks.First(s=>s.Modality=="CT"&&s.CanMpr);var links=RegistrationReader.Read(catalog);var timer=Stopwatch.StartNew();int count=0,fallbacks=0,retries=0,estimated=0;long triangles=0,worst=0;var reasons=new Dictionary<string,int>();
   foreach(var entry in catalog.Files.Where(e=>e.Modality=="RTSTRUCT"))foreach(var roi in StructureSet.Load(entry).Rois)
   {
    if(!ThreeDGeometry.DefaultRoi(roi))continue;var transform=RegistrationReader.Resolve(links,roi.FrameUid,stack.FrameUid);if(transform==null)continue;
    var watch=Stopwatch.StartNew();string reason;var mesh=ThreeDGeometry.BuildRoiSurface(roi,transform,CancellationToken.None,out reason);worst=Math.Max(worst,watch.ElapsedMilliseconds);count++;if(mesh==null){fallbacks++;if(!reasons.ContainsKey(reason))reasons[reason]=0;reasons[reason]++;}else{triangles+=mesh.Indices.Count/3;if(reason.Contains("retry"))retries++;if(reason.Contains("estimated start"))estimated++;}
   }
   Console.WriteLine("Fine ROI aggregate: objects="+count+" fallbacks="+fallbacks+" fine_budget_retries="+retries+" estimated_budget_starts="+estimated+" triangles="+triangles+" total_ms="+timer.ElapsedMilliseconds+" slowest_ms="+worst);foreach(var pair in reasons)Console.WriteLine("Fallback category: "+pair.Key+" count="+pair.Value);return 0;
  }
  catch(Exception e){Console.WriteLine("Surface benchmark failed: "+e.GetType().Name);return 1;}
 }
}
