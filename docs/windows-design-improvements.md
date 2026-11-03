# QuickLookRT: design and interaction improvements for the Windows agent

This document collects changes from the macOS port that can be reused in the Windows viewer. Keep the original QuickLookRT compact dark interface, panel hierarchy and numerical algorithms. This is a transfer guide, not an instruction to replace the Windows interface with WebKit. Mac implementation is on the `macos-preview` branch; inspect the linked source when porting.

## Automatic subfolder discovery and multiple-plan selection

**Port this behavior from macOS 0.2.47 / Build 7:** selecting a single CT, RTPLAN, RTSTRUCT or RTDOSE must automatically find its referenced image series, plan, structures and dose in nearby nested export folders. The ordinary opening flow must not require **Search subfolders…**, **Search more images…** or a folder picker first. Keep the selected file/image visible while discovery runs. Ask for a choice when plans are ambiguous or the search area is too large.

### Search sequence and limits

1. Read and show useful selected-file content first: native image, RT slice or MLC aperture. Run discovery off the UI thread and report actual enumeration/catalog/image work in the existing loading strip.
2. Search descendants of the selected file's containing folder. If the referenced context remains incomplete, progressively expand through up to three parent folders, including their eligible subfolders. Reuse already enumerated complete subtrees and paths.
3. Start with **4,096 files**, **128 directories** and **six descendant levels**. Count encountered files, including unrelated files; an archive full of non-DICOM files still consumes the enumeration budget. If a traversal exceeds a budget, retain the last completed catalog and ask before extending the search. Do not silently load a misleading partial result as a complete plan.
4. Stop automatic expansion at collection boundaries. On Windows, resolve the actual user-profile and known Desktop/Documents/Downloads/media folders, drive roots and UNC share roots; do not paste the Mac's POSIX boundary strings. Never silently widen the search to an entire user profile, drive or share. Respect ACLs and unavailable/offline locations.
5. Skip directory symlinks/junctions and other reparse-point entries. The Mac also skips dot-prefixed folders, application bundles and `node_modules`; preserve the intent of avoiding unrelated application/cache trees. Inspect DICOM headers rather than relying on file names, extensions or folder names.
6. Stop expanding when at least one relevant plan has its referenced structure set, a referenced multi-plane image series, an explicitly associated dose, and the required spatial mappings. Missing registration is a reason to continue searching within the bounds; never substitute an identity transform. Existing geometry/MPR/coverage gates still apply after discovery.

On a user-confirmed **Weiter suchen** continuation, the Mac multiplies file and directory budgets by four, capped at **65,536 files / 2,048 directories**, and increases descendant depth by four, capped at **18**. A discovered child-folder choice starts with the initial limits and no automatic parent expansion. The Mac offers up to 128 enumerated child-folder candidates. Keep continuation bounded and retain manual search as a fallback outside the supported neighborhood; this is not whole-disk indexing or a persistent patient cache.

### Association rules

Use [DicomPatientScope.Matches](../Core/DicomPatientScope.cs), the original catalog/stack partitioning and explicit source references. Patient identity is a scope filter, not proof that every plan for that patient belongs to the selected image.

| Selected object | Relevant plan/context |
| --- | --- |
| CT image | Plans whose referenced structure set explicitly references that image's series |
| RTPLAN | The exact opened plan, before any alternatives |
| RTSTRUCT | Plans explicitly referencing that exact structure-set instance |
| RTDOSE | The exact opened dose and its referenced plan; keep that dose selected among alternatives |

Resolve plan → structure-set and dose → plan links by source instance identity; use the original registration resolver for spatial mappings. A matching patient, frame alone, file order or similar folder names must not select a plan. Keep unknown/unsupported associations explicit.

Merge slices split across subfolders into their genuine source series using the original acquisition/geometry partitioning. Deduplicate repeated SOP instances before validating the remaining stack; duplicated backup files must not discard the valid series. Preserve the DICOM reader's null/invalid-file handling during filtered header discovery so ordinary text files do not abort the scan. See [ReadContext in PreviewSession](../Mac/Worker/PreviewSession.cs) and [DicomCatalog](../Core/DicomCatalog.cs).

### Choice UI and state

| State | Required behavior |
| --- | --- |
| One unambiguous referenced context | Load it automatically, including the associated images, structures and dose; start the existing automatic Target/Organ DVHs |
| Several relevant plans, no exact opened plan already selected | Show **Plan auswählen** with only the plans linked to the selected image/structure. Do not auto-select the first matching plan. Distinguish choices by plan label, field count and ordinal; keep the existing useful fallback for empty/`None` labels |
| Search budget exceeded, or still incomplete after the bounded ancestor search | Show **Suchbereich auswählen** with discovered folder choices and **Weiter suchen**. This uses already discovered locations, not an OS folder-picker dialog |
| Cancel / Escape | **Aktuelle Ansicht behalten**: retain the available image/context without guessing a plan. Keep an inline action to reopen the choice |
| Confirm / Enter | **Plan laden** applies exactly the chosen plan and its associated structures/dose, retaining the valid merged image series; **Weiter suchen** resumes only the chosen search scope |
| Another file/document replaces the current one | Close the old choice, cancel obsolete work and reject its pending callbacks/choice IDs; it must not change the new document |

