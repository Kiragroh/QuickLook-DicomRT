using QuickLook.DicomRT;
using Dicom;
using System.Collections.Concurrent;

// Display normalization, never a prescription inferred from ROI names.
// DICOM Target Prescription Dose: PS3.3 C.8.8.10 (300A,0026).
public static class HotVolumeDose
{
    // Minimum grid-centre dose in the hottest requested absolute volume. Native
    // voxel volume weights are retained, including nonuniform frame spacing.
    public static double? Calculate(IEnumerable<DoseVoxel> samples,double volumeCc,Func<Vec3,bool> contains,CancellationToken token)
    {
        if(!(volumeCc>0)||!double.IsFinite(volumeCc))throw new ArgumentOutOfRangeException(nameof(volumeCc));
        var hottest=new PriorityQueue<DoseVoxel,float>();double volume=0;int count=0;
        foreach(var sample in samples){
            if((count++&4095)==0)token.ThrowIfCancellationRequested();
            if(!float.IsFinite(sample.Dose)||sample.Dose<0||!double.IsFinite(sample.VolumeCc)||sample.VolumeCc<=0)continue;
            if(volume>=volumeCc&&hottest.TryPeek(out _,out var threshold)&&sample.Dose<threshold)continue;
            if(!contains(sample.Position))continue;
            hottest.Enqueue(sample,sample.Dose);volume+=sample.VolumeCc;
            while(hottest.TryPeek(out var low,out _)&&volume-low.VolumeCc>=volumeCc-1e-12){hottest.Dequeue();volume-=low.VolumeCc;}
        }
        return volume+1e-12>=volumeCc&&hottest.TryPeek(out _,out var dose)?dose:null;
    }
}

