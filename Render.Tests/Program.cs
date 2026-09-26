using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using QuickLook.DicomRT;
using Dicom;
using Dicom.Imaging;
using Dicom.IO.Buffer;
class Program
{
 static DoseGrid MakeDose(){var d=new DicomDataset();d.Add(DicomTag.Rows,(ushort)3);d.Add(DicomTag.Columns,(ushort)3);d.Add(DicomTag.BitsAllocated,(ushort)16);d.Add(DicomTag.BitsStored,(ushort)16);d.Add(DicomTag.HighBit,(ushort)15);d.Add(DicomTag.SamplesPerPixel,(ushort)1);d.Add(DicomTag.PixelRepresentation,(ushort)0);d.Add(DicomTag.PhotometricInterpretation,"MONOCHROME2");d.Add(DicomTag.ImagePositionPatient,new double[]{0,0,0});d.Add(DicomTag.ImageOrientationPatient,new double[]{1,0,0,0,1,0});d.Add(DicomTag.PixelSpacing,new double[]{3,2});d.Add(DicomTag.GridFrameOffsetVector,new double[]{0,4});d.Add(DicomTag.DoseGridScaling,.01);var px=DicomPixelData.Create(d,true);var bytes=new byte[18];for(int i=0;i<9;i++){ushort q=(ushort)((i%3)*500);bytes[i*2]=(byte)q;bytes[i*2+1]=(byte)(q>>8);}px.AddFrame(new MemoryByteBuffer(bytes));px.AddFrame(new MemoryByteBuffer(bytes));return DoseGrid.Load(new DicomEntry{Dataset=d});}
 static int checks;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
 static void Near(double x,double y,string name){Check(Math.Abs(x-y)<1e-5,name);}
 static bool HasColor(SlicePixels p){for(int i=0;i<p.Pixels.Length;i+=4)if(p.Pixels[i]!=p.Pixels[i+2])return true;return false;}
 static object FrameOf(SlicePane pane)=>typeof(SlicePane).GetField("frame",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(pane);
 static void PumpUntil(Func<bool> done)
 {
  var frame=new DispatcherFrame();var watch=System.Diagnostics.Stopwatch.StartNew();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(10)};
  timer.Tick+=(a,b)=>{if(done()||watch.ElapsedMilliseconds>10000)frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);timer.Stop();Check(done(),"dispatcher render completed");
 }
 [STAThread] static int Main(string[] args){if(args.Length==2&&args[0]=="--reformat-benchmark")return ReformatTests.Benchmark(args[1]);if(args.Length==2&&args[0]=="--benchmark")return Benchmark.Run(args[1]);try{
 FieldApertureTests.Run(Check);
 var e=new DicomEntry {Columns=3,Rows=3,Origin=new Vec3(10,20,30),AxisX=new Vec3(0,1,0),AxisY=new Vec3(-1,0,0),SpacingX=2,SpacingY=4,HasGeometry=true};
 var s=new RenderScene {Entry=e,Native=new PixelPlane {Width=3,Height=3,Values=new float[]{0,10,20,30,40,50,60,70,80}},Plane="Native",Focus=new Vec3(6,22,30),WindowCenter=40,WindowWidth=80};
 var g=SliceGeometry.Create(s); var p=g.WorldAt(.5,.5); Near(p.X,6,"native oblique center X");Near(p.Y,22,"native oblique center Y");Near(p.Z,30,"native center Z");
 s.Zoom=2;s.Focus=new Vec3(8,21,30);var zoomed=SliceGeometry.Create(s);Near(zoomed.Center.X,8,"native zoom centers picked focus X");Near(zoomed.Center.Y,21,"native zoom centers picked focus Y");s.Zoom=1;s.Focus=new Vec3(6,22,30);
 Near(g.WidthMm,6,"native pixel-edge width");Near(g.HeightMm,12,"native pixel-edge height");
 Near(SliceRaster.SampleImage(s,p),40,"native bilinear patient-space center");
 foreach(bool invert in new[]{false,true})foreach(double zoom in new[]{.75,1,2.3})
 {
  s.Native.Invert=invert;s.Zoom=zoom;var exact=SliceRaster.Render(s,37,29,CancellationToken.None);bool identical=true;
  for(int yy=0;yy<exact.Height;yy++)for(int xx=0;xx<exact.Width;xx++){byte expected=SliceRaster.Window(SliceRaster.SampleImage(s,exact.Geometry.WorldAt((xx+.5)/exact.Width,(yy+.5)/exact.Height)),s.WindowCenter,s.WindowWidth,invert);int k=(yy*exact.Width+xx)*4;if(exact.Pixels[k]!=expected||exact.Pixels[k+1]!=expected||exact.Pixels[k+2]!=expected||exact.Pixels[k+3]!=255)identical=false;}
  Check(identical,"optimized native raster exactly retains reference bilinear/window pixels");
 }
 s.Native.Invert=false;s.Zoom=1;
 Check(float.IsNaN(SliceRaster.SampleImage(s,new Vec3(6,22,40))),"native excludes distant plane");
 var v=new VolumeData{Width=3,Height=3,Depth=3,Origin=new Vec3(0,0,0),AxisX=new Vec3(1,0,0),AxisY=new Vec3(0,1,0),AxisZ=new Vec3(0,0,1),SpacingX=2,SpacingY=3,SpacingZ=4,Values=new float[27]};
 s.Volume=v;s.Plane="Coronal";s.Focus=new Vec3(2,3,4); g=SliceGeometry.Create(s);Near(g.WorldAt(.5,.5).Y,3,"coronal fixed Y");Check(g.WorldAt(.5,0).Z>g.WorldAt(.5,1).Z,"superior at top");
 s.Plane="Sagittal";g=SliceGeometry.Create(s);Near(g.WorldAt(.5,.5).X,2,"sagittal fixed X");
 s.Plane="Native";var raster=SliceRaster.Render(s,1024,1024,CancellationToken.None);Check(raster.Width<=512&&raster.Height<=512,"bounded raster");Check(raster.Pixels.Length==raster.Width*raster.Height*4,"raster payload");
 var roi=new StructureRoi{Contours=new List<Contour>{new Contour{GeometricType="CLOSED_PLANAR",Points=new List<Vec3>{new Vec3(0,0,0),new Vec3(4,0,0),new Vec3(4,4,0),new Vec3(0,4,0)}}}};
 s.Plane="Axial";s.Focus=new Vec3(2,2,4);g=SliceGeometry.Create(s);Check(SliceGeometry.ContourLines(roi,Matrix4.Identity,g,.01).Count==0,"no distant contour projection");
 s.Plane="Coronal";s.Focus=new Vec3(2,2,0);g=SliceGeometry.Create(s);var lines=SliceGeometry.ContourLines(roi,Matrix4.Identity,g,.01);Check(lines.Count==1,"orthogonal true intersection segment");Near((lines[0].B-lines[0].A).Length,4,"intersection length");
 Check(SliceRaster.Window(40,40,80,false)==128,"window center");Check(SliceRaster.Window(0,40,80,false)==0,"window black");Check(SliceRaster.Window(80,40,80,true)==0,"MONOCHROME1 invert");
 var cancelled=new CancellationTokenSource();cancelled.Cancel();bool sawCancel=false;try{SliceRaster.Render(s,20,20,cancelled.Token);}catch(OperationCanceledException){sawCancel=true;}Check(sawCancel,"render cancellation");
 var dose=MakeDose();s.Plane="Axial";s.Focus=new Vec3(2,3,0);s.Doses.Add(new DoseOverlay{Dose=dose});s.Isodoses=true;var colored=SliceRaster.Render(s,64,64,CancellationToken.None);Check(colored.Isolines.Count>0,"physical dose isodose intersections");int coloredCount=0;for(int i=0;i<colored.Pixels.Length;i+=4)if(colored.Pixels[i]!=colored.Pixels[i+2])coloredCount++;Check(coloredCount>0,"dose wash colors");
 s.Doses[0].ImageToDose=new Matrix4(new double[]{1,0,0,100,0,1,0,0,0,0,1,0,0,0,0,1});var displaced=SliceRaster.Render(s,64,64,CancellationToken.None);Check(displaced.Isolines.Count==0,"registered dose sampled in dose coordinates");
 s.Doses[0].ImageToDose=Matrix4.Identity;var watch=System.Diagnostics.Stopwatch.StartNew();SliceRaster.Render(s,512,512,CancellationToken.None);watch.Stop();Console.WriteLine("Synthetic 512x512 image + dose + isodoses: "+watch.ElapsedMilliseconds+" ms");
 s.DoseWash=false;s.IsoLevels=new double[]{30,30,double.NaN,-1,101};var linesOnly=SliceRaster.Render(s,64,64,CancellationToken.None);
 Check(!HasColor(linesOnly)&&linesOnly.Isolines.Count>0,"isodoses independently enabled with wash disabled");
 foreach(var line in linesOnly.Isolines){Near(line.DosePercent,30,"custom isodose level metadata");Near(line.A.X,1.2,"custom physical isodose location");Near(line.B.X,1.2,"custom physical isodose endpoint");}
 var snap=s.Snapshot();s.IsoLevels[0]=70;Near(snap.IsoLevels[0],30,"snapshot owns isodose levels");
 s.Isodoses=false;s.DoseWash=true;s.DoseMinimumPercent=55;s.DoseMaximumPercent=65;var band=SliceRaster.Render(s,64,64,CancellationToken.None);Check(HasColor(band)&&band.Isolines.Count==0,"wash independently enabled with isodoses disabled");
 for(int y=0;y<band.Height;y++)for(int x=0;x<band.Width;x++){int k=(y*band.Width+x)*4;if(band.Pixels[k]==band.Pixels[k+2])continue;var percent=100*dose.Sample(band.Geometry.WorldAt((x+.5)/band.Width,(y+.5)/band.Height))/dose.Maximum;Check(percent>=55&&percent<=65,"dose wash respects threshold interval");}
 s.DoseMinimumPercent=101;Check(!HasColor(SliceRaster.Render(s,32,32,CancellationToken.None)),"dose threshold excludes entire wash");
 double rr,gg,bb;SliceRaster.DoseColor(50,out rr,out gg,out bb);Check(rr==255&&gg==255&&bb==0,"shared 50 percent dose color");
 var fusion=new RenderScene{Volume=v,Plane="Axial",Focus=new Vec3(2,3,0),WindowCenter=0,WindowWidth=400};
 var overlay=new VolumeData{Width=3,Height=3,Depth=3,Origin=new Vec3(100,50,0),AxisX=new Vec3(0,1,0),AxisY=new Vec3(-1,0,0),AxisZ=new Vec3(0,0,1),SpacingX=2,SpacingY=3,SpacingZ=4,Values=Enumerable.Repeat(400f,27).ToArray()};
 fusion.OverlayVolume=overlay;fusion.ImageToOverlay=new Matrix4(new double[]{0,-1,0,100,1,0,0,50,0,0,1,0,0,0,0,1});fusion.OverlayOpacity=1;
 var fused=SliceRaster.Render(fusion,3,3,CancellationToken.None);Check(fused.Pixels[(1*3+1)*4]==255,"fusion applies rotation and translation in physical space");
 fusion.OverlayOpacity=.5;Check(SliceRaster.Render(fusion,3,3,CancellationToken.None).Pixels[16]==191,"fusion blends configured opacity");
 fusion.OverlayOpacity=1;fusion.OverlayWindowCenter=400;Check(SliceRaster.Render(fusion,3,3,CancellationToken.None).Pixels[16]==128,"fusion uses independent window");
 fusion.OverlayWindowCenter=0;fusion.ImageToOverlay=Matrix4.Identity;Check(SliceRaster.Render(fusion,3,3,CancellationToken.None).Pixels[16]==128,"fusion out of field preserves base image");
 fusion.OverlayVolume=null;fusion.Entry=new DicomEntry{SeriesUid="synthetic-a",HasGeometry=true};
 var previousContext=SynchronizationContext.Current;SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
 try{using(var pane=new SlicePane())
 {
  pane.Measure(new Size(300,240));pane.Arrange(new Rect(0,0,300,240));pane.Scene=fusion;PumpUntil(()=>FrameOf(pane)!=null);var old=FrameOf(pane);
  var next=fusion.Snapshot();next.Focus=new Vec3(2,3,4);next.WindowCenter=100;pane.Scene=next;
  Check(ReferenceEquals(old,FrameOf(pane)),"scroll retains exact complete previous frame synchronously");
  var oldScene=(RenderScene)old.GetType().GetField("Scene").GetValue(old);Near(oldScene.Focus.Z,0,"retained frame retains original focus");Near(oldScene.WindowCenter,0,"retained frame retains original window");
  var newest=next.Snapshot();newest.Focus=new Vec3(2,3,8);pane.Scene=newest;PumpUntil(()=>FrameOf(pane)!=null&&!ReferenceEquals(old,FrameOf(pane)));
  var accepted=(RenderScene)FrameOf(pane).GetType().GetField("Scene").GetValue(FrameOf(pane));Near(accepted.Focus.Z,8,"newest generation replaces old frame atomically");
  var different=newest.Snapshot();different.Entry=new DicomEntry{SeriesUid="synthetic-b",HasGeometry=true};pane.Scene=different;Check(FrameOf(pane)==null,"source series change clears old frame");PumpUntil(()=>FrameOf(pane)!=null);
  var nativeCoronal=new RenderScene{Plane="Native",Entry=new DicomEntry{SeriesUid="synthetic-coronal",HasGeometry=true,Origin=new Vec3(-10,0,10),AxisX=new Vec3(1,0,0),AxisY=new Vec3(0,0,-1),SpacingX=1,SpacingY=1},Native=new PixelPlane{Width=21,Height=21,Values=new float[441]}};
  var stackRoi=new StructureRoi();foreach(double z in new[]{0d,2,4})stackRoi.Contours.Add(new Contour{GeometricType="CLOSED_PLANAR",Points=new List<Vec3>{new Vec3(-5,-5,z),new Vec3(5,-5,z),new Vec3(5,5,z),new Vec3(-5,5,z)}});
  nativeCoronal.Structures.Add(new RoiOverlay{Roi=stackRoi});pane.Scene=nativeCoronal;PumpUntil(()=>FrameOf(pane)!=null);
  var nativeFrame=FrameOf(pane);Check((bool)nativeFrame.GetType().GetField("InterpolatedContours").GetValue(nativeFrame),"native pane actually routes off-axis contours through boundary renderer");
  pane.Scene=null;Check(FrameOf(pane)==null,"missing source clears frame");
 }}finally{SynchronizationContext.SetSynchronizationContext(previousContext);}
 ReformatTests.Run();
 Console.WriteLine("PASS "+checks+" synthetic render/coordinate checks");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
