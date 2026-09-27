using System;
using System.Collections.Generic;
using System.Linq;
namespace QuickLook.DicomRT
{
    public static class BeamMotion
    {
        public static bool IsImaging(PlanBeam b)=>b.TreatmentDeliveryType=="SETUP"||b.TreatmentDeliveryType=="PORTFILM"||b.TreatmentDeliveryType=="OPEN_PORTFILM"||string.Equals(b.Name,"CBCT",StringComparison.OrdinalIgnoreCase)||string.Equals(b.Name,"BCT",StringComparison.OrdinalIgnoreCase);
        public static double Travel(ControlPoint a,ControlPoint b)
        {
            if(!BeamProjection.Finite(a.Gantry)||!BeamProjection.Finite(b.Gantry))return double.NaN;
            if(a.GantryRotationDirection=="CW"||a.GantryRotationDirection=="CC"){
                int sign=a.GantryRotationDirection=="CW"?1:-1;double delta=((b.Gantry-a.Gantry)*sign%360+360)%360;
                return sign*(delta<1e-8?360:delta);
            }
            double shortest=((b.Gantry-a.Gantry)%360+540)%360-180;
            return Math.Abs(shortest)<1e-6?0:double.NaN;
        }
        public static bool IsArc(PlanBeam b)=>b.ControlPoints.Zip(b.ControlPoints.Skip(1),(a,c)=>Math.Abs(Travel(a,c))>1e-5||Math.Abs(a.Gantry-c.Gantry)>1e-5).Any(x=>x);
        public sealed class Sample {public Vec3 Iso,SourceDirection;public double Rate;public bool Tick,Break;}
        // Projected schematic source track. Radius is chosen by the view, never a fabricated SAD.
        public static Sample[] Path(PlanBeam beam,Matrix4 map)
        {
            var points=new List<Sample>();
            for(int i=0;i+1<beam.ControlPoints.Count;i++){
                var a=beam.ControlPoints[i];var b=beam.ControlPoints[i+1];double travel=Travel(a,b);if(!BeamProjection.Finite(travel))continue;
                int count=Math.Max(1,(int)Math.Ceiling(Math.Abs(travel)/2));
                for(int k=0;k<=count;k++){
                    double t=k/(double)count;
                    // Changing support angles cannot be inferred without their rotation direction.
                    if(Math.Abs(a.Couch-b.Couch)>1e-6)continue;
                    var cp=new ControlPoint{Gantry=a.Gantry+travel*t,Couch=a.Couch,Collimator=a.Collimator,Isocenter=a.Isocenter+(b.Isocenter-a.Isocenter)*t,GantryPitch=a.GantryPitch,TablePitch=a.TablePitch,TableRoll=a.TableRoll,TableEccentric=a.TableEccentric};
                    string reason;var p=BeamProjection.Create(beam,cp,map,out reason);if(p==null)continue;
                    points.Add(new Sample{Iso=p.Iso,SourceDirection=(p.Source-p.Iso).Normalized(),Rate=a.DoseRateSet,Tick=k==0,Break=k==0});
                }
            }
            return points.ToArray();
        }
    }
}
