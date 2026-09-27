using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using QuickLook.DicomRT;

internal static class ProjectionCacheScenarios
{
    public static void Run(Action<bool,string> check)
    {
        var type=typeof(ViewerControl).Assembly.GetType("QuickLook.DicomRT.MlcProjectionCache");
        var cache=Activator.CreateInstance(type);var beam=new PlanBeam{PatientPosition="HFS",SourceAxisDistance=1000};
        var cp=new ControlPoint();beam.ControlPoints.Add(cp);var plan=new PlanData();plan.Beams.Add(beam);
        var ct=new VolumeData{Width=11,Height=11,Depth=11,Values=new float[1331],Origin=new Vec3(-5,-5,-5),AxisX=new Vec3(1,0,0),AxisY=new Vec3(0,1,0),AxisZ=new Vec3(0,0,1),SpacingX=1,SpacingY=1,SpacingZ=1};
        var scene=new RenderScene{Entry=new DicomEntry{Modality="CT"},Volume=ct};
        foreach(var kind in new[]{"PTV","ORGAN","GTV"}){var roi=new StructureRoi{Name="Fixture "+kind,InterpretedType=kind};foreach(double z in new[]{-2d,0d,2d})roi.Contours.Add(new Contour{GeometricType="CLOSED_PLANAR",Points=new System.Collections.Generic.List<Vec3>{new Vec3(-2,-2,z),new Vec3(2,-2,z),new Vec3(2,2,z),new Vec3(-2,2,z)}});scene.Structures.Add(new RoiOverlay{Roi=roi});}
string why;var projection=BeamProjection.Create(beam,cp,Matrix4.Identity,out why);
        try{
            type.GetMethod("Configure").Invoke(cache,new object[]{plan,scene,Matrix4.Identity});
            ((Task)type.GetProperty("WarmCompletion").GetValue(cache)).GetAwaiter().GetResult();
            var args=new object[]{projection,ct,new RoiOverlay[0],100d,true,null};
            check((bool)type.GetMethod("TryGet").Invoke(cache,args),"DRR prepared globally before opening MLC");
            foreach(var roi in scene.Structures){var subset=new object[]{projection,ct,new[]{roi},100d,false,null};check((bool)type.GetMethod("TryGet").Invoke(cache,subset),"PTV/ORGAN/Other projection ready without enabling its checkbox");var ready=subset[5];check(((System.Collections.ICollection)ready.GetType().GetField("Outlines").GetValue(ready)).Count==1,"independently cached ROI has actual silhouette");}
            var frame=args[5];var image=(BitmapSource)frame.GetType().GetField("Drr").GetValue(frame);
            check(image.IsFrozen&&image.PixelWidth==384,"cached DRR is immutable and full 384 resolution");
            type.GetMethod("Configure").Invoke(cache,new object[]{new PlanData(),scene,Matrix4.Identity});
            args[5]=null;check((bool)type.GetMethod("TryGet").Invoke(cache,args),"switching plan retains previous cached projection");
            var other=new VolumeData();args[1]=other;args[5]=null;check(!(bool)type.GetMethod("TryGet").Invoke(cache,args),"cached anatomy cannot cross CT identity");
            args[1]=ct;args[2]=new[]{new RoiOverlay{Roi=new StructureRoi(),RoiToImage=Matrix4.Identity}};args[4]=false;
            check(!(bool)type.GetMethod("TryGet").Invoke(cache,args),"changed outline selection cannot reuse stale outlines");
            args[2]=new RoiOverlay[0];args[0]=BeamProjection.Create(beam,new ControlPoint{Gantry=90},Matrix4.Identity,out why);args[4]=true;
            check(!(bool)type.GetMethod("TryGet").Invoke(cache,args),"new beam angle does not display prior projection");
        }finally{((IDisposable)cache).Dispose();}
    }
}
