using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace QuickLook.DicomRT
{
    public sealed partial class ViewerControl
    {
        private IEnumerable<StructureSet> SelectedStructures => !sumMode&&!userSelectedPlan&&initialEntry?.Modality=="RTSTRUCT" ? structures.Where(s=>s.Entry.SopUid==initialEntry.SopUid) : !sumMode&&!userSelectedPlan&&initialEntry?.Modality=="RTDOSE"&&selectedPlan==null ? DoseCompatibleStructures : sumMode ? structures.Where(s=>sumResult?.IncludedPlanUids!=null&&planData.Any(p=>sumResult.IncludedPlanUids.Contains(p.Entry.SopUid)&&p.StructureSopUid==s.Entry.SopUid)) : selectedPlan != null ? structures.Where(s => s.Entry.SopUid == selectedPlan.StructureSopUid) : planData.Count>1?Enumerable.Empty<StructureSet>():structures;
        private IEnumerable<StructureSet> DoseCompatibleStructures
        {
            get {var dose=doses.FirstOrDefault(d=>d.Entry.SopUid==initialEntry?.SopUid);return dose==null?Enumerable.Empty<StructureSet>():structures.Where(s=>s.Rois.Any(r=>RegistrationReader.Resolve(registrations,r.FrameUid,dose.FrameUid)!=null));}
        }
        private IEnumerable<DoseGrid> SelectedDoses => !sumMode&&!userSelectedPlan&&initialEntry?.Modality=="RTDOSE" ? doses.Where(d=>d.Entry.SopUid==initialEntry.SopUid) : sumMode ? (sumResult?.Dose==null?Enumerable.Empty<DoseGrid>():new[]{sumResult.Dose}) : selectedPlan != null ? doses.Where(d => d.PlanUid == selectedPlan.Entry.SopUid) : planData.Count>1?Enumerable.Empty<DoseGrid>():doses;
        private string SceneFrameUid => HasImage&&!string.IsNullOrEmpty(currentEntry?.FrameUid)?currentEntry.FrameUid:
            !string.IsNullOrEmpty(selectedPlan?.FrameUid)?selectedPlan.FrameUid:
            !sumMode&&!userSelectedPlan&&initialEntry?.Modality=="RTDOSE"?doses.FirstOrDefault(d=>d.Entry.SopUid==initialEntry.SopUid)?.FrameUid:
            SelectedStructures.SelectMany(s=>s.Rois).Select(r=>r.FrameUid).Concat(SelectedDoses.Select(d=>d.FrameUid)).FirstOrDefault(f=>!string.IsNullOrEmpty(f));
        private Matrix4 TransformToImage(string frame) => RegistrationReader.Resolve(registrations,frame,SceneFrameUid);

        private void RefreshRt()
        {
            if (disposed) return;
            BuildRoiList(); BuildDoseList(); UpdateOverlayChoices(); if(workspaceMode=="DVH"){UpdateDvhChoices();UpdateDvh();} if(!sumMode&&workspaceMode=="MLC"&&selectedPlan!=centralPlan)SetWorkspace("MLC");
            int regFiles = catalog?.Files.Count(f => f.Modality == "REG") ?? 0;
            var frames = SelectedStructures.SelectMany(s => s.Rois).Select(r => r.FrameUid).Concat(SelectedDoses.Select(d => d.FrameUid)).Where(f => !string.IsNullOrEmpty(f)).Distinct().ToArray();
            bool own = currentEntry != null && frames.Any(f => f == currentEntry.FrameUid);
            bool registered = currentEntry != null && frames.Any(f => f != currentEntry.FrameUid && TransformToImage(f) != null);
            registrationStatus.Foreground = own || registered ? Theme.Accent : Theme.Muted;
            registrationStatus.Text = registered ? "REG active · RT mapped to this image series" : own ? "RT in the same coordinate system" : frames.Length == 0 ? "" : $"No unambiguous RT association with this series ({regFiles} REG).";
            Redraw();
        }
        private void SetRois(bool visible) { foreach (var roi in SelectedStructures.SelectMany(s => s.Rois)) roi.Visible = visible; BuildRoiList(); RoiVisibilityChanged(); }
        private void RoiVisibilityChanged(){Redraw();if(workspaceMode=="DVH")UpdateDvh();}
        private void BuildRoiList()
        {
            roiList.Children.Clear(); string query = roiSearch.Text.Trim();
            foreach (var roi in SelectedStructures.SelectMany(s => s.Rois).Where(r => query.Length == 0 || r.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                var transform = TransformToImage(roi.FrameUid); var row = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
                var toggle = new CheckBox { IsChecked = roi.Visible, IsEnabled = workspaceMode=="DVH" || transform != null, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(1, 0, 6, 0), ToolTip = "Show/hide structure" };
                toggle.Checked += (s, e) => { roi.Visible = true; RoiVisibilityChanged(); }; toggle.Unchecked += (s, e) => { roi.Visible = false; RoiVisibilityChanged(); }; DockPanel.SetDock(toggle, Dock.Left); row.Children.Add(toggle);
                var color = new Border { Background = new SolidColorBrush(Color.FromRgb(roi.Red, roi.Green, roi.Blue)), Width = 4, Margin = new Thickness(0, 2, 5, 2) }; DockPanel.SetDock(color, Dock.Left); row.Children.Add(color);
                var jump = Theme.Button(roi.Name); jump.HorizontalContentAlignment = HorizontalAlignment.Left; jump.Padding = new Thickness(4); jump.Margin = new Thickness(0); jump.FontSize = 11; jump.IsEnabled = workspaceMode=="DVH" || transform != null; jump.ToolTip = transform == null ? "No matching registration to the displayed series" : "Go to structure";
                jump.Click += async (s, e) => { if(workspaceMode=="DVH"){dvhView?.FocusStructure(roi);return;} var map = TransformToImage(roi.FrameUid); if (map != null){bool in3D=workspaceMode=="3D"||workspaceMode=="Bild"&&(string)planes.SelectedItem=="MPR + 3D";if(in3D&&!roi.Visible){roi.Visible=true;BuildRoiList();}await MoveFocusAsync(map.Transform(roi.Center));if(in3D){if(workspaceMode=="3D")threeDView?.FocusStructure(roi);else mprThreeD?.FocusStructure(roi);}} };
                row.Children.Add(jump); roiList.Children.Add(row);
            }
            if (roiList.Children.Count == 0) roiList.Children.Add(Theme.Text("No matching structures", 11, Theme.Muted));
        }
        private void BuildDoseList()
        {
            doseList.Children.Clear();if(sumMode) doseList.Children.Add(Theme.Text(sumResult?.Message??"Preparing plan sum …",11,Theme.Accent));
            foreach (var dose in SelectedDoses)
            {
                bool available = TransformToImage(dose.FrameUid) != null;
                var check = new CheckBox { Content = dose.Label, IsChecked = dose.Visible, IsEnabled = available, Foreground = Theme.Foreground, Margin = new Thickness(2, 10, 2, 4) };
                check.Checked += (s, e) => { dose.Visible = true; Redraw(); }; check.Unchecked += (s, e) => { dose.Visible = false; Redraw(); };
                doseList.Children.Add(check); doseList.Children.Add(Theme.Text($"Maximum {dose.Maximum:0.###} {dose.Units}\nReference for percentages", 11, Theme.Muted));
                if (!available) doseList.Children.Add(Theme.Text("No matching registration.", 11, Theme.Muted));
            }
            if (doseList.Children.Count == 0) doseList.Children.Add(Theme.Text("No dose associated with this plan", 11, Theme.Muted));
        }
        private async Task MoveFocusAsync(Vec3 world)
        {
            userNavigatedImage=true;focus = world;viewportCenter=world;
            if (workspaceMode=="Bild" && currentStack != null && (string)planes.SelectedItem == "Native")
            {
                int nearest = Enumerable.Range(0, currentStack.Entries.Count).OrderBy(i => Math.Abs((currentStack.Entries[i].Origin - world).Dot(currentStack.Entries[i].AxisX.Cross(currentStack.Entries[i].AxisY)))).First();
                await ShowSliceAsync(nearest, true);
                return; // ShowSliceAsync already redraws; do not cancel that frame with a duplicate.
            }
            Redraw();
        }
        private async Task ScrollAsync(string plane, int steps)
        {
            userNavigatedImage=true;
            if (plane == "Native") { await ShowSliceAsync(requestedSliceIndex + steps, true); return; }
            if (volume == null) return;
            double step = Math.Min(volume.SpacingX, Math.Min(volume.SpacingY, volume.SpacingZ));
            Vec3 axis = plane == "Axial" ? new Vec3(0, 0, 1) : plane == "Coronal" ? new Vec3(0, 1, 0) : new Vec3(1, 0, 0);
            var candidate = focus + axis * (steps * step);
            // Constrain the scroll coordinate to the physical volume's bounding box.
            var corners = new List<Vec3>(); foreach (int x in new[] { 0, volume.Width - 1 }) foreach (int y in new[] { 0, volume.Height - 1 }) foreach (int z in new[] { 0, volume.Depth - 1 }) corners.Add(volume.WorldAt(x, y, z));
            double value = candidate.Dot(axis), bounded = Math.Max(corners.Min(p => p.Dot(axis)), Math.Min(corners.Max(p => p.Dot(axis)), value));
            focus = candidate + axis * (bounded - value); Redraw();
        }
        private void Redraw()
        {
            if (disposed) return;ExportIdentity.SetContext(this,CurrentExportIdentity());UpdateImageModeButtons();windowRange?.SetSource(volume?.Values??native?.Values,volume?.Min??native?.Min??0,volume?.Max??native?.Max??1,currentEntry?.Modality=="CT"?"HU":"");
            var identitySource=(workspaceMode=="MLC"?selectedPlan?.Entry:null)??currentEntry??selectedPlan?.Entry??initialEntry;
            var patientName=identitySource?.Dataset?.GetSingleValueOrDefault<string>(Dicom.DicomTag.PatientName,"")??"";
            var patientId=identitySource?.Dataset?.GetSingleValueOrDefault<string>(Dicom.DicomTag.PatientID,"")??"";
            patientIdentity.Text=(string.IsNullOrWhiteSpace(patientName)?"Name unavailable":patientName.Replace('^',' '))+"  ·  ID: "+(string.IsNullOrWhiteSpace(patientId)?"unavailable":patientId);patientIdentity.ToolTip=patientIdentity.Text;
            if(workspaceMode=="MLC"&&centralPlayback!=null)centralPlayback.SetBodyRegion(HasImage&&selectedPlan!=null&&TransformToImage(selectedPlan.FrameUid)!=null?currentEntry?.Dataset?.GetSingleValueOrDefault<string>(Dicom.DicomTag.BodyPartExamined,""):null);
            var allRoiOverlays = new List<RoiOverlay>(); var doseOverlays = new List<DoseOverlay>();
            if (!string.IsNullOrEmpty(SceneFrameUid))
            {
                foreach (var roi in SelectedStructures.SelectMany(s => s.Rois))
                {
                    var transform = TransformToImage(roi.FrameUid);
                    if (transform != null) allRoiOverlays.Add(new RoiOverlay { Roi = roi, RoiToImage = transform });
                }
                foreach (var dose in SelectedDoses.Where(d => d.Visible))
                {
                    var transform = RegistrationReader.Resolve(registrations, SceneFrameUid, dose.FrameUid);
                    if (transform != null) doseOverlays.Add(new DoseOverlay { Dose = dose, ImageToDose = transform });
                }
            }
            var roiOverlays=allRoiOverlays.Where(r=>r.Roi.Visible).ToList();
            var isocenters=IsocenterNavigation.MappedPoints(selectedPlan,selectedPlan==null?null:TransformToImage(selectedPlan.FrameUid));
            UpdateIsocenterButton(isocenters);UpdateIsodoseContext();UpdateIsodoseLegend(doseOverlays);
            latestScene = new RenderScene { FrameUid=SceneFrameUid,InteractionPreview=movingCrosshair,Plan=selectedPlan,PlanToImage=selectedPlan==null?null:TransformToImage(selectedPlan.FrameUid),ShowFields=showFields.IsChecked==true,ActiveControlPointIndex=centralPlayback?.LocalPosition??0,
                ActiveBeam=neutralFields?null:selectedPlan?.Beams.Contains(activeField)==true?activeField:MlcTimeline.PlaybackOrder(selectedPlan).FirstOrDefault(),ActiveControlPoint=neutralFields?null:selectedPlan?.Beams.Contains(activeField)==true?activeFieldPoint:MlcTimeline.PlaybackOrder(selectedPlan).FirstOrDefault()?.ControlPoints.FirstOrDefault(),Isocenters=isocenters, Volume = volume, Native = native, Entry = currentEntry, Plane = (string)planes.SelectedItem, Focus = focus, ViewCenter=viewportCenter, WindowCenter = windowCenter, WindowWidth = windowWidth, Zoom = zoom, Structures = roiOverlays, Doses = doseOverlays, DoseOpacity = opacity.Value, Isodoses = !movingCrosshair&&iso.IsChecked == true,
                DoseWash=wash.IsChecked==true,DoseMinimumPercent=doseMin.Value,DoseMaximumPercent=doseMax.Value,IsoLevels=displayedIsoLevels,AbsoluteIsodoses=absoluteIsodoses,IsoColorMaximum=isodoseMaximum,IsoColors=new Dictionary<double,int>(isodoseColors),
                OverlayVolume=overlayVolume,ImageToOverlay=overlayStack==null?null:RegistrationReader.Resolve(registrations,currentEntry?.FrameUid,overlayStack.FrameUid),OverlayOpacity=blend.Value,
                OverlayWindowCenter=overlayStack?.Entries[0].WindowWidth>0?overlayStack.Entries[0].WindowCenter:((overlayVolume?.Min??0)+(overlayVolume?.Max??1))/2.0,
                OverlayWindowWidth=overlayStack?.Entries[0].WindowWidth>0?overlayStack.Entries[0].WindowWidth:Math.Max(1,(overlayVolume?.Max??1)-(overlayVolume?.Min??0)) };
            if(selectedPlan!=null||centralPlayback!=null){EnsurePlayback();centralPlayback.SetAnatomy(latestScene,latestScene.PlanToImage);var preload=latestScene.Snapshot();preload.Structures=allRoiOverlays;centralPlayback.Preload(selectedPlan,preload,latestScene.PlanToImage);SyncFieldControls();}
            if(workspaceMode=="Bild")foreach(var pane in panes){var scene=latestScene.Snapshot();scene.Plane=(string)pane.Tag;pane.Scene=scene;PatientOrientationBadge badge;if(orientationBadges.TryGetValue(pane,out badge)){badge.Visibility=scene.Volume!=null||scene.Entry?.HasGeometry==true?Visibility.Visible:Visibility.Collapsed;if(badge.Visibility==Visibility.Visible)badge.SetPlane(SliceGeometry.Create(scene));}}
            if(!movingCrosshair&&(threeDView!=null||latestScene.Volume!=null||latestScene.Structures.Count>0||latestScene.Doses.Count>0)){EnsureThreeDView();threeDView.PreloadScene(latestScene);}
            if(workspaceMode=="3D")AttachThreeD(false);
            if(workspaceMode=="Bild"&&(string)planes.SelectedItem=="MPR + 3D"){AttachThreeD(true);mprThreeD.SetSlicePlanes(focus,volume);}
            position.Text = $"Slice {sliceIndex + 1}/{currentStack?.Entries.Count ?? 1}  ·  W {windowWidth:0} / L {windowCenter:0}  ·  LPS {focus.X:0.0}, {focus.Y:0.0}, {focus.Z:0.0} mm";
        }
    }
}