Use one compact WPF choice surface within the existing viewer, with keyboard focus on the labeled selector, Enter to submit and Escape to cancel. Keep the original sidebar plan selector available afterwards. If another modal is open, use the inline choice action rather than opening a second modal. Do not add a permanent permission/sharing toolbar. The Mac's read-only sandbox entitlement is platform-specific and must not be copied into a Windows feature.

The Mac model exposes `discovery = null` or `{ kind: "plans" | "scope", message, choices: [{ id, kind, label }] }`. A plan choice executes normal context selection; a scope choice resumes `autoDiscover` with its current opaque choice ID. IDs are local to the active document/search, not DICOM identifiers or arbitrary user-supplied root paths. Windows may implement the equivalent with native types and direct calls. Guard both the worker/controller and UI against stale choices. If using an IPC bridge, register the continuation operation there too: native acceptance caught a missing bridge allowlist entry on Mac even though the worker tests passed.

### Windows integration points and acceptance checks

Inspect these sources on `macos-preview`:

| Source | What to transfer |
| --- | --- |
| [Mac/Worker/AutomaticDiscovery.cs](../Mac/Worker/AutomaticDiscovery.cs) | Bounded enumeration, ancestor expansion, reference-completeness checks, plan/scope prompts and validated continuation |
| [Mac/Worker/PreviewSession.cs](../Mac/Worker/PreviewSession.cs), [DoseSelection.cs](../Mac/Worker/DoseSelection.cs) | Staged/full opening, deduplication, exact source priority and context resolution |
| [Mac/Web/parity.js](../Mac/Web/parity.js) | `offerDiscoveredContext`, submit/cancel/reopen behavior and document-change guards; translate to WPF |
| [Viewer/ViewerFolderSearch.cs](../Viewer/ViewerFolderSearch.cs) | Existing Windows manual picker and cancellable search; add an automatic bounded path while retaining manual fallback |
| [Viewer/ViewerLoading.cs](../Viewer/ViewerLoading.cs) | Existing incremental RT loading and `RefreshPlanChoices`; replace first-matching-plan selection for images with unique-candidate resolution or an explicit choice |
| [Mac/Tests/test_auto_discovery.py](../Mac/Tests/test_auto_discovery.py), [DiscoveryAcceptance.swift](../Mac/Tests/DiscoveryAcceptance.swift) | Real-worker and native interaction contracts to reproduce in Windows tests |

The current Windows manual folder search uses `imageFilter: e => false` and defers all images. Reusing it unchanged would still require **Search more images…** to assemble a split CT series. The automatic path must discover and load the referenced image series as part of the same opening flow while retaining the original deferred-loading behavior for unrelated series. Port the behavior to the Windows target framework; the Mac worker's records and newer .NET APIs are not a drop-in source replacement.

Before merging the Windows adaptation, verify:

- Single CT in one deep image-part folder finds the other image part and separate plan/structure/dose folders, with a valid complete stack and automatic DVHs, without a folder picker.
- Starting with RTPLAN, RTSTRUCT or RTDOSE resolves the same split context. An exact opened RT object is retained when alternatives exist.
- Two explicitly linked plans produce a choice; cancel preserves images, reopen works, and Enter loads exactly the chosen plan with its dose/structures. An unrelated plan or foreign patient with colliding frame/reference metadata is not offered.
- A required registration outside the first case subtree is found before claiming complete context. Missing/unsupported mappings remain unavailable.
- Repeated SOP instances, unrelated text/invalid files, directory symlinks/junctions and denied folders do not corrupt the loaded stack or cause unbounded traversal.
- A large/noisy tree reaches the budget prompt; explicit continuation works through the actual WPF/controller/worker path, not just a direct worker call. Old/replayed choice IDs and completions after a document change are rejected.
- Source files remain byte-identical. Keep original identifiers, filenames, UIDs, paths and screenshots out of public test evidence.

Mac validation covered all three original cases split across nested folders (173/155/184 instances and 47/42/51 automatic DVHs), generated ambiguity/limit/registration/duplicate/patient-isolation cases, and 10 native choice checks. This is evidence for the transfer contract, not a claim that Windows already implements or passes it. Full scope: [automatic discovery design and evidence](macos-auto-discovery.md).

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
