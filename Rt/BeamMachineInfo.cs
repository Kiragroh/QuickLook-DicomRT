using System;
using System.Linq;
namespace QuickLook.DicomRT
{
    public enum LinacForm { Unknown, CArm, Ring }
    public static class BeamMachineInfo
    {
        // Model identity is independent of MLC layer count. Local station aliases
        // and filenames do not establish a manufacturer/model.
        public static LinacForm Form(PlanBeam beam)
        {
            var model=(beam?.ManufacturerModelName??"").ToUpperInvariant();
            var tokens=model.Split(new[]{' ','-','_','/',','},StringSplitOptions.RemoveEmptyEntries);
            if(tokens.Any(t=>t=="HALCYON"||t=="ETHOS"))return LinacForm.Ring;
            if(tokens.Any(t=>t=="ACCELA"||t=="TRUEBEAM"||t=="CLINAC"||t=="VERSA"||t=="SYNERGY"))return LinacForm.CArm;
            return LinacForm.Unknown;
        }
        public static string Label(PlanBeam beam)
        {
            if(beam==null)return "Machine not specified";
            var values=new[]{beam.TreatmentMachineName,beam.ManufacturerModelName}.Where(s=>!string.IsNullOrWhiteSpace(s)).Distinct().ToArray();
            return values.Length==0?"Machine not specified":string.Join(" · ",values);
        }
        public static string Detail(PlanBeam beam)
        {
            if(beam==null)return "No beam selected";
            int layers=beam.ControlPoints.FirstOrDefault()?.MlcLayers.Count??0;
            return Label(beam)+"\nManufacturer: "+(string.IsNullOrWhiteSpace(beam.Manufacturer)?"not specified":beam.Manufacturer)+
                "\n"+layers+" MLC layer(s) · "+(beam.EnhancedDevices?"Enhanced indexed devices":"Classic devices")+
                (beam.DynamicCollimator?"\nDynamic collimator · CP preview, not delivery timing":"")+
                "\n"+(Form(beam)==LinacForm.Unknown?"Model not recognized: generic schematic":"Model-based "+(Form(beam)==LinacForm.Ring?"ring":"C-arm")+" schematic")+"; not machine dimensions";
        }
    }
}
