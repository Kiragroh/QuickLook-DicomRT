using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Dicom;
using Dicom.Imaging;
using Dicom.Imaging.Codec;
using Dicom.IO.Buffer;
using QuickLook.DicomRT;
class Program {
 static string folder;
 static int failures;
 static int Main(string[] args){if(args.Length==2&&args[0]=="--accept")return Acceptance.Run(args[1]);folder=Path.Combine(Path.GetTempPath(),"quicklook-core-tests-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder); try {
  Run("cached parallel volume assembly",ParallelVolume);Run("sampling",Sampling); Run("pixel decode",Pixels); Run("header and stack ordering",Catalog); Run("RT-first incremental catalog",PriorityCatalog); Run("unsafe geometry",Unsafe); Run("complete nested tags",Tags); Run("endian and bit layout",Endian);Run("volume boundaries",Boundaries);Run("stack partitioning",Partitioning);Run("unsupported intensity transforms",UnsupportedTransforms);
  Console.WriteLine(failures==0?"PASS: all Core synthetic checks":"FAIL: "+failures+" Core synthetic groups");return failures==0?0:1;
 }finally{Directory.Delete(folder,true);}}
 static void Run(string name,Action action){try{action();Console.WriteLine("PASS: "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL: "+name+" ("+e.GetType().Name+")");}}
 static void ParallelVolume(){
  var stack=Stack(0,2,4,6,8,10);var baseline=VolumeData.Load(stack,CancellationToken.None);var cached=PixelPlane.Load(stack.Entries[2]);int decodes=0,reused=0,active=0,maxActive=0;var gate=new object();var progress=new List<int>();
  var actual=VolumeData.Load(stack,CancellationToken.None,n=>progress.Add(n),e=>{
   lock(gate){active++;maxActive=Math.Max(maxActive,active);}try{
    if(ReferenceEquals(e,stack.Entries[2])){Interlocked.Increment(ref reused);return cached;}
    Interlocked.Increment(ref decodes);return PixelPlane.Load(e);
   }finally{lock(gate)active--;}
  },2);
  Check(decodes==5&&reused==1,"cached slice not decoded twice");Check(maxActive<=2,"bounded decoding concurrency");Check(actual.Values.SequenceEqual(baseline.Values)&&actual.Min==baseline.Min&&actual.Max==baseline.Max&&actual.Invert==baseline.Invert,"parallel assembly preserves intensities and photometric interpretation");Check(progress.SequenceEqual(Enumerable.Range(1,6)),"parallel completion progress monotonic");
  bool rejected=false;try{VolumeData.Load(stack,CancellationToken.None,null,e=>ReferenceEquals(e,stack.Entries[3])?new PixelPlane{Width=1,Height=1,Values=new float[1]}:PixelPlane.Load(e),2);}catch(InvalidOperationException){rejected=true;}Check(rejected,"parallel geometry failure keeps its original exception type");
 }
 static void Sampling(){
  var v=new VolumeData{Width=2,Height=2,Depth=2,Origin=new Vec3(10,20,30),AxisX=new Vec3(0,1,0),AxisY=new Vec3(0,0,1),AxisZ=new Vec3(1,0,0),SpacingX=2,SpacingY=3,SpacingZ=4,Values=new float[]{0,1,2,3,4,5,6,7}};
  var p=v.WorldAt(1,1,1); Check((p-new Vec3(14,22,33)).Length<1e-8,"oblique pixel centers");
  Check(Math.Abs(v.Sample(v.Center)-3.5)<1e-6,"trilinear interpolation");
  Check(v.SampleNearest(v.WorldAt(0.9,0.9,0.9))==7,"nearest neighbor");
  Check(float.IsNaN(v.Sample(v.WorldAt(-1,0,0))),"outside sampling");
 }
 static DicomDataset Make(double z,int echo=1){
  var d=new DicomDataset(); d.Add(DicomTag.SOPClassUID,DicomUID.CTImageStorage); d.Add(DicomTag.SOPInstanceUID,"2.25."+Math.Abs(Guid.NewGuid().GetHashCode()));
  d.Add(DicomTag.SeriesInstanceUID,"2.25.100");d.Add(DicomTag.StudyInstanceUID,"2.25.200");d.Add(DicomTag.FrameOfReferenceUID,"2.25.300");d.Add(DicomTag.Modality,"CT");d.Add(DicomTag.PatientID,"SYNTHETIC");
  d.Add(DicomTag.Rows,(ushort)2);d.Add(DicomTag.Columns,(ushort)2);d.Add(DicomTag.ImagePositionPatient,10.0,20.0,z);d.Add(DicomTag.ImageOrientationPatient,1.0,0,0,0,1,0);d.Add(DicomTag.PixelSpacing,3.0,2.0);d.Add(DicomTag.EchoNumbers,echo);
  d.Add(DicomTag.BitsAllocated,(ushort)16);d.Add(DicomTag.BitsStored,(ushort)12);d.Add(DicomTag.HighBit,(ushort)11);d.Add(DicomTag.PixelRepresentation,(ushort)1);d.Add(DicomTag.SamplesPerPixel,(ushort)1);d.Add(DicomTag.PhotometricInterpretation,"MONOCHROME2");d.Add(DicomTag.RescaleSlope,2.0);d.Add(DicomTag.RescaleIntercept,-1024.0);
  var p=DicomPixelData.Create(d,true);p.AddFrame(new MemoryByteBuffer(new byte[]{0xff,0x0f,0x00,0x00,0x01,0x00,0xff,0x07}));return d;
 }
 static string Save(DicomDataset d,string name){string p=Path.Combine(folder,name);new DicomFile(d).Save(p);return p;}
 static void Pixels(){var path=Save(Make(0),"pixels.dcm");var e=DicomCatalog.ReadEntry(path);var p=PixelPlane.Load(e);Check(p.Values.SequenceEqual(new float[]{-1026,-1024,-1022,3070}),"signed/rescaled 12 bits");Check(p.Min==-1026&&p.Max==3070,"physical range");Check(e.SpacingX==2&&e.SpacingY==3,"DICOM row/column spacing");
  var d=Make(0);d.AddOrUpdate(DicomTag.Modality,"MR");d.AddOrUpdate(DicomTag.RescaleSlope,1.0);d.AddOrUpdate(DicomTag.RescaleIntercept,0.0);d.AddOrUpdate(DicomTag.PixelRepresentation,(ushort)0);d.AddOrUpdate(DicomTag.PhotometricInterpretation,"MONOCHROME1");p=PixelPlane.Load(DicomCatalog.ReadEntry(Save(d,"mr.dcm")));Check(p.Values[0]==4095&&p.Invert,"unsigned MR and inversion");
 }
 static void Catalog(){string dir=Path.Combine(folder,"catalog");Directory.CreateDirectory(dir);string path=null;foreach(int z in new[]{4,0,2}){path=Path.Combine(dir,"slice"+z+".dcm");new DicomFile(Make(z)).Save(path);}new DicomFile(Make(0,2)).Save(Path.Combine(dir,"echo.dcm"));File.WriteAllText(Path.Combine(dir,"other.txt"),"not dicom");var c=DicomCatalog.Scan(path,CancellationToken.None);Check(c.Files.Count==4&&c.SkippedFiles==1,"bounded directory scan");Check(c.Stacks.Count==2,"echo split");var s=c.Stacks.Single(x=>x.Entries.Count==3);Check(s.CanMpr&&s.Entries[0].Origin.Z==0&&s.Entries[2].Origin.Z==4,"geometric ordering");var v=VolumeData.Load(s,CancellationToken.None);Check(v.Depth==3&&v.SpacingZ==2&&v.Values.Length==12,"load volume");}
 static ImageStack Stack(params double[] zs){var s=new ImageStack();int i=0;foreach(var z in zs)s.Entries.Add(DicomCatalog.ReadEntry(Save(Make(z),"unsafe"+(i++)+".dcm")));return s;}
 static void RecursiveCatalog(){
  var root=Path.Combine(folder,"nested");var ct=Path.Combine(root,"CT");var rt=Path.Combine(root,"RT","plan");Directory.CreateDirectory(ct);Directory.CreateDirectory(rt);string selected=Path.Combine(ct,"ct.dcm");new DicomFile(Make(0)).Save(selected);
  var dataset=new DicomDataset().Add(DicomTag.SOPClassUID,DicomUID.RTPlanStorage).Add(DicomTag.SOPInstanceUID,"2.25.99801").Add(DicomTag.Modality,"RTPLAN").Add(DicomTag.PatientID,"SYNTHETIC");new DicomFile(dataset).Save(Path.Combine(rt,"opaque.bin"));
  Check(DicomCatalog.Scan(selected,CancellationToken.None).Files.Count==1,"default search remains folder-local");var seen=new List<string>();var catalog=DicomCatalog.Scan(selected,CancellationToken.None,entryFound:e=>seen.Add(e.Modality),searchRoot:root,recursive:true);Check(catalog.Files.Count==2&&seen[0]=="RTPLAN","chosen common parent finds sibling RT with RT-first decoding");
  var rtOnly=DicomCatalog.Scan(selected,CancellationToken.None,searchRoot:root,recursive:true,imageFilter:e=>false);Check(rtOnly.Files.Count==1&&rtOnly.Files[0].Modality=="RTPLAN"&&rtOnly.Stacks.Count==0&&rtOnly.DeferredImages.Count==1,"RT discovery defers full image entries without losing identity");
  var targeted=DicomCatalog.Scan(selected,CancellationToken.None,knownPaths:rtOnly.DeferredImages.Select(e=>e.Path));Check(targeted.Files.Count==1&&targeted.Stacks.Count==1,"known image paths load without rescanning unrelated folders");
  using(var cancel=new CancellationTokenSource()){cancel.Cancel();bool stopped=false;try{DicomCatalog.Scan(selected,cancel.Token,searchRoot:root,recursive:true);}catch(OperationCanceledException){stopped=true;}Check(stopped,"recursive discovery is cancellable");}
 }
 static void PriorityCatalog(){
  var dir=Path.Combine(folder,"priority");Directory.CreateDirectory(dir);var paths=new List<string>();
  for(int i=0;i<48;i++){var path=Path.Combine(dir,"000-image-"+i.ToString("D3")+".dcm");new DicomFile(Make(i*2)).Save(path);paths.Add(path);}
  var rtPaths=new List<string>();var uids=new[]{DicomUID.RTPlanStorage,DicomUID.RTStructureSetStorage,DicomUID.RTDoseStorage,DicomUID.SpatialRegistrationStorage};var modalities=new[]{"RTPLAN","RTSTRUCT","RTDOSE","REG"};
  for(int i=0;i<uids.Length;i++){
   var d=new DicomDataset();d.Add(DicomTag.SOPClassUID,uids[i]);d.Add(DicomTag.SOPInstanceUID,"2.25.990"+i);d.Add(DicomTag.Modality,modalities[i]);d.Add(DicomTag.SeriesInstanceUID,"2.25.880"+i);d.Add(DicomTag.PatientID,"SYNTHETIC");
   d.Add(new DicomSequence(DicomTag.BeamSequence,new DicomDataset(new DicomLongString(DicomTag.BeamName,"full metadata sentinel"))));
   var path=Path.Combine(dir,"opaque-"+i+".bin");new DicomFile(i==3?d.Clone(DicomTransferSyntax.ExplicitVRBigEndian):d).Save(path);
   if(i==1){var bytes=File.ReadAllBytes(path);File.WriteAllBytes(path,bytes.Skip(132).ToArray());} // Valid DICOM without preamble.
   if(i==2){int metaLength=(int)DicomFile.Open(path).FileMetaInfo.GetSingleValue<uint>(DicomTag.FileMetaInformationGroupLength);var bytes=File.ReadAllBytes(path);File.WriteAllBytes(path,bytes.Skip(132+12+metaLength).ToArray());} // Raw explicit-VR dataset, no file meta information.
   rtPaths.Add(path);paths.Add(path);
  }
  var extra=Path.Combine(dir,"zzz-image.dcm");new DicomFile(Make(96)).Save(extra);paths.Add(extra);
  File.WriteAllText(Path.Combine(dir,"RP-misleading-name.dcm"),"not a dicom object");
  var seen=new List<DicomEntry>();var completed=new List<int>();var phases=new List<Tuple<string,int,int>>();bool headersComplete=false;int caller=Thread.CurrentThread.ManagedThreadId;
  var seed=DicomCatalog.ReadEntry(paths[0]);
  var catalog=DicomCatalog.Scan(paths[0],CancellationToken.None,completed.Add,e=>{
   Check(Thread.CurrentThread.ManagedThreadId==caller,"callback is on scan caller thread");
   if(rtPaths.Contains(e.Path)){Check(!headersComplete,"RT is published during minimal header pass");Check(e.Dataset.GetSequence(DicomTag.BeamSequence).Items[0].GetSingleValue<string>(DicomTag.BeamName)=="full metadata sentinel","RT callback has full metadata");}
   seen.Add(e);
  },(phase,count,total)=>{phases.Add(Tuple.Create(phase,count,total));if(phase=="headers"&&count==total)headersComplete=true;},seed);
  Check(seen.Take(4).All(e=>rtPaths.Contains(e.Path)),"all opaque RT and REG filenames published before any image");
  Check(seen.Count==paths.Count&&seen.Select(e=>e.Path).Distinct().Count()==paths.Count,"one callback per valid file");
  Check(ReferenceEquals(seen.Single(e=>e.Path==seed.Path),seed),"selected entry reused without duplicate read");
  Check(catalog.Files.Select(e=>e.Path).OrderBy(p=>p).SequenceEqual(paths.OrderBy(p=>p))&&catalog.SkippedFiles==1,"all original valid files retained and invalid counted once");
  Check(catalog.Stacks.Count==1&&catalog.Stacks[0].Entries.Count==49&&catalog.Stacks[0].CanMpr,"image stack reconstructed completely after RT priority");
  Check(completed.SequenceEqual(Enumerable.Range(1,paths.Count+1)),"legacy progress monotonic and complete");
  Check(phases.Where(p=>p.Item1=="headers").Select(p=>p.Item2).SequenceEqual(Enumerable.Range(0,paths.Count+2)),"header progress includes every candidate");
  Check(phases.Last().Item1=="complete"&&phases.Last().Item2==paths.Count+1&&phases.All(p=>p.Item3==paths.Count+1&&p.Item2<=p.Item3),"phase totals bounded and complete");
  int finalCount=-1;DicomCatalog.Scan(paths[0],CancellationToken.None,phaseProgress:(phase,count,total)=>{if(phase=="complete")finalCount=count;});Check(finalCount==paths.Count+1,"phase progress independent of legacy callback");
  using(var cancel=new CancellationTokenSource()){bool canceled=false;try{DicomCatalog.Scan(paths[0],cancel.Token,phaseProgress:(phase,count,total)=>{if(phase=="headers"&&count==1)cancel.Cancel();});}catch(OperationCanceledException){canceled=true;}Check(canceled,"header discovery responds to cancellation");}
 }
 static void Reject(ImageStack s){try{VolumeData.Load(s,CancellationToken.None);throw new Exception("accepted bad geometry");}catch(InvalidOperationException){}}
 static void Unsafe(){Reject(Stack(0,2,2));Reject(Stack(0,2,5));var s=Stack(0,2,4);s.Entries[1].Origin=new Vec3(11,20,2);Reject(s);s=Stack(0,2,4);s.Entries[1].AxisX=new Vec3(0,1,0);Reject(s);var d=Make(0);d.AddOrUpdate(DicomTag.PixelSpacing,-1.0,2);Check(!DicomCatalog.ReadEntry(Save(d,"badspacing.dcm")).HasGeometry,"negative spacing");}
 static void Tags(){var d=Make(0);d.Add(new DicomSequence(DicomTag.ReferencedStudySequence,new DicomDataset(new DicomSequence(DicomTag.ReferencedSeriesSequence,new DicomDataset(new DicomLongString(DicomTag.SeriesDescription,"nested sentinel"))))));var rows=TagReader.Read(d);Check(rows.Any(x=>x.Value=="nested sentinel"&&x.Path.Contains("[0]")),"nested sequence leaf");Check(rows.Any(x=>x.Tag==DicomTag.PixelData.ToString()&&x.Value.Contains("omitted")),"pixel suppression");}
 static void Endian(){var d=Make(0);var big=d.Clone(DicomTransferSyntax.ExplicitVRBigEndian);var p=PixelPlane.Load(DicomCatalog.ReadEntry(Save(big,"big-endian.dcm")));Check(p.Values.SequenceEqual(new float[]{-1026,-1024,-1022,3070}),"big endian scalar16");
  d=Make(0);d.AddOrUpdate(DicomTag.BitsStored,(ushort)12);d.AddOrUpdate(DicomTag.HighBit,(ushort)15);var pixels=DicomPixelData.Create(d,true);pixels.AddFrame(new MemoryByteBuffer(new byte[]{0xf0,0xff,0,0,0x10,0,0xf0,0x7f}));p=PixelPlane.Load(new DicomEntry{Dataset=d});Check(p.Values.SequenceEqual(new float[]{-1026,-1024,-1022,3070}),"shifted high bit");
  d=Make(0);d.AddOrUpdate(DicomTag.BitsAllocated,(ushort)32);d.AddOrUpdate(DicomTag.BitsStored,(ushort)32);d.AddOrUpdate(DicomTag.HighBit,(ushort)31);d.AddOrUpdate(DicomTag.PixelRepresentation,(ushort)0);d.AddOrUpdate(DicomTag.RescaleSlope,1.0);d.AddOrUpdate(DicomTag.RescaleIntercept,0.0);pixels=DicomPixelData.Create(d,true);var bytes=new byte[16];uint[] values={0,65537,0x12345678,0xffffffff};for(int i=0;i<4;i++)Array.Copy(BitConverter.GetBytes(values[i]),0,bytes,i*4,4);pixels.AddFrame(new MemoryByteBuffer(bytes));big=d.Clone(DicomTransferSyntax.ExplicitVRBigEndian);p=PixelPlane.Load(DicomCatalog.ReadEntry(Save(big,"big-endian32.dcm")));Check(p.Values.SequenceEqual(values.Select(x=>(float)x)),"big endian scalar32");
 }
 static void Boundaries(){var s=Stack(0,2,4);s.Entries[0].SpacingX=double.NaN;Reject(s);s=Stack(0,2,4);foreach(var e in s.Entries){e.AxisX=new Vec3(2,0,0);e.AxisY=new Vec3(0,0.5,0);}Reject(s);s=Stack(0,2,4);foreach(var e in s.Entries){e.Rows=65535;e.Columns=65535;}Reject(s);s=Stack(0,2,4);try{VolumeData.Load(s,new CancellationToken(true));throw new Exception("cancellation ignored");}catch(OperationCanceledException){} }
 static void Partitioning(){string dir=Path.Combine(folder,"partitions");Directory.CreateDirectory(dir);string path=null;for(int kind=0;kind<6;kind++)for(int slice=0;slice<2;slice++){
  var d=Make(slice*2);
  if(kind==1)d.AddOrUpdate(DicomTag.ImageOrientationPatient,0.0,1,0,-1,0,0);
  if(kind==2)d.AddOrUpdate(DicomTag.Columns,(ushort)4);
  if(kind==3)d.AddOrUpdate(DicomTag.PixelSpacing,1.0,1);
  if(kind==4)d.AddOrUpdate(DicomTag.EchoNumbers,9);
  if(kind==5)d.AddOrUpdate(DicomTag.TemporalPositionIdentifier,2);
  path=Path.Combine(dir,kind+"-"+slice+".dcm");new DicomFile(d).Save(path);
 }var c=DicomCatalog.Scan(path,CancellationToken.None);Check(c.Stacks.Count==6&&c.Stacks.All(x=>x.Entries.Count==2&&x.CanMpr),"orientation,size,spacing,echo,time partitions");}
 static void UnsupportedTransforms(){
  var d=Make(0);d.Add(new DicomSequence(DicomTag.ModalityLUTSequence,new DicomDataset()));RejectPixels(d);
  foreach(var tag in new[]{DicomTag.SharedFunctionalGroupsSequence,DicomTag.PerFrameFunctionalGroupsSequence}){
   d=Make(0);d.Add(new DicomSequence(tag,new DicomDataset(new DicomSequence(DicomTag.PixelValueTransformationSequence,new DicomDataset(new DicomDecimalString(DicomTag.RescaleSlope,3m))))));RejectPixels(d);
  }
 }
 static void RejectPixels(DicomDataset d){try{PixelPlane.Load(new DicomEntry{Dataset=d});throw new Exception("unsupported intensity transform ignored");}catch(NotSupportedException){}}
 static void Check(bool result,string name){if(!result)throw new Exception(name);}
}