sealed partial class PreviewSession
{
    ConcurrentDictionary<string,Lazy<object>> referenceCache=new();
    object DoseReference(System.Text.Json.JsonElement r){var selected=RequestDose(r);return referenceCache.GetOrAdd(CacheKey("reference:"+DoseId(selected)),_=>new Lazy<object>(()=>CalculateReference(selected),LazyThreadSafetyMode.ExecutionAndPublication)).Value;}
    object CalculateReference(DoseGrid dose)
    {
        if(!IsodoseConfiguration.IsPhysicalGy(dose))return new{available=false,prescriptions=Array.Empty<double>(),d0035=(double?)null,message="Keine physikalische Gy-Dosis."};
        var owner=plans.FirstOrDefault(p=>p.Entry.SopUid==dose.PlanUid);
        var rx=owner==null?Array.Empty<double>():RtDicom.Items(RtDicom.Full(owner.Entry),DicomTag.DoseReferenceSequence)
            .Where(d=>RtDicom.Text(d,DicomTag.DoseReferenceType)=="TARGET")
            .Select(d=>RtDicom.Number(d,DicomTag.TargetPrescriptionDose)).Where(d=>double.IsFinite(d)&&d>0).Select(d=>Math.Round(d,6)).Distinct().OrderBy(d=>d).ToArray();
        var candidates=owner==null?ActiveRois():structures.Where(s=>s.Entry.SopUid==owner.StructureSopUid).SelectMany(s=>s.Rois);
        var external=candidates.Where(r=>(r.InterpretedType??"").Trim().Equals("EXTERNAL",StringComparison.OrdinalIgnoreCase)).ToArray();
        if(external.Length==0)external=candidates.Where(r=>RoiClassification.Category(r,owner)==RoiCategory.External).ToArray();
        var masks=external.Select(r=>BodyMask.Create(r,Map(dose.FrameUid,r.FrameUid))).Where(m=>m!=null).ToArray();
        double? maximum=masks.Length==0?null:HotVolumeDose.Calculate(dose.Voxels(token),.035,p=>masks.Any(m=>m.Contains(p)),token);
        token.ThrowIfCancellationRequested();
        return new{available=maximum.HasValue&&maximum>0||rx.Length>0,prescriptions=rx,d0035=maximum,
            message=maximum.HasValue?"D0,035 cm³ · Körperkontur · native Dosisrasterwerte und Voxelvolumina (Näherung)":masks.Length==0?"D0,035 cm³ nicht verfügbar: keine unterstützte Körperkontur. Referenz manuell wählen.":"D0,035 cm³ nicht verfügbar: zu wenig bekanntes Dosisvolumen. Referenz manuell wählen."};
    }
    sealed class BodyMask
    {
        sealed class Polygon {public double[] X,Y;}
        sealed class Plane {public double Z;public List<Polygon> Polygons=new();}
        Matrix4 map;Vec3 normal,u,v;Plane[] planes;bool xor;double typical;
        public static BodyMask Create(StructureRoi roi,Matrix4 map)
        {
            if(map==null||!BeamProjection.IsRigid(map)||roi.Contours.Count<2)return null;
            bool xor=roi.Contours[0].GeometricType=="CLOSEDPLANAR_XOR";
            if(roi.Contours.Any(c=>c.Points.Count<3||c.GeometricType!=(xor?"CLOSEDPLANAR_XOR":"CLOSED_PLANAR")))return null;
            var first=roi.Contours[0].Points;Vec3 normal=new();for(int i=0;i<first.Count;i++)normal+=first[i].Cross(first[(i+1)%first.Count]);
            if(normal.Length<1e-8)return null;normal=normal.Normalized();var u=(first[1]-first[0]).Normalized();if(u.Length<.9)return null;var v=normal.Cross(u).Normalized();
            var planes=new List<Plane>();
            foreach(var contour in roi.Contours){
                double z=normal.Dot(contour.Points[0]);if(contour.Points.Any(p=>Math.Abs(normal.Dot(p)-z)>.05))return null;
                var plane=planes.FirstOrDefault(p=>Math.Abs(p.Z-z)<.01);if(plane==null){plane=new Plane{Z=z};planes.Add(plane);}
                plane.Polygons.Add(new Polygon{X=contour.Points.Select(p=>p.Dot(u)).ToArray(),Y=contour.Points.Select(p=>p.Dot(v)).ToArray()});
            }
            if(planes.Count<2)return null;var sorted=planes.OrderBy(p=>p.Z).ToArray();var gaps=sorted.Zip(sorted.Skip(1),(a,b)=>b.Z-a.Z).OrderBy(x=>x).ToArray();
            return new BodyMask{map=map,normal=normal,u=u,v=v,planes=sorted,xor=xor,typical=gaps[(gaps.Length-1)/2]};
        }
        public bool Contains(Vec3 point)
        {
            var p=map.Transform(point);double z=p.Dot(normal),x=p.Dot(u),y=p.Dot(v);int lo=0,hi=planes.Length-1;
            while(hi-lo>1){int mid=(hi+lo)/2;if(planes[mid].Z<z)lo=mid;else hi=mid;}
            int index=Math.Abs(planes[lo].Z-z)<Math.Abs(planes[hi].Z-z)?lo:hi;
            double gap=z<planes[index].Z&&index>0?planes[index].Z-planes[index-1].Z:index+1<planes.Length?planes[index+1].Z-planes[index].Z:typical;
            if(Math.Abs(z-planes[index].Z)>Math.Min(typical,gap)*.5+1e-6)return false;
            bool inside=false;
            foreach(var polygon in planes[index].Polygons){bool hit=false;for(int i=0,j=polygon.X.Length-1;i<polygon.X.Length;j=i++)
                if((polygon.Y[i]>y)!=(polygon.Y[j]>y)&&x<(polygon.X[j]-polygon.X[i])*(y-polygon.Y[i])/(polygon.Y[j]-polygon.Y[i])+polygon.X[i])hit=!hit;
                if(xor)inside^=hit;else inside|=hit;
            }
            return inside;
        }
    }
}
