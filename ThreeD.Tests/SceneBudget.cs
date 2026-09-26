using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Media.Media3D;
using QuickLook.DicomRT;
internal static class SceneBudget
{
 public static int Run(string folder)
 {
  try
  {
   var cat=DicomCatalog.Scan(Directory.EnumerateFiles(folder).First(),CancellationToken.None);var stack=cat.Stacks.First(x=>x.Modality=="CT"&&x.CanMpr);var volume=VolumeData.Load(stack,CancellationToken.None);var links=RegistrationReader.Read(cat);var s=new RenderScene{Volume=volume,Entry=stack.Entries[0]};
   foreach(var e in cat.Files.Where(e=>e.Modality=="RTSTRUCT"))foreach(var r in StructureSet.Load(e).Rois){var m=RegistrationReader.Resolve(links,r.FrameUid,stack.FrameUid);if(m!=null&&r.Contours.Any(c=>c.Points.Count>0))s.Structures.Add(new RoiOverlay{Roi=r,RoiToImage=m});}
   foreach(var e in cat.Files.Where(e=>e.Modality=="RTDOSE")){var dose=DoseGrid.Load(e);var m=RegistrationReader.Resolve(links,stack.FrameUid,dose.FrameUid);if(m!=null)s.Doses.Add(new DoseOverlay{Dose=dose,ImageToDose=m});}
   var prepare=typeof(ThreeDControl).GetMethod("PrepareCore",BindingFlags.NonPublic|BindingFlags.Static);
   using(var control=new ThreeDControl())
   {
    var cache=typeof(ThreeDControl).GetField("cache",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(control);
    for(int mode=0;mode<4;mode++)
    {
     if(mode==1)((IDictionary)cache).Clear();bool full=mode==0;bool noCt=mode==3;if(noCt)s.Volume=null;
     var sw=Stopwatch.StartNew();var result=prepare.Invoke(null,new object[]{s,!noCt,.5,CancellationToken.None,cache,full,full,full,full});sw.Stop();
     int roi=0,doses=0,ct=0,triangles=0,vertices=0;foreach(var part in (IEnumerable)result.GetType().GetField("Parts").GetValue(result)){string kind=(string)part.GetType().GetField("Kind").GetValue(part);if(kind=="Roi")roi++;else if(kind=="Dose")doses++;else ct++;var mesh=(MeshGeometry3D)part.GetType().GetField("Mesh").GetValue(part);triangles+=mesh.TriangleIndices.Count/3;vertices+=mesh.Positions.Count;}
     int expected=full?s.Structures.Count:s.Structures.Count(r=>ThreeDGeometry.DefaultRoi(r.Roi));
     Console.WriteLine("SCENE_BUDGET mode="+new[]{"full_cold","default_cold","default_warm","no_CT_warm"}[mode]+" requested_rois="+expected+" retained_rois="+roi+" retained_doses="+doses+" ct_context="+ct+" triangles="+triangles+" vertices="+vertices+" elapsed_ms="+sw.ElapsedMilliseconds+" cached="+result.GetType().GetField("CacheHits").GetValue(result)+" skipped="+result.GetType().GetField("Skipped").GetValue(result));
     if(roi!=expected||doses!=(full?s.Doses.Count:0)||triangles>400000||(!full&&triangles>180000)){Console.WriteLine("FAIL: scene budget omitted available geometry");return 1;}
    }
   }
   Console.WriteLine("PASS: complete scene allocation, defaults, cache, no CT requirement");return 0;
  }
  catch(Exception e){Console.WriteLine("Scene budget failed: "+e.GetType().Name);return 1;}
 }
}
