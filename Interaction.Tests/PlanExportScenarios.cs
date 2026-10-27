using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using Dicom;
using QuickLook.DicomRT;

internal static class PlanExportScenarios
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static DicomEntry Entry(string dir,string modality,string uid,string series="1.2.3.900")
    {
        var cls=modality=="CT"?DicomUID.CTImageStorage:modality=="RTPLAN"?DicomUID.RTPlanStorage:modality=="RTSTRUCT"?DicomUID.RTStructureSetStorage:DicomUID.RTDoseStorage;
        var d=new DicomDataset();d.Add(DicomTag.SOPClassUID,cls);d.Add(DicomTag.SOPInstanceUID,uid);d.Add(DicomTag.SeriesInstanceUID,series);d.Add(DicomTag.Modality,modality);d.Add(DicomTag.PatientID,"EXPORT-TEST");
        var path=Path.Combine(dir,uid+".dcm");new DicomFile(d).Save(path);return DicomCatalog.ReadEntry(path);
    }
    static DicomDataset Reference(string uid){var d=new DicomDataset();d.Add(DicomTag.ReferencedSOPInstanceUID,uid);return d;}
    static void Save(DicomEntry e){new DicomFile(e.Dataset).Save(e.Path);}
    public static void Run(Action<bool,string> check)
    {
        string root=Path.Combine(Path.GetTempPath(),"dicom-plan-export-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var rp=Entry(root,"RTPLAN","1.2.3.1");var rs=Entry(root,"RTSTRUCT","1.2.3.2");var ct=Entry(root,"CT","1.2.3.3");var otherCt=Entry(root,"CT","1.2.3.4","1.2.3.901");var rd=Entry(root,"RTDOSE","1.2.3.5");var otherRd=Entry(root,"RTDOSE","1.2.3.6");var sum=Entry(root,"RTDOSE","1.2.3.7");
            var series=new DicomDataset();series.Add(DicomTag.SeriesInstanceUID,ct.SeriesUid);var study=new DicomDataset();study.Add(new DicomSequence(DicomTag.RTReferencedSeriesSequence,series));var frame=new DicomDataset();frame.Add(new DicomSequence(DicomTag.RTReferencedStudySequence,study));rs.Dataset.Add(new DicomSequence(DicomTag.ReferencedFrameOfReferenceSequence,frame));Save(rs);
            rp.Dataset.Add(new DicomSequence(DicomTag.ReferencedStructureSetSequence,Reference(rs.SopUid)));Save(rp);
            rd.Dataset.Add(new DicomSequence(DicomTag.ReferencedRTPlanSequence,Reference(rp.SopUid)));Save(rd);
            otherRd.Dataset.Add(new DicomSequence(DicomTag.ReferencedRTPlanSequence,Reference("1.2.3.99")));Save(otherRd);
            sum.Dataset.Add(new DicomSequence(DicomTag.ReferencedRTPlanSequence,Reference(rp.SopUid),Reference("1.2.3.99")));Save(sum);
            var files=new[]{rp,rs,ct,otherCt,rd,otherRd,sum};var plan=PlanData.Load(rp);var selected=PlanDicomExport.Resolve(plan,files);
            check(selected.Parts["CT"].Single()==ct&&selected.Parts["RS"].Single()==rs&&selected.Parts["RD"].Single()==rd,"ZIP association excludes other series, other plans and combined-plan dose");
            string zipPath=Path.Combine(root,"selected.zip");PlanDicomExport.WriteZip(selected,new[]{"CT","RD","RS","RP"},zipPath,CancellationToken.None);
            using(var archive=ZipFile.OpenRead(zipPath))
            {
                check(archive.Entries.Count==4,"ZIP contains only selected associated instances");
                foreach(var item in selected.Parts){using(var stream=archive.Entries.Single(e=>e.FullName.StartsWith(item.Key+"/")).Open())using(var copy=new MemoryStream()){stream.CopyTo(copy);check(copy.ToArray().SequenceEqual(File.ReadAllBytes(item.Value[0].Path)),"Export preserves original bytes: "+item.Key);}}
            }
            PlanDicomExport.WriteZip(selected,new[]{"RP"},zipPath,CancellationToken.None);using(var zip=ZipFile.OpenRead(zipPath))check(zip.Entries.Count==1&&zip.Entries[0].FullName.StartsWith("RP/"),"Component selection and atomic replacement work");
            byte[] prior=File.ReadAllBytes(zipPath);bool cancelled=false;try{PlanDicomExport.WriteZip(selected,new[]{"CT"},zipPath,new CancellationToken(true));}catch(OperationCanceledException){cancelled=true;}
            check(cancelled&&prior.SequenceEqual(File.ReadAllBytes(zipPath))&&!Directory.GetFiles(root,"*.partial").Any(),"Cancellation preserves previous ZIP and removes partial output");
            using(var cts=new CancellationTokenSource()){cancelled=false;try{PlanDicomExport.WriteZip(selected,new[]{"CT","RD","RS","RP"},zipPath,cts.Token,(done,total)=>cts.Cancel());}catch(OperationCanceledException){cancelled=true;}check(cancelled&&prior.SequenceEqual(File.ReadAllBytes(zipPath))&&!Directory.GetFiles(root,"*.partial").Any(),"Cancelling after partial progress preserves existing archive");}
            var missing=PlanDicomExport.Resolve(plan,new[]{rp,ct,rd});check(missing.Parts["CT"].Length==0&&missing.Parts["RS"].Length==0&&missing.Parts["RD"].Length==1,"Missing RS never guesses a CT association");
            bool protectedSource=false;try{PlanDicomExport.WriteZip(selected,new[]{"RP"},rp.Path,CancellationToken.None);}catch(InvalidOperationException){protectedSource=true;}check(protectedSource,"Export cannot overwrite a source instance");
            rd.Dataset.AddOrUpdate(DicomTag.SOPInstanceUID,"1.2.3.999");Save(rd);bool changed=false;try{PlanDicomExport.WriteZip(selected,new[]{"RD"},zipPath,CancellationToken.None);}catch(IOException){changed=true;}check(changed&&prior.SequenceEqual(File.ReadAllBytes(zipPath)),"Changed source identity fails without replacing destination");
            TestRotation(check);
        }
        finally{Directory.Delete(root,true);}
    }
    static void TestRotation(Action<bool,string> check)
    {
        var type=typeof(ViewerControl).Assembly.GetType("QuickLook.DicomRT.MlcAperture");var aperture=(FrameworkElement)Activator.CreateInstance(type,true);
        var a=new ControlPoint{Collimator=0,CollimatorRotationDirection="CC",XJaws=new[]{-40d,20d},YJaws=new[]{-15d,30d}};var b=new ControlPoint{Collimator=90,XJaws=a.XJaws,YJaws=a.YJaws};
        type.GetMethod("Set",Flags).Invoke(aperture,new object[]{a,b,.5});check(Math.Abs((double)type.GetProperty("DisplayAngle",Flags).GetValue(aperture)+45)<1e-8,"Dynamic collimator interpolates display angle");
        var beam=new PlanBeam{PatientPosition="HFS",SourceAxisDistance=1000};string reason;
        var zero=BeamProjection.Create(beam,a,Matrix4.Identity,out reason);var rotated=BeamProjection.Create(beam,b,Matrix4.Identity,out reason);double x,y;rotated.Project(zero.PlanePoint(20,10),out x,out y);
        type.GetMethod("Set",Flags).Invoke(aperture,new object[]{b,b,0d});var angle=(double)type.GetProperty("DisplayAngle",Flags).GetValue(aperture);var onScreen=new RotateTransform(angle).Transform(new Point(x,-y));
        check(Math.Abs(onScreen.X-20)<1e-8&&Math.Abs(onScreen.Y+10)<1e-8,"DRR/ROI projection stays aligned in fixed source view at 90 degrees");
        type.GetProperty("CenterCollimator",Flags).SetValue(aperture,true);check((double)type.GetProperty("DisplayAngle",Flags).GetValue(aperture)==0,"Center collimator restores local leaf axes");
        type.GetProperty("CenterCollimator",Flags).SetValue(aperture,false);a.Collimator=350;b.Collimator=10;type.GetMethod("Set",Flags).Invoke(aperture,new object[]{a,b,.5});angle=(double)type.GetProperty("DisplayAngle",Flags).GetValue(aperture);check(Math.Abs(Math.Sin(angle*Math.PI/180))<1e-8&&Math.Cos(angle*Math.PI/180)>.999,"Collimator rotation interpolates across zero without a jump");
        aperture.Measure(new Size(600,600));aperture.Arrange(new Rect(0,0,600,600));aperture.UpdateLayout();var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(600,600,96,96,PixelFormats.Pbgra32);bitmap.Render(aperture);check(bitmap.PixelWidth==600,"Rotated aperture renders successfully");
        using(var control=new MlcPlaybackControl()){var checkbox=(System.Windows.Controls.CheckBox)typeof(MlcPlaybackControl).GetField("centerCollimator",Flags).GetValue(control);var shown=typeof(MlcPlaybackControl).GetField("aperture",Flags).GetValue(control);check(checkbox.IsChecked==false,"MLC starts with visible collimator rotation");checkbox.IsChecked=true;check((bool)type.GetProperty("CenterCollimator",Flags).GetValue(shown),"Center checkbox updates the visible aperture");checkbox.IsChecked=false;check(!(bool)type.GetProperty("CenterCollimator",Flags).GetValue(shown),"Unchecking center restores rotation");}
    }
    public static int Inspect(string folder)
    {
        string temp=Path.Combine(Path.GetTempPath(),"dicom-plan-acceptance-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
        try
        {
            var entries=Directory.EnumerateFiles(folder).Select(DicomCatalog.ReadEntry).ToArray();int index=0;
            foreach(var plan in entries.Where(e=>e.Modality=="RTPLAN").Select(PlanData.Load))
            {
                var selection=PlanDicomExport.Resolve(plan,entries);string path=Path.Combine(temp,(++index)+".zip");PlanDicomExport.WriteZip(selection,new[]{"CT","RD","RS","RP"},path,CancellationToken.None);
                using(var archive=ZipFile.OpenRead(path))foreach(var part in selection.Parts)foreach(var pair in part.Value.Select((e,i)=>new{Entry=e,Index=i}))
                {
                    var zipped=archive.Entries.Where(e=>e.FullName.StartsWith(part.Key+"/")).ElementAt(pair.Index);using(var s=zipped.Open())using(var bytes=new MemoryStream()){s.CopyTo(bytes);if(!bytes.ToArray().SequenceEqual(File.ReadAllBytes(pair.Entry.Path)))throw new Exception("Byte verification failed");}
                }
                Console.WriteLine("Plan "+index+": beams="+plan.Beams.Count+", dynamic collimator="+plan.Beams.Count(b=>b.DynamicCollimator)+", "+string.Join(", ",selection.Parts.Select(p=>p.Key+"="+p.Value.Length))+"; ZIP original bytes verified.");
            }
            return index>0?0:1;
        }
        catch(Exception e){Console.WriteLine("Acceptance failed: "+e.GetType().Name);return 1;}
        finally{Directory.Delete(temp,true);}
    }
}
