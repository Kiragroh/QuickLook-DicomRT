using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dicom;
using QuickLook.DicomRT;
internal static class RoiArchiveAudit
{
    public static int Run(string root)
    {
        var candidates=new List<string>();var folders=new Stack<string>();folders.Push(root);
        int unreadable=0;
        while(folders.Count>0){var dir=folders.Pop();try{
            foreach(var child in Directory.GetDirectories(dir))if((File.GetAttributes(child)&FileAttributes.ReparsePoint)==0)folders.Push(child);
            candidates.AddRange(Directory.GetFiles(dir).Where(p=>Path.GetFileName(p).StartsWith("RS",StringComparison.OrdinalIgnoreCase)||Path.GetFileName(p).IndexOf("RTSTRUCT",StringComparison.OrdinalIgnoreCase)>=0));
        }catch(IOException){unreadable++;}catch(UnauthorizedAccessException){unreadable++;}}
        candidates.Sort(StringComparer.OrdinalIgnoreCase);
        var counts=new SortedDictionary<string,int>();Action<string> add=key=>{if(!counts.ContainsKey(key))counts[key]=0;counts[key]++;};
        int sample=Math.Min(32,candidates.Count);
        for(int i=0;i<sample;i++)try{
            var path=candidates[(int)((long)i*candidates.Count/sample)];var d=DicomFile.Open(path,FileReadOption.ReadLargeOnDemand).Dataset;
            if(d.GetSingleValueOrDefault(DicomTag.Modality,"")!="RTSTRUCT"){add("not_structure");continue;}
            // Metadata only: geometry, patient identifiers and free-text names never enter the report.
            var types=new Dictionary<int,string>();DicomSequence seq;
            if(d.TryGetSequence(DicomTag.RTROIObservationsSequence,out seq))foreach(var row in seq.Items)types[row.GetSingleValueOrDefault(DicomTag.ReferencedROINumber,-1)]=row.GetSingleValueOrDefault(DicomTag.RTROIInterpretedType,"");
            add("structures");if(!d.TryGetSequence(DicomTag.StructureSetROISequence,out seq))continue;
            foreach(var row in seq.Items){string type;types.TryGetValue(row.GetSingleValueOrDefault(DicomTag.ROINumber,-1),out type);
                var roi=new StructureRoi{Name=row.GetSingleValueOrDefault(DicomTag.ROIName,""),InterpretedType=type};var category=RoiClassification.Category(roi);add("rois");add("category_"+category);
                if(category==RoiCategory.Organ&&!string.Equals(type,"ORGAN",StringComparison.OrdinalIgnoreCase))add("organ_name_fallback");
                if(category==RoiCategory.Target&&!new[]{"PTV","CTV","GTV","ITV","TREATED_VOLUME"}.Contains(type))add("target_name_fallback");
            }
        }catch{add("read_failure");}
        Console.WriteLine("scope=32 evenly spaced RS/RTSTRUCT-name candidates maximum; no plan-reference inference in this audit");
        Console.WriteLine("candidate_files="+candidates.Count+" unreadable_directories="+unreadable);
        foreach(var p in counts)Console.WriteLine(p.Key+"="+p.Value);
        return counts.ContainsKey("read_failure")?1:0;
    }
}
