using QuickLook.DicomRT;
using System.Collections.Concurrent;
using Dicom;
using System.Text;
using System.Text.Json;
using System.Globalization;

sealed partial class PreviewSession
{
    ConcurrentDictionary<string,(ThreeDMeshData mesh,string reason)> meshCache=new();
    ConcurrentDictionary<string,object> projectionCache=new();
    VolumeData overlayVolume;ImageStack overlayStack;
    double overlayLevel=40,overlayWidth=400;
    DoseSumResult sumResult;
    readonly List<StructureSet> sumSets=new();
    object Mesh(JsonElement r)
    {
        string kind=Text(r,"kind","roi");int id=Int(r,"roi",-1);double level=Real(r,"level",dose?.Maximum*.5??0);
        string key=CacheKey(kind+":"+id+":"+level.ToString("R",CultureInfo.InvariantCulture));
        if(!meshCache.TryGetValue(key,out var saved)) {
            ThreeDMeshData mesh=null;string reason="";
            if(kind=="roi") {
                var roi=ActiveRois().Single(x=>RoiKey(x)==id);var map=Map(roi.FrameUid,Frame);
                if(map!=null) {
                    var original=OriginalMesh(roi);reason=original.reason;
                    if(original.mesh!=null){mesh=new ThreeDMeshData();mesh.Points.AddRange(original.mesh.Points.Select(map.Transform));mesh.Indices.AddRange(original.mesh.Indices);var zero=map.Transform(new Vec3());mesh.Normals.AddRange(original.mesh.Normals.Select(n=>(map.Transform(n)-zero).Normalized()));}
                }
            } else if(kind=="skin"||kind=="bone") {
                if(volume!=null&&stack.Modality=="CT")mesh=AdaptiveSurface(volume,volume.Sample,kind=="skin"?-350:300,500000,out reason);
                else reason="CT-HU-Daten erforderlich";
            } else if(kind=="dose"&&dose!=null) {
                var map=Map(dose.FrameUid,Frame);if(map!=null) {
                    var bounds=dose.SamplingBounds;mesh=AdaptiveSurface(bounds,dose.Sample,level,500000,out reason);
                    if(mesh!=null)for(int i=0;i<mesh.Points.Count;i++)mesh.Points[i]=map.Transform(mesh.Points[i]);
                }
            }
            if(meshCache.Count>=96||meshCache.Values.Sum(x=>(long)(x.mesh?.Indices.Count??0)/3)+(mesh?.Indices.Count??0)/3>10000000)meshCache.Clear();
            saved=(mesh,reason);meshCache[key]=saved;
        }
        if(saved.mesh==null)return new{available=false,reason=saved.reason};
        var m=saved.mesh;
        return new{available=true,kind,id,reason=saved.reason,triangles=m.Indices.Count/3,
            positions=Encode(m.Points.SelectMany(p=>new[]{(float)p.X,(float)p.Y,(float)p.Z}).ToArray()),
            normals=m.Normals.Count==m.Points.Count?Encode(m.Normals.SelectMany(p=>new[]{(float)p.X,(float)p.Y,(float)p.Z}).ToArray()):null,
            indices=Encode(m.Indices.ToArray())};
    }
    ThreeDMeshData AdaptiveSurface(VolumeData bounds,Func<Vec3,float> sample,double level,int budget,out string reason)
    {
        reason="";
        foreach(int resolution in new[]{160,128,96,80,64}) {
            token.ThrowIfCancellationRequested();
            try {var m=ThreeDGeometry.Isosurface(bounds,sample,level,resolution,token);if(m.Indices.Count/3>budget)continue;
                reason=resolution<160?"Auf Vorschaubudget begrenzte Oberfläche":"";return ThreeDGeometry.Smooth(m,.5,token);
            }catch(InvalidOperationException){}
        }
        reason="Oberfläche überschreitet das Vorschau-Budget";return null;
    }
    static string Encode(Array array){var bytes=new byte[Buffer.ByteLength(array)];Buffer.BlockCopy(array,0,bytes,0,bytes.Length);return Convert.ToBase64String(bytes);}
    object Fusion(JsonElement r)
    {
        int index=Int(r,"series",-1);overlayVolume=null;overlayStack=null;
        if(index>=0) {
            var candidate=catalog.Stacks[index];
            if(candidate==stack||!candidate.CanMpr||Map(Frame,candidate.FrameUid)==null)throw new NotSupportedException();
            overlayVolume=VolumeData.Load(candidate,token);overlayStack=candidate;
            var entry=candidate.Entries[0];overlayLevel=double.IsFinite(entry.WindowCenter)?entry.WindowCenter:overlayVolume.Min/2d+overlayVolume.Max/2d;
            overlayWidth=double.IsFinite(entry.WindowWidth)&&entry.WindowWidth>0?entry.WindowWidth:Math.Max(1,overlayVolume.Max-overlayVolume.Min);
        }
        return new{available=overlayVolume!=null,level=overlayLevel,width=overlayWidth};
    }
    object Sum(JsonElement r)
    {
        var ids=r.GetProperty("doses").EnumerateArray().Select(x=>x.GetInt32()).Distinct().ToArray();
        if(ids.Length<2)throw new NotSupportedException();
        var sources=ids.Select(i=>doses[i]).ToArray();
        sumResult=DoseSum.Calculate(sources,links,token);
        if(sumResult.Dose==null||sumResult.IncludedCount<2)throw new NotSupportedException();
        dose=sumResult.Dose;dose.Label="Σ "+string.Join(" + ",plans.Where(p=>sumResult.IncludedPlanUids.Contains(p.Entry.SopUid)).Select(p=>p.Label));
        sumSets.Clear();sumSets.AddRange(structures.Where(s=>plans.Any(p=>sumResult.IncludedPlanUids.Contains(p.Entry.SopUid)&&p.StructureSopUid==s.Entry.SopUid)));
        plan=null;structure=null;dvhCache.Clear();LoadStack(ChooseStack());return Model();
    }
    object Discover(JsonElement r)
    {
        var oldPlan=plan?.Entry.SopUid;var oldDose=dose?.Entry?.SopUid;var oldStructure=structure?.Entry.SopUid;
        var found=DicomCatalog.Scan(opened.Path,token,searchRoot:r.GetProperty("root").GetString(),recursive:true);
        var same=found.Files.Where(e=>DicomPatientScope.Matches(opened,e)).ToArray();
        foreach(var entry in same.Where(e=>catalog.Files.All(o=>o.SopUid!=e.SopUid))) {
            catalog.Files.Add(entry);
            try {if(entry.Modality=="RTPLAN")plans.Add(PlanData.Load(entry));else if(entry.Modality=="RTSTRUCT")structures.Add(StructureSet.Load(entry));else if(entry.Modality=="RTDOSE")doses.Add(DoseGrid.Load(entry));}catch {failures++;}
        }
        catalog.Stacks=catalog.Stacks.Concat(found.Stacks.Where(s=>s.Entries.All(e=>same.Contains(e)))).GroupBy(s=>s.Key).Select(g=>g.OrderByDescending(s=>s.Entries.Count).First()).ToList();
        links=RegistrationReader.Read(catalog);sumResult=null;sumSets.Clear();
        plan=plans.FirstOrDefault(p=>p.Entry.SopUid==oldPlan);structure=structures.FirstOrDefault(s=>s.Entry.SopUid==oldStructure);dose=doses.FirstOrDefault(d=>d.Entry.SopUid==oldDose);
        if(plan==null&&opened.Modality!="RTDOSE")plan=Unique(plans.Where(p=>structures.Any(s=>s.Entry.SopUid==p.StructureSopUid&&s.ReferencedSeries.Contains(opened.SeriesUid))));
        if(plan!=null)Associate();LoadStack(ChooseStack());return Model();
    }
    object ExportInfo()
    {
        var selected=PlanDicomExport.Resolve(plan,catalog.Files);
        return new{parts=selected.Parts.ToDictionary(x=>x.Key,x=>x.Value.Length),note=selected.Note};
    }
    object Export(JsonElement r)
    {
        string kind=Text(r,"kind","zip"),destination=r.GetProperty("destination").GetString();
        if(kind=="zip") {var selected=PlanDicomExport.Resolve(plan,catalog.Files);PlanDicomExport.WriteZip(selected,r.GetProperty("parts").EnumerateArray().Select(x=>x.GetString()),destination,token);}
        else if(kind=="csv") {
            bool simple=Flag(r,"simple");var ids=r.GetProperty("rois").EnumerateArray().Select(x=>x.GetInt32()).ToHashSet();
            var text=new StringBuilder(simple?"Structure,Dose,DoseUnit,VolumePercent\r\n":"PatientID,PlanID,Structure,ROIType,Dose,DoseUnit,CumulativeVolumePercent,EstimatedVolumeCc,CoverageFraction,SamplingStepMm,Status,Note,Dmean,Dmedian,Dmax,Dmin,D98,D2\r\n");
            string Q(string s)=>"\""+(s??"").Replace("\"","\"\"")+"\"";
            string N(JsonElement o,string key)=>o.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.Number?v.GetDouble().ToString("0.####",CultureInfo.InvariantCulture):"";
            foreach(var roi in ActiveRois().Where(roi=>ids.Contains(RoiKey(roi)))) {
                int id=RoiKey(roi);if(!dvhCache.TryGetValue(CacheKey(id),out var value))continue;
                var data=JsonSerializer.SerializeToElement(value);var xs=data.GetProperty("x").EnumerateArray().ToArray();var ys=data.GetProperty("y").EnumerateArray().ToArray();
                for(int i=0;i<Math.Max(1,xs.Length);i++) {
                    string x=i<xs.Length?xs[i].GetDouble().ToString("0.####",CultureInfo.InvariantCulture):"",y=i<ys.Length?ys[i].GetDouble().ToString("0.####",CultureInfo.InvariantCulture):"";
                    var cells=simple?new[]{Q(roi.Name),x,Q(dose?.Units),y}:new[]{Q(DicomCatalog.Text(opened.Dataset,DicomTag.PatientID)),Q(plan?.Label??dose?.Label),Q(roi.Name),Q(roi.InterpretedType),x,Q(dose?.Units),y,N(data,"volume"),N(data,"coverage"),N(data,"sampling"),Q(data.GetProperty("status").GetString()),Q("Approximate preview; "+data.GetProperty("message").GetString()),N(data,"mean"),N(data,"median"),N(data,"max"),N(data,"min"),N(data,"d98"),N(data,"d2")};
                    text.AppendLine(string.Join(",",cells));
                }
            }
            if(catalog.Files.Any(e=>Path.GetFullPath(e.Path)==Path.GetFullPath(destination)))throw new IOException();
            File.WriteAllText(destination,text.ToString(),new UTF8Encoding(true));
        } else throw new NotSupportedException();
        return new{saved=true};
    }
    object Histogram()
    {
        var values=volume?.Values??native?.Values;if(values==null)return new{available=false};
        float min=volume?.Min??native.Min,max=volume?.Max??native.Max;var bins=new int[256];int step=Math.Max(1,values.Length/1000000);
        for(int i=0;i<values.Length;i+=step)if(float.IsFinite(values[i]))bins[Math.Clamp((int)((values[i]-min)/Math.Max(1,max-min)*255),0,255)]++;
        return new{available=true,min,max,bins};
    }
    object RoiVolume(JsonElement r)
    {
        var roi=ActiveRois().Single(x=>RoiKey(x)==Int(r,"roi",-1));var v=DvhCalculator.EstimateVolume(roi,token);
        return new{volume=v.EstimatedVolumeCc,status=v.Status.ToString()};
    }
}
