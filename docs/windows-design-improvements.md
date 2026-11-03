# QuickLookRT: design and interaction improvements for the Windows agent

This document collects changes from the macOS port that can be reused in the Windows viewer. Keep the original QuickLookRT compact dark interface, panel hierarchy and numerical algorithms. This is a transfer guide, not an instruction to replace the Windows interface with WebKit. Mac implementation is on the `macos-preview` branch; inspect the linked source when porting.

## Reusable image-view icons

| View | Asset | Meaning |
| --- | --- | --- |
| Axial | ![Axial](assets/view-icons/axial.svg) [SVG](assets/view-icons/axial.svg) | Horizontal section through a shared reference cube |
| Coronal | ![Coronal](assets/view-icons/coronal.svg) [SVG](assets/view-icons/coronal.svg) | Vertical plane along one cube axis |
| Sagittal | ![Sagittal](assets/view-icons/sagittal.svg) [SVG](assets/view-icons/sagittal.svg) | Vertical plane along the other cube axis |
| Linked views | ![2×2](assets/view-icons/quad.svg) [SVG](assets/view-icons/quad.svg) | Axial, coronal, sagittal and 3D in a 2×2 layout |
| 3D | ![3D](assets/view-icons/3d.svg) [SVG](assets/view-icons/3d.svg) | Standalone 3D overview |

Use the same cube outline for all three section views. The two vertical planes run in opposite screen directions, so coronal and sagittal remain distinguishable. Keep visible **Axial**, **Coronal**, **Sagittal**, **2 × 2**, and **3D** labels alongside the symbols. A tooltip gives the action and shortcut; it should not be the only way to identify a view. Native keeps its text label.

These files are plain vectors created in this repository, with no external asset dependency. They match the inline SVG paths in [Mac/Web/index.html](../Mac/Web/index.html). Render at 19–20 device-independent pixels; use a 1.2-unit stroke in a 24×24 view box, rounded line joins, the viewer accent `#64B5F6`, and a plane fill at about 33% alpha. The active view can increase the plane-fill opacity. Let the button, rather than the icon, provide the click target and keyboard focus.

Windows already creates its own DrawingGroup in [Viewer/ViewerNavigation.cs](../Viewer/ViewerNavigation.cs). Reuse that mechanism. `Geometry.Parse` accepts the path commands below; draw the outline and the filled section as two geometries:

```text
Shared cube:
M3 7 12 2 21 7 21 17 12 22 3 17Z M3 7 12 12 21 7 M12 12V22
Axial section:
M12 7 21 12 12 17 3 12Z
Coronal section:
M7.5 4.5 16.5 9.5 16.5 19.5 7.5 14.5Z
Sagittal section:
M7.5 9.5 16.5 4.5 16.5 14.5 7.5 19.5Z
```

Keep the original Windows shortcut order: Ctrl+1 Native, Ctrl+2 Axial, Ctrl+3 Coronal, Ctrl+4 Sagittal, Ctrl+5 linked 2×2, Ctrl+6 3D. macOS substitutes Command. Do not reverse the last two.

## Small changes that improve readability

- **Settings:** preserve the existing button dimensions; give the gear a clear 20–21 px drawing rather than enlarging the toolbar. The Mac gear path is in `Mac/Web/index.html`; Windows can use a native vector at the same visual size.
- **Plan selection:** when RTPlanLabel is empty or literally `None`, show a useful fallback such as `RT-Plan 1 · 6 Felder`. Retain the original value in the metadata/tooltip and export identity. Do not rewrite the DICOM or treat a placeholder as absence of a plan.
- **Dose choices:** give same-plan alternatives distinguishable labels, including a dose ordinal, summation type and maximum/unit. Source filenames are unnecessary. Keep independent visibility controls in the existing Doses tab, and the DVH dose picker beside its chart.
- **Loading:** reserve one compact strip for real stage/count information. Show what is being prepared: catalog, images, DVHs, contours or DRRs. Use known completed/total counts; use an indeterminate indicator when no total is known. Expose a useful initial image/aperture while the remaining context loads.
- **Folder access:** remove a permanent permission toolbar. A local folder-access action belongs only in the missing-context state and must say what will be read. This is a macOS integration detail, not a new Windows sharing feature.

## MLC, jaws and machine information

See [Mac/Web/app.js](../Mac/Web/app.js), [Mac/Web/parity.js](../Mac/Web/parity.js), and [Mac/Worker/BeamViews.cs](../Mac/Worker/BeamViews.cs).

1. Draw actual leaf-bank tips independently of the list of open aperture rectangles. A closed opening must not make leaf edges disappear. On Mac, tips use a 3 px dark backing with a 1.7 px bright cyan/white stroke; additional layers use a warm contrasting stroke. These are screen-space widths, so zoom does not make them unreadable.
2. Draw actual moving X1/X2 and Y1/Y2 jaws, when positions exist, with separate colors and short labels. Mac uses yellow `#FFD166` for X and lavender `#D991FF` for Y, with a dark backing. Missing X positions stay absent; the MLC extent is not evidence of a jaw position. The jaw-outline toggle does not remove physical occlusion.
3. Remove the decorative circular ring from the beam-eye aperture view. This is separate from a real ring LINAC's orientation schematic.
4. Display `BeamMachineInfo.Label` beside the field selection; expose its detail in a tooltip. Unknown identity remains unknown. Never promote a local alias to a verified manufacturer/model.
5. Keep DRR, aperture and structure silhouettes at exactly the same control point and geometry. A fast aperture can appear immediately; old-angle DRRs or silhouettes must not remain over a new aperture. Refine the exact current DRR after navigation pauses.
6. Preserve the original grouped, movable/resizable orientation context: axial field arrangement at the active control-point isocenter plus LINAC orientation. Reuse its CT raster when only the beam angle/leaf positions change. Keep Fields synchronized across image, MLC and 3D.

