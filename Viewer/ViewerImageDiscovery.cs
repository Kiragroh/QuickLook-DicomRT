using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
namespace QuickLook.DicomRT
{
    public sealed partial class ViewerControl
    {
        private readonly Button searchMoreImages=Theme.Button("Search more images…");
        private CancellationTokenSource imageSearch;
        private sealed class SavedImage {public ImageStack Stack;public DicomEntry Entry;public VolumeData Volume;public PixelPlane Native;public Vec3 Focus;public Vec3? Center;public int Index;public double Width,Level,Zoom;}
        private SavedImage suspendedImage;
        private Func<DicomEntry,bool> PatientImagePredicate()
        {
            var patient=initialEntry?.PatientKey;var study=initialEntry?.StudyUid;var sop=initialEntry?.SopUid;bool hasInitial=initialEntry!=null;
            return e=>e!=null&&!(e.Modality??"").StartsWith("RT")&&e.Modality!="REG"&&(!hasInitial||(!string.IsNullOrEmpty(patient)&&patient!="|"?e.PatientKey==patient:!string.IsNullOrEmpty(study)?e.StudyUid==study:!string.IsNullOrEmpty(sop)&&e.SopUid==sop));
        }
        private Func<DicomEntry,bool> BuildImagePredicate(PlanData plan,DoseGrid dose,StructureSet structure)
        {
            if(dose!=null&&plan==null)plan=planData.FirstOrDefault(p=>p.Entry.SopUid==dose.PlanUid);
            if(structure==null&&plan!=null)structure=structures.FirstOrDefault(s=>s.Entry.SopUid==plan.StructureSopUid);
            var refs=new HashSet<string>(structure?.ReferencedSeries??new HashSet<string>());
            var primary=dose?.FrameUid??plan?.FrameUid;var frames=!string.IsNullOrEmpty(primary)?new[]{primary}:(structure?.Rois.Select(r=>r.FrameUid)??Enumerable.Empty<string>()).Concat(new[]{structure?.Entry.FrameUid}).Where(f=>!string.IsNullOrEmpty(f)).Distinct().ToArray();
            var links=registrations.ToList();var patientImage=PatientImagePredicate();
            return e=>patientImage(e)&&(refs.Count==0||refs.Contains(e.SeriesUid))&&frames.Any(f=>RegistrationReader.Resolve(links,f,e.FrameUid)!=null);
        }
        private Func<DicomEntry,bool> InitialImagePredicate()
        {
            if(!initialEntry.Modality.StartsWith("RT")){var seriesUid=initialEntry.SeriesUid;var patient=initialEntry.PatientKey;return e=>e.PatientKey==patient&&(!string.IsNullOrEmpty(seriesUid)?e.SeriesUid==seriesUid:e.SopUid==initialEntry.SopUid);}
            return BuildImagePredicate(planData.FirstOrDefault(p=>p.Entry.SopUid==initialEntry.SopUid),doses.FirstOrDefault(d=>d.Entry.SopUid==initialEntry.SopUid),structures.FirstOrDefault(s=>s.Entry.SopUid==initialEntry.SopUid));
        }
        private ImageStack MatchingObjectStack(PlanData plan,DoseGrid dose,StructureSet structure)
        {
            if(catalog==null)return null;var match=BuildImagePredicate(plan,dose,structure);var candidates=catalog.Stacks.Where(s=>s.Entries.All(match)).ToArray();
            if(currentStack!=null&&candidates.Contains(currentStack))return currentStack;
            if(candidates.Length==1)return candidates[0];var ct=candidates.Where(s=>s.Modality=="CT").ToArray();return ct.Length==1?ct[0]:null;
        }
        private string ImageAvailability(PlanData plan,DoseGrid dose,StructureSet structure)
        {
            var match=BuildImagePredicate(plan,dose,structure);
            if((HasImage&&match(currentEntry))||catalog?.Stacks.Any(s=>s.Entries.All(match))==true)return " · images available";
            if(catalog?.DeferredImages.Any(match)==true)return " · images not loaded";
            return scanComplete?" · no matching images":" · images not loaded";
        }
        private void DetachUnrelatedImage()
        {
            if(HasImage)suspendedImage=new SavedImage{Stack=currentStack,Entry=currentEntry,Volume=volume,Native=native,Focus=focus,Center=viewportCenter,Index=sliceIndex,Width=windowWidth,Level=windowCenter,Zoom=zoom};
            seriesLoad?.Cancel();++seriesGeneration;++pixelGeneration;overlayLoad?.Cancel();overlayVolume=null;overlayStack=null;
            volume=null;native=null;currentStack=null;currentEntry=(sumMode?sumResult?.Dose?.Entry:null)??selectedPlan?.Entry??selectedDose?.Entry??selectedStructure?.Entry??initialEntry;
            changing=true;series.SelectedItem=null;sliceSlider.Maximum=0;sliceSlider.Value=0;changing=false;viewportCenter=null;UpdateTags();Redraw();
        }
        private Func<DicomEntry,bool> ActiveImagePredicate()=>sumMode&&sumResult?.Dose!=null?BuildImagePredicate(null,sumResult.Dose,null):selectedPlan!=null||selectedDose!=null||selectedStructure!=null?BuildImagePredicate(selectedPlan,selectedDose,selectedStructure):PatientImagePredicate();
        private async Task ApplySelectedImagesAsync()
        {
            var match=ActiveImagePredicate();
            if(HasImage&&match(currentEntry))return;
            var stack=MatchingObjectStack(selectedPlan,sumMode?sumResult?.Dose:selectedDose,selectedStructure);
            DetachUnrelatedImage();
            if(stack==null){status.Text="RT-only preview · no matching images loaded. Use Search more images.";return;}
            changing=true;series.SelectedItem=stack;changing=false;
            if(suspendedImage?.Stack==stack){var saved=suspendedImage;suspendedImage=null;currentStack=stack;currentEntry=saved.Entry;volume=saved.Volume;native=saved.Native;focus=saved.Focus;viewportCenter=saved.Center;windowWidth=saved.Width;windowCenter=saved.Level;zoom=saved.Zoom;sliceIndex=requestedSliceIndex=saved.Index;seriesLoad=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);changing=true;sliceSlider.Maximum=stack.Entries.Count-1;sliceSlider.Value=sliceIndex;changing=false;RefreshRt();return;}
            await SelectStackAsync(stack);
        }
        private async Task SearchMoreImagesAsync()
        {
            if(imageSearch!=null){imageSearch.Cancel();return;}
            if(sumMode&&sumResult?.Dose==null){activity.Visibility=Visibility.Visible;activity.Text="Generate the sum before searching its images.";return;}
            var predicate=ActiveImagePredicate();
            var paths=catalog?.DeferredImages.Where(predicate).Select(e=>e.Path).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            string root=Path.GetDirectoryName(initialEntry.Path);
            if(paths==null||paths.Length==0){using(var picker=new System.Windows.Forms.FolderBrowserDialog{Description="Search images for the selected RT object: choose a common parent folder",SelectedPath=root,ShowNewFolderButton=false}){if(picker.ShowDialog()!=System.Windows.Forms.DialogResult.OK)return;root=picker.SelectedPath;}}
            await SearchImagesAsync(root,paths?.Length>0?paths:null);
        }
        private async Task SearchImagesAsync(string root,string[] paths=null)
        {
            if(disposed||imageSearch!=null||folderSearch!=null)return;
            var predicate=ActiveImagePredicate();
            int selection=planSelectionRevision;var source=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);imageSearch=source;var token=source.Token;
            searchMoreImages.Content="Cancel image search";searchSubfolders.IsEnabled=false;activity.Visibility=Visibility.Visible;activity.Text="● Finding images for the selected RT object …";
            var clock=System.Diagnostics.Stopwatch.StartNew();
            try{
                var found=await Task.Run(()=>DicomCatalog.Scan(initialEntry.Path,token,phaseProgress:(phase,count,total)=>{if(clock.ElapsedMilliseconds<200)return;clock.Restart();Dispatcher.BeginInvoke(new Action(()=>{if(!disposed&&!token.IsCancellationRequested)activity.Text=$"● Image search · {count}/{total} files";}));},searchRoot:root,recursive:true,imageFilter:predicate,knownPaths:paths),token);
                token.ThrowIfCancellationRequested();MergeDiscoveredCatalog(found);
                var snapshot=catalog;var links=await Task.Run(()=>RegistrationReader.Read(snapshot),token);token.ThrowIfCancellationRequested();registrations=links;
                if(selection==planSelectionRevision){var extra=catalog.DeferredImages.Where(ActiveImagePredicate()).Select(e=>e.Path).ToArray();if(extra.Length>0){var additional=await Task.Run(()=>DicomCatalog.Scan(initialEntry.Path,token,knownPaths:extra),token);token.ThrowIfCancellationRequested();MergeDiscoveredCatalog(additional);found.Stacks.AddRange(additional.Stacks);}}
                RefreshPlanChoices();
                if(selection==planSelectionRevision&&(sumMode||selectedPlan!=null||selectedDose!=null||selectedStructure!=null)){await ApplySelectedImagesAsync();if(!disposed&&selection==planSelectionRevision){RefreshRt();await JumpToPlanDoseAsync();}}
                activity.Text=found.Stacks.Count>0?$"✓ {found.Stacks.Count} matching image groups available":"No matching images found · RT-only preview remains available";
            }catch(OperationCanceledException){if(!disposed)activity.Text="Image search cancelled";}catch(Exception){if(!disposed)activity.Text="Image search incomplete · check folder access";}
            finally{if(imageSearch==source)imageSearch=null;source.Dispose();if(!disposed){searchMoreImages.Content="Search more images…";searchSubfolders.IsEnabled=true;}}
        }
        private void MergeDiscoveredCatalog(DicomCatalog found)
        {
            Func<DicomEntry,bool> same=e=>initialEntry.PatientKey!="|"&&!string.IsNullOrEmpty(initialEntry.PatientKey)?e.PatientKey==initialEntry.PatientKey:!string.IsNullOrEmpty(initialEntry.StudyUid)&&e.StudyUid==initialEntry.StudyUid;
            var old=catalog??new DicomCatalog();var files=old.Files.Concat(found.Files.Where(same)).GroupBy(e=>e.SopUid).Select(g=>g.First()).ToList();var loaded=new HashSet<string>(files.Select(e=>e.SopUid));
            catalog=new DicomCatalog{Files=files,DeferredImages=old.DeferredImages.Concat(found.DeferredImages.Where(same)).Where(e=>!loaded.Contains(e.SopUid)).GroupBy(e=>e.SopUid).Select(g=>g.First()).ToList(),Stacks=old.Stacks.Concat(found.Stacks.Where(s=>s.Entries.All(same))).GroupBy(s=>s.Key).Select(g=>g.OrderByDescending(s=>s.Entries.Count).First()).ToList(),SkippedFiles=found.SkippedFiles};
            changing=true;series.ItemsSource=catalog.Stacks;series.SelectedItem=currentStack;changing=false;
        }
    }
}
