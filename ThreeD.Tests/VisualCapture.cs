using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using QuickLook.DicomRT;
internal static class VisualCapture
{
 public static int Run(string folder,string output)
 {
  SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
  try
  {
   var catalog=DicomCatalog.Scan(Directory.EnumerateFiles(folder).First(),CancellationToken.None);var rois=catalog.Files.Where(e=>e.Modality=="RTSTRUCT").SelectMany(e=>StructureSet.Load(e).Rois).ToList();var chosen=rois.FirstOrDefault(r=>(r.Name.IndexOf("brainstem",StringComparison.OrdinalIgnoreCase)>=0||r.Name.IndexOf("hirnstamm",StringComparison.OrdinalIgnoreCase)>=0||r.Name.IndexOf("brain stem",StringComparison.OrdinalIgnoreCase)>=0));if(chosen==null){Console.WriteLine("Named organ matches=0; selecting a metadata-defined organ only for visual inspection");chosen=rois.First(r=>r.InterpretedType=="ORGAN");}
   string reason;var vox=ThreeDGeometry.VoxelizeRoi(chosen,Matrix4.Identity,32,CancellationToken.None,out reason,true);var raw=ThreeDGeometry.Isosurface(vox,vox.Sample,.5,32,CancellationToken.None);var original=ThreeDGeometry.Smooth(raw,0,CancellationToken.None);var mesh=ThreeDGeometry.Smooth(raw,1,CancellationToken.None);var edges=new System.Collections.Generic.Dictionary<long,int>();var winding=new System.Collections.Generic.Dictionary<long,int>();int inversions=0;
   for(int i=0;i<mesh.Indices.Count;i+=3){int a=mesh.Indices[i],b=mesh.Indices[i+1],c=mesh.Indices[i+2];var n=(mesh.Points[b]-mesh.Points[a]).Cross(mesh.Points[c]-mesh.Points[a]);var before=(original.Points[b]-original.Points[a]).Cross(original.Points[c]-original.Points[a]);if(before.Length>1e-10&&n.Dot(before)<=0)inversions++;for(int k=0;k<3;k++){int x=mesh.Indices[i+k],y=mesh.Indices[i+(k+1)%3];long key=((long)Math.Min(x,y)<<32)|(uint)Math.Max(x,y);if(!edges.ContainsKey(key)){edges[key]=0;winding[key]=0;}edges[key]++;winding[key]+=x<y?1:-1;}}
   var boundary=edges.Where(e=>e.Value!=2).Select(e=>new[]{(int)(e.Key>>32),(int)(e.Key&uint.MaxValue)}).ToArray();var bv=boundary.SelectMany(e=>e).Distinct().ToArray();int nearDuplicates=bv.Count(a=>bv.Any(b=>a!=b&&(original.Points[a]-original.Points[b]).Length<.001));Console.WriteLine("Boundary near_duplicate_vertices="+nearDuplicates+" min_edge="+(boundary.Length==0?0:boundary.Min(e=>(original.Points[e[0]]-original.Points[e[1]]).Length))+" max_edge="+(boundary.Length==0?0:boundary.Max(e=>(original.Points[e[0]]-original.Points[e[1]]).Length)));
   Console.WriteLine("Selected mesh boundary_edges="+edges.Values.Count(n=>n!=2)+" orientation_conflicts="+winding.Values.Count(n=>n!=0)+" inverted_after_smoothing="+inversions);
   if(edges.Values.Any(n=>n!=2)||winding.Values.Any(n=>n!=0)||inversions!=0)throw new InvalidOperationException("Public surface failed topology checks");
   Console.WriteLine("Selected visual check interpreted type="+(chosen.InterpretedType??"missing"));Directory.CreateDirectory(output);
   using(var control=new ThreeDControl())
   {
    var window=new Window{Width=1000,Height=800,Left=-30000,Top=-30000,ShowInTaskbar=false,ShowActivated=false,WindowStyle=WindowStyle.None,Content=control};window.Show();
    try
    {
     var field=typeof(ThreeDControl).GetField("prepared",BindingFlags.Instance|BindingFlags.NonPublic);
     for(int mode=0;mode<2;mode++)
     {
      ((System.Windows.Controls.CheckBox)typeof(ThreeDControl).GetField("allRois",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(control)).IsChecked=mode==0;
      var selected=mode==0?new[]{chosen}:rois.Where(r=>r.FrameUid==chosen.FrameUid).ToArray();control.SetScene(new RenderScene{Structures=selected.Select(r=>new RoiOverlay{Roi=r}).ToList()});
      var frame=new DispatcherFrame();var start=DateTime.UtcNow;var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(30)};timer.Tick+=(s,e)=>{if(field.GetValue(control)!=null||(DateTime.UtcNow-start).TotalSeconds>15)frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);timer.Stop();if(field.GetValue(control)==null)throw new TimeoutException();
      typeof(ThreeDControl).GetMethod("ResetCamera",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(control,null);control.UpdateLayout();
      var image=new RenderTargetBitmap(1000,800,96,96,PixelFormats.Pbgra32);image.Render(control);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var file=File.Create(Path.Combine(output,mode==0?"public-3d-selected.png":"public-3d-default.png")))encoder.Save(file);
     }
    }
    finally{window.Close();}
   }
   Console.WriteLine("PASS approved public 3D offscreen captures (no identifying labels)");return 0;
  }
  catch(Exception e){Console.WriteLine("3D capture failed: "+e.GetType().Name);return 1;}
 }
}
