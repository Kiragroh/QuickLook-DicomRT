using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuickLook.DicomRT;
internal static class AccelaPhantomAcceptance
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static T Get<T>(object target,string name)=>(T)target.GetType().GetField(name,F).GetValue(target);
    static void Assert(bool value,string message){if(!value)throw new Exception(message);}
    static void Save(FrameworkElement element,string path){element.UpdateLayout();var b=new RenderTargetBitmap((int)element.ActualWidth,(int)element.ActualHeight,96,96,PixelFormats.Pbgra32);b.Render(element);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(b));using(var stream=File.Create(path))png.Save(stream);}
    public static int Run(string folder)
    {
        int result=0;var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        var output=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"accela-review");Directory.CreateDirectory(output);
        var viewer=new ViewerControl();var window=new Window{Title="SYNTHETIC Accela-inspired test · not a vendor export",Width=1400,Height=950,Content=viewer,ShowActivated=false};
        window.Loaded+=async(s,e)=>{
            try{
                var planFile=Path.Combine(folder,"RTPLAN_SYNTHETIC_DMAT.dcm");viewer.Open(planFile);await viewer.LoadCompletion;
                var plan=Get<PlanData>(viewer,"selectedPlan");Assert(plan!=null&&plan.Beams.Count==2,"Plan opens from RT file with matching CT");
                var cat=Get<DicomCatalog>(viewer,"catalog");var ct=cat.Stacks.Single(x=>x.Modality=="CT");Assert(ct.Entries.Count==73,"All CT slices loaded once");
                var volume=await Task.Run(()=>VolumeData.Load(ct,CancellationToken.None));
                var structure=StructureSet.Load(cat.Files.Single(x=>x.Modality=="RTSTRUCT"));var dose=DoseGrid.Load(cat.Files.Single(x=>x.Modality=="RTDOSE"));
                Assert(structure.Rois.Count==7&&plan.StructureSopUid==structure.Entry.SopUid&&dose.PlanUid==plan.Entry.SopUid,"Exact CT/structure/plan/dose references");
                Assert(plan.FrameUid==dose.FrameUid&&plan.FrameUid==ct.FrameUid,"Matching physical coordinate frames");
                int dynamicViews=0;foreach(var beam in plan.Beams){
                    Assert(beam.EnhancedDevices&&beam.DynamicCollimator&&BeamMachineInfo.Form(beam)==LinacForm.CArm,"Enhanced dynamic C-arm metadata");
                    for(int i=0;i+1<beam.ControlPoints.Count;i++){
                        var a=beam.ControlPoints[i];var b=beam.ControlPoints[i+1];var cp=MlcTimeline.Interpolate(a,b,.5);string reason;
                        var projection=BeamProjection.Create(beam,cp,Matrix4.Identity,out reason);Assert(projection!=null,"Intermediate projection: "+reason);
                        Assert(cp.MlcLayers.Count==2&&cp.MlcLayers[0].Positions.Length==92&&cp.MlcLayers[1].Positions.Length==94,"Independent 46/47 paired leaves");
                        Assert(BeamAperture.Rectangles(cp).Count>0,"Nonempty intersection aperture");dynamicViews++;
                    }
                }
                int dvhs=0;foreach(var roi in structure.Rois.Where(r=>r.Name.StartsWith("PTV"))){
                    var dvh=await Task.Run(()=>DvhCalculator.Calculate(roi,dose,Matrix4.Identity,CancellationToken.None));
                    Assert(dvh.Status==DvhStatus.Complete&&dvh.CoverageFraction>.999&&dvh.Dmean>0,"Complete PTV DVH against analytic dose");dvhs++;
                    string reason;var mesh=await Task.Run(()=>ThreeDGeometry.BuildRoiSurface(roi,Matrix4.Identity,CancellationToken.None,out reason));Assert(mesh!=null&&mesh.Indices.Count>0,"PTV surface generated");
                }
                // Exercise actual viewer projection and its overlay pipeline at three dynamic poses.
                typeof(ViewerControl).GetMethod("SetWorkspace",F).Invoke(viewer,new object[]{"MLC"});
                var mlc=Get<MlcPlaybackControl>(viewer,"centralPlayback");Get<CheckBox>(mlc,"showDrr").IsChecked=true;
                var cursor=Get<Slider>(mlc,"cursor");int poses=0;
                foreach(double position in new[]{0d,40.5,180.5}){
                    cursor.Value=position;var until=DateTime.UtcNow.AddSeconds(90);object frame=null;bool ready=false;
                    do{await Task.Delay(50);frame=Get<object>(Get<object>(mlc,"aperture"),"projection");
                        ready=!Get<bool>(mlc,"projectionBusy")&&!Get<System.Windows.Threading.DispatcherTimer>(mlc,"projectionDelay").IsEnabled&&
                            Get<string>(mlc,"displayedProjectionKey")==Get<string>(mlc,"projectionKey")&&frame!=null&&frame.GetType().GetField("Drr").GetValue(frame)!=null;
                        if(ready)break;
                    }while(DateTime.UtcNow<until);
                    Assert(ready,"CT DRR ready at the requested pose, with refinement finished");
                    Assert(((System.Collections.ICollection)frame.GetType().GetField("Outlines").GetValue(frame)).Count>=2,"Both target outlines projected");poses++;
                }
                Save(mlc,Path.Combine(output,"synthetic-mlc.png"));
                var model=(FrameworkElement)Get<object>(mlc,"orientation");var set=model.GetType().GetMethod("SetContext");
                foreach(string name in new[]{"Accela","Halcyon","Ethos"}){
                    set.Invoke(model,new object[]{new PlanBeam{ManufacturerModelName=name,TreatmentMachineName="SYNTHETIC",PatientPosition="HFS"},"HEAD",false});
                    model.GetType().GetMethod("Set").Invoke(model,new object[]{55d,0d});model.GetType().GetMethod("SetCollimator").Invoke(model,new object[]{35d});await Task.Delay(100);
                    Save(model,Path.Combine(output,"synthetic-model-"+name+".png"));
                }
                Console.WriteLine($"PASS synthetic full set: 73 CT slices, 7 ROIs, 2 arcs, {dynamicViews} interpolated geometry checks, {dvhs} DVHs/surfaces, {poses} DRR+PTV views; model captures in {output}");
            }catch(Exception ex){result=1;Console.WriteLine("FAIL synthetic full set: "+ex);}finally{viewer.Dispose();window.Close();app.Shutdown();}
        };app.Run(window);return result;
    }
}
