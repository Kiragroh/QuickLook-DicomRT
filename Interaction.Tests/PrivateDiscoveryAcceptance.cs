using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using QuickLook.DicomRT;

internal static class PrivateDiscoveryAcceptance
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static T Get<T>(object o,string n)=>(T)o.GetType().GetField(n,F).GetValue(o);
    static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    public static int Run(string folder)
    {
        int result=0;var app=new Application();ViewerControl viewer=null;
        var window=new Window{Width=1100,Height=800,Left=-30000,Top=-30000,ShowActivated=false,ShowInTaskbar=false};
        window.Loaded+=async(s,e)=>{
            try{
                var cat=await Task.Run(()=>DicomCatalog.Scan(Directory.EnumerateFiles(folder,"*",SearchOption.AllDirectories).First(),CancellationToken.None,searchRoot:folder,recursive:true));
                var entries=cat.Files.Where(x=>x.Modality=="RTDOSE").ToArray();Check(entries.Length>0,"No doses in test input");
                foreach(var entry in entries){
                    viewer?.Dispose();viewer=new ViewerControl();window.Content=viewer;var clock=System.Diagnostics.Stopwatch.StartNew();viewer.Open(entry.Path);await viewer.LoadCompletion;
                    var plan=Get<PlanData>(viewer,"selectedPlan");var dose=Get<List<DoseGrid>>(viewer,"doses").Single(d=>d.Entry.SopUid==entry.SopUid);
                    Check(plan!=null&&plan.Entry.SopUid==dose.PlanUid,"Referenced plan must be selected");
                    Check(Get<List<StructureSet>>(viewer,"structures").Any(r=>r.Entry.SopUid==plan.StructureSopUid),"Referenced structure set missing");
                    Check(viewer.HasImage&&Get<VolumeData>(viewer,"volume")!=null,"Matching CT volume missing");
                    var shown=(IEnumerable<DoseGrid>)typeof(ViewerControl).GetProperty("SelectedDoses",F).GetValue(viewer);
                    Check(shown.Count()==1&&shown.Single()==dose,"Opened dose must remain selected");
                    var export=PlanDicomExport.Resolve(plan,Get<DicomCatalog>(viewer,"catalog").Files);
                    Check(new[]{"CT","RD","RS","RP"}.All(k=>export.Parts[k].Length>0),"Complete plan export association missing");
                    var stack=Get<ImageStack>(viewer,"currentStack");
                    await (Task)typeof(ViewerControl).GetMethod("SearchFolderAsync",F).Invoke(viewer,new object[]{folder});
                    Check(Get<ImageStack>(viewer,"currentStack")==stack,"Repeat folder search must preserve loaded stack");
                    Console.WriteLine("PASS dose entry: linked plan/structures, CT slices="+stack.Entries.Count+", CT/RD/RS/RP export resolved, repeat search retained image; elapsed_s="+clock.Elapsed.TotalSeconds.ToString("0.0"));
                }
            }catch(Exception ex){result=1;Console.WriteLine("FAIL private discovery: "+ex.GetType().Name);}
            finally{viewer?.Dispose();window.Close();}
        };
        app.Run(window);return result;
    }
}
