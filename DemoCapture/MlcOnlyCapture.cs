using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Dicom;
using QuickLook.DicomRT;

internal static class MlcOnlyCapture
{
    const int Width=1600,Height=900,Frames=90;
    static double[] Copy(double[] values)=>values==null?new double[0]:(double[])values.Clone();
    static string Direction(string value)=>value=="CW"||value=="CC"||value=="NONE"?value:"";
    // Build a whitelist-only display model. No source DicomEntry, patient/study/frame identifiers,
    // free text, beam identifiers or isocenter coordinates enter the visual tree or manifest.
    static PlanData DisplayModel(PlanData source)
    {
        var result=new PlanData{Label="Dual-layer plan"};
        foreach(var beam in source.Beams)
        {
            var clean=new PlanBeam{Number=result.Beams.Count+1,Name="Beam "+(result.Beams.Count+1)};
            foreach(var cp in beam.ControlPoints)
                clean.ControlPoints.Add(new ControlPoint{Index=clean.ControlPoints.Count,Gantry=cp.Gantry,Collimator=cp.Collimator,Couch=cp.Couch,MetersetWeight=cp.MetersetWeight,
                    GantryRotationDirection=Direction(cp.GantryRotationDirection),CollimatorRotationDirection=Direction(cp.CollimatorRotationDirection),CouchRotationDirection=Direction(cp.CouchRotationDirection),
                    XJaws=Copy(cp.XJaws),YJaws=Copy(cp.YJaws),MlcLayers=cp.MlcLayers.Select(l=>l.Copy()).ToList()});
            result.Beams.Add(clean);
        }
        return result;
    }
    static PlanData Read(string folder)
    {
        foreach(var path in Directory.EnumerateFiles(folder))
        {
            DicomFile file;try{file=DicomFile.Open(path,FileReadOption.ReadLargeOnDemand);}catch{continue;}
            if(file.Dataset.GetSingleValueOrDefault(DicomTag.Modality,"")!="RTPLAN")continue;
            var parsed=PlanData.Load(new DicomEntry{Dataset=file.Dataset});
            if(parsed.Beams.Any(b=>b.ControlPoints.Any(c=>c.MlcLayers.Count==2)))return DisplayModel(parsed);
        }
        throw new InvalidOperationException("No supported dual-layer plan");
    }
    static void Save(FrameworkElement element,string path)
    {
        element.UpdateLayout();var bitmap=new RenderTargetBitmap(Width,Height,96,96,PixelFormats.Pbgra32);bitmap.Render(element);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(path))encoder.Save(stream);
    }
    public static int Run(string sourceFolder,string outputFolder)
    {
        int result=0;
        try
        {
            var plan=Read(sourceFolder);var output=Path.GetFullPath(outputFolder);Directory.CreateDirectory(output);Directory.CreateDirectory(Path.Combine(output,"frames"));
            var app=new Application();app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/QuickLook.DicomRT.Viewer;component/Theme.xaml",UriKind.Relative)});
            var control=new MlcPlaybackControl{Width=Width,Height=Height,Background=new SolidColorBrush(Color.FromRgb(17,19,20))};
            var window=new Window{Width=Width,Height=Height,Left=-30000,Top=-30000,ShowInTaskbar=false,ShowActivated=false,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,Content=control,Title="MLC-only renderer"};
            window.Loaded+=async(s,e)=>
            {
                try
                {
                    control.SetPlan(plan);var cursor=(Slider)typeof(MlcPlaybackControl).GetField("cursor",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(control);
                    var beamSelector=(ComboBox)typeof(MlcPlaybackControl).GetField("beams",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(control);
                    // All selector labels must originate in the whitelist model.
                    if(beamSelector.Items.Cast<PlanBeam>().Where((b,i)=>b.Name!="Beam "+(i+1)||b.Number!=i+1).Any())throw new InvalidOperationException("Unsafe display model");
                    int first=plan.Beams.FindIndex(b=>b.ControlPoints.Any(c=>c.MlcLayers.Count==2));double start=plan.Beams.Take(first).Sum(b=>b.ControlPoints.Count);
                    cursor.Value=start+(cursor.Maximum-start)*.24;control.UpdateLayout();await control.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.Render);Save(control,Path.Combine(output,"dual-layer-mlc.png"));
                    for(int i=0;i<Frames;i++)
                    {
                        cursor.Value=start+(cursor.Maximum-start)*i/(Frames-1.0);control.UpdateLayout();await control.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.Render);
                        Save(control,Path.Combine(output,"frames","frame-"+i.ToString("000")+".png"));if(i%15==0)Console.WriteLine("MLC_FRAMES "+i+"/"+Frames);
                    }
                    var manifest=new{version=1,width=Width,height=Height,frames=Frames,effectiveFps=15,durationSeconds=6,still="dual-layer-mlc.png",sequence="frames/frame-%03d.png",source="User-authorized clinical MLC geometry only",privacy="Whitelist display model: generic plan and beam labels; no source entry, patient/study/frame identifiers, free text, images, tags, source path or isocenter coordinates. Source DICOM remains read-only.",method="Actual standalone MlcPlaybackControl RenderTargetBitmap; 90 scrubbed control-point states, not actual delivery timing.",beamCount=plan.Beams.Count,controlPoints=plan.Beams.Sum(b=>b.ControlPoints.Count),doubleLayerControlPoints=plan.Beams.Sum(b=>b.ControlPoints.Count(c=>c.MlcLayers.Count==2))};
                    File.WriteAllText(Path.Combine(output,"manifest.json"),new JavaScriptSerializer().Serialize(manifest));Console.WriteLine("MLC_CAPTURE_COMPLETE frames="+Frames);
                }
                catch(Exception ex){result=1;Console.WriteLine("MLC_CAPTURE_FAILED "+ex.GetType().Name);}
                finally{control.Dispose();window.Close();}
            };
            app.Run(window);
        }
        catch(Exception ex){result=1;Console.WriteLine("MLC_CAPTURE_FAILED "+ex.GetType().Name);}
        return result;
    }
}
