using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using QuickLook.DicomRT;

internal static class FieldApertureTests
{
    public static void Run(Action<bool,string> check)
    {
        var motion=new PlanBeam{PatientPosition="HFS",SourceAxisDistance=1000};motion.ControlPoints.Add(new ControlPoint{Gantry=350,GantryRotationDirection="CW",DoseRateSet=600});motion.ControlPoints.Add(new ControlPoint{Gantry=10});
        check(BeamMotion.IsArc(motion)&&Math.Abs(BeamMotion.Travel(motion.ControlPoints[0],motion.ControlPoints[1])-20)<1e-8,"arc uses directed wraparound");
        var path=BeamMotion.Path(motion,Matrix4.Identity);check(path.Length==11&&path.All(p=>p.Rate==600),"arc source track carries segment rate");
        motion.ControlPoints[1].Gantry=350;check(BeamMotion.IsArc(motion)&&BeamMotion.Path(motion,Matrix4.Identity).Length==181,"full rotation with identical endpoints retained");
        motion.ControlPoints[0].GantryRotationDirection="NONE";check(!BeamMotion.IsArc(motion),"fixed gantry beam has aperture instead of ring");
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
