using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dicom;
using QuickLook.DicomRT;

// Read-only archive acceptance: no paths, identifiers, labels or exception text in output.
internal static class LinacArchiveAudit
{
    public static int Run(string root,bool planNamesOnly=false)
    {
        DicomRtDictionary.EnsureLoaded();
        var totals=new SortedDictionary<string,int>();var gate=new object();
        Action<string,int> add=(key,n)=>{lock(gate){if(!totals.ContainsKey(key))totals[key]=0;totals[key]+=n;}};
        var folders=new Stack<string>();folders.Push(root);int dirs=0;
        while(folders.Count>0){
            var dir=folders.Pop();string[] files;
            try{foreach(var child in Directory.GetDirectories(dir))if((File.GetAttributes(child)&FileAttributes.ReparsePoint)==0)folders.Push(child);files=Directory.GetFiles(dir);}
            catch(IOException){add("directory_unreadable",1);continue;}
            catch(UnauthorizedAccessException){add("directory_unreadable",1);continue;}
            // Every DICOM/extensionless file is inspected; filename-based RP priority is only an ordering hint.
            Parallel.ForEach(files.Where(p=>planNamesOnly?
                Path.GetFileName(p).StartsWith("RP",StringComparison.OrdinalIgnoreCase)||Path.GetFileName(p).IndexOf("RTPLAN",StringComparison.OrdinalIgnoreCase)>=0:
                string.IsNullOrEmpty(Path.GetExtension(p))||Path.GetExtension(p).Equals(".dcm",StringComparison.OrdinalIgnoreCase)||Path.GetFileName(p).StartsWith("RP",StringComparison.OrdinalIgnoreCase)),new ParallelOptions{MaxDegreeOfParallelism=4},path=>{
                add("headers",1);
                DicomDataset header;
                try{
                    // One bounded network read avoids hundreds of tiny SMB reads per image header.
                    var bytes=new byte[16384];int count;
                    using(var file=File.OpenRead(path)){count=file.Read(bytes,0,bytes.Length);}
                    using(var memory=new MemoryStream(bytes,0,count))header=DicomFile.Open(memory,Encoding.UTF8,s=>s.SequenceDepth==0&&s.Tag.CompareTo(new DicomTag(0x0008,0x0070))>=0,FileReadOption.ReadLargeOnDemand)?.Dataset;
                }
                catch{add("header_unreadable",1);return;}
                if(header==null)return;
                var modality=header.GetSingleValueOrDefault(DicomTag.Modality,"");
                if(modality!="RTPLAN"&&modality!="RTIONPLAN")return;
                if(modality=="RTIONPLAN"){add("unsupported_ion_plan",1);return;}
                add("plans_found",1);
                try{
                    var entry=DicomCatalog.ReadEntry(path);var raw=entry.Dataset;
                    var plan=PlanData.Load(entry);add("plans_parsed",1);
                    if(plan.Beams.Count==0){add("plans_without_external_beams",1);return;}
                    foreach(var b in plan.Beams){
                        add("beams",1);add("form_"+BeamMachineInfo.Form(b),1);
                        // Only allowlisted model families leave this process; unknown raw metadata stays private.
                        string m=(b.ManufacturerModelName??"").ToUpperInvariant();
                        string family=new[]{"HALCYON","ETHOS","ACCELA","TRUEBEAM","CLINAC","VERSA","SYNERGY","ELEKTA","TOMOTHERAPY","TOMO","ARTISTE","PRIMUS","ONCOR","NOVALIS","CYBERKNIFE","UNITY"}.FirstOrDefault(t=>m.Contains(t))??"unspecified_or_other";
                        add("model_"+family,1);
                        add("layers_"+(b.ControlPoints.FirstOrDefault()?.MlcLayers.Count??0),1);
                        if((b.TreatmentMachineName??"").Trim().StartsWith("Hal",StringComparison.OrdinalIgnoreCase))add("local_Hal_prefix",1);
                        foreach(var cp in b.ControlPoints){
                            add("control_points",1);string reason;
                            var projection=BeamProjection.Create(b,cp,Matrix4.Identity,out reason);
                            add(projection==null?"projection_unavailable":"projection_valid",1);
                            if(projection==null){
                                // Production reason strings are fixed messages, but classify rather than print them.
                                if(!BeamProjection.Finite(b.SourceAxisDistance)||b.SourceAxisDistance<=0)add("geometry_missing_SAD",1);
                                else if(string.IsNullOrEmpty(b.PatientPosition))add("geometry_missing_patient_position",1);
                                else add("geometry_other_unsupported",1);
                            }
                            var rectangles=BeamAperture.Rectangles(cp);
                            if(rectangles.Any(r=>!BeamProjection.Finite(r.Left)||!BeamProjection.Finite(r.Right)||!BeamProjection.Finite(r.Top)||!BeamProjection.Finite(r.Bottom)))throw new ArithmeticException();
                        }
                    }
                }catch(Exception ex){add("plan_failure_"+ex.GetType().Name,1);
                    var known=new[]{"Invalid MLC leaf boundaries.","Nonfinite aperture coordinates.","Invalid X jaw positions.","Invalid Y jaw positions.","Ambiguous or undefined MLC layer update.","Invalid MLC positions.","Missing initial MLC layer positions.","Unsupported or missing block contour.","Degenerate block contour.","Block contour cannot be triangulated."};
                    int code=Array.IndexOf(known,ex.Message);add("failure_reason_code_"+code,1);
                }
            });
            if(++dirs%50==0){lock(gate)Console.WriteLine("PROGRESS folders="+dirs+" headers="+(totals.ContainsKey("headers")?totals["headers"]:0)+" plans="+(totals.ContainsKey("plans_found")?totals["plans_found"]:0));}
        }
        Console.WriteLine("scope="+(planNamesOnly?"RP-prefix and RTPLAN-name candidates; archives excluded":"DICOM and extensionless headers; archives excluded"));
        foreach(var p in totals)Console.WriteLine(p.Key+"="+p.Value);
        return totals.Keys.Any(k=>k.StartsWith("plan_failure_"))?1:0;
    }
}
