using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using QuickLook.DicomRT;
internal static partial class Program
{
    static async Task ArcBandReview(string folder)
    {
        await Load(folder,"approved public nonpatient benchmark");Layers(true,true);SelectTourRois();Panels(false,false);
        var plan=Get<PlanData>(viewer,"selectedPlan");var beam=plan.Beams.First(BeamMotion.IsArc);
        var mlc=Get<MlcPlaybackControl>(viewer,"centralPlayback");mlc.Navigate(beam,(beam.ControlPoints.Count-1)*.4);
        Mode("Bild");Get<ComboBox>(viewer,"planes").SelectedItem="MPR + 3D";Get<CheckBox>(viewer,"showFields").IsChecked=true;
        await Task.Delay(1000);await Settle();var three=Get<ThreeDControl>(viewer,"threeDView");
        Invoke(three,"ResetCamera");Invoke(three,"FitFieldGuides");await Save("arc-profile-mpr.png","Actual approved public benchmark: dense radial angular meterset samples with linear per-field scales.");
        Mode("3D");await Settle();await Save("arc-profile-3d.png","Actual 3D beam paths: radial modulation at one-degree visual spacing, active source and collimator-oriented aperture.");
        Console.WriteLine("ARC_BAND_REVIEW_PASS");
    }
}
