using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using Dicom;

namespace QuickLook.DicomRT
{
    public sealed class PlanDicomSelection
    {
        public readonly Dictionary<string,DicomEntry[]> Parts=new Dictionary<string,DicomEntry[]>();
        public string Note;
    }
    public static class PlanDicomExport
    {
        static string[] References(DicomEntry entry,DicomTag sequence)=>RtItems(entry.Dataset,sequence).Select(d=>d.GetSingleValueOrDefault<string>(DicomTag.ReferencedSOPInstanceUID,"")).Where(s=>s.Length>0).Distinct().ToArray();
        static IEnumerable<DicomDataset> RtItems(DicomDataset dataset,DicomTag tag)=>dataset!=null&&dataset.Contains(tag)?dataset.GetSequence(tag).Items:Enumerable.Empty<DicomDataset>();
        static bool SamePatient(DicomEntry a,DicomEntry b)=>string.IsNullOrWhiteSpace(a.PatientKey)||a.PatientKey=="|"||string.IsNullOrWhiteSpace(b.PatientKey)||b.PatientKey=="|"||a.PatientKey==b.PatientKey;
        public static PlanDicomSelection Resolve(PlanData plan,IEnumerable<DicomEntry> discovered)
        {
            if(plan?.Entry==null||string.IsNullOrEmpty(plan.Entry.SopUid))throw new InvalidOperationException("Select an original RTPLAN first.");
            var files=discovered.Concat(new[]{plan.Entry}).Where(e=>e!=null&&!string.IsNullOrEmpty(e.SopUid)&&SamePatient(e,plan.Entry)).GroupBy(e=>e.SopUid).Select(g=>g.First()).ToArray();
            var sets=files.Where(e=>e.Modality=="RTSTRUCT"&&e.SopUid==plan.StructureSopUid).ToArray();
            var series=new HashSet<string>();var images=new HashSet<string>();
            foreach(var entry in sets)
            {
                var set=StructureSet.Load(entry);series.UnionWith(set.ReferencedSeries);images.UnionWith(set.Rois.SelectMany(r=>r.Contours).SelectMany(c=>c.ReferencedSops));
            }
            var doses=files.Where(e=>e.Modality=="RTDOSE").Where(e=>{var refs=References(e,DicomTag.ReferencedRTPlanSequence);return refs.Length==1&&refs[0]==plan.Entry.SopUid;}).ToArray();
            var result=new PlanDicomSelection();
            result.Parts["CT"]=files.Where(e=>e.Modality=="CT"&&(series.Contains(e.SeriesUid)||images.Contains(e.SopUid))).ToArray();
            result.Parts["RD"]=doses;result.Parts["RS"]=sets;result.Parts["RP"]=new[]{plan.Entry};
            result.Note="Only discovered files with explicit DICOM references are included. Original files retain patient information."
                +(sets.Length==0?" Referenced structure set not found; CT association unavailable.":result.Parts["CT"].Length==0?" Referenced CT not found; use Search more images if needed.":"");
            return result;
        }
        public static void WriteZip(PlanDicomSelection selection,IEnumerable<string> parts,string destination,CancellationToken token,Action<int,int> progress=null)
        {
            var chosen=parts.Distinct().Where(selection.Parts.ContainsKey).SelectMany(k=>selection.Parts[k].Select(e=>new{Kind=k,Entry=e})).ToArray();
            if(chosen.Length==0)throw new InvalidOperationException("Select at least one available component.");
            string target=Path.GetFullPath(destination),temp=target+"."+Guid.NewGuid().ToString("N")+".partial";
            if(chosen.Any(x=>string.Equals(Path.GetFullPath(x.Entry.Path),target,StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("The destination cannot replace a source DICOM file.");
            try
            {
                using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                using(var zip=new ZipArchive(stream,ZipArchiveMode.Create))
                {
                    int done=0;var buffer=new byte[131072];
                    foreach(var item in chosen)
                    {
                        token.ThrowIfCancellationRequested();
                        var current=DicomCatalog.ReadEntry(item.Entry.Path);
                        if(current.SopUid!=item.Entry.SopUid||current.Modality!=item.Entry.Modality||!SamePatient(current,item.Entry))throw new IOException("A source file changed; reopen the export dialog.");
                        var entry=zip.CreateEntry(item.Kind+"/"+item.Kind+"_"+(done+1).ToString("D5")+".dcm",CompressionLevel.Fastest);
                        using(var input=new FileStream(item.Entry.Path,FileMode.Open,FileAccess.Read,FileShare.Read))
                        using(var output=entry.Open()){int n;while((n=input.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();output.Write(buffer,0,n);}}
                        progress?.Invoke(++done,chosen.Length);
                    }
                }
                token.ThrowIfCancellationRequested();
                if(File.Exists(target))File.Replace(temp,target,null);else File.Move(temp,target);
            }
            finally{if(File.Exists(temp))File.Delete(temp);}
        }
    }
}
