using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using QuickLook.DicomRT;

internal static class PrivateRtSlicesAcceptance
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static T Get<T>(object o,string n)=>(T)o.GetType().GetField(n,F).GetValue(o);
    static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,F).Invoke(o,args);
    public static int Run(string folder)
    {
        int result=0;var app=new Application();ViewerControl viewer=null;
        var window=new Window{Width=1300,Height=900,Left=-30000,Top=-30000,ShowActivated=false,ShowInTaskbar=false};
        window.Loaded+=async(s,e)=>{
            try{
                var entries=Directory.EnumerateFiles(folder,"*.dcm").Select(DicomCatalog.ReadEntry).Where(x=>x.Modality.StartsWith("RT")).ToArray();
                foreach(var entry in entries)
                {
                    viewer?.Dispose();viewer=new ViewerControl();window.Content=viewer;viewer.Open(entry.Path);await viewer.LoadCompletion;await Get<Task>(viewer,"rtSliceCompletion");
                    if(viewer.HasImage)throw new Exception("Test expects a folder without CT/MR");
                    var sets=Get<List<StructureSet>>(viewer,"structures");int descriptions=sets.SelectMany(x=>x.Rois).Count(r=>!string.IsNullOrWhiteSpace(r.Description));
                    Get<ComboBox>(viewer,"roiLabelMode").SelectedIndex=2;
                    foreach(string plane in new[]{"Native","Axial","Coronal","Sagittal","MPR + 3D"})
                    {
                        Call(viewer,"SelectImageMode",plane);
                        var before=Get<Vec3>(viewer,"focus");await (Task)Call(viewer,"ScrollAsync",plane=="MPR + 3D"?"Axial":plane,1);
                        if((Get<Vec3>(viewer,"focus")-before).Length<.001)throw new Exception("RT scroll did not move");
                        var panes=Get<List<SlicePane>>(viewer,"panes");var limit=DateTime.UtcNow.AddSeconds(20);
                        while(panes.Any(p=>Get<object>(p,"frame")==null)&&DateTime.UtcNow<limit)await Task.Delay(30);
                        if(panes.Any(p=>Get<object>(p,"frame")==null))throw new Exception("RT slice frame missing");
                        if(Get<RenderScene>(viewer,"latestScene").Volume!=null)throw new Exception("RT-only scene has an image volume");
                    }
                    Call(viewer,"SetWorkspace","DVH");var dvh=Get<DvhControl>(viewer,"dvhView");await dvh.Completion;
                    if(dvh.CalculationCount==0)throw new Exception("No DVH work");
                    Console.WriteLine($"PASS RT-only {entry.Modality}: {sets.Sum(x=>x.Rois.Count)} ROIs, {descriptions} descriptions, five slice layouts rendered and scrolled, DVH calculated; no CT required.");
                }
            }catch(Exception ex){result=1;Console.WriteLine("FAIL RT-only acceptance: "+ex.GetType().Name+" / "+ex.Message);}
            finally{viewer?.Dispose();window.Close();}
        };
        app.Run(window);return result;
    }
}
