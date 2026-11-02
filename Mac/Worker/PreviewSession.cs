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
    public PreviewSession Snapshot(){var snapshot=new PreviewSession(lifetime){opened=opened,catalog=catalog,plans=plans.ToList(),structures=structures.ToList(),doses=doses.ToList(),links=links.ToList(),plan=plan,structure=structure,dose=dose,stack=stack,volume=volume,rtBounds=rtBounds,native=native,image=image,focus=focus,failures=failures,imageWarning=imageWarning,contextVersion=contextVersion,dvhCache=dvhCache,meshCache=meshCache,projectionCache=projectionCache,rays=rays,roiMeshes=roiMeshes,fieldTracks=fieldTracks,referenceCache=referenceCache,contourCache=contourCache};snapshot.sumResult=sumResult;snapshot.sumSets.AddRange(sumSets);return snapshot;}
    CancellationToken token => lifetime.Token;
    public void Cancel()=>lifetime.Cancel();
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
        "open" => Open(r.GetProperty("path").GetString(), Flag(r,"folder")),
        "select" => Select(Int(r,"plan",-1), Int(r,"structure",-1), Int(r,"dose",-1), Int(r,"series",-1)),
        "render" => Render(r), "scene" => Scene(), "beam" => Beam(r), "dvh" => Dvh(r),
        "tags" => TagReader.Read((Flag(r,"source")?opened:image??opened).Dataset),
        "mesh" => Mesh(r), "fusion" => Fusion(r), "sum" => Sum(r),
        "discover" => Discover(r), "export" => Export(r), "exportInfo" => ExportInfo(),
        "histogram" => Histogram(), "volume" => RoiVolume(r), "doseReference" => DoseReference(), "progress" => Progress(),
        _ => throw new ArgumentException("Unknown operation")
    };
    public object Open(string path, bool folder)
    {
        ReportLoad("DICOM-Datei lesen",0,0);
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
        var exactDose=dose;
        Associate();
        if(exactDose!=null)dose=exactDose;
        LoadStack(ChooseStack());
        var model=Model();ReportLoad("Bereit",1,1,true);return model;
    }
    void Associate()
    {
        if(plan!=null) {
            structure=Unique(structures.Where(s=>s.Entry.SopUid==plan.StructureSopUid));
            dose=Unique(doses.Where(d=>d.PlanUid==plan.Entry.SopUid && d.ReferencedPlanCount==1 && d.SummationType=="PLAN"));
        }
        dvhCache.Clear();
    }
    ImageStack ChooseStack()
    {
        var exact=catalog.Stacks.FirstOrDefault(s=>s.Entries.Any(e=>e.SopUid==opened.SopUid));
        if(plan==null && structure==null && dose==null)return exact;
        var frame=plan?.FrameUid??dose?.FrameUid??structure?.Rois.FirstOrDefault()?.FrameUid;
        var candidates=catalog.Stacks.Where(s=>Map(frame,s.FrameUid)!=null && (structure==null || structure.ReferencedSeries.Count==0 || structure.ReferencedSeries.Contains(s.Entries[0].SeriesUid))).ToArray();
        if(exact!=null&&candidates.Contains(exact))return exact;
        return Unique(candidates)??Unique(candidates.Where(s=>s.Modality=="CT"));
    }
    object Select(int p,int s,int d,int series)
    {
        var priorFocus=focus;var priorFrame=Frame;
        sumResult=null;sumSets.Clear();
        plan=p>=0&&p<plans.Count?plans[p]:null;
        structure=s>=0&&s<structures.Count?structures[s]:null;
        dose=d>=0&&d<doses.Count?doses[d]:null;
        if(plan!=null)Associate(); else dvhCache.Clear();
        LoadStack(series>=0&&series<catalog.Stacks.Count?catalog.Stacks[series]:ChooseStack());
        var preserved=Map(priorFrame,Frame);if(series>=0&&preserved!=null)focus=preserved.Transform(priorFocus);
        return Model();
    }
    void LoadStack(ImageStack selected)
    {
        contextVersion++;volume=null; native=null; image=null; stack=selected; imageWarning="";overlayVolume=null;overlayStack=null;meshCache.Clear();projectionCache.Clear();rays.Clear();fieldTracks.Clear();referenceCache.Clear();contourCache.Clear();
        if(stack!=null) {
            image=stack.Entries.FirstOrDefault(e=>e.SopUid==opened.SopUid)??stack.Entries[stack.Entries.Count/2];
            try {native=PixelPlane.Load(image);if(stack.CanMpr)volume=VolumeData.Load(stack,token,current=>ReportLoad("Bilder laden",current,stack.Entries.Count),null,2);}
            catch {imageWarning="Bilddaten nicht dekodierbar oder keine reguläre MPR-Geometrie.";}
        }
        var frame=stack?.FrameUid??plan?.FrameUid??dose?.FrameUid??structure?.Rois.FirstOrDefault()?.FrameUid;
        var points=ActiveRois().Where(r=>Map(r.FrameUid,frame)!=null).SelectMany(r=>r.Contours.SelectMany(c=>c.Points).Select(Map(r.FrameUid,frame).Transform)).Where(Finite).ToArray();
        rtBounds=null;
        if(points.Length>0) {
            var low=new Vec3(points.Min(p=>p.X),points.Min(p=>p.Y),points.Min(p=>p.Z));
            var high=new Vec3(points.Max(p=>p.X),points.Max(p=>p.Y),points.Max(p=>p.Z));
            rtBounds=new VolumeData {Width=2,Height=2,Depth=2,Origin=low-new Vec3(10,10,10),AxisX=new Vec3(1,0,0),AxisY=new Vec3(0,1,0),AxisZ=new Vec3(0,0,1),SpacingX=high.X-low.X+20,SpacingY=high.Y-low.Y+20,SpacingZ=high.Z-low.Z+20};
        }
        rtBounds=RtSliceBounds.Create(ActiveRois().Where(r=>Map(r.FrameUid,frame)!=null).Select(r=>new RoiOverlay{Roi=r,RoiToImage=Map(r.FrameUid,frame)}),dose!=null&&Map(dose.FrameUid,frame)!=null?new[]{Tuple.Create(dose,Map(dose.FrameUid,frame))}:Array.Empty<Tuple<DoseGrid,Matrix4>>(),token);
        focus=volume?.Center??(native!=null&&image.HasGeometry?image.Origin+image.AxisX*((image.Columns-1)*image.SpacingX/2)+image.AxisY*((image.Rows-1)*image.SpacingY/2):rtBounds?.Center??new Vec3());
        var iso=plan?.Beams.FirstOrDefault()?.Isocenter;
        var transform=Map(plan?.FrameUid,frame);
        if(iso.HasValue&&Finite(iso.Value)&&transform!=null)focus=transform.Transform(iso.Value);
    }
    string Frame => stack?.FrameUid??plan?.FrameUid??dose?.FrameUid??structure?.Rois.FirstOrDefault()?.FrameUid;
    IEnumerable<StructureRoi> ActiveRois()=>sumResult?.Dose!=null?sumSets.SelectMany(s=>s.Rois):structure?.Rois??Enumerable.Empty<StructureRoi>();
    int RoiKey(StructureRoi roi)=>structures.SelectMany(s=>s.Rois).ToList().IndexOf(roi)+1;
    Matrix4 Map(string a,string b)=>RegistrationReader.Resolve(links,a,b);
    object Model()
    {
        var bounds=volume??rtBounds;
        var corners=bounds==null?new[]{focus}:Corners(bounds);
        return new {
            version="0.1.0 · macOS", modality=opened.Modality,
            patientId=DicomCatalog.Text(opened.Dataset,DicomTag.PatientID),planLabel=plan?.Label??dose?.Label??"",
            title=DicomCatalog.Text(opened.Dataset,DicomTag.PatientName).Replace('^',' '),
            plan=plans.IndexOf(plan),structure=structures.IndexOf(structure),dose=doses.IndexOf(dose),series=catalog.Stacks.IndexOf(stack),
            plans=plans.Select((p,i)=>new{id=i,label=string.IsNullOrWhiteSpace(p.Label)||p.Label.Trim().Equals("None",StringComparison.OrdinalIgnoreCase)?"RT-Plan "+(i+1)+" · "+p.Beams.Count+" Felder":p.Label,detail="DICOM-Planlabel: "+p.Label,beams=p.Beams.Count}),
            structures=structures.Select((s,i)=>new{id=i,label=DicomCatalog.Text(s.Entry.Dataset,DicomTag.StructureSetLabel),count=s.Rois.Count}),
            doses=doses.Select((d,i)=>new{id=i,label=(plans.FirstOrDefault(p=>p.Entry.SopUid==d.PlanUid)?.Label??"Standalone")+" · "+d.Label,units=d.Units,maximum=Num(d.Maximum)}),
            seriesList=catalog.Stacks.Select((s,i)=>new{id=i,label=s.ToString(),mpr=s.CanMpr}),
            rois=ActiveRois().Select(r=>new{id=RoiKey(r),number=r.Number,name=r.Name,description=r.Description,color=Color(r),type=r.InterpretedType,category=RoiClassification.Category(r,plans.FirstOrDefault(p=>p.StructureSopUid==r.StructureSopUid)).ToString(),center=Map(r.FrameUid,Frame)==null?null:V(Map(r.FrameUid,Frame).Transform(r.Center))}),
            beams=plan?.Beams.Select((b,i)=>new{id=i,label=b.Number+" · "+b.Name,count=b.ControlPoints.Count,mu=Num(b.Meterset),machine=b.TreatmentMachineName,setup=BeamMotion.IsImaging(b),hasMlc=b.ControlPoints.Any(cp=>cp.MlcLayers.Count>0)}).OrderBy(b=>b.setup?2:b.hasMlc?0:1)??Enumerable.Empty<object>(),
            focus=V(focus), min=new[]{corners.Min(p=>p.X),corners.Min(p=>p.Y),corners.Min(p=>p.Z)},max=new[]{corners.Max(p=>p.X),corners.Max(p=>p.Y),corners.Max(p=>p.Z)},
            hasImage=native!=null, mpr=volume!=null, nativeCount=stack?.Entries.Count??0,hasDose=dose!=null,isSum=sumResult?.Dose!=null,
            sumInfo=sumResult==null?null:new{included=sumResult.IncludedCount,excluded=sumResult.ExcludedCount,coverage=sumResult.CoverageFraction,message=sumResult.Message},
            sumGroups=DoseSum.CompatibleGroups(doses,links).Select(g=>g.Select(d=>doses.IndexOf(d)).ToArray()),
            fusionCandidates=catalog.Stacks.Select((s,i)=>new{id=i,label=s.ToString(),eligible=s!=stack&&s.CanMpr&&Map(Frame,s.FrameUid)!=null}).Where(s=>s.eligible),
            nativeIndex=stack?.Entries.IndexOf(image)??0,
            sliceSteps=volume==null?new[]{1d,1d,1d}:new[]{new Vec3(1,0,0),new Vec3(0,1,0),new Vec3(0,0,1)}.Select(a=>1/Math.Sqrt(Math.Pow(a.Dot(volume.AxisX)/volume.SpacingX,2)+Math.Pow(a.Dot(volume.AxisY)/volume.SpacingY,2)+Math.Pow(a.Dot(volume.AxisZ)/volume.SpacingZ,2))).ToArray(),
            window=Num(image?.WindowCenter??40)??40,width=Num(image?.WindowWidth??400)??400,
            failures, imageWarning,files=catalog.Files.Count,
            doseInfo=dose==null?null:new{maximum=Num(dose.Maximum),units=dose.Units,type=dose.DoseType},
            initialView=opened.Modality=="RTPLAN"?"mlc":opened.Modality.StartsWith("RT")?"3d":"quad"
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
            if(image!=stack.Entries[index]) {image=stack.Entries[index];native=PixelPlane.Load(image);}
        }
        if(plane!="Native" && native!=null && volume==null)throw new NotSupportedException();
        var scene=new RenderScene {Entry=image,Native=native,Volume=volume,RtBounds=rtBounds,FrameUid=Frame,Focus=focus,Plane=plane,
            WindowCenter=Real(r,"level",40),WindowWidth=Math.Clamp(Real(r,"width",400),1,100000),Zoom=Math.Clamp(Real(r,"zoom",1),.1,20),
            DoseNormalization=Real(r,"reference",0),MaximumRelativeIsoLevel=Real(r,"maximumPercent",100),
            DoseWash=Flag(r,"wash"),Isodoses=Flag(r,"isodoses"),DoseOpacity=Math.Clamp(Real(r,"opacity",.38),0,.9),DoseMinimumPercent=Math.Clamp(Real(r,"washMin",5),0,200),DoseMaximumPercent=Math.Clamp(Real(r,"washMax",100),0,200),
            OverlayVolume=overlayVolume,ImageToOverlay=Map(Frame,overlayStack?.FrameUid),OverlayOpacity=Real(r,"blend",.5),OverlayWindowCenter=Real(r,"overlayLevel",overlayLevel),OverlayWindowWidth=Real(r,"overlayWidth",overlayWidth)};
        if(r.TryGetProperty("palette",out var palette)&&palette.ValueKind==JsonValueKind.Array)foreach(var stop in palette.EnumerateArray()){
            var values=stop.EnumerateArray().ToArray();double percent=values[0].GetDouble();if(percent>=0&&percent<=200)scene.DoseColorStops[percent]=Convert.ToInt32(values[1].GetString().TrimStart('#'),16);
        }
        scene.AbsoluteIsodoses=Flag(r,"absolute")&&IsodoseConfiguration.IsPhysicalGy(dose);
        scene.IsoColorMaximum=dose?.Maximum??100;
        if(r.TryGetProperty("levels",out var levels)&&levels.ValueKind==JsonValueKind.Array)scene.IsoLevels=levels.EnumerateArray().Select(x=>x.GetDouble()).ToArray();
        else scene.IsoLevels=IsodoseConfiguration.AutomaticLevels(dose?.Maximum??100,scene.AbsoluteIsodoses);
        if(r.TryGetProperty("hiddenLevels",out var hiddenLevels))scene.HiddenIsoLevels=hiddenLevels.EnumerateArray().Select(x=>x.GetDouble()).ToHashSet();
        if(r.TryGetProperty("isoColors",out var colors))foreach(var pair in colors.EnumerateObject())if(double.TryParse(pair.Name,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var value))scene.IsoColors[value]=Convert.ToInt32(pair.Value.GetString().TrimStart('#'),16);
        var doseMap=Map(Frame,dose?.FrameUid);
        if(r.TryGetProperty("viewCenter",out var viewCenter)){var vc=viewCenter.EnumerateArray().Select(v=>v.GetDouble()).ToArray();if(vc.Length==3&&vc.All(double.IsFinite))scene.ViewCenter=new Vec3(vc[0],vc[1],vc[2]);}
        if(dose!=null&&doseMap!=null)scene.Doses.Add(new DoseOverlay {Dose=dose,ImageToDose=doseMap});
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
        return new{width=pixels.Width,height=pixels.Height,rgba=Convert.ToBase64String(pixels.Pixels),outlines,
            isolines=pixels.Isolines.Where(l=>!scene.HiddenIsoLevels.Contains(l.DoseLevel)).Select(l=>new{level=l.DoseLevel,points=new[]{Project(l.A),Project(l.B)}}),
            center=V(g.Center),right=V(g.Right),down=V(g.Down),widthMm=g.WidthMm,heightMm=g.HeightMm,fields=Fields(g,r),
            cross=Project(focus),iso=iso.HasValue&&Finite(iso.Value)&&planMap!=null?Project(planMap.Transform(iso.Value)):null,
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
        int number=Int(r,"roi",-1);if(dvhCache.TryGetValue(CacheKey(number),out var cached))return cached;
        var roi=ActiveRois().Single(x=>RoiKey(x)==number);
        var result=DvhCalculator.Calculate(roi,dose,Map(roi.FrameUid,dose?.FrameUid),token);
        object response=new{id=number,status=result.Status.ToString(),message=result.Message,units=result.DoseUnits,
            x=result.DoseValues,y=result.CumulativeVolumePercent,coverage=result.CoverageFraction,volume=result.EstimatedVolumeCc,
            mean=Num(result.Dmean),median=Num(result.Dmedian),max=Num(result.Dmax),min=Num(result.Dmin),d98=Num(result.D98),d2=Num(result.D2),sampling=result.SamplingStepMm};
        dvhCache[CacheKey(number)]=response;return response;
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
