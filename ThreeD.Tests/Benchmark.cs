using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using QuickLook.DicomRT;
internal static class Benchmark
{
 public static int Run(string folder)
 {
  try
  {
   var cat=DicomCatalog.Scan(Directory.EnumerateFiles(folder).First(),CancellationToken.None);var stack=cat.Stacks.First(s=>s.Modality=="CT"&&s.CanMpr);var v=VolumeData.Load(stack,CancellationToken.None);var links=RegistrationReader.Read(cat);var clock=Stopwatch.StartNew();int total=0;
   foreach(double threshold in new[]{300d,-350d}){var sw=Stopwatch.StartNew();var m=ThreeDGeometry.Isosurface(v,v.Sample,threshold,56,CancellationToken.None);sw.Stop();total+=m.Indices.Count/3;Console.WriteLine("CT threshold="+threshold+" triangles="+m.Indices.Count/3+" ms="+sw.ElapsedMilliseconds);}
   int rois=0,fallbacks=0,skipped=0;long worst=0;var roiWatch=Stopwatch.StartNew();
   foreach(var e in cat.Files.Where(e=>e.Modality=="RTSTRUCT"))foreach(var roi in StructureSet.Load(e).Rois)
   {
    var t=RegistrationReader.Resolve(links,roi.FrameUid,stack.FrameUid);if(t==null)continue;var sw=Stopwatch.StartNew();string reason;
    try{var voxel=ThreeDGeometry.VoxelizeRoi(roi,t,24,CancellationToken.None,out reason);var mesh=voxel==null?ThreeDGeometry.ContourLines(roi,t,CancellationToken.None):ThreeDGeometry.Isosurface(voxel,voxel.Sample,.5,24,CancellationToken.None);if(voxel==null)fallbacks++;rois++;total+=mesh.Indices.Count/3;}catch(InvalidOperationException){skipped++;}sw.Stop();worst=Math.Max(worst,sw.ElapsedMilliseconds);
   }
   roiWatch.Stop();Console.WriteLine("ROI meshes="+rois+" fallbacks="+fallbacks+" skipped="+skipped+" total_ms="+roiWatch.ElapsedMilliseconds+" slowest_roi_ms="+worst);
   foreach(var e in cat.Files.Where(e=>e.Modality=="RTDOSE")){var d=DoseGrid.Load(e);var t=RegistrationReader.Resolve(links,stack.FrameUid,d.FrameUid);if(t==null)continue;var sw=Stopwatch.StartNew();var b=d.Volume??v;var mesh=ThreeDGeometry.Isosurface(b,d.Volume!=null?new Func<Vec3,float>(d.Sample):p=>d.Sample(t.Transform(p)),d.Maximum*.5,64,CancellationToken.None);sw.Stop();total+=mesh.Indices.Count/3;Console.WriteLine("Dose triangles="+mesh.Indices.Count/3+" ms="+sw.ElapsedMilliseconds);}
   clock.Stop();Console.WriteLine("Aggregate 3D geometry triangles="+total+" elapsed_ms="+clock.ElapsedMilliseconds+" cap=240000");return 0;
  }
  catch(Exception e){Console.WriteLine("3D benchmark failed: "+e.GetType().Name);return 1;}
 }
}
