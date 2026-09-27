using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace QuickLook.DicomRT
{
    public sealed class ExportIdentity
    {
        public string PatientId {get;set;}="";
        public string PlanId {get;set;}="";
        public static readonly DependencyProperty ContextProperty=DependencyProperty.RegisterAttached("Context",typeof(ExportIdentity),typeof(ExportIdentity),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.Inherits));
        public static void SetContext(DependencyObject view,ExportIdentity value)=>view.SetValue(ContextProperty,value);
        public static ExportIdentity GetContext(DependencyObject view)=>(ExportIdentity)view.GetValue(ContextProperty)??new ExportIdentity();
        public string Caption=>"Patient ID: "+(string.IsNullOrWhiteSpace(PatientId)?"unavailable":PatientId)+(string.IsNullOrWhiteSpace(PlanId)?"":"   |   Plan ID: "+PlanId);
        static string Safe(string text){var invalid=Path.GetInvalidFileNameChars();var clean=new string((text??"").Select(c=>invalid.Contains(c)||char.IsControl(c)?'_':c).ToArray()).Trim().TrimEnd('.');return clean.Length>72?clean.Substring(0,72):clean;}
        public string FileName(string view,string extension)=>"Patient_"+Safe(string.IsNullOrWhiteSpace(PatientId)?"unavailable":PatientId)+(string.IsNullOrWhiteSpace(PlanId)?"":"_Plan_"+Safe(PlanId))+"_"+Safe(view)+"_"+DateTime.Now.ToString("yyyyMMdd_HHmmssfff")+extension;
        public BitmapSource Stamp(BitmapSource image)
        {
            var label=new TextBlock{Text=Caption,Foreground=Brushes.White,FontSize=12,TextWrapping=TextWrapping.Wrap,Padding=new Thickness(10,7,10,7),Background=Theme.Background,Width=image.PixelWidth};label.Measure(new Size(image.PixelWidth,double.PositiveInfinity));double height=Math.Ceiling(label.DesiredSize.Height);label.Arrange(new Rect(0,0,image.PixelWidth,height));
            var drawing=new DrawingVisual();using(var dc=drawing.RenderOpen()){dc.DrawImage(image,new Rect(0,0,image.PixelWidth,image.PixelHeight));dc.DrawRectangle(new VisualBrush(label),null,new Rect(0,image.PixelHeight,image.PixelWidth,height));}
            var result=new RenderTargetBitmap(image.PixelWidth,image.PixelHeight+(int)height,96,96,PixelFormats.Pbgra32);result.Render(drawing);result.Freeze();return result;
        }
    }
    public sealed partial class ViewerControl
    {
        ExportIdentity CurrentExportIdentity(bool dvh=false)
        {
            var dose=dvh?(dvhDose.SelectedItem as DoseChoice)?.Dose:selectedDose;
            var plan=dvh?(dose==null?null:planData.FirstOrDefault(p=>p.Entry?.SopUid==dose.PlanUid)):(selectedPlan??(dose==null?null:planData.FirstOrDefault(p=>p.Entry?.SopUid==dose.PlanUid)));
            var entry=dvh?dose?.Entry:workspaceMode=="MLC"?plan?.Entry:currentEntry;
            entry=entry??plan?.Entry??initialEntry;
            string label=plan?.Label??"";
            if(sumMode&&workspaceMode!="MLC"&&(!dvh||dose==sumResult?.Dose)){var labels=planData.Where(p=>sumResult?.IncludedPlanUids?.Contains(p.Entry?.SopUid)==true).Select(p=>p.Label).ToArray();label=labels.Length>0?"SUM: "+string.Join(" + ",labels):"SUM (preparing)";}
            else if(plan==null&&!string.IsNullOrEmpty(dose?.PlanUid))label="UID "+dose.PlanUid;
            else if(!dvh&&workspaceMode!="MLC"&&plan!=null&&TransformToImage(plan.FrameUid)==null)label="";
            return new ExportIdentity{PatientId=entry?.Dataset?.GetSingleValueOrDefault<string>(Dicom.DicomTag.PatientID,"")??"",PlanId=label};
        }
    }
}
