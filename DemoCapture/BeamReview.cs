using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using QuickLook.DicomRT;

internal static partial class Program
{
    static async Task BeamReview(string folder)
    {
        await Load(folder,"user-approved public nonpatient benchmark");Panels(true,false);Mode("MLC");
        var playback=Get<MlcPlaybackControl>(viewer,"centralPlayback");Get<CheckBox>(playback,"showDrr").IsChecked=true;var watch=Stopwatch.StartNew();
        await Wait(()=>!Get<bool>(playback,"projectionBusy")&&!Get<System.Windows.Threading.DispatcherTimer>(playback,"projectionDelay").IsEnabled&&Get<object>(Get<object>(playback,"aperture"),"projection")!=null,"Initial DRR projection",180);
        var frame=Get<object>(Get<object>(playback,"aperture"),"projection");
        if(frame.GetType().GetField("Drr").GetValue(frame)==null)throw new Exception("DRR absent");
        var outlines=(System.Collections.ICollection)frame.GetType().GetField("Outlines").GetValue(frame);
        if(outlines.Count==0)throw new Exception("PTV silhouettes absent");
        Console.WriteLine("BEV_INITIAL ms="+watch.ElapsedMilliseconds+" outlines="+outlines.Count);
        await BeamResponsiveness(playback);
        await Save("mlc-drr-ptv.png","Actual CT-derived DRR with perspective PTV silhouettes and field arrangement at isocenter.");
        var cursor=Get<Slider>(playback,"cursor");var timings=new double[8];
        for(int i=0;i<timings.Length;i++){watch.Restart();cursor.Value=i+1;await Wait(()=>!Get<bool>(playback,"projectionBusy")&&!Get<System.Windows.Threading.DispatcherTimer>(playback,"projectionDelay").IsEnabled&&Get<object>(Get<object>(playback,"aperture"),"projection")!=null,"DRR scrub",60);timings[i]=watch.Elapsed.TotalMilliseconds;}
        Console.WriteLine("BEV_SCRUB settled_384_median_ms="+timings.OrderBy(x=>x).ElementAt(4).ToString("0"));
        for(int i=0;i<timings.Length;i++){watch.Restart();cursor.Value=i+1;await Wait(()=>!Get<bool>(playback,"projectionBusy")&&!Get<System.Windows.Threading.DispatcherTimer>(playback,"projectionDelay").IsEnabled&&Get<object>(Get<object>(playback,"aperture"),"projection")!=null,"Cached DRR scrub",60);timings[i]=watch.Elapsed.TotalMilliseconds;}
        Console.WriteLine("BEV_REVISIT cached_384_median_ms="+timings.OrderBy(x=>x).ElementAt(4).ToString("0"));
        var all=Get<System.Collections.Generic.List<StructureSet>>(viewer,"structures").SelectMany(s=>s.Rois).ToArray();
        var organ=all.FirstOrDefault(r=>r.InterpretedType=="ORGAN"&&r.Name.IndexOf("brainstem",StringComparison.OrdinalIgnoreCase)>=0)??all.First(r=>r.InterpretedType=="ORGAN");
        foreach(var roi in all)roi.Visible=roi.InterpretedType=="PTV"||roi==organ;
        Invoke(viewer,"RefreshRt");Get<CheckBox>(playback,"showOrgans").IsChecked=true;
        var retained=Get<object>(Get<object>(playback,"aperture"),"projection");if(retained==null||((System.Collections.ICollection)retained.GetType().GetField("Outlines").GetValue(retained)).Count<outlines.Count)throw new Exception("PTVs disappeared when organs enabled");
        Console.WriteLine("CATEGORY_TOGGLE retains_previous_PTVs=True");
        await Wait(()=>!Get<bool>(playback,"projectionBusy")&&!Get<System.Windows.Threading.DispatcherTimer>(playback,"projectionDelay").IsEnabled&&Get<object>(Get<object>(playback,"aperture"),"projection")!=null,"Organ silhouette",180);
        frame=Get<object>(Get<object>(playback,"aperture"),"projection");Console.WriteLine("BEV_ORGAN outlines="+((System.Collections.ICollection)frame.GetType().GetField("Outlines").GetValue(frame)).Count);if(((System.Collections.ICollection)frame.GetType().GetField("Outlines").GetValue(frame)).Count<=outlines.Count)throw new Exception("Organ outline not added");
        await Save("mlc-drr-organs.png","PTV and selected organ outer silhouettes above the actual MLC leaf banks.");
        var beams=Get<PlanBeam[]>(playback,"playbackBeams");int noncoplanar=Array.FindIndex(beams,b=>b.ControlPoints.Any(c=>Math.Abs(Math.Sin(c.Couch*Math.PI/180))>.1));
        if(noncoplanar>=0){cursor.Value=beams.Take(noncoplanar).Sum(b=>b.ControlPoints.Count);await Wait(()=>!Get<bool>(playback,"projectionBusy")&&!Get<System.Windows.Threading.DispatcherTimer>(playback,"projectionDelay").IsEnabled&&Get<object>(Get<object>(playback,"aperture"),"projection")!=null,"Noncoplanar DRR",90);await Save("mlc-drr-noncoplanar.png","Noncoplanar beam geometry; DRR and contour silhouettes share the same projection.");}
        Mode("Bild");Get<CheckBox>(viewer,"showFields").IsChecked=true;Get<ComboBox>(viewer,"planes").SelectedItem="Axial";await Save("image-fields.png","Optional field aperture intersections and projected central axes over the image.");
        Get<CheckBox>(viewer,"showFields").IsChecked=false;Get<ComboBox>(viewer,"planes").SelectedItem="Native";
        var targets=all.Where(r=>r.InterpretedType=="PTV").Take(8).ToArray();var jumps=new double[targets.Length];
        for(int i=0;i<targets.Length;i++){watch.Restart();await Call(viewer,"MoveFocusAsync",targets[i].Center);await Settle();jumps[i]=watch.Elapsed.TotalMilliseconds;}
        Console.WriteLine("STRUCTURE_JUMP median_settled_ms="+jumps.OrderBy(x=>x).ElementAt(jumps.Length/2).ToString("0"));
        Mode("3D");await Settle();var three=Get<ThreeDControl>(viewer,"threeDView");var before=Get<object>(three,"prepared");Get<CheckBox>(three,"showBeamFields").IsChecked=true;await Task.Delay(700);await Save("three-d-all-fields.png","All treatment fields with angular meterset modulation; schematic orientation guides.");Get<CheckBox>(three,"showBeamFields").IsChecked=false;watch.Restart();Get<CheckBox>(three,"organs").IsChecked=true;
        if(Get<object>(three,"prepared")==null)throw new Exception("3D disappeared on additive selection");
        await Settle();Console.WriteLine("ADDITIVE_ORGANS retained_scene=True ready_ms="+watch.ElapsedMilliseconds);await Save("three-d-additive.png","Existing targets retained while selected organs are added.");
        Mode("Bild");Get<ComboBox>(viewer,"planes").SelectedItem="MPR + 3D";await Save("mpr-interactive.png","Linked orthogonal views, draggable crosshair and patient identity.");
        Get<ComboBox>(viewer,"fieldPicker").SelectedIndex=0;Get<CheckBox>(viewer,"showFields").IsChecked=true;await Save("fields-neutral.png","All treatment fields equally weighted; arcs show source paths.");
        Invoke(viewer,"ApplyWindowPreset","Custom");await Save("window-histogram.png","Custom window limits and sampled histogram.");
        Mode("DVH");await Settle();var dvh=Get<DvhControl>(viewer,"dvhView");await dvh.Completion;await Save("dvh-compact.png","Compact structure names and volumes; metric details in info hover.");
        var identity=ExportIdentity.GetContext(dvh);System.IO.File.WriteAllText(System.IO.Path.Combine(output,identity.FileName("DVH",".csv")),dvh.ExportCsv());System.IO.File.WriteAllText(System.IO.Path.Combine(output,identity.FileName("DVH-curves",".csv")),dvh.ExportCsv(true));
        var chart=dvh.ExportChart();var chartEncoder=new PngBitmapEncoder();chartEncoder.Frames.Add(BitmapFrame.Create(chart));using(var file=System.IO.File.Create(System.IO.Path.Combine(output,identity.FileName("DVH",".png"))))chartEncoder.Save(file);
        foreach(var name in new[]{"MLC","3D"}){Mode(name);await Settle();var view=name=="MLC"?(System.Windows.FrameworkElement)playback:Get<ThreeDControl>(viewer,"threeDView");var capture=typeof(ViewerControl).Assembly.GetType("QuickLook.DicomRT.ViewerSnapshot").GetMethod("Capture").Invoke(null,new object[]{view});var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create((System.Windows.Media.Imaging.BitmapSource)capture));using(var file=System.IO.File.Create(System.IO.Path.Combine(output,name+"-single-export.png")))encoder.Save(file);if(view.ContextMenu==null)throw new Exception("View export context menu missing");}
        Console.WriteLine("BEAM_REVIEW_PASS");
    }
}