The Windows implementation already has useful corresponding parts: [MlcContextPanel](../Viewer/MlcContextPanel.cs), [FieldArrangementControl](../Viewer/FieldArrangementControl.cs), [MlcPlaybackControl](../Viewer/MlcPlaybackControl.cs), [ViewerFields](../Viewer/ViewerFields.cs), and [LinacOrientationControl](../Viewer/LinacOrientationControl.cs). Preserve these behaviors while transferring visual improvements.

## User-confirmed dose display changes

The following are additional user requests implemented in the Mac UI; the existing Windows defaults were intentionally retained in the shared renderer. A Windows agent must implement and test these deliberately rather than assume they are already inherited.

| Preset | Required green levels | Upper levels | Reference |
| --- | --- | --- | --- |
| Standard | 95% dark green `#006400`; 100% light green `#90EE90` | 105% yellow, 107% red, 110%/120% magenta tones | Unambiguous positive TARGET prescription =100%; otherwise body D0.035 cm³ =110% |
| STX | 70% dark green; 80% light green | 90% red, 100% magenta; stops every 10% | Body D0.035 cm³ =100% |

Lower levels progress brown → blue → green. Exact stops and validation are in [Mac/Web/dose-presets.js](../Mac/Web/dose-presets.js), with checks in [Mac/Tests/test_dose_presets.js](../Mac/Tests/test_dose_presets.js). Both palettes/levels can be edited; isodose line width is configurable. Absolute Gy labels have at most one decimal place; internal thresholds keep precision. Switching percent/Gy preserves colors and dose values.

[Mac/Worker/DoseReference.cs](../Mac/Worker/DoseReference.cs) calculates a grid-based, voxel-volume-weighted D0.035 cm³ within supported body contours. It is not an interpolated TPS maximum. Missing/unsupported body geometry or insufficient known dose volume requires an explicit unavailable/manual-reference state; do not silently use the point maximum. Multiple prescriptions remain explicit. References belong to individual source doses. Values above the final palette stop retain that final color.

## Performance changes worth transferring

- Key 3D dose surfaces by source dose plus their resolved physical Gy threshold, not just the displayed percentage. Changing a manual reference must select a new surface; percent/Gy switches must convert existing custom levels, colors and hidden states.
- Cache field extent once per source beam; do not scan every control point and every leaf for each animation frame.
- Key DRRs and structure silhouettes by projection geometry, volume/registration, structure and extent. Leaf-only movement does not change the ray integral or anatomy silhouette. Keep display tone mapping separate from tracing.
- Reuse a loaded volume when plan/dose selection keeps the same source series. Derive native slices from its intensity array. Keep decoded-pixel caches bounded; never create a persistent patient-data cache for this optimization.
- Update field guides independently of CT, structures, dose raster and surfaces. This already exists in `ViewerFields.UpdateFieldOverlays`; preserve it.
- Retain original LINAC/character geometry across animation frames and update transforms. The Mac renderer now batches appearance changes into one full render per animation frame. Windows should inspect its own allocation/render path before copying the technique.
- Coalesce input while rendering, show complete consistent frames during the gesture, and settle on the latest requested slice at full resolution. Do not debounce until scrolling stops, or lower final quality to claim a speedup.
- Cancel obsolete work by document/context/projection generation, and reject stale completion callbacks. Keep context-dependent DVHs independent of workspace switches.

## Export and regression checks

Targets and organs populate DVHs automatically. Keep numeric results, partial-coverage status and unsupported states visible. A separate DVH dose choice must preserve image position, ROI visibility and display references. Verify dose switching with two numerically different source grids, not merely two nonempty curves.

Retain Windows' detailed CSV rows for enabled structures without computed curves (`NotCalculated`). Simple CSV contains calculated curves only. A dedicated DVH PNG includes equal-opacity enabled curves, legend, metrics, statuses and approximation/partial-coverage notice; focusing one curve in the live chart must not dim the others in the export. This PNG is distinct from the whole-viewer screenshot.

Before merging a Windows adaptation, check: original keyboard/mouse behavior, RT-only slices, exact opened RTDOSE, ambiguous associations, multiple plans/doses, both jaw axes, closed MLC apertures, dynamic collimator, source-specific dose references, continuous scrolling, and unchanged DICOM files. Do not include patient identifiers, source filenames, DICOM UIDs, private screenshots or internal paths in public evidence.

The implementation plan and independent source-review findings are in [macOS parity plan](macos-parity-plan.md) and [Goldfish review report](goldfish_review_report.md). Runtime validation and measured timing belong in [Mac/README.md](../Mac/README.md). Three private one-plan examples do not establish universal multi-plan or device compatibility; synthetic arrangements cover additional selection contracts.
