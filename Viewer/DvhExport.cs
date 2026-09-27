using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QuickLook.DicomRT
{
    public sealed partial class DvhControl
    {
        StructureRoi[] exportRois=new StructureRoi[0];
        void InitializeExport()
        {
            var menu=new ContextMenu{Background=Theme.Panel,Foreground=Theme.Foreground};
            var csv=new MenuItem{Header="Export curves and metrics (CSV)",ToolTip="All enabled structures with dose, cumulative volume, coverage and calculation status"};csv.Click+=(s,e)=>SaveExport(false);menu.Items.Add(csv);
            var simple=new MenuItem{Header="Export curves only (CSV)",ToolTip="Four columns: Structure, Dose, DoseUnit, VolumePercent. IDs in filename; no metrics or extra metadata rows."};simple.Click+=(s,e)=>SaveExport(false,true);menu.Items.Add(simple);
            var png=new MenuItem{Header="Export active chart (PNG)",ToolTip="All enabled curves at equal opacity, with legend and approximation notice"};png.Click+=(s,e)=>SaveExport(true);menu.Items.Add(png);
            ContextMenu=menu;ViewerSnapshot.AttachMenu(this,null,"DVH");ToolTip="Right-click to export active DVH curves or chart";
        }
        bool ExportVisible(StructureRoi roi){bool enabled;return !visibility.TryGetValue(roi,out enabled)||enabled;}
        static string Quote(string value)=>"\""+(value??"").Replace("\"","\"\"")+"\"";
        static string Number(double value)=>double.IsNaN(value)||double.IsInfinity(value)?"":value.ToString("0.####",CultureInfo.InvariantCulture);
        static string MetricDetails(DvhResult r)
        {
            Func<double,string> value=v=>double.IsNaN(v)?"unavailable":v.ToString("0.##",CultureInfo.CurrentCulture)+" "+(r.DoseUnits=="GY"?"Gy":r.DoseUnits);
            return "Dmean  "+value(r.Dmean)+"\nDmedian  "+value(r.Dmedian)+"\nDmax  "+value(r.Dmax)+"\nDmin  "+value(r.Dmin)+"\nD98  "+value(r.D98)+"\nD2  "+value(r.D2)+
                $"\n\nCoverage {r.CoverageFraction:P1} · sampling ≤ {r.SamplingStepMm:0.##} mm\n"+r.Message+"\nApproximate sampling metrics; Dxx uses the interpolated cumulative curve. Full-structure metrics require full dose coverage.";
        }
        public string ExportCsv()=>ExportCsv(false);
        public string ExportCsv(bool simple)
        {
            if(simple)return ExportSimpleCsv();
            var identity=ExportIdentity.GetContext(this);
            var text=new StringBuilder("PatientID,PlanID,Structure,ROIType,Dose,DoseUnit,CumulativeVolumePercent,EstimatedVolumeCc,CoverageFraction,SamplingStepMm,Status,Note,Dmean,Dmedian,Dmax,Dmin,D98,D2\r\n");
            foreach(var roi in exportRois.Where(ExportVisible)){
                var curve=plot.Curves.FirstOrDefault(c=>c.Roi==roi);var r=curve?.Result;
                int count=Math.Max(1,r?.DoseValues.Length??0);
                for(int i=0;i<count;i++)text.AppendLine(string.Join(",",new[]{Quote(identity.PatientId),Quote(identity.PlanId),Quote(roi.Name),Quote(roi.InterpretedType),
                    r!=null&&i<r.DoseValues.Length?Number(r.DoseValues[i]):"",Quote(r?.DoseUnits??plot.Units),
                    r!=null&&i<r.CumulativeVolumePercent.Length?Number(r.CumulativeVolumePercent[i]):"",
                    r==null?"":Number(r.EstimatedVolumeCc),r==null?"":Number(r.CoverageFraction),r==null?"":Number(r.SamplingStepMm),
                    Quote(r?.Status.ToString()??"NotCalculated"),Quote(r==null?"No calculated curve available":"Approximate preview; "+r.Message),
                    r==null?"":Number(r.Dmean),r==null?"":Number(r.Dmedian),r==null?"":Number(r.Dmax),r==null?"":Number(r.Dmin),r==null?"":Number(r.D98),r==null?"":Number(r.D2)}));
            }
            return text.ToString();
        }
        string ExportSimpleCsv()
        {
            // No metadata rows or metrics: identifiers are carried by the chosen filename.
            var text=new StringBuilder("Structure,Dose,DoseUnit,VolumePercent\r\n");
            foreach(var curve in plot.Curves.Where(c=>ExportVisible(c.Roi)))for(int i=0;i<curve.Result.DoseValues.Length;i++)text.AppendLine(string.Join(",",new[]{Quote(curve.Roi.Name),Number(curve.Result.DoseValues[i]),Quote(curve.Result.DoseUnits),Number(curve.Result.CumulativeVolumePercent[i])}));
            return text.ToString();
        }
        public BitmapSource ExportChart()
        {
            var selected=exportRois.Where(ExportVisible).ToArray();var grid=new Grid{Width=1500,Height=Math.Max(900,selected.Length*82+140),Background=Theme.Background};
            grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});grid.RowDefinitions.Add(new RowDefinition());grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1050)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(450)});
            var header=Theme.Text("Cumulative DVH - active structures",24);header.Margin=new Thickness(24);Grid.SetColumnSpan(header,2);grid.Children.Add(header);
            var chart=new DvhPlot{Units=plot.Units,Margin=new Thickness(24,12,12,24)};chart.Curves.AddRange(plot.Curves.Where(c=>c.Visible&&selected.Contains(c.Roi)));Grid.SetRow(chart,1);grid.Children.Add(chart);
            var names=new StackPanel{Margin=new Thickness(10,12,24,24)};foreach(var roi in selected){var curve=plot.Curves.FirstOrDefault(c=>c.Roi==roi);names.Children.Add(Theme.Text(roi.Name+(curve?.Result.DoseValues.Length>0?"":" (no curve)"),12,new SolidColorBrush(Color.FromRgb(roi.Red,roi.Green,roi.Blue))));if(curve?.Result!=null){var r=curve.Result;Func<double,string> f=v=>double.IsNaN(v)?"n/a":v.ToString("0.##",CultureInfo.InvariantCulture);var metrics=Theme.Text($"{r.EstimatedVolumeCc:0.##} cm³ · {r.DoseUnits}\nMean {f(r.Dmean)} · Median {f(r.Dmedian)} · Min {f(r.Dmin)}\nMax {f(r.Dmax)} · D98 {f(r.D98)} · D2 {f(r.D2)}",10,Theme.Muted);metrics.Margin=new Thickness(0,0,0,12);names.Children.Add(metrics);}}Grid.SetRow(names,1);Grid.SetColumn(names,1);grid.Children.Add(names);
            var footer=Theme.Text("Approximate inspection DVH; not TPS validation. Dashed curves have partial dose coverage. "+StatusText,12,Theme.Muted);footer.Margin=new Thickness(24);Grid.SetRow(footer,2);Grid.SetColumnSpan(footer,2);grid.Children.Add(footer);
            grid.Measure(new Size(grid.Width,grid.Height));grid.Arrange(new Rect(0,0,grid.Width,grid.Height));grid.UpdateLayout();return ExportIdentity.GetContext(this).Stamp(ViewerSnapshot.Capture(grid));
        }
        void SaveExport(bool chart,bool simple=false)
        {
            var identity=ExportIdentity.GetContext(this);var dialog=new Microsoft.Win32.SaveFileDialog{Filter=chart?"PNG image|*.png":"CSV table|*.csv",FileName=identity.FileName(simple?"DVH-curves":"DVH",chart?".png":".csv"),Title="Export enabled DVH structures"};if(dialog.ShowDialog(Window.GetWindow(this))!=true)return;
            try{if(chart)ViewerSnapshot.Save(ExportChart(),dialog.FileName);else File.WriteAllText(dialog.FileName,ExportCsv(simple),new UTF8Encoding(true));status.Text="DVH export saved.";}catch(Exception){status.Text="Unable to save DVH export.";}
        }
    }
}
