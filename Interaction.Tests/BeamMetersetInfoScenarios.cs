using System;
using System.Reflection;
using QuickLook.DicomRT;

static class BeamMetersetInfoScenarios
{
    public static void Run(Action<bool,string> check)
    {
        var type=typeof(ViewerControl).Assembly.GetType("QuickLook.DicomRT.BeamMetersetInfo");
        var at=type.GetMethod("At",BindingFlags.Static|BindingFlags.NonPublic);
        Func<PlanBeam,double,object> read=(b,p)=>at.Invoke(null,new object[]{b,p});
        Func<object,string,string> field=(o,n)=>(string)type.GetField(n).GetValue(o);
        var beam=new PlanBeam {PrimaryDosimeterUnit="MU",Meterset=100,FinalCumulativeMetersetWeight=1,
            ControlPoints={new ControlPoint{Gantry=350,GantryRotationDirection="CW",MetersetWeight=0},new ControlPoint{Gantry=354,GantryRotationDirection="CW",MetersetWeight=.004},new ControlPoint{Gantry=0,MetersetWeight=1}}};
        var first=read(beam,0);var between=read(beam,.9);var second=read(beam,1);var last=read(beam,2);
        check(field(first,"Text").EndsWith("MU/°")&&!field(first,"Text").StartsWith("0 MU"),"Small nonzero MU per degree remains visible numerically");
        check(field(first,"Text")==field(between,"Text")&&field(between,"Detail").Contains("CP 1 → 2"),"Fractional CP readout refers to its enclosing interval");
        check(field(second,"Detail").Contains("CP 2 → 3")&&field(second,"Detail").Contains("6°"),"Exact CP boundary moves to next interval across the angular wrap");
        check(field(last,"Text").Contains("last interval")&&field(last,"Detail").Contains("end of field"),"Last CP explicitly reports the preceding interval instead of a false zero");
        beam.ControlPoints[1].MetersetWeight=0;check(field(read(beam,0),"Text")=="0 MU/°","A genuinely zero interval is displayed as zero");
        beam.ControlPoints[0].GantryRotationDirection="NONE";beam.ControlPoints[1].Gantry=350;
        check(field(read(beam,0),"Text").StartsWith("—")&&field(read(beam,0),"Detail").Contains("undefined"),"Stationary field shows undefined angular meterset rather than zero");
        beam.Meterset=double.NaN;check(field(read(beam,1),"Text").StartsWith("—"),"Missing beam meterset does not invent a zero");
        string tiny=(string)type.GetMethod("Number",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{.0000008});
        check(tiny.Contains("E")&&!tiny.StartsWith("0"),"Very small nonzero values use scientific notation instead of rounding to zero");
        beam.Meterset=100;beam.PrimaryDosimeterUnit="CGY";check(field(read(beam,1),"Text").Contains("cGy/°"),"Non-MU dosimeter units remain correctly labeled");
    }
}
