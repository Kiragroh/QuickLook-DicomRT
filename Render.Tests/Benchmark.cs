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
   var catalog=DicomCatalog.Scan(Directory.EnumerateFiles(folder).First(),CancellationToken.None);
   var links=RegistrationReader.Read(catalog);var rois=new List<StructureRoi>();var doses=new List<DoseGrid>();
   foreach(var e in catalog.Files.Where(x=>x.Modality=="RTSTRUCT"))rois.AddRange(StructureSet.Load(e).Rois);
   foreach(var e in catalog.Files.Where(x=>x.Modality=="RTDOSE"))doses.Add(DoseGrid.Load(e));
   Console.WriteLine("Aggregate read-only benchmark: files="+catalog.Files.Count+" ROIs="+rois.Count+" dose grids="+doses.Count+" registration links="+links.Count);
   foreach(string modality in new[]{"CT","MR"})
   {
    var stack=catalog.Stacks.Where(x=>x.Modality==modality&&x.CanMpr).OrderByDescending(x=>rois.Count(r=>RegistrationReader.Resolve(links,r.FrameUid,x.FrameUid)!=null)).ThenByDescending(x=>x.Entries.Count).FirstOrDefault();if(stack==null){Console.WriteLine(modality+": no MPR-capable stack");continue;}
    var volume=VolumeData.Load(stack,CancellationToken.None);var e=stack.Entries[stack.Entries.Count/2];var native=PixelPlane.Load(e);
    var scene=new RenderScene{Volume=volume,Entry=e,Native=native,Focus=volume.Center,WindowCenter=(volume.Max+volume.Min)/2.0,WindowWidth=Math.Max(1,volume.Max-volume.Min)};
    foreach(var roi in rois){var m=RegistrationReader.Resolve(links,roi.FrameUid,stack.FrameUid);if(m!=null)scene.Structures.Add(new RoiOverlay{Roi=roi,RoiToImage=m});}
    foreach(var dose in doses){var m=RegistrationReader.Resolve(links,stack.FrameUid,dose.FrameUid);if(m!=null)scene.Doses.Add(new DoseOverlay{Dose=dose,ImageToDose=m});}
    Console.WriteLine(modality+": dimensions="+volume.Width+"x"+volume.Height+"x"+volume.Depth+" mapped ROIs="+scene.Structures.Count+" mapped doses="+scene.Doses.Count);
    foreach(string plane in modality=="CT"?new[]{"Native"}:new[]{"Axial","Coronal"})
    {
     scene.Plane=plane;
     foreach(bool isodoses in new[]{false,true})
     for(int pass=0;pass<3;pass++)
     {
      scene.Isodoses=isodoses;var sw=Stopwatch.StartNew();var raster=SliceRaster.Render(scene,512,512,CancellationToken.None);long rasterMs=sw.ElapsedMilliseconds;int segments=0;
      foreach(var roi in scene.Structures)segments+=SliceGeometry.ContourLines(roi.Roi,roi.RoiToImage,raster.Geometry,plane=="Native"?volume.SpacingZ*.49:.01).Count;
      sw.Stop();Console.WriteLine(modality+" "+plane+" isodoses="+isodoses+" pass="+pass+" raster_ms="+rasterMs+" contours_ms="+(sw.ElapsedMilliseconds-rasterMs)+" total_ms="+sw.ElapsedMilliseconds+" segments="+segments);
     }
    }
   }
   return 0;
  }
  catch(Exception e){Console.WriteLine("Benchmark failed: "+e.GetType().Name);return 1;}
 }
}
