using QuickLook.DicomRT;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Globalization;

sealed partial class PreviewSession
{
    ConcurrentDictionary<string,float[]> rays=new();
    ConcurrentDictionary<StructureRoi,Lazy<(ThreeDMeshData mesh,string reason)>> roiMeshes=new();
    object AdvancedBeam(JsonElement r,PlanBeam b,ControlPoint c,double cursor,BeamProjection projection,string reason)
    {
        int size=Math.Clamp(Int(r,"size",384),96,512);double extent=Math.Max(100,b.ControlPoints.SelectMany(cp=>cp.MlcLayers.SelectMany(l=>l.Boundaries.Concat(l.Positions)).Concat(cp.XJaws??Array.Empty<double>()).Concat(cp.YJaws??Array.Empty<double>())).Where(double.IsFinite).Select(v=>Math.Abs(v)+10).DefaultIfEmpty(100).Max());
        string key=CacheKey(plans.IndexOf(plan)+":"+plan.Beams.IndexOf(b)+":"+cursor.ToString("R",CultureInfo.InvariantCulture)+":"+size);
        object drr=null;var outlines=new List<object>();int omitted=0;
        if(Flag(r,"drr")&&projection!=null&&volume!=null&&stack.Modality=="CT") {
            if(!rays.TryGetValue(key,out var values)) {
                values=projection.Integrate(volume,extent,size,size<300?2:1,token,2);
                while(rays.Count>0&&(rays.Count>=256||rays.Values.Sum(x=>(long)x.Length)*4+values.LongLength*4>64L*1024*1024))rays.TryRemove(rays.Keys.First(),out _);
                rays[key]=values;
            }
            var gray=Tone(values,Text(r,"drrPreset","High contrast"),Real(r,"drrCenter",.5),Real(r,"drrWidth",1));
            drr=new{size,gray=Convert.ToBase64String(gray),extent};
        }
        var hidden=r.TryGetProperty("hidden",out var h)?h.EnumerateArray().Select(x=>x.GetInt32()).ToHashSet():new HashSet<int>();
        var categories=r.TryGetProperty("categories",out var cats)?cats.EnumerateArray().Select(x=>x.GetString()).ToHashSet():new HashSet<string>{"Target"};
        if(Flag(r,"outlines")&&projection!=null)foreach(var roi in ActiveRois().Where(roi=>!hidden.Contains(RoiKey(roi)))) {
            var category=RoiClassification.Category(roi,plan).ToString();if(!categories.Contains(category))continue;
            var map=Map(roi.FrameUid,Frame);if(map==null){omitted++;continue;}
            var cached=OriginalMesh(roi);if(cached.mesh==null){omitted++;continue;}
            string projectionKey=key+":"+RoiKey(roi);
            if(!projectionCache.TryGetValue(projectionKey,out var outline)) {
                var boundary=ProjectionSilhouette.Build(cached.mesh,map,projection,extent,512,token);
                outline=new{id=RoiKey(roi),color=Color(roi),boundary};
                if(projectionCache.Count>=512)projectionCache.TryRemove(projectionCache.Keys.First(),out _);projectionCache[projectionKey]=outline;
            }
            outlines.Add(outline);
        }
        var modulation=BeamMetersetInfo.At(b,cursor);
        var aperture=c;string layer=Text(r,"layer","all");
        if(layer!="all")aperture=new ControlPoint{Blocks=c.Blocks,XJaws=c.XJaws,YJaws=c.YJaws,MlcLayers=c.MlcLayers.Where(x=>x.Key==layer).ToList()};
        return new{beam=plan.Beams.IndexOf(b),extent,cp=cursor,count=b.ControlPoints.Count,gantry=Num(c.Gantry),collimator=Num(c.Collimator),couch=Num(c.Couch),mu=Num(b.Meterset),
            weight=Num(c.MetersetWeight),rectangles=BeamAperture.Rectangles(aperture).Select(a=>new[]{a.Left,a.Bottom,a.Right,a.Top}),
            blocks=c.Blocks.Select(x=>new{type=x.Type,points=x.Outline.Select(V)}),
            layers=c.MlcLayers.Select(x=>new{key=x.Key,type=x.Type,isY=x.IsY,boundaries=x.Boundaries,positions=x.Positions}),jaws=new{x=c.XJaws,y=c.YJaws},
            drr,reason,outlines,omitted,modulation=new{text=modulation.Text,detail=modulation.Detail},
            machine=new{label=BeamMachineInfo.Label(b),detail=BeamMachineInfo.Detail(b),form=BeamMachineInfo.Form(b).ToString(),position=b.PatientPosition,patientToIec=PatientOrientation.ToIec(b.PatientPosition)?.Values},
            source=projection==null?null:V(projection.Source),iso=Finite(c.Isocenter)&&Map(plan.FrameUid,Frame)!=null?V(Map(plan.FrameUid,Frame).Transform(c.Isocenter)):null,
            right=projection==null?null:V(projection.Right),up=projection==null?null:V(projection.Up),sad=Num(b.SourceAxisDistance),
            tracks=!Flag(r,"tracks")?null:Tracks()};
    }
    (ThreeDMeshData mesh,string reason) OriginalMesh(StructureRoi roi)
    {
        if(roiMeshes.Count>=256||roiMeshes.Values.Where(x=>x.IsValueCreated).Sum(x=>(long)(x.Value.mesh?.Points.Count??0))>8000000)roiMeshes.Clear();
        return roiMeshes.GetOrAdd(roi,_=>new Lazy<(ThreeDMeshData mesh,string reason)>(()=>{var mesh=ThreeDGeometry.BuildRoiSurface(roi,Matrix4.Identity,token,out var reason);return(mesh,reason);},LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    static byte[] Tone(float[] values,string preset,double center,double width)
    {
        double high=Math.Max(1,values.Where(float.IsFinite).DefaultIfEmpty(1).Max());
        var raw=values.Select(v=>(ushort)Math.Clamp(v/high*65535,0,65535)).ToArray();var hist=new int[4096];int count=0;
        foreach(var v in raw)if(v>0){hist[v>>4]++;count++;}
        double Percentile(double q){int sum=0,threshold=Math.Max(1,(int)Math.Ceiling(count*q));for(int i=0;i<hist.Length;i++){sum+=hist[i];if(sum>=threshold)return i*16/65535d;}return 1;}
        double lo=0,hi=1,gamma=1;
        if(preset=="Custom"){lo=center-width/2;hi=center+width/2;}
        else if(preset=="Original"){hi=Percentile(.99);gamma=.8;}
        else if(count>0){lo=Percentile(preset=="High contrast"?.08:.02);hi=Percentile(preset=="High contrast"?.95:.98);if(preset=="Soft"){lo=Math.Max(0,lo-(hi-lo)*.2);gamma=.8;}}
        if(hi-lo<.001){lo=0;hi=Math.Max(.001,hi);}
        var lut=new byte[65536];for(int i=0;i<lut.Length;i++)lut[i]=(byte)Math.Round(255*Math.Pow(Math.Clamp((i/65535d-lo)/(hi-lo),0,1),gamma));
        return raw.Select(v=>lut[v]).ToArray();
    }
}
