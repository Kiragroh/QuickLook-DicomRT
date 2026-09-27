using System;
using System.Linq;
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
        var cp=new ControlPoint{GantryRotationDirection="CW",CollimatorRotationDirection="CC",CouchRotationDirection="NONE"};beam.ControlPoints.Add(cp);beam.ControlPoints.Add(new ControlPoint{Gantry=10,Collimator=15});var plan=new PlanData();plan.Beams.Add(beam);
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
            check(image.IsFrozen&&image.PixelWidth==192,"background DRR is immutable and bounded 192 resolution");
            var refined=type.GetMethod("Refine").Invoke(cache,new object[]{projection,ct,new RoiOverlay[0],100d,CancellationToken.None});
            check(((BitmapSource)refined.GetType().GetField("Drr").GetValue(refined)).PixelWidth==384,"stationary DRR refines to 384 without clearing cached frame");
            var unrequested=BeamProjection.Create(beam,MlcTimeline.Interpolate(cp,beam.ControlPoints[1],.2),Matrix4.Identity,out why);
            check(!(bool)type.GetMethod("TryGet").Invoke(cache,new object[]{unrequested,ct,scene.Structures.ToArray(),100d,true,null}),"unused fractional views do not multiply whole-plan preload");
            type.GetMethod("PrepareNearby").Invoke(cache,new object[]{beam,0d,Matrix4.Identity,null,scene.Structures.Take(1).ToArray(),.2,true});
            ((Task)type.GetProperty("WarmCompletion").GetValue(cache)).GetAwaiter().GetResult();
            check((bool)type.GetMethod("TryGet").Invoke(cache,new object[]{unrequested,null,scene.Structures.Take(1).ToArray(),100d,false,null}),"outline lookahead works with DRR disabled");
            check(!(bool)type.GetMethod("TryGet").Invoke(cache,new object[]{unrequested,ct,new RoiOverlay[0],100d,true,null}),"DRR-disabled lookahead does not ray trace hidden fractional images");
            type.GetMethod("PrepareNearby").Invoke(cache,new object[]{beam,0d,Matrix4.Identity,ct,scene.Structures.ToArray(),.2,true});
            ((Task)type.GetProperty("WarmCompletion").GetValue(cache)).GetAwaiter().GetResult();
            check((bool)type.GetProperty("PlaybackPrepared").GetValue(cache),"selected field buffer includes every intermediate playback view before ready");
            var completeTask=type.GetProperty("WarmCompletion").GetValue(cache);
            type.GetMethod("PrepareNearby").Invoke(cache,new object[]{beam,.4,Matrix4.Identity,ct,scene.Structures.ToArray(),.2,true});
            check(ReferenceEquals(completeTask,type.GetProperty("WarmCompletion").GetValue(cache)),"prepared playback does not start cache-hit workers or flicker the busy indicator");
            for(int n=1;n<5;n++){
                var fractional=BeamProjection.Create(beam,MlcTimeline.Interpolate(cp,beam.ControlPoints[1],n*.2),Matrix4.Identity,out why);
                var fractionalArgs=new object[]{fractional,ct,scene.Structures.ToArray(),100d,true,null};
                check((bool)type.GetMethod("TryGet").Invoke(cache,fractionalArgs),"active-field lookahead prepares all selected exact-angle fractional overlays");
            }
            // Repeated complete previews must be pure hits, even after a speculative scan.
            int generated=0;Action notification=()=>Interlocked.Increment(ref generated);type.GetEvent("FrameReady").AddEventHandler(cache,notification);
            for(int repeat=0;repeat<3;repeat++)for(int n=0;n<=5;n++){
                var same=BeamProjection.Create(beam,MlcTimeline.Interpolate(cp,beam.ControlPoints[1],n*.2),Matrix4.Identity,out why);
                check((bool)type.GetMethod("TryGet").Invoke(cache,new object[]{same,ct,scene.Structures.ToArray(),100d,true,null}),"repeated beam retains matching DRR and all three ROI categories");
            }
            check(generated==0,"three repeated prepared beams require zero projection generation");type.GetEvent("FrameReady").RemoveEventHandler(cache,notification);
            type.GetMethod("PrepareNearby").Invoke(cache,new object[]{beam,.08,Matrix4.Identity,ct,scene.Structures.ToArray(),.08,true});
            ((Task)type.GetProperty("WarmCompletion").GetValue(cache)).GetAwaiter().GetResult();
            var slowProjection=BeamProjection.Create(beam,MlcTimeline.Interpolate(cp,beam.ControlPoints[1],.16),Matrix4.Identity,out why);
            var slowArgs=new object[]{slowProjection,ct,scene.Structures.ToArray(),100d,true,null};
            check((bool)type.GetMethod("TryGet").Invoke(cache,slowArgs),"lookahead also warms exact fractional positions at a different playback speed");
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
