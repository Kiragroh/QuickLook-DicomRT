using System;
using System.Globalization;
namespace QuickLook.DicomRT
{
    internal static class RtObjectLabels
    {
        internal static string Plan(PlanData plan,int ordinal)
        {
            var label=plan?.Label?.Trim();
            return string.IsNullOrWhiteSpace(label)||string.Equals(label,"None",StringComparison.OrdinalIgnoreCase)||label=="RTPLAN"
                ?"RT-Plan "+ordinal+" · "+(plan?.Beams.Count??0)+" fields":label;
        }
        internal static string Dose(DoseGrid dose,int ordinal)=>"Dose "+ordinal+" · "+(string.IsNullOrWhiteSpace(dose.SummationType)?"type unknown":dose.SummationType)+" · max "+dose.Maximum.ToString("0.##",CultureInfo.InvariantCulture)+" "+(string.Equals(dose.Units,"GY",StringComparison.OrdinalIgnoreCase)?"Gy":dose.Units??"unit unknown");
        internal static string DoseDetails(DoseGrid dose)=>Original(dose.Label)+"\n"+Dose(dose,1).Substring("Dose 1 · ".Length);
        internal static string Original(string label)=>"Original DICOM label: "+(string.IsNullOrWhiteSpace(label)?"(empty)":label);
    }
    public sealed partial class ViewerControl
    {
        string PlanDisplay(PlanData plan)=>RtObjectLabels.Plan(plan,Math.Max(1,planData.IndexOf(plan)+1));
        string DoseDisplay(DoseGrid dose)=>doses.Contains(dose)?RtObjectLabels.Dose(dose,doses.IndexOf(dose)+1):"Generated sum · "+dose.Label;
    }
}
