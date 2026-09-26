using System;
using System.Linq;
using System.Threading;
using QuickLook.DicomRT;
internal static class BeamProjectionScenarios
{
    public static void Private(string folder)
    {
        var catalog=DicomCatalog.Scan(System.IO.Directory.EnumerateFiles(folder).First(),CancellationToken.None);
        var registrations=RegistrationReader.Read(catalog);int plans=0,beams=0,points=0,valid=0,matched=0;
        foreach(var entry in catalog.Files.Where(e=>e.Modality=="RTPLAN")){
            var plan=PlanData.Load(entry);plans++;
            var ct=catalog.Stacks.FirstOrDefault(s=>s.Modality=="CT"&&RegistrationReader.Resolve(registrations,plan.FrameUid,s.FrameUid)!=null);if(ct!=null)matched++;
            foreach(var beam in plan.Beams){beams++;foreach(var cp in beam.ControlPoints){points++;string reason;if(BeamProjection.Create(beam,cp,Matrix4.Identity,out reason)!=null)valid++;}}
        }
        if(plans==0||valid==0)throw new Exception("No supported projection geometry");
        Console.WriteLine("PASS private beam geometry: plans="+plans+" associated_CT_plans="+matched+" beams="+beams+" control_points="+points+" supported_projection_points="+valid);
    }
    public static void Run()
    {
        Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception("Projection: "+label);};
        Action<double,double> near=(a,b)=>check(Math.Abs(a-b)<1e-6,"numeric mismatch");
        var beam=new PlanBeam{PatientPosition="HFS",SourceAxisDistance=1000};var cp=new ControlPoint{Isocenter=new Vec3()};string why;
        var p=BeamProjection.Create(beam,cp,Matrix4.Identity,out why);check(p!=null,why);near(p.Source.Y,-1000);near(p.Right.X,1);near(p.Up.Z,1);
        double x,y;p.Project(new Vec3(10,-500,20),out x,out y);near(x,20);near(y,40);
        p.Project(new Vec3(10,500,20),out x,out y);near(x,10/1.5);near(y,20/1.5);
        check(!p.Project(new Vec3(0,-1100,0),out x,out y),"behind source rejected");
        cp.Gantry=90;cp.Couch=90;p=BeamProjection.Create(beam,cp,Matrix4.Identity,out why);near(p.Source.Z,-1000);near(p.Right.Y,1);near(p.Up.X,1);
        cp.Gantry=0;cp.Couch=0;cp.Collimator=90;p=BeamProjection.Create(beam,cp,Matrix4.Identity,out why);near(p.Right.Z,1);near(p.Up.X,-1);
        foreach(var position in new[]{"HFS","HFP","FFS","FFP","HFDR","HFDL","FFDR","FFDL"})foreach(double couch in new[]{0d,90d,270d})foreach(double gantry in new[]{0d,71d,90d,180d,270d}){
            beam.PatientPosition=position;cp.Couch=couch;cp.Gantry=gantry;cp.Collimator=37;p=BeamProjection.Create(beam,cp,Matrix4.Identity,out why);
            near(p.Right.Length,1);near(p.Up.Length,1);near(p.Right.Dot(p.Up),0);near(p.Right.Cross(p.Up).Dot(p.Forward),-1);
            p.Project(p.PlanePoint(12,-7),out x,out y);near(x,12);near(y,-7);
        }
        beam.PatientPosition="HFS";cp.Couch=cp.Gantry=cp.Collimator=0;
        var map=new Matrix4(new double[]{0,-1,0,30,1,0,0,40,0,0,1,50,0,0,0,1});p=BeamProjection.Create(beam,cp,map,out why);p.Project(map.Transform(new Vec3(10,-500,20)),out x,out y);near(x,20);near(y,40);
        var volume=new VolumeData{Width=11,Height=11,Depth=11,Values=new float[1331],Origin=new Vec3(-5,-5,-5),AxisX=new Vec3(1,0,0),AxisY=new Vec3(0,1,0),AxisZ=new Vec3(0,0,1),SpacingX=1,SpacingY=1,SpacingZ=1};
        p=BeamProjection.Create(beam,cp,Matrix4.Identity,out why);var integral=p.Integrate(volume,1,3,.7,CancellationToken.None);near(integral[4],10);
        for(int i=0;i<volume.Values.Length;i++)volume.Values[i]=-1000;near(p.Integrate(volume,1,3,1,CancellationToken.None)[4],0);
        bool cancelled=false;try{p.Integrate(volume,1,3,1,new CancellationToken(true));}catch(OperationCanceledException){cancelled=true;}check(cancelled,"ray cancellation");
        cp.TablePitch=2;check(BeamProjection.Create(beam,cp,Matrix4.Identity,out why)==null,"unsupported pitch blocked");cp.TablePitch=0;beam.SourceAxisDistance=double.NaN;check(BeamProjection.Create(beam,cp,Matrix4.Identity,out why)==null,"unknown SAD blocked");beam.SourceAxisDistance=1000;beam.PatientPosition="";check(BeamProjection.Create(beam,cp,Matrix4.Identity,out why)==null,"unknown setup blocked");
        check(!BeamProjection.IsRigid(new Matrix4(new double[]{2,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1})),"scaled registration blocked");
        Console.WriteLine("PASS: DRR analytic ray integrals, magnification, registration, 8 patient positions, gantry/couch/collimator axes and unsupported-geometry gates");
    }
}
