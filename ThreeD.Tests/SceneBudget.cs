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
   var sw=Stopwatch.StartNew();var result=typeof(ThreeDControl).GetMethod("Prepare",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{s,true,.5,CancellationToken.None});sw.Stop();
   int roi=0,doses=0,ct=0,triangles=0;foreach(var part in (IEnumerable)result.GetType().GetField("Parts").GetValue(result)){string kind=(string)part.GetType().GetField("Kind").GetValue(part);if(kind=="Roi")roi++;else if(kind=="Dose")doses++;else ct++;triangles+=((MeshGeometry3D)part.GetType().GetField("Mesh").GetValue(part)).TriangleIndices.Count/3;}
   Console.WriteLine("SCENE_BUDGET requested_rois="+s.Structures.Count+" retained_rois="+roi+" requested_doses="+s.Doses.Count+" retained_doses="+doses+" ct_context="+ct+" triangles="+triangles+" elapsed_ms="+sw.ElapsedMilliseconds+" skipped="+result.GetType().GetField("Skipped").GetValue(result));
   if(roi!=s.Structures.Count||doses!=s.Doses.Count){Console.WriteLine("FAIL: scene budget omitted available geometry");return 1;}Console.WriteLine("PASS: complete scene allocation");return 0;
  }
  catch(Exception e){Console.WriteLine("Scene budget failed: "+e.GetType().Name);return 1;}
 }
}
