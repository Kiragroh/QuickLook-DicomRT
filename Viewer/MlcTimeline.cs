using System;
using System.Linq;
namespace QuickLook.DicomRT
{
    public static class MlcTimeline
    {
        // Anchor preview samples to each beam, not to the last mouse/slider position
        // or the number of CPs in preceding beams. Preload and playback share this grid.
        public static double NextLocal(double local,double last,double step)
        {
            if(step<=0||double.IsNaN(step)||double.IsInfinity(step))throw new ArgumentOutOfRangeException(nameof(step));
            return Math.Min(last,Math.Round((Math.Floor(Math.Round(local/step,8))+1)*step,8));
        }
        public static double NextPlayback(int[] counts,double cursor,double step)
        {
            int beam;double local;Locate(counts,cursor,out beam,out local);if(beam<0)return 0;
            int offset=counts.Take(beam).Sum();
            if(local<counts[beam]-1)return offset+NextLocal(local,counts[beam]-1,step);
            offset+=counts[beam];for(int next=beam+1;next<counts.Length;next++){if(counts[next]>0)return offset;offset+=counts[next];}
            return 0;
        }
        public static double WheelStep(double cursor,double maximum,int delta,ref int remainder)
        {
            // Preserve high-resolution wheel deltas. One detent is one tenth CP.
            remainder=0;return Math.Max(0,Math.Min(maximum,cursor-delta/1200.0));
        }
        public static double RecordedWheelStep(double cursor,double maximum,int delta,ref int remainder)
        {
            remainder+=delta;int detents=remainder/120;remainder-=detents*120;
            if(detents==0)return cursor;
            double next=detents<0?Math.Ceiling(cursor+1e-8)-detents-1:Math.Floor(cursor-1e-8)-detents+1;
            return Math.Max(0,Math.Min(maximum,next));
        }
        public static ControlPoint Interpolate(ControlPoint a,ControlPoint b,double t)
        {
            return new ControlPoint {Blocks=a.Blocks,DoseRateSet=t<1?a.DoseRateSet:b.DoseRateSet,Gantry=Angle(a.Gantry,b.Gantry,t,a.GantryRotationDirection,true),Couch=Angle(a.Couch,b.Couch,t,a.CouchRotationDirection,false),Collimator=Angle(a.Collimator,b.Collimator,t,a.CollimatorRotationDirection,false),
                Isocenter=a.Isocenter+(b.Isocenter-a.Isocenter)*t,GantryPitch=a.GantryPitch+(b.GantryPitch-a.GantryPitch)*t,TablePitch=a.TablePitch+(b.TablePitch-a.TablePitch)*t,TableRoll=a.TableRoll+(b.TableRoll-a.TableRoll)*t,TableEccentric=a.TableEccentric+(b.TableEccentric-a.TableEccentric)*t,
                XJaws=Positions(a.XJaws,b.XJaws,t),YJaws=Positions(a.YJaws,b.YJaws,t),MlcLayers=Layers(a,b,t).ToList()};
        }
        // Display order only: keep source beam identity and relative order within each group.
        public static PlanBeam[] PlaybackOrder(PlanData plan) => (plan?.Beams??new System.Collections.Generic.List<PlanBeam>()).OrderBy(b=>
            b.TreatmentDeliveryType=="SETUP"||b.TreatmentDeliveryType=="PORTFILM"?2:
            b.ControlPoints.Any(c=>c.MlcLayers.Count>0||(c.MlcPositions?.Length??0)>0)?0:1).ToArray();
        public static MlcLayer[] Layers(ControlPoint a, ControlPoint b, double fraction)
        {
            var left=a.MlcLayers;var right=b.MlcLayers;
            if(left.Count!=right.Count)throw new ArgumentException("MLC layers differ.");
            return left.Select(layer=>{
                var other=right.SingleOrDefault(x=>x.Key==layer.Key);
                if(other==null || other.Type!=layer.Type || !layer.Boundaries.SequenceEqual(other.Boundaries))throw new ArgumentException("MLC geometry differs.");
                return new MlcLayer {Key=layer.Key,Type=layer.Type,Boundaries=(double[])layer.Boundaries.Clone(),Positions=Positions(layer.Positions,other.Positions,fraction)};
            }).ToArray();
        }
        // IEC FIXED coordinates: +Y points towards gantry, +Z up; normalized schematic source/couch axes.
        public static double[] SourceDirection(double gantry) {double r=gantry*Math.PI/180;return new[]{Math.Sin(r),0,Math.Cos(r)};}
        public static double[] CouchDirection(double couch) {double r=couch*Math.PI/180;return new[]{-Math.Sin(r),Math.Cos(r),0};}
        // Each beam owns its CP range; never interpolate across a field boundary.
        public static void Locate(int[] counts,double cursor,out int beam,out double local)
        {
            beam=-1;local=0;int offset=0;
            for(int i=0;i<counts.Length;i++)
            {
                int n=Math.Max(0,counts[i]);if(n>0){beam=i;local=Math.Max(0,Math.Min(n-1,Math.Round(cursor-offset,8)));if(cursor<offset+n)return;}offset+=n;
            }
        }
        public static double Angle(double a, double b, double fraction, string direction, bool clockwiseIncreases)
        {
            if(double.IsNaN(a)||double.IsNaN(b))return double.NaN;
            double t=Math.Max(0,Math.Min(1,fraction));
            if(t==0)return (a%360+360)%360;if(t==1)return (b%360+360)%360;
            if(direction=="NONE")return Math.Abs(((b-a)%360+540)%360-180)<1e-6?(a%360+360)%360:double.NaN;
            if(direction!="CW"&&direction!="CC")return double.NaN;
            int sign=(direction=="CW")==clockwiseIncreases?1:-1;
            double distance=((b-a)*sign%360+360)%360;if(distance<1e-9)distance=360;
            return ((a+sign*distance*t)%360+360)%360;
        }
        public static double Angle(double a, double b, double fraction)
        {
            double delta = ((b-a)%360+540)%360-180;
            return ((a+delta*Math.Max(0,Math.Min(1,fraction)))%360+360)%360;
        }
        public static double[] Positions(double[] a, double[] b, double fraction)
        {
            a = a ?? new double[0]; b = b ?? new double[0];
            if (a.Length != b.Length) throw new ArgumentException("Leaf/jaw arrays differ in length.");
            var result = new double[a.Length]; double t = Math.Max(0,Math.Min(1,fraction));
            for (int i=0;i<result.Length;i++) result[i]=a[i]+(b[i]-a[i])*t;
            return result;
        }
    }
}
