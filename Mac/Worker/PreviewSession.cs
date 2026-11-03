using Dicom;
using System.Collections.Concurrent;
using System.Text.Json;
using QuickLook.DicomRT;

sealed partial class PreviewSession
{
    readonly CancellationTokenSource lifetime;
    public readonly object Gate=new();
    int contextVersion;
    public PreviewSession():this(new CancellationTokenSource()){}
    PreviewSession(CancellationTokenSource lifetime){this.lifetime=lifetime;}
    string CacheKey(object id)=>contextVersion+":"+id;
    public PreviewSession Snapshot(){var snapshot=new PreviewSession(lifetime){opened=opened,catalog=catalog,plans=plans.ToList(),structures=structures.ToList(),doses=doses.ToList(),links=links.ToList(),plan=plan,structure=structure,dose=dose,stack=stack,volume=volume,rtBounds=rtBounds,native=native,image=image,focus=focus,failures=failures,imageWarning=imageWarning,contextVersion=contextVersion,dvhCache=dvhCache,meshCache=meshCache,projectionCache=projectionCache,rays=rays,roiMeshes=roiMeshes,fieldTracks=fieldTracks,referenceCache=referenceCache,contourCache=contourCache,overlayVolume=overlayVolume,overlayStack=overlayStack,overlayLevel=overlayLevel,overlayWidth=overlayWidth,contextDoses=contextDoses.ToArray(),contextSets=contextSets.ToArray(),roiIds=roiIds,pixels=pixels,workGroups=workGroups,partial=partial,beamExtents=beamExtents,projectionCacheGate=projectionCacheGate};snapshot.sumResult=sumResult;snapshot.sumSets.AddRange(sumSets);return snapshot;}
    CancellationToken token => workToken.CanBeCanceled?workToken:lifetime.Token;
    public void Cancel(){lifetime.Cancel();workGroups.CancelAll();}
    public sealed record LoadProgress(string phase,int current,int total,bool finished);
    volatile LoadProgress loadProgress=new("Bereit",0,0,true);
    public object Progress()=>loadProgress;
    void ReportLoad(string phase,int current,int total,bool finished=false)=>loadProgress=new(phase,current,total,finished);
    DicomEntry opened;
    DicomCatalog catalog;
    List<PlanData> plans = new();
    List<StructureSet> structures = new();
    List<DoseGrid> doses = new();
    List<RegistrationLink> links = new();
    PlanData plan;
    StructureSet structure;
    DoseGrid dose;
    ImageStack stack;
    VolumeData volume, rtBounds;
    PixelPlane native;
    DicomEntry image;
    Vec3 focus;
    int failures;
    string imageWarning = "";
    ConcurrentDictionary<string, object> dvhCache = new();
    public object Execute(string op, JsonElement r) => op switch {
        "open" => Open(r.GetProperty("path").GetString(), Flag(r,"folder"),Flag(r,"partial")),
        "select" => Select(Int(r,"plan",-1), Int(r,"structure",-1), Int(r,"dose",-1), Int(r,"series",-1),Flag(r,"keepContext"),PointArgument(r,"focus")),
        "render" => Render(r), "scene" => Scene(), "beam" => Beam(r), "dvh" => Dvh(r),
        "tags" => Tags(r), "navigate" => Navigation(r), "fields" => FieldUpdates(r),
        "mesh" => Mesh(r), "fusion" => Fusion(r), "sum" => Sum(r),
        "discover" => Discover(r), "export" => Export(r), "exportInfo" => ExportInfo(),
        "histogram" => Histogram(), "volume" => RoiVolume(r), "doseReference" => DoseReference(r), "progress" => Progress(), "cancelWork" => CancelWork(r),
        _ => throw new ArgumentException("Unknown operation")
    };
    bool partial;
    public object Open(string path, bool folder,bool isPartial=false)
    {
        workGroups.CancelAll();partial=isPartial;ReportLoad("DICOM-Datei lesen",0,0);
        pixels=new PixelCache();roiMeshes=new();beamExtents=new();
        opened = DicomCatalog.ReadEntry(path);
        failures=0; plans.Clear(); structures.Clear(); doses.Clear(); dvhCache.Clear();sumResult=null;sumSets.Clear();meshCache.Clear();projectionCache.Clear();
        catalog = folder ? DicomCatalog.Scan(path, token, phaseProgress:(phase,current,total)=>ReportLoad("DICOM-Ordner prüfen",current,total),seed:opened) : DicomCatalog.Scan(path,token,seed:opened,knownPaths:new[]{path});
        catalog.Files=catalog.Files.Where(e=>e.SopUid==opened.SopUid || DicomPatientScope.Matches(opened,e)).GroupBy(e=>e.SopUid).Select(g=>g.First()).ToList();
        catalog.Stacks=catalog.Stacks.Where(s=>s.Entries.All(e=>catalog.Files.Contains(e))).ToList();
        links=RegistrationReader.Read(catalog);
        int rtDone=0,rtTotal=catalog.Files.Count(e=>e.Modality.StartsWith("RT"));
        foreach(var e in catalog.Files)
        {
            try {
                if(e.Modality=="RTPLAN")plans.Add(PlanData.Load(e));
                else if(e.Modality=="RTSTRUCT")structures.Add(StructureSet.Load(e));
                else if(e.Modality=="RTDOSE")doses.Add(DoseGrid.Load(e));
            } catch { failures++; }
            if(e.Modality.StartsWith("RT"))ReportLoad("Plan, Strukturen und Dosis",++rtDone,rtTotal);
        }
        plan=plans.SingleOrDefault(p=>p.Entry.SopUid==opened.SopUid);
        dose=doses.SingleOrDefault(d=>d.Entry.SopUid==opened.SopUid);
        structure=structures.SingleOrDefault(s=>s.Entry.SopUid==opened.SopUid);
        if(plan==null && dose!=null)plan=Unique(plans.Where(p=>p.Entry.SopUid==dose.PlanUid));
        // Images select a plan only when one explicit structure/image association exists.
        if(plan==null && !opened.Modality.StartsWith("RT"))
            plan=Unique(plans.Where(p=>structures.Any(s=>s.Entry.SopUid==p.StructureSopUid && s.ReferencedSeries.Contains(opened.SeriesUid))));
        if(plan==null && structure!=null)plan=Unique(plans.Where(p=>p.StructureSopUid==structure.Entry.SopUid));
        IndexRois();ResolveContext(dose);
        LoadStack(ChooseStack());
        var model=Model();ReportLoad("Bereit",1,1,true);return model;
    }
    void Associate()=>ResolveContext();
    ImageStack ChooseStack()
    {
        var exact=catalog.Stacks.FirstOrDefault(s=>s.Entries.Any(e=>e.SopUid==opened.SopUid));
        if(plan==null && structure==null && dose==null)return exact;
        var frame=plan?.FrameUid??dose?.FrameUid??structure?.Rois.FirstOrDefault()?.FrameUid;
        var candidates=catalog.Stacks.Where(s=>Map(frame,s.FrameUid)!=null && (structure==null || structure.ReferencedSeries.Count==0 || structure.ReferencedSeries.Contains(s.Entries[0].SeriesUid))).ToArray();
        if(exact!=null&&candidates.Contains(exact))return exact;
        return Unique(candidates)??Unique(candidates.Where(s=>s.Modality=="CT"));
    }
    object Select(int p,int s,int d,int series,bool keepContext=false,Vec3? navigationFocus=null)
    {
        var priorFocus=navigationFocus??focus;var priorFrame=Frame;
        if(!keepContext){sumResult=null;sumSets.Clear();
        plan=p>=0&&p<plans.Count?plans[p]:null;
        structure=s>=0&&s<structures.Count?structures[s]:null;
        dose=d>=0&&d<doses.Count?doses[d]:null;
        ResolveContext(plan==null?dose:null);}
        LoadStack(series>=0&&series<catalog.Stacks.Count?catalog.Stacks[series]:ChooseStack());
        var preserved=Map(priorFrame,Frame);if(series>=0&&preserved!=null)focus=preserved.Transform(priorFocus);
        return Model();
    }
    void LoadStack(ImageStack selected)
    {
        var sameStack=selected!=null&&stack!=null&&selected.Key==stack.Key&&selected.Entries.Count==stack.Entries.Count;
        var retainedVolume=sameStack?volume:null;var retainedNative=sameStack?native:null;var retainedImage=sameStack?image:null;
        contextVersion++;workGroups.CancelAll();volume=retainedVolume;native=retainedNative;image=retainedImage;stack=selected;imageWarning="";overlayVolume=null;overlayStack=null;
        // Old snapshots retain their own cache instances and cannot poison the new context.
        dvhCache=new();meshCache=new();projectionCache=new();rays=new();fieldTracks=new();referenceCache=new();contourCache=new();
        if(stack!=null){
            image??=stack.Entries.FirstOrDefault(e=>e.SopUid==opened.SopUid)??stack.Entries[stack.Entries.Count/2];
            try{native??=NativePlane(image);if(stack.CanMpr&&volume==null)volume=VolumeData.Load(stack,token,current=>ReportLoad("Bilder laden",current,stack.Entries.Count),entry=>NativePlane(entry),2);}
            catch(OperationCanceledException){throw;}
            catch{imageWarning="Bilddaten nicht dekodierbar oder keine reguläre MPR-Geometrie.";}
        }
        var frame=stack?.FrameUid??plan?.FrameUid??DisplayDose?.FrameUid??contextSets.SelectMany(s=>s.Rois).FirstOrDefault()?.FrameUid;
        rtBounds=RtSliceBounds.Create(ActiveRois().Where(r=>Map(r.FrameUid,frame)!=null).Select(r=>new RoiOverlay{Roi=r,RoiToImage=Map(r.FrameUid,frame)}),contextDoses.Where(d=>Map(d.FrameUid,frame)!=null).Select(d=>Tuple.Create(d,Map(d.FrameUid,frame))),token);
        focus=volume?.Center??(native!=null&&image.HasGeometry?image.Origin+image.AxisX*((image.Columns-1)*image.SpacingX/2)+image.AxisY*((image.Rows-1)*image.SpacingY/2):rtBounds?.Center??new Vec3());
        var iso=plan?.Beams.FirstOrDefault()?.Isocenter;
        var transform=Map(plan?.FrameUid,frame);
        if(iso.HasValue&&Finite(iso.Value)&&transform!=null)focus=transform.Transform(iso.Value);
    }
    string Frame => stack?.FrameUid??plan?.FrameUid??DisplayDose?.FrameUid??contextSets.SelectMany(s=>s.Rois).FirstOrDefault()?.FrameUid;
    IEnumerable<StructureRoi> ActiveRois()=>contextSets.SelectMany(s=>s.Rois).Distinct();
    int RoiKey(StructureRoi roi)=>roiIds.TryGetValue(roi,out var id)?id:-1;
    Matrix4 Map(string a,string b)=>RegistrationReader.Resolve(links,a,b);
    object Model()
    {
        var bounds=volume??rtBounds;
        var corners=bounds==null?new[]{focus}:Corners(bounds);
        return new {
            partial,context=contextVersion,selectionKey=sumResult?.Dose!=null?"sum:"+contextVersion:InstanceKey(plan?.Entry??dose?.Entry??structure?.Entry??opened),version="0.2.45 · macOS build 5", modality=opened.Modality,
            patientId=DicomCatalog.Text(opened.Dataset,DicomTag.PatientID),planLabel=plan?.Label??dose?.Label??"",
            title=DicomCatalog.Text(opened.Dataset,DicomTag.PatientName).Replace('^',' '),
            plan=plans.IndexOf(plan),structure=structures.IndexOf(structure),dose=doses.IndexOf(dose),series=catalog.Stacks.IndexOf(stack),
            plans=plans.Select((p,i)=>new{id=i,key=InstanceKey(p.Entry),label=string.IsNullOrWhiteSpace(p.Label)||p.Label.Trim().Equals("None",StringComparison.OrdinalIgnoreCase)?"RT-Plan "+(i+1)+" · "+p.Beams.Count+" Felder":p.Label,detail="DICOM-Planlabel: "+p.Label,beams=p.Beams.Count}),
            structures=structures.Select((s,i)=>new{id=i,key=InstanceKey(s.Entry),label=DicomCatalog.Text(s.Entry.Dataset,DicomTag.StructureSetLabel),count=s.Rois.Count}),
            doses=doses.Select(DoseModel),activeDoses=contextDoses.Select(DoseModel),dvhDose=DoseId(DisplayDose),
            isocenters=Isocenters(),tagSources=TagSources(),
            seriesList=catalog.Stacks.Select((s,i)=>new{id=i,key=InstanceKey(s.Entries.FirstOrDefault()),label=s.ToString(),mpr=s.CanMpr}),
            rois=ActiveRois().Select(r=>new{id=RoiKey(r),key=InstanceKey(structures.FirstOrDefault(s=>s.Rois.Contains(r))?.Entry)+":"+r.Number,number=r.Number,empty=!r.Contours.Any(c=>c.Points.Count>0),registered=Map(r.FrameUid,Frame)!=null,name=r.Name,description=r.Description,color=Color(r),type=r.InterpretedType,category=RoiClassification.Category(r,plan??(sumResult?.Dose!=null?null:plans.FirstOrDefault(p=>p.StructureSopUid==r.StructureSopUid))).ToString(),center=Map(r.FrameUid,Frame)==null?null:V(Map(r.FrameUid,Frame).Transform(r.Center))}),
            beams=plan?.Beams.Select((b,i)=>new{id=i,label=b.Number+" · "+b.Name,count=b.ControlPoints.Count,mu=Num(b.Meterset),machine=b.TreatmentMachineName,setup=BeamMotion.IsImaging(b),hasMlc=b.ControlPoints.Any(cp=>cp.MlcLayers.Count>0)}).OrderBy(b=>b.setup?2:b.hasMlc?0:1)??Enumerable.Empty<object>(),
            focus=V(focus), min=new[]{corners.Min(p=>p.X),corners.Min(p=>p.Y),corners.Min(p=>p.Z)},max=new[]{corners.Max(p=>p.X),corners.Max(p=>p.Y),corners.Max(p=>p.Z)},
            hasImage=native!=null, mpr=volume!=null,canReformat=volume!=null||rtBounds!=null, nativeCount=stack?.Entries.Count??0,hasDose=contextDoses.Length>0,isSum=sumResult?.Dose!=null,
            sumInfo=sumResult==null?null:new{included=sumResult.IncludedCount,excluded=sumResult.ExcludedCount,coverage=sumResult.CoverageFraction,message=sumResult.Message},
            sumGroups=DoseSum.CompatibleGroups(doses,links).Select(g=>g.Select(d=>doses.IndexOf(d)).ToArray()),
            fusionCandidates=catalog.Stacks.Select((s,i)=>new{id=i,label=s.ToString(),eligible=s!=stack&&s.CanMpr&&Map(Frame,s.FrameUid)!=null}).Where(s=>s.eligible),
            nativeIndex=stack?.Entries.IndexOf(image)??0,
            sliceSteps=volume==null?new[]{1d,1d,1d}:new[]{new Vec3(1,0,0),new Vec3(0,1,0),new Vec3(0,0,1)}.Select(a=>1/Math.Sqrt(Math.Pow(a.Dot(volume.AxisX)/volume.SpacingX,2)+Math.Pow(a.Dot(volume.AxisY)/volume.SpacingY,2)+Math.Pow(a.Dot(volume.AxisZ)/volume.SpacingZ,2))).ToArray(),
            window=Num(image?.WindowCenter??40)??40,width=Num(image?.WindowWidth??400)??400,
            failures, imageWarning,files=catalog.Files.Count,
            doseInfo=DisplayDose==null?null:new{maximum=Num(DisplayDose.Maximum),units=DisplayDose.Units,type=DisplayDose.DoseType},
            firstFrame=!partial||native==null&&rtBounds==null?null:Render(JsonSerializer.SerializeToElement(new{plane=native!=null?"Native":"Axial",size=256,focus=V(focus)})),
            firstBeam=!partial||plan==null||plan.Beams.Count==0?null:Beam(JsonSerializer.SerializeToElement(new{beam=plan.Beams.IndexOf(MlcTimeline.PlaybackOrder(plan).First()),cp=0})),
            initialView=partial&&plan==null&&rtBounds!=null?"image":opened.Modality=="RTPLAN"?"mlc":opened.Modality.StartsWith("RT")?"3d":"quad"
        };
    }
    object Render(JsonElement r)
    {
        var requested=r.TryGetProperty("focus",out var f)?f.EnumerateArray().Select(x=>x.GetDouble()).ToArray():V(focus);
        if(requested.Length!=3||requested.Any(x=>!double.IsFinite(x)))throw new ArgumentException();
        focus=new Vec3(requested[0],requested[1],requested[2]);
        var plane=Text(r,"plane","Axial");
        if(!new[]{"Native","Axial","Coronal","Sagittal"}.Contains(plane))throw new ArgumentException();
        if(plane=="Native" && stack!=null) {
            int index=Math.Clamp(Int(r,"index",0),0,stack.Entries.Count-1);
            if(image!=stack.Entries[index]) {image=stack.Entries[index];native=NativePlane(image);}
        }
        if(plane!="Native" && volume==null&&rtBounds==null)throw new NotSupportedException();
        var scene=new RenderScene {RasterParallelism=Int(r,"size",384)>=384?2:1,Entry=image,Native=native,Volume=volume,RtBounds=rtBounds,FrameUid=Frame,Focus=focus,Plane=plane,
            WindowCenter=Real(r,"level",40),WindowWidth=Math.Clamp(Real(r,"width",400),1,100000),Zoom=Math.Clamp(Real(r,"zoom",1),.1,20),
            DoseNormalization=Real(r,"reference",0),MaximumRelativeIsoLevel=Real(r,"maximumPercent",100),
            DoseWash=Flag(r,"wash"),Isodoses=Flag(r,"isodoses"),DoseOpacity=Math.Clamp(Real(r,"opacity",.38),0,.9),DoseMinimumPercent=Math.Clamp(Real(r,"washMin",5),0,200),DoseMaximumPercent=Math.Clamp(Real(r,"washMax",100),0,200),
            OverlayVolume=overlayVolume,ImageToOverlay=Map(Frame,overlayStack?.FrameUid),OverlayOpacity=Real(r,"blend",.5),OverlayWindowCenter=Real(r,"overlayLevel",overlayLevel),OverlayWindowWidth=Real(r,"overlayWidth",overlayWidth)};
        if(r.TryGetProperty("palette",out var palette)&&palette.ValueKind==JsonValueKind.Array)foreach(var stop in palette.EnumerateArray()){
            var values=stop.EnumerateArray().ToArray();double percent=values[0].GetDouble();if(percent>=0&&percent<=200)scene.DoseColorStops[percent]=Convert.ToInt32(values[1].GetString().TrimStart('#'),16);
        }
        scene.AbsoluteIsodoses=Flag(r,"absolute")&&contextDoses.All(IsodoseConfiguration.IsPhysicalGy);
        scene.IsoColorMaximum=DisplayDose?.Maximum??100;
        if(r.TryGetProperty("levels",out var levels)&&levels.ValueKind==JsonValueKind.Array)scene.IsoLevels=levels.EnumerateArray().Select(x=>x.GetDouble()).ToArray();
        else scene.IsoLevels=IsodoseConfiguration.AutomaticLevels(dose?.Maximum??100,scene.AbsoluteIsodoses);
        if(r.TryGetProperty("hiddenLevels",out var hiddenLevels))scene.HiddenIsoLevels=hiddenLevels.EnumerateArray().Select(x=>x.GetDouble()).ToHashSet();
        if(r.TryGetProperty("isoColors",out var colors))foreach(var pair in colors.EnumerateObject())if(double.TryParse(pair.Name,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var value))scene.IsoColors[value]=Convert.ToInt32(pair.Value.GetString().TrimStart('#'),16);
        var doseMap=Map(Frame,dose?.FrameUid);
        if(r.TryGetProperty("viewCenter",out var viewCenter)){var vc=viewCenter.EnumerateArray().Select(v=>v.GetDouble()).ToArray();if(vc.Length==3&&vc.All(double.IsFinite))scene.ViewCenter=new Vec3(vc[0],vc[1],vc[2]);}
        if(r.TryGetProperty("overlays",out var overlays)){
            foreach(var overlay in overlays.EnumerateArray()){
                int id=overlay.GetProperty("id").GetInt32();var selected=contextDoses.FirstOrDefault(d=>DoseId(d)==id);
                if(selected==null)throw new ArgumentException();var map=Map(Frame,selected.FrameUid);double reference=Real(overlay,"reference",0);
                if(Flag(overlay,"visible")&&map!=null&&reference>0)scene.Doses.Add(new DoseOverlay{Dose=selected,ImageToDose=map,Normalization=reference});
            }
        }else if(DisplayDose!=null&&Map(Frame,DisplayDose.FrameUid)!=null)scene.Doses.Add(new DoseOverlay{Dose=DisplayDose,ImageToDose=Map(Frame,DisplayDose.FrameUid)});
        scene.AbsoluteIsodoses=Flag(r,"absolute")&&scene.Doses.All(d=>IsodoseConfiguration.IsPhysicalGy(d.Dose));
        var geometry=SliceGeometry.Create(scene);
        double ratio=geometry.WidthMm/geometry.HeightMm;
        int size=Math.Clamp(Int(r,"size",384),64,512);
        int width=ratio>=1?size:Math.Max(2,(int)Math.Round(size*ratio));
        int height=ratio>=1?Math.Max(2,(int)Math.Round(size/ratio)):size;
        var pixels=SliceRaster.Render(scene,width,height,token);
        // Shared Windows renderer emits BGRA; HTML canvas consumes RGBA.
        for(int i=0;i<pixels.Pixels.Length;i+=4)(pixels.Pixels[i],pixels.Pixels[i+2])=(pixels.Pixels[i+2],pixels.Pixels[i]);
        var g=pixels.Geometry;
        double[] Project(Vec3 p){var delta=p-g.Center;return new[]{.5+delta.Dot(g.Right)/g.WidthMm,.5+delta.Dot(g.Down)/g.HeightMm};}
        var hidden=r.TryGetProperty("hidden",out var h)?h.EnumerateArray().Select(x=>x.GetInt32()).ToHashSet():new HashSet<int>();
        var outlines=new List<object>();
        foreach(var roi in ActiveRois().Where(x=>!hidden.Contains(RoiKey(x)))) {
            var map=Map(roi.FrameUid,Frame); if(map==null)continue;
            var lines=ContourLines(roi,map,g,out var kind);
            outlines.Add(new {color=Color(roi),kind=kind.ToString(),lines=lines.Select(l=>new[]{Project(l.A),Project(l.B)})});
        }
        var iso=plan?.Beams.FirstOrDefault()?.Isocenter;var planMap=Map(plan?.FrameUid,Frame);
        return new{absolute=scene.AbsoluteIsodoses,width=pixels.Width,height=pixels.Height,rgba=Convert.ToBase64String(pixels.Pixels),outlines,
            isolines=pixels.Isolines.Where(l=>!scene.HiddenIsoLevels.Contains(l.DoseLevel)).Select(l=>new{level=l.DoseLevel,points=new[]{Project(l.A),Project(l.B)}}),
            center=V(g.Center),right=V(g.Right),down=V(g.Down),widthMm=g.WidthMm,heightMm=g.HeightMm,fields=Fields(g,r),
            isocenters=Isocenters().Select(p=>new Vec3(p[0],p[1],p[2])).Select(p=>new{point=Project(p),distance=Num((p-g.Center).Dot(g.Normal))}),
            windowCenter=scene.WindowCenter,windowWidth=scene.WindowWidth,zoom=scene.Zoom,cross=Project(focus),iso=iso.HasValue&&Finite(iso.Value)&&planMap!=null?Project(planMap.Transform(iso.Value)):null,
            isoDistance=iso.HasValue&&Finite(iso.Value)&&planMap!=null?Num((planMap.Transform(iso.Value)-g.Center).Dot(g.Normal)):null,
            left=SliceGeometry.Direction(g.Right*-1),top=SliceGeometry.Direction(g.Down*-1)};
    }
    object Scene()
    {
        long budget=0;int omitted=0;
        var rois=new List<object>();
        foreach(var roi in ActiveRois()) {
            var map=Map(roi.FrameUid,Frame);if(map==null){omitted++;continue;}
            long count=roi.Contours.Sum(c=>(long)c.Points.Count);
            if(budget+count>1000000){omitted++;continue;}budget+=count;
            rois.Add(new{id=RoiKey(roi),color=Color(roi),contours=roi.Contours.Select(c=>new{closed=c.GeometricType=="CLOSED_PLANAR"||c.GeometricType=="CLOSEDPLANAR_XOR",points=c.Points.Select(p=>V(map.Transform(p)))})});
        }
        return new{rois,omitted,focus=V(focus),label="Originale Konturstapel · keine rekonstruierte Oberfläche"};
    }
    object Beam(JsonElement r)
    {
        if(plan==null)throw new NotSupportedException();
        var b=plan.Beams[Math.Clamp(Int(r,"beam",0),0,plan.Beams.Count-1)];
        if(b.ControlPoints.Count==0)throw new NotSupportedException();
        double cursor=Math.Clamp(Real(r,"cp",0),0,b.ControlPoints.Count-1);
        int lo=(int)Math.Floor(cursor),hi=Math.Min(lo+1,b.ControlPoints.Count-1);
        var c=cursor==lo?b.ControlPoints[lo]:MlcTimeline.Interpolate(b.ControlPoints[lo],b.ControlPoints[hi],cursor-lo);
        var projection=BeamProjection.Create(b,c,Map(plan.FrameUid,Frame),out var reason);
        return AdvancedBeam(r,b,c,cursor,projection,reason);
    }

