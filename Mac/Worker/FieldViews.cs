using Clipper2Lib;
using System.Collections.Concurrent;
using QuickLook.DicomRT;
using System.Text.Json;

sealed partial class PreviewSession
{
    ConcurrentDictionary<string,BeamMotion.Sample[]> fieldTracks=new();
    BeamMotion.Sample[] Track(PlanBeam beam)
    {
        string key=CacheKey(plan.Beams.IndexOf(beam));if(fieldTracks.TryGetValue(key,out var samples))return samples;
        var map=Map(plan?.FrameUid,Frame);samples=map==null?Array.Empty<BeamMotion.Sample>():BeamMotion.Path(beam,map);fieldTracks[key]=samples;return samples;
    }
    object[] Tracks()
    {
        if(plan==null)return Array.Empty<object>();var map=Map(plan.FrameUid,Frame);
        return plan.Beams.Select((b,i)=>{
            var first=BeamProjection.Create(b,b.ControlPoints.FirstOrDefault(),map,out _);
            return (object)new{id=i,number=b.Number,setup=BeamMotion.IsImaging(b),arc=BeamMotion.IsArc(b),unit=BeamMotion.Unit(b,BeamModulationMode.AngularMeterset),
                iso=first==null?null:V(first.Iso),direction=first==null?null:V((first.Source-first.Iso).Normalized()),
                samples=Track(b).Select(s=>new{iso=V(s.Iso),direction=V(s.SourceDirection),angular=Num(s.Angular),tick=s.Tick,split=s.Break})};
        }).ToArray();
    }
    object FieldUpdates(JsonElement r)
    {
        Vec3 Vector(JsonElement v){var a=v.EnumerateArray().Select(x=>x.GetDouble()).ToArray();if(a.Length!=3)throw new ArgumentException();return new Vec3(a[0],a[1],a[2]);}
        return r.GetProperty("frames").EnumerateArray().Take(4).Select(f=>{
            var g=SliceGeometry.FromFrame(Vector(f.GetProperty("center")),Vector(f.GetProperty("right")),Vector(f.GetProperty("down")),f.GetProperty("widthMm").GetDouble(),f.GetProperty("heightMm").GetDouble());
            return new{key=f.GetProperty("key").GetString(),fields=Fields(g,r)};
        }).ToArray();
    }
    object Fields(SliceGeometry g,JsonElement request)
    {
        if(plan==null||!Flag(request,"fields"))return null;
        int active=Int(request,"beam",-1);double cpIndex=Real(request,"cp",0);var map=Map(plan.FrameUid,Frame);if(map==null)return null;
        double[] Project(Vec3 p)=>new[]{g.U(p),g.V(p)};double radius=Math.Min(g.WidthMm,g.HeightMm)*.36;
        var fields=new List<object>();int missing=0;
        for(int i=0;i<plan.Beams.Count;i++) {
            var beam=plan.Beams[i];if(BeamMotion.IsImaging(beam)&&i!=active)continue;
            if(beam.ControlPoints.Count==0)continue;
            var cp=beam.ControlPoints[0];
            if(i==active){double cursor=Math.Clamp(cpIndex,0,beam.ControlPoints.Count-1);int lo=(int)cursor,hi=Math.Min(lo+1,beam.ControlPoints.Count-1);cp=cursor==lo?beam.ControlPoints[lo]:MlcTimeline.Interpolate(beam.ControlPoints[lo],beam.ControlPoints[hi],cursor-lo);}
            var projection=BeamProjection.Create(beam,cp,map,out _);if(projection==null){missing++;continue;}
            var samples=Track(beam);double max=samples.Select(s=>s.Angular).Where(double.IsFinite).DefaultIfEmpty(0).Max();
            var arc=BeamMotion.IsArc(beam);
            var opening=arc?Array.Empty<double[][]>():Opening(projection,cp,g);
            fields.Add(new{id=i,number=beam.Number,active=i==active,arc,opening,maximum=Num(max),unit=BeamMotion.Unit(beam,BeamModulationMode.AngularMeterset),
                iso=Project(projection.Iso),source=Project(projection.Iso-projection.Forward*radius),
                track=samples.Select(s=>new{point=Project(s.Iso+s.SourceDirection*radius),outer=Project(s.Iso+s.SourceDirection*radius*(1+(double.IsFinite(s.Angular)&&max>0?Math.Clamp(s.Angular/max,0,1)*.22:0))),tick=s.Tick&&double.IsFinite(s.Angular),split=s.Break})});
        }
        return new{fields,missing};
    }
    double[][][] Opening(BeamProjection projection,ControlPoint cp,SliceGeometry g)
    {
        PathsD ProjectPolygons(IEnumerable<Vec3[]> parts) {
            var shapes=new PathsD();
            foreach(var part in parts) {
                token.ThrowIfCancellationRequested();var corners=part.Select(v=>projection.PlanePoint(v.X,v.Y)).ToArray();
                var polygon=new List<Vec3>{g.WorldAt(0,0),g.WorldAt(1,0),g.WorldAt(1,1),g.WorldAt(0,1)};
                var center=projection.PlanePoint(part.Average(v=>v.X),part.Average(v=>v.Y));
                for(int i=0;i<corners.Length&&polygon.Count>0;i++) {
                    var n=(corners[i]-projection.Source).Cross(corners[(i+1)%corners.Length]-projection.Source);if(n.Length<1e-10)continue;n=n.Normalized();if(n.Dot(center-projection.Source)<0)n=n*-1;polygon=Clip(polygon,projection.Source,n);
                }
                polygon=Clip(polygon,projection.Source+projection.Forward*.001,projection.Forward);
                if(polygon.Count>=3)shapes.Add(new PathD(polygon.Select(p=>new PointD(g.U(p),g.V(p)))));
            }
            return Clipper.Union(shapes,new PathsD(),FillRule.NonZero,6);
        }
        var result=ProjectPolygons(BeamAperture.Rectangles(cp).Select(r=>new[]{new Vec3(r.Left,r.Bottom,0),new Vec3(r.Right,r.Bottom,0),new Vec3(r.Right,r.Top,0),new Vec3(r.Left,r.Top,0)}));
        foreach(var block in cp.Blocks){var shape=ProjectPolygons(block.Triangles);result=block.Type=="APERTURE"?Clipper.Intersect(result,shape,FillRule.NonZero,6):Clipper.Difference(result,shape,FillRule.NonZero,6);}
        return result.Select(path=>path.Select(p=>new[]{p.x,p.y}).ToArray()).ToArray();
    }
    static List<Vec3> Clip(List<Vec3> input,Vec3 source,Vec3 normal)
    {
        var output=new List<Vec3>();if(input.Count==0)return output;var previous=input[^1];double a=(previous-source).Dot(normal);
        foreach(var p in input){double b=(p-source).Dot(normal);if((a>=0)!=(b>=0))output.Add(previous+(p-previous)*(a/(a-b)));if(b>=0)output.Add(p);previous=p;a=b;}return output;
    }
}
