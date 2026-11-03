using Dicom;
using QuickLook.DicomRT;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

sealed partial class PreviewSession
{
    DoseGrid[] contextDoses=Array.Empty<DoseGrid>();
    StructureSet[] contextSets=Array.Empty<StructureSet>();
    Dictionary<StructureRoi,int> roiIds=new();
    static string InstanceKey(DicomEntry entry)=>entry==null?"":Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(entry.SopUid??entry.Path??"derived"))).Substring(0,20);
    DoseGrid DisplayDose=>dose??contextDoses.FirstOrDefault();
    int DoseId(DoseGrid value)=>value==null?-1:value==sumResult?.Dose?-2:doses.IndexOf(value);
    DoseGrid RequestDose(JsonElement request)
    {
        if(!request.TryGetProperty("doseId",out var id))return DisplayDose;
        int i=id.GetInt32();if(i==-1&&contextDoses.Length==0)return null;var value=i==-2?sumResult?.Dose:i>=0&&i<doses.Count?doses[i]:null;
        if(value==null||!contextDoses.Contains(value))throw new ArgumentException("Dose is outside selected context");return value;
    }
    void IndexRois()=>roiIds=structures.SelectMany(s=>s.Rois).Distinct().Select((roi,i)=>(roi,id:i+1)).ToDictionary(x=>x.roi,x=>x.id);
    void ResolveContext(DoseGrid exact=null)
    {
        if(sumResult?.Dose!=null){contextSets=sumSets.ToArray();contextDoses=new[]{sumResult.Dose};return;}
        if(plan!=null){
            contextSets=structures.Where(s=>s.Entry.SopUid==plan.StructureSopUid).ToArray();structure=Unique(contextSets);
            contextDoses=exact!=null?new[]{exact}:doses.Where(d=>d.ReferencedPlanCount==1&&d.PlanUid==plan.Entry.SopUid).ToArray();
            dose=exact??Unique(contextDoses);return;
        }
        if(dose!=null){
            contextDoses=new[]{dose};var referenced=plans.FirstOrDefault(p=>p.Entry.SopUid==dose.PlanUid);
            contextSets=referenced!=null?structures.Where(s=>s.Entry.SopUid==referenced.StructureSopUid).ToArray():structures.Where(s=>s.Rois.Any(r=>Map(r.FrameUid,dose.FrameUid)!=null)).ToArray();
            if(structure!=null)contextSets=new[]{structure};structure=Unique(contextSets);return;
        }
        contextSets=structure==null?Array.Empty<StructureSet>():new[]{structure};
        var compatible=structure==null?Array.Empty<DoseGrid>():doses.Where(d=>structure.Rois.Any(r=>Map(r.FrameUid,d.FrameUid)!=null)).ToArray();
        contextDoses=compatible.Length==1?compatible:Array.Empty<DoseGrid>();dose=Unique(contextDoses);
    }
    object DoseModel(DoseGrid d)=>new{id=DoseId(d),key=InstanceKey(d.Entry),label=(DoseId(d)==-2?"Plansumme":"Dosis "+(DoseId(d)+1))+" · "+d.Label+" · "+(Num(d.Maximum)?.ToString("0.#",System.Globalization.CultureInfo.InvariantCulture)??"—")+" "+d.Units,units=d.Units,maximum=Num(d.Maximum),type=d.DoseType,registered=Map(Frame,d.FrameUid)!=null,maximumPosition=d.MaximumPosition.HasValue&&Map(d.FrameUid,Frame)!=null?V(Map(d.FrameUid,Frame).Transform(d.MaximumPosition.Value)):null};
    object TagSources()=>catalog.Files.Where(e=>e.Modality.StartsWith("RT")||e.Modality=="REG"||e.SopUid==opened.SopUid).Select(e=>new{id=catalog.Files.IndexOf(e),key=InstanceKey(e),label=e.Modality+" · "+(e.Description??""),opened=e.SopUid==opened.SopUid});
    object Tags(JsonElement request)
    {
        DicomEntry entry;
        if(request.TryGetProperty("sourceId",out var source)&&source.GetInt32()>=0){int i=source.GetInt32();if(i>=catalog.Files.Count)throw new ArgumentException();entry=catalog.Files[i];}
        else if(Flag(request,"source"))entry=opened;
        else if(request.TryGetProperty("index",out var index)&&stack!=null)entry=stack.Entries[Math.Clamp(index.GetInt32(),0,stack.Entries.Count-1)];
        else entry=image??opened;
        return TagReader.Read(entry.Dataset);
    }
    double[][] Isocenters()
    {
        var map=Map(plan?.FrameUid,Frame);var points=new List<Vec3>();if(map==null)return Array.Empty<double[]>();
        foreach(var b in plan.Beams)foreach(var cp in b.ControlPoints){var p=map.Transform(cp.Isocenter);if(Finite(p)&&points.All(q=>(q-p).Length>=.1))points.Add(p);}
        return points.Select(V).ToArray();
    }
    object Navigation(JsonElement r)
    {
        if(stack!=null&&Text(r,"plane","")=="Native"){
            int index=Math.Clamp(Int(r,"index",stack.Entries.IndexOf(image)),0,stack.Entries.Count-1);
            if(r.TryGetProperty("point",out var point)){var a=point.EnumerateArray().Select(x=>x.GetDouble()).ToArray();if(a.Length!=3||a.Any(x=>!double.IsFinite(x)))throw new ArgumentException();var p=new Vec3(a[0],a[1],a[2]);index=Enumerable.Range(0,stack.Entries.Count).OrderBy(i=>Math.Abs((stack.Entries[i].Origin-p).Dot(stack.Entries[i].AxisX.Cross(stack.Entries[i].AxisY)))).First();}
            image=stack.Entries[index];native=NativePlane(image);
        }
        return new{index=stack?.Entries.IndexOf(image)??0};
    }
}