    object Dvh(JsonElement r)
    {
        var selected=RequestDose(r);string key=CacheKey("dvh:"+DoseId(selected)+":"+Int(r,"roi",-1));int number=Int(r,"roi",-1);if(dvhCache.TryGetValue(key,out var cached))return cached;
        var roi=ActiveRois().Single(x=>RoiKey(x)==number);
        var result=DvhCalculator.Calculate(roi,selected,Map(roi.FrameUid,selected?.FrameUid),token);
        object response=new{id=number,status=result.Status.ToString(),message=result.Message,units=result.DoseUnits,
            x=result.DoseValues,y=result.CumulativeVolumePercent,coverage=result.CoverageFraction,volume=result.EstimatedVolumeCc,
            mean=Num(result.Dmean),median=Num(result.Dmedian),max=Num(result.Dmax),min=Num(result.Dmin),d98=Num(result.D98),d2=Num(result.D2),sampling=result.SamplingStepMm};
        token.ThrowIfCancellationRequested();dvhCache[key]=response;return response;
    }
    static Vec3? PointArgument(JsonElement r,string key)
    {
        if(!r.TryGetProperty(key,out var value))return null;
        var a=value.EnumerateArray().Select(x=>x.GetDouble()).ToArray();if(a.Length!=3||a.Any(x=>!double.IsFinite(x)))throw new ArgumentException();return new Vec3(a[0],a[1],a[2]);
    }
    static T Unique<T>(IEnumerable<T> values) where T:class {var a=values.Take(2).ToArray();return a.Length==1?a[0]:null;}
    static Vec3[] Corners(VolumeData v)=>(from x in new[]{0,v.Width-1} from y in new[]{0,v.Height-1} from z in new[]{0,v.Depth-1} select v.WorldAt(x,y,z)).ToArray();
    static bool Finite(Vec3 v)=>double.IsFinite(v.X)&&double.IsFinite(v.Y)&&double.IsFinite(v.Z);
    static double[] V(Vec3 v)=>new[]{v.X,v.Y,v.Z};
    static double? Num(double d)=>double.IsFinite(d)?d:null;
    static string Color(StructureRoi r)=>$"#{r.Red:x2}{r.Green:x2}{r.Blue:x2}";
    static bool Flag(JsonElement r,string key)=>r.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.True;
    static string Text(JsonElement r,string key,string fallback)=>r.TryGetProperty(key,out var v)?v.GetString():fallback;
    static int Int(JsonElement r,string key,int fallback)=>r.TryGetProperty(key,out var v)?v.GetInt32():fallback;
    static double Real(JsonElement r,string key,double fallback)=>r.TryGetProperty(key,out var v)&&v.TryGetDouble(out var x)&&double.IsFinite(x)?x:fallback;
}
