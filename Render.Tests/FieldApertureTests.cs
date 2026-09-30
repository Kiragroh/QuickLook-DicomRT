using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using QuickLook.DicomRT;

internal static class FieldApertureTests
{
    public static void Run(Action<bool,string> check)
    {
        var block=BeamBlock.Create("APERTURE",new[]{-20d,-20,20,-20,20,0,0,0,0,20,-20,20},6);
        var blockPoint=new ControlPoint{Gantry=54,Collimator=32,Isocenter=new Vec3(35,-40,0),XJaws=new[]{-100d,100},YJaws=new[]{-100d,100},Blocks={block}};
        string blockReason;var blockBeam=new PlanBeam{PatientPosition="HFS",SourceAxisDistance=1000};var project=BeamProjection.Create(blockBeam,blockPoint,Matrix4.Identity,out blockReason);
        var blockVolume=new VolumeData{Width=101,Height=101,Depth=101,Origin=new Vec3(-100,-100,-100),AxisX=new Vec3(1,0,0),AxisY=new Vec3(0,1,0),AxisZ=new Vec3(0,0,1),SpacingX=2,SpacingY=2,SpacingZ=2};
        foreach(var plane in new[]{"Axial","Coronal","Sagittal"}){
            var slice=SliceGeometry.Create(new RenderScene{Volume=blockVolume,Plane=plane,Focus=blockPoint.Isocenter});var actual=FieldArrangementDrawing.Opening(project,blockPoint,slice).Clone();actual.Transform=new ScaleTransform(1000,1000);int compared=0;
            for(int y=0;y<37;y++)for(int x=0;x<37;x++){double u=(x+.37)/37,v=(y+.61)/37,bx,by;var world=slice.WorldAt(u,v);bool front=project.Project(world,out bx,out by);bool expected=front&&bx>=-20&&bx<=20&&by>=-20&&by<=20&&(bx<=0||by<=0);if(actual.FillContains(new Point(u*1000,v*1000),.000001,ToleranceType.Absolute)!=expected)throw new Exception("custom block slice disagrees with independent analytic ray projection in "+plane);compared++;}
            check(compared==1369,"rotated concave electron aperture matches ray projection in "+plane);
        }
        var motion=new PlanBeam{PatientPosition="HFS",SourceAxisDistance=1000};motion.ControlPoints.Add(new ControlPoint{Gantry=350,GantryRotationDirection="CW",DoseRateSet=600});motion.ControlPoints.Add(new ControlPoint{Gantry=10});
        check(BeamMotion.IsArc(motion)&&Math.Abs(BeamMotion.Travel(motion.ControlPoints[0],motion.ControlPoints[1])-20)<1e-8,"arc uses directed wraparound");
        var path=BeamMotion.Path(motion,Matrix4.Identity);check(path.Length==21&&path.Count(p=>p.Tick)==20&&path.All(p=>p.Rate==600),"one-degree arc source track carries segment rate without a duplicate end tick");
        motion.Meterset=200;motion.FinalCumulativeMetersetWeight=100;motion.PrimaryDosimeterUnit="MU";motion.ControlPoints[1].MetersetWeight=10;
        check(Math.Abs(BeamMotion.AngularMeterset(motion,motion.ControlPoints[0],motion.ControlPoints[1])-1)<1e-8,"angular modulation normalizes final weight and directed degrees");
        check(BeamMotion.Unit(motion,BeamModulationMode.AngularMeterset)=="MU/°"&&BeamMotion.Unit(motion,BeamModulationMode.PlannedRate)=="MU/min","rate and angular units explicitly distinct");
        motion.ControlPoints[1].MetersetWeight=20;check(BeamMotion.Path(motion,Matrix4.Identity).All(s=>s.Angular==2&&s.Rate==600),"variable meterset density independent of constant rate setting");
        motion.Meterset=double.NaN;check(double.IsNaN(BeamMotion.AngularMeterset(motion,motion.ControlPoints[0],motion.ControlPoints[1])),"missing meterset does not invent modulation");motion.Meterset=200;
        motion.ControlPoints[1].MetersetWeight=-1;check(double.IsNaN(BeamMotion.AngularMeterset(motion,motion.ControlPoints[0],motion.ControlPoints[1])),"decreasing weights rejected");motion.ControlPoints[1].MetersetWeight=20;
        motion.ControlPoints[1].Gantry=350;check(BeamMotion.IsArc(motion)&&BeamMotion.Path(motion,Matrix4.Identity).Length==361,"full rotation with identical endpoints retained at one-degree spacing");
        motion.ControlPoints[0].GantryRotationDirection="NONE";check(double.IsNaN(BeamMotion.AngularMeterset(motion,motion.ControlPoints[0],motion.ControlPoints[1])),"stationary gantry has no angular density");check(!BeamMotion.IsArc(motion),"fixed gantry beam has aperture instead of ring");
        check(BeamMotion.IsImaging(new PlanBeam{TreatmentDeliveryType="SETUP"})&&BeamMotion.IsImaging(new PlanBeam{Name="CBCT"}),"setup and CBCT recognized for explicit selection only");
        var cp=new ControlPoint{XJaws=new[]{-30d,30d},YJaws=new[]{-20d,20d}};
        cp.MlcLayers.Add(new MlcLayer{Type="MLCX",Boundaries=new[]{-20d,0d,20d},Positions=new[]{-20d,-10d,10d,20d}});
        var rectangles=BeamAperture.Rectangles(cp);check(rectangles.Count==2,"two distinct leaf openings retained");
        check(Math.Abs(rectangles.Sum(r=>(r.Right-r.Left)*(r.Top-r.Bottom))-1200)<1e-8,"leaf area is not the envelope");
        cp.MlcLayers.Add(new MlcLayer{Type="MLCY",Boundaries=new[]{-30d,0d,30d},Positions=new[]{-10d,-15d,15d,10d}});
        rectangles=BeamAperture.Rectangles(cp);check(rectangles.Count==4,"orthogonal dual-layer intersection partitions into four openings");
        check(Math.Abs(rectangles.Sum(r=>(r.Right-r.Left)*(r.Top-r.Bottom))-700)<1e-8,"both leaf layers clip the opening");
        cp.MlcLayers.RemoveAt(1);string reason;var projection=BeamProjection.Create(new PlanBeam{PatientPosition="HFS",SourceAxisDistance=1000},cp,Matrix4.Identity,out reason);
        var volume=new VolumeData{Width=101,Height=101,Depth=101,Origin=new Vec3(-50,-50,-50),AxisX=new Vec3(1,0,0),AxisY=new Vec3(0,1,0),AxisZ=new Vec3(0,0,1),SpacingX=1,SpacingY=1,SpacingZ=1};
        foreach(double y in new[]{-200d,0d,200d}){
            var g=SliceGeometry.Create(new RenderScene{Volume=volume,Plane="Coronal",Focus=new Vec3(0,y,0)});
            var shape=FieldArrangementDrawing.Opening(projection,cp,g);
            double factor=(1000+y)/1000;
            Func<double,double,Point> screen=(x,z)=>new Point(g.U(new Vec3(x*factor,y,z*factor)),g.V(new Vec3(x*factor,y,z*factor)));
            check(shape.FillContains(screen(-15,-10),.000001,ToleranceType.Absolute),"aperture open leaf scales with source distance");
            check(!shape.FillContains(screen(-15,10),.000001,ToleranceType.Absolute),"MLC notch remains closed");
            check(!shape.StrokeContains(new Pen(Brushes.White,.001),screen(0,0),.000001,ToleranceType.Absolute),"merged opening has no interior leaf seam");
        }
        cp.XJaws=new[]{-5d,5d};check(BeamAperture.Rectangles(cp).All(r=>r.Left>=-5&&r.Right<=5),"jaws additionally clip MLC");
        cp.MlcLayers[0].Positions=new[]{5d,5d,-5d,-5d};check(BeamAperture.Rectangles(cp).Count==0,"closed and crossed leaves do not invent an opening");
        check(BeamAperture.Rectangles(new ControlPoint()).Count==0,"missing aperture is not replaced with default rectangle");
    }
}
