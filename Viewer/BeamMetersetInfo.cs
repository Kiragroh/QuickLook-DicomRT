using System;
using System.Globalization;

namespace QuickLook.DicomRT
{
    internal sealed class BeamMetersetInfo
    {
        public string Text,Detail;
        internal static string Number(double value)
        {
            if(!BeamProjection.Finite(value))return "unavailable";
            return value!=0&&Math.Abs(value)<.0001?value.ToString("0.###E+0",CultureInfo.CurrentCulture):value.ToString("0.####",CultureInfo.CurrentCulture);
        }
        internal static BeamMetersetInfo At(PlanBeam beam,double position)
        {
            if(beam==null)return new BeamMetersetInfo {Text="",Detail="Select a field."};
            string unit=BeamMotion.Unit(beam,BeamModulationMode.AngularMeterset);
            if(beam.ControlPoints.Count<2||!BeamProjection.Finite(position))return new BeamMetersetInfo {Text="— "+unit,Detail="Not enough control points to calculate angular meterset."};
            bool last=position>=beam.ControlPoints.Count-1;
            int i=Math.Max(0,Math.Min(beam.ControlPoints.Count-2,(int)Math.Floor(position)));
            var a=beam.ControlPoints[i];var b=beam.ControlPoints[i+1];double angle=Math.Abs(BeamMotion.Travel(a,b));
            double value=BeamMotion.AngularMeterset(beam,a,b);
            string interval="CP "+(i+1)+" → "+(i+2)+(last?" (last interval; end of field)":"");
            string text=BeamProjection.Finite(value)?Number(value):"—";
            string detail=interval+"\n";
            if(BeamProjection.Finite(value))detail+=Number(value*angle)+" "+(beam.PrimaryDosimeterUnit=="MU"?"MU":beam.PrimaryDosimeterUnit=="CGY"?"cGy":"meterset")+" / "+Number(angle)+"° = "+Number(value)+" "+unit;
            else detail+=BeamProjection.Finite(angle)&&angle<1e-6?"No gantry rotation: MU per degree is undefined, not zero.":"Angular meterset unavailable: missing or inconsistent meterset / gantry information.";
            detail+="\nPlanned interval average, not a measured dose rate. Fractional CP positions use their enclosing interval.";
            return new BeamMetersetInfo {Text=text+" "+unit+(last?" (last interval)":""),Detail=detail};
        }
    }
}
