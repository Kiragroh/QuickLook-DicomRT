using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QuickLook.DicomRT
{
    public sealed partial class ViewerControl
    {
        public void Open(string path)
        {
            if (disposed) return;
            if (elapsed.IsRunning) throw new InvalidOperationException("Create a fresh viewer for another file.");
            elapsed.Start(); LoadCompletion = LoadAsync(path);
        }

        private async Task LoadAsync(string path)
        {
            var token = lifetime.Token;
            try
            {
                initialEntry = await Task.Run(() => DicomCatalog.ReadEntry(path), token);
                if (token.IsCancellationRequested) return;
                currentEntry = initialEntry;
                tagSource.Items.Add(new TagChoice("Current image", null)); tagSource.SelectedIndex = 0;
                UpdateTags();
                // The selected image is available before the containing folder is indexed.
                if (initialEntry.Rows > 0 && initialEntry.Columns > 0 && initialEntry.Modality != "RTDOSE")
                {
                    try
                    {
                        native = await Task.Run(() => PixelPlane.Load(initialEntry), token);
                        if (token.IsCancellationRequested) return;
                        SetEntryFocus(initialEntry); SetInitialWindow(); Cache(initialEntry.Path, native);
                        FirstImageMilliseconds = elapsed.Elapsed.TotalMilliseconds; Redraw();
                    }
                    catch (OperationCanceledException) { return; }
                    catch (Exception) { status.Text = "Unable to display the individual image file; checking available series."; }
                }
                status.Text = "Scanning DICOM files in the same folder …";
                var progressClock=System.Diagnostics.Stopwatch.StartNew();
                catalog = await Task.Run(() => DicomCatalog.Scan(path,token,null,OnEntryFound,(phase,count,total)=>
                {
                    if(progressClock.ElapsedMilliseconds<120&&count!=total)return;progressClock.Restart();
                    Dispatcher.BeginInvoke(new Action(()=>{if(disposed||scanComplete)return;activity.Text=phase=="headers"?$"● Searching RT files · {count}/{total} headers":$"● RT ready · scanning image series {count}/{total}";}));
                },initialEntry),token);
                IndexMilliseconds=elapsed.Elapsed.TotalMilliseconds;scanComplete=true;activity.Visibility=System.Windows.Visibility.Collapsed;activity.Text=$"✓ Scan complete · {planData.Count} plans · {doses.Count} doses · {catalog.Files.Count} files"+(rtFailures>0?$" · {rtFailures} unreadable RT files":"");
                if (token.IsCancellationRequested) return;
                Func<DicomEntry,bool> samePatient = e => initialEntry.PatientKey != "|" && !string.IsNullOrEmpty(initialEntry.PatientKey) ? e.PatientKey == initialEntry.PatientKey : !string.IsNullOrEmpty(initialEntry.StudyUid) && e.StudyUid == initialEntry.StudyUid;
                catalog.Files = catalog.Files.Where(samePatient).ToList(); catalog.Stacks = catalog.Stacks.Where(s => s.Entries.All(samePatient)).ToList();
                registrations=await Task.Run(()=>RegistrationReader.Read(catalog),token);loadRevision++;sumResult=null;
                changing = true; series.ItemsSource = catalog.Stacks; changing = false;
                var rtTask = Task.CompletedTask;
                var first = catalog.Stacks.FirstOrDefault(s => s.Entries.Any(e => SamePath(e.Path, path)));
                if (first != null)
                {
                    changing = true; series.SelectedItem = first; changing = false;
                    await Task.WhenAll(SelectStackAsync(first), rtTask);
                }
                else
                {
                    await rtTask;
                    // RT opening follows an explicit reference to the planning images.
                    var wantedSet = structures.FirstOrDefault(s => SamePath(s.Entry.Path, path));
                    var wantedPlan = planData.FirstOrDefault(p => SamePath(p.Entry.Path, path));
                    var wantedDose = doses.FirstOrDefault(d => SamePath(d.Entry.Path, path));
                    if (wantedDose != null) wantedPlan = planData.FirstOrDefault(p => p.Entry.SopUid == wantedDose.PlanUid);
                    if (wantedPlan == null && wantedSet != null) { var matches=planData.Where(p=>p.StructureSopUid==wantedSet.Entry.SopUid).ToArray();if(matches.Length==1)wantedPlan=matches[0]; }
                    if (wantedPlan != null) { wantedSet = structures.FirstOrDefault(s => s.Entry.SopUid == wantedPlan.StructureSopUid);if(!userSelectedPlan){selectedPlan=wantedPlan;sumMode=false;RefreshPlanChoices();} }
                    if (wantedSet != null)
                        first = catalog.Stacks.FirstOrDefault(s => s.Entries.Any(e => wantedSet.ReferencedSeries.Contains(e.SeriesUid)));
                    if (first != null) { changing = true; series.SelectedItem = first; changing = false; await SelectStackAsync(first); }
                    else status.Text = "No unambiguously referenced image series. DICOM tags are available; select an image series above.";
                }
                if (token.IsCancellationRequested) return;
                // An RT object's attributes stay explicitly selectable after its images appear.
                if (initialEntry.Modality.StartsWith("RT") || initialEntry.Modality == "REG")
                {
                    var choice = tagSource.Items.Cast<TagChoice>().FirstOrDefault(x => x.Entry != null && SamePath(x.Entry.Path, initialEntry.Path));
                    if (choice != null) tagSource.SelectedItem = choice;
                }
                RefreshPlanChoices();RefreshRt();if(sumMode)await BuildSumAsync();
            }
            catch (OperationCanceledException) { }
            catch (Exception) { if (!disposed) status.Text = "Unable to load the complete DICOM preview. Check the file, read permissions or encoding."; }
        }

        private async Task SelectStackAsync(ImageStack stack)
        {
            seriesLoad?.Cancel(); seriesLoad = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            var token = seriesLoad.Token; int generation = ++seriesGeneration; ++pixelGeneration;
            var focusMap=RegistrationReader.Resolve(registrations,currentEntry?.FrameUid,stack.FrameUid);
            Vec3? preservedFocus=currentStack!=null&&focusMap!=null?(Vec3?)focusMap.Transform(focus):null;
            overlayLoad?.Cancel();overlayVolume=null;overlayStack=null;
            currentStack = stack; currentEntry = null; volume = null; native = null; pixelCache.Clear(); pixelOrder.Clear();
            changing = true; planes.SelectedItem = "Native";
            sliceSlider.Maximum = Math.Max(0, stack.Entries.Count - 1);
            int selected = stack.Entries.FindIndex(e => initialEntry != null && SamePath(e.Path, initialEntry.Path));
            sliceIndex = preservedFocus.HasValue ? Enumerable.Range(0,stack.Entries.Count).OrderBy(i=>Math.Abs((stack.Entries[i].Origin-preservedFocus.Value).Dot(stack.Entries[i].AxisX.Cross(stack.Entries[i].AxisY)))).First() : selected >= 0 ? selected : stack.Entries.Count / 2; requestedSliceIndex = sliceIndex; sliceSlider.Value = sliceIndex; changing = false;
            currentEntry = stack.Entries[sliceIndex]; SetEntryFocus(currentEntry); if(preservedFocus.HasValue)focus=preservedFocus.Value; UpdateTags();
            RebuildPanes();
            try
            {
                await ShowSliceAsync(sliceIndex, preservedFocus.HasValue);
                if (token.IsCancellationRequested || generation != seriesGeneration) return;
                SetInitialWindow(); RefreshRt();
                if (!stack.CanMpr) { status.Text = "Native image stack · " + stack.GeometryWarning; return; }
                status.Text = $"{stack.Entries.Count} slices · loading volume in the background …";
                var loaded = await Task.Run(() => VolumeData.Load(stack, token), token);
                if (token.IsCancellationRequested || generation != seriesGeneration) return;
                volume = loaded; pixelCache.Clear(); pixelOrder.Clear(); if(native==null)await ShowSliceAsync(requestedSliceIndex,true); Redraw();
                status.Text = $"{catalog?.Files.Count ?? stack.Entries.Count} files · {catalog?.Stacks.Count ?? 1} image groups · MPR ready" + ((catalog?.SkippedFiles ?? 0) > 0 ? $" · {catalog.SkippedFiles} files skipped" : "");
            }
            catch (OperationCanceledException) { }
            catch (Exception) { if (!token.IsCancellationRequested) status.Text = "Native image stack available. MPR is unavailable for this geometry, encoding or memory size."; }
        }

        private async Task ShowSliceAsync(int index, bool preserveInPlaneFocus)
        {
            if (disposed || currentStack == null || currentStack.Entries.Count == 0) return;
            var stack = currentStack; index = Math.Max(0, Math.Min(stack.Entries.Count - 1, index));
            var entry = stack.Entries[index]; requestedSliceIndex=index; int generation = ++pixelGeneration; int seriesId=seriesGeneration;
            var token=seriesLoad?.Token??lifetime.Token;
            try
            {
                PixelPlane plane;
                if (volume != null && volume.Depth == stack.Entries.Count)
                {
                    int size = volume.Width * volume.Height; var values = new float[size]; Array.Copy(volume.Values, size * index, values, 0, size);
                    plane = new PixelPlane { Width = volume.Width, Height = volume.Height, Values = values, Min = volume.Min, Max = volume.Max, Invert = volume.Invert };
                }
                else if (!pixelCache.TryGetValue(entry.Path, out plane)) plane = await DecodeAsync(entry,token,()=>generation==pixelGeneration&&seriesId==seriesGeneration);
                if (disposed || generation != pixelGeneration || currentStack != stack) return;
                Cache(entry.Path, plane); native = plane; currentEntry = entry; sliceIndex = index;
                if (FirstImageMilliseconds == 0) FirstImageMilliseconds = elapsed.Elapsed.TotalMilliseconds;
                if (preserveInPlaneFocus && entry.HasGeometry)
                {
                    var normal = entry.AxisX.Cross(entry.AxisY).Normalized(); focus = focus + normal * ((entry.Origin - focus).Dot(normal));
                }
                else SetEntryFocus(entry);
                changing = true; sliceSlider.Value = index; changing = false; UpdateTags(); Redraw();
                if (volume == null) Prefetch(stack, index, seriesGeneration);
            }
            catch (OperationCanceledException) { }
            catch (Exception) { if (!disposed && generation == pixelGeneration) status.Text = "Unable to decode this slice. The last displayed slice remains identified."; }
        }

        private async void Prefetch(ImageStack stack, int center, int generation)
        {
            var token=seriesLoad?.Token??lifetime.Token;int request=pixelGeneration;
            // A small neighborhood is enough while the full volume is being prepared.
            foreach (int offset in new[] { 1, -1, 2, -2 })
            {
                int i = center + offset;
                if (i < 0 || i >= stack.Entries.Count || disposed || generation != seriesGeneration || volume != null) continue;
                var entry = stack.Entries[i]; if (pixelCache.ContainsKey(entry.Path)) continue;
                try
                {
                    var p = await DecodeAsync(entry,token,()=>generation==seriesGeneration&&request==pixelGeneration&&volume==null);
                    if (disposed || generation != seriesGeneration || volume != null) return; Cache(entry.Path, p);
                }
                catch (Exception) { /* Explicit navigation will report a decode failure. */ }
            }
        }

        private void Cache(string key, PixelPlane plane)
        {
            if (pixelCache.ContainsKey(key)) return;
            long size=plane.Values.LongLength*4L;if(size>32L*1024*1024)return;
            while ((pixelCache.Count >= 9||pixelCache.Values.Sum(p=>p.Values.LongLength*4L)+size>32L*1024*1024) && pixelOrder.Count > 0) pixelCache.Remove(pixelOrder.Dequeue());
            pixelCache[key] = plane; pixelOrder.Enqueue(key);
        }
        private readonly SemaphoreSlim decodeGate=new SemaphoreSlim(2);
        private int requestedSliceIndex;
        private async Task<PixelPlane> DecodeAsync(DicomEntry entry,CancellationToken token,Func<bool> isCurrent)
        {
            await decodeGate.WaitAsync(token);
            try{token.ThrowIfCancellationRequested();if(disposed||!isCurrent())throw new OperationCanceledException();return await Task.Run(()=>PixelPlane.Load(entry),token);}
            finally{decodeGate.Release();}
        }
        private void SetEntryFocus(DicomEntry entry) { focus = entry.Origin + entry.AxisX * ((entry.Columns - 1) * entry.SpacingX / 2) + entry.AxisY * ((entry.Rows - 1) * entry.SpacingY / 2); }
        private void SetInitialWindow()
        {
            if (currentEntry != null && currentEntry.WindowWidth > 0) SetWindow(currentEntry.WindowCenter, currentEntry.WindowWidth);
            else if (currentEntry?.Modality == "CT") SetWindow(40, 400); else AutoWindow();
        }
        private static bool SamePath(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        private sealed class TagChoice
        {
            public string Label { get; } public DicomEntry Entry { get; }
            public TagChoice(string label, DicomEntry entry) { Label = label; Entry = entry; }
            public override string ToString() => Label;
        }
        private DicomEntry lastTagEntry;
        private void UpdateTags()
        {
            if (disposed) return; var entry = (tagSource.SelectedItem as TagChoice)?.Entry ?? currentEntry;
            if (entry == null) return;
            try { if (lastTagEntry != entry) { tags = TagReader.Read(entry.Dataset); tagTree.SetRows(tags, lastTagEntry != null && lastTagEntry.SeriesUid != entry.SeriesUid); lastTagEntry = entry; } FilterTags(); }
            catch (Exception) { tagStatus.Text = "Unable to read attributes."; }
        }
        private void FilterTags()
        {
            string q = tagSearch.Text.Trim();
            var filtered = q.Length == 0 ? tags : tags.Where(t => new[] { t.Path, t.Tag, t.Name, t.VR, t.Value }.Any(s => s?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
            tagTree.Filter(q); tagStatus.Text = $"{filtered.Count} / {tags.Count} attributes · read-only" + ((string)planes.SelectedItem != "Native" && (tagSource.SelectedItem as TagChoice)?.Entry == null ? "\nNative source file; MPR has no separate tags." : "");
        }
    }
}
