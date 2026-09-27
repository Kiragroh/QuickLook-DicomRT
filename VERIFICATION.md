# 0.2.19 verification

292 WPF interaction checks passed. Added two-frame workflow coverage: only the initially opened series is fully cataloged, the matching plan is preferred even when another plan is discovered first, a different plan starts without unrelated anatomy, targeted image search loads that plan's frame and updates its availability label, and returning to the first plan restores the correct frame. Individual RTDOSE and RTSTRUCT entries are selectable despite an associated plan. Existing CT-free RTPLAN, RTSTRUCT, RTDOSE/DVH and recursive patient-filtering checks remain active.

The Core suite verifies deferred image identities, direct loading from known paths without rescanning unrelated directories, RT-first discovery and cancellation. 136 RT assertions passed, including REG frame inference from deferred image references without image decoding.

The user-supplied private multi-plan case passed local read-only checks for four plan selections, three-plan and two-plan sums, and a targeted image search that attached the previously unloaded image frame. No patient identifiers, source paths or images were exported to the repository or release media.

Large-folder performance remains dependent on directory enumeration and metadata I/O. Deferred entries contain identity metadata, not image buffers. Source DICOM files remain unchanged. These are engineering checks, not clinical acceptance.

---

# 0.2.18 verification

281 WPF interaction checks passed, including visibility of subfolder search after RTPLAN-only loading and recursive discovery that preserves the current image, finds matching RT and excludes another patient's plan. The underlying sum, Dmax and electron-cutout checks are retained from 0.2.17.

---

# 0.2.17 verification

280 WPF interaction checks passed. New coverage includes compatible sum grouping, explicit member exclusion, maximum-voxel navigation, concave block area, shielding subtraction, prepared block-geometry reuse, and recursive UI discovery while retaining the displayed image and rejecting another patient's RT.

371 render/coordinate checks and 48 contour reformat checks passed. A rotated, concave electron aperture was checked against 4,107 independent analytic ray-projection samples across axial, coronal and sagittal planes. Raster capture was unavailable in the disconnected test session; field-pane clipping is verified from the actual WPF drawing tree and inherited clip regions instead of claiming a screenshot check.

135 RT assertions and the Core suite passed, including irregular/descending dose-maximum coordinates, folder-local default discovery, recursive sibling-folder discovery with RT priority, and cancellation.

The user-supplied private case was read locally without exporting identifiers or images. Four plans and four doses loaded. Separate coordinate groups were identified; the three compatible electron plans generated a sum and a two-plan subset regenerated correctly. Dmax navigation and all four individual-plan switches were exercised. Three custom cutouts parsed successfully. First BEV cutout preparation took 5.4 ms; 100 prepared geometry lookups took 0.3 ms in one local run.

These are engineering checks, not clinical acceptance or guaranteed cold-start timings. Sums combine user-selected physical PLAN doses without fraction scaling, extrapolation, or inferred clinical course equivalence. Custom block guides do not model electron transport or penumbra. Private data is absent from release assets.

---

# 0.2.16 verification

265 WPF interaction checks passed, including fractional exact-angle contour/DRR cache hits, changed playback speeds, stationary 384-pixel DRR refinement, a raster check for visible gantry geometry beyond the CT rectangle, fixed-field intersections beyond that rectangle, and pane clipping. Render checks passed: 368 render/coordinate checks and 48 contour reformat checks.

The approved public nonpatient benchmark was exercised with all 55 structures and DRR enabled, using the actual playback timer. First pass: 119 advances in 12 seconds, zero missing enabled cached overlays, longest interval 294 ms. Warm repeat: 82 advances in 8 seconds, zero missing overlays, median interval 94 ms and longest interval 110 ms. Initial selected-overlay preparation took 2953 ms after case loading. Stationary DRR refinement reached 384 pixels. Twelve warmed integer-CP scroll positions had zero cache misses and 1.73 ms median UI dispatch time.

These are local engineering measurements with background preparation and warm OS files, not guaranteed cold-start, display-frame or clinical performance. First-time preparation may still buffer playback; navigation is not blocked. Actual MLC and image captures were inspected locally. Test fixtures remain separate from the public presentation and release media.

---

# 0.2.15 verification

256 WPF interaction checks passed. Initial positioning checks cover absent dose, incompatible dose frame, preserving manual navigation, choosing the native isocenter slice while in MLC, and preventing repeat automatic jumps.

The approved public nonpatient benchmark was opened directly from RTPLAN, switching early to MLC/3D. Initial dose positioning selected the expected native isocenter slice. Automatic field fitting still matched explicit Reset view after CT arrival (scene radius 200.8 mm on this fixture).

PNG capture with both sidebars open produced a 1600 × 900 whole-viewer image and a 941 × 771 individual 3D crop. GPU readback was present; the whole-viewer export was inspected locally. These are engineering checks, not clinical acceptance.

---

# 0.2.14 verification

251 WPF interaction checks passed, including enlarged/grouped orientation, moving and resizing the shared panel without changing source geometry, shared play/pause across view switches, plan-end looping and active-field synchronization, collimator text, automatic fit not locking the camera, and CT-based radius updates with manual camera preservation.

An approved public nonpatient benchmark was opened directly from RTPLAN. Fields was enabled before the CT volume existed, then the view switched to 3D. After loading, its automatically fitted radius and camera distance matched an explicit Reset view exactly (scene radius 200.8 mm on this fixture). Captures of early loading, the larger MLC orientation panel, standalone 3D and linked 2 × 2 were inspected locally.

Across 60 CP updates, dispatch latency was 0.21 ms median / 0.38 ms p95, with anatomy and track arrays reused. This is local event-dispatch evidence, not a frame-time or clinical validation claim.

---

# 0.2.13 verification

241 WPF interaction checks passed. New raster checks verify zero alpha inside the aperture, decreasing bank-side opacity to zero, removal of shared leaf edges, and transparency only inside the combined double-layer opening. Existing checks cover gantry/couch/collimator orientation, CP updates, static fields and shared view controls.

The approved public nonpatient benchmark was visually inspected in standalone and linked 3D captures. Across 60 CP changes, dispatch latency was 0.20 ms median / 0.46 ms p95 with exact anatomy and track reuse. This measures event dispatch, not end-to-end display latency or clinical acceptance.

---

# 0.2.12 verification

Engineering checks on 27 September 2026. These are inspection previews, not clinical acceptance.

- 237 WPF interaction checks passed, including explicit RTSTRUCT frame retention and a world-space miniature normal facing the isocenter, nonzero collimator/couch axes, static and arc CP updates, dual-layer data, unavailable-geometry clearing, shared Fields visibility, automatic panel discovery and respecting manual close, and retaining the scene/camera when compatible CT data arrives.
- The approved public nonpatient benchmark was inspected in standalone 3D and linked 2 × 2 captures. The active MLC plane follows the source track and changes perspective with the scene. Only this public benchmark was captured.
- Across 60 CP changes, measured event-dispatch latency was **0.19 ms median / 0.75 ms p95** on this workstation, with the prepared anatomy and track arrays retained. This measures dispatch, not end-to-end display or background rendering latency.
- A 16 ms input-priority dispatcher heartbeat during CT opening recorded a **423 ms secondary stall before** the loading changes; the repeat run had **no post-construction interval above 150 ms**. Initial viewer construction still took approximately 396 ms. Whole-run p95 intervals were 36.5 ms before and 41.2 ms after; this is a specific removed interruption, not a general throughput claim or a guarantee for every dataset. OS file caches were warm.

---

# 0.2.9 verification

Release suites passed: 200 WPF interaction checks; 132 RT checks plus analytic DRR projection scenarios; 362 render checks and 48 contour checks; 82 DVH checks; directed playback checks; 44 3D checks plus surface, cache, framing and interaction suites.

Added analytic regressions cover planned dose-rate inheritance, directed wraparound and full 360-degree arc paths, setup classification, fractional parallel contour slabs with missing-level gaps, stable in-plane crosshair geometry, constant/gradient DVH metrics and unavailable full-structure metrics for partial coverage. UI checks cover the neutral field choice, icon view buttons, presets, background preparation of hidden ROI groups, active CSV inclusion and chart export.

The user-approved public nonpatient multicentre benchmark was opened through the actual WPF viewer. DRR/PTV/OAR snapshots, projected arc fields, neutral selection, custom histogram, compact DVH and standalone MLC/Direct3D PNG exports were generated and visually inspected locally. No clinical images or identifiers are included in the release.

Observed on this workstation, with warm OS file cache: 60 MLC wheel events with DRR/PTV/organs enabled took 0.58 ms median and 2.71 ms p95 in the event handler. Including a deliberate 16 ms frame delay, p95 was 62.04 ms. Full 384px DRR settling was 276 ms median for the first tested positions and 46 ms on revisit; structure jump settling was 122 ms median. These are local component observations, not a cold-start guarantee or proof that background projections are instantaneous. Unavailable current overlays stay blank until ready.

Dose Rate Set (300A,0115) follows the segment beginning at its control point; it is not inferred from meterset weights: https://dicom.nema.org/medical/dicom/current/output/chtml/part03/sect_c.8.8.14.html

A read-only local multi-plan acceptance run verified both plans, associated DRRs and current identity after automatically following the uniquely referenced compatible image series. Only aggregate pass results were retained; no clinical images or identifiers were exported.

All geometric, dose, contour and DRR results remain inspection previews; software checks do not establish clinical validation.

---

# Version 0.2.8 — asynchronous projections and actual multi-layer field apertures

Engineering validation on 2026-09-26; source DICOM files were read-only. This is not clinical acceptance.

- Actual field openings are intersected with the displayed plane. Analytic checks cover notches, source-distance scaling, jaw clipping, orthogonal dual-layer intersection, closed leaves and removal of internal leaf seams. Missing aperture data no longer produces an invented default rectangle. Rendering passed 357 checks plus 45 contour boundary checks.
- WPF interaction passed 177 checks, including fine wheel navigation across beam boundaries, default-off DRR, global preparation before entering MLC, frozen 384-pixel cache frames, cache preservation on plan changes, and isolation by CT identity, ROI selection and beam geometry. Playback interpolation checks also passed.
- Public nonpatient multimets case, 1600 × 900, DRR + PTV + organ outlines enabled: 60 wheel events had a median synchronous handler time of **0.86 ms**, p95 **1.50 ms**. Render-dispatch cycles including an intentional 16 ms delay had a p95 of **46.52 ms**. These are local warm-OS-cache observations, not end-to-end input-to-photon measurements or latency guarantees.
- At 384-pixel DRR resolution, the first sweep of eight positions settled in **294 ms median**, with missing overlays left blank during work. Repeating cached positions settled in **46 ms median**. These measurements include debounce/polling and test settle overhead. Cold projections still take time, but the MLC/timeline no longer waits for them.
- Actual UI review covered selected-field dropdown/CP synchronization, projected PTV plus an independently enabled organ, a noncoplanar beam, main/mini field views, direct MPR navigation, and retained 3D surfaces while organs arrive. The 3D suite passed its 44 base checks and all geometry, focus, lifecycle and GPU sub-suites. The authorized private dual-layer input still imported 182 dual-layer CPs among 184 total; no private data were exported. Only the approved public nonpatient dataset was captured locally.

The background worker prepares recorded CPs and prioritizes nearby 0.1-CP positions. It does not precompute an infinite continuum or guarantee every position is ready immediately. DRRs are bounded to 160 MiB; projected-outline frames and source meshes have separate bounded caches. Evicted or new projections are recomputed asynchronously. The 512-pixel silhouette masks and contour-derived surfaces remain approximate; the beam geometry support gates from 0.2.7 remain in force. No clinical dataset or patient identifier is included in release artifacts.

---

# Version 0.2.7 — DRR, projected ROI silhouettes and responsive interaction

Validated 2026-09-26 on the Windows development workstation. These are engineering checks, not clinical acceptance.

- Analytic beam tests: source-view IEC basis, perspective magnification, registered coordinate transforms, eight supported patient positions, directed control-point interpolation, HU line integrals, cancellation, missing geometry and nonrigid mapping rejection.
- WPF tests: wheel navigation across field boundaries, sub-detent accumulation, continuous crosshair mapping/clamping, current patient identity and identity clearing, concave projected silhouettes with internal mesh edges removed.
- 3D checks: retained scene during additive ROI selection, exact mesh/camera retention between full and MPR layouts, focus cancellation and geometry/opacity regression checks.
- Public nonpatient multimets review: actual CT-derived DRRs, 24 PTV silhouettes plus an independently enabled organ, noncoplanar beam, mini/main field arrangements and additive 3D. Captures are local engineering artifacts, not synthetic anatomy.
- Warm public benchmark, 1200 × 800, hidden tag panel, 20 slice changes: median dispatch **2.24 ms**, complete-frame settle **31.58 ms**. Another 1600 × 900 scripted review measured approximately **124 ms** per structure jump and **258 ms** per settled 384-pixel DRR update. The latter includes debounce and capture-settle overhead; no cold-start or real-time delivery claim.
- Additional authorized private inputs, aggregate-only: two breast plans, two CT associations, 18 beams and all 56 control points accepted by the geometry gate; dual-layer case retained 182 double-layer control points out of 184. No private screenshots or source data were exported.

DRR limitations: scalar CT HU integration, automatic display normalization, 192-pixel playback / 384-pixel settled raster, 512-pixel ROI silhouette masks. ROI surfaces are approximate; incomplete CT coverage stays incomplete. Pitch/roll/eccentric rotations and missing setup/SAD are explicitly unsupported. Field arrangement intersects the jaw envelope (or leaf-position envelope when jaws are absent) with the selected plane; other beams use their first CP and centerlines are projected, not integrated dose or a delivery simulation.

---

# Version 0.2.6 — MLC-first playback, visible 3D ISO and feature tour

Release builds passed without warnings or errors. RT passed 131 assertions, Playback passed stable MLC-first ordering and timeline/source-preservation checks, and ThreeD passed 44 base checks plus all sub-suites. New checks cover projected ISO position during orbit, clipping and visible cyan pixels. SUPPORT/EXTERNAL are excluded from Other and focus; their controls are absent in both layouts. Installer validation passed 32 archive/path/checksum checks and the embedded 32-file payload check.

The presentation uses fresh actual ViewerControl captures from the approved public nonpatient multi-metastasis case. GPT Image artwork is conceptual only. The 106-second HyperFrames film is edited and is not a performance benchmark, delivery simulator or clinical validation. Full capture and browser/render evidence is documented with the presentation.
# Version 0.2.5 — background preparation and shared 3D workspace

Targeted Release builds passed without warnings or errors. The WPF interaction suite passed 161 checks, including the Default button, explicit Gy/% mode, local-only Gy edits, ignored legacy Gy files, independent dose contexts and globally persisted/broadcast percentage presets. Tests use isolated temporary settings and do not change the user's preferences.

The full 3D suite passed its 44 base checks and surface, context, framing, focus, guide, absolute-dose and lifecycle suites. New workspace tests verify that a hidden 3D control prepares before activation, only PTV surfaces start enabled, Organs/Support/External/Other work independently, and switching between full 3D, 2x2 MPR and native images retains the exact same prepared scene and generation. The same control exposes all switches in both layouts; opacity, dose selection and manual camera state are unchanged. Closing cancels pending preloading. An RTPLAN's early-arriving dose does not enable dose surfaces before CT/structures arrive.

Direct3D pixel checks still retained all 8,594 internal target pixels with enclosing skin and a mean draw-order difference of 0.0000. Surface reconstruction is unchanged from 0.2.4. Initial preparation now overlaps ordinary image/RT inspection; it is still cancellable work and may not have finished if 3D is requested immediately. GPU upload occurs when the view becomes visible. No zero-latency startup guarantee is made.

Actual full/MPR UI captures use only the approved nonpatient benchmark. They show the complete shared 3D toolbar and scope-aware Apply/Apply globally plus Default controls. Synthetic geometry is used only for engineering tests. Historical 0.2.4 and earlier measurements below refer to those versions and their defaults.

# Version 0.2.4 — detailed GPU surfaces and dose/tag controls

All seven engineering suites passed with zero build warnings/errors: Core (10 groups), RT (130 assertions), rendering (341 checks plus 45 contour checks), playback, DVH (71), WPF interaction (138), and 3D (44 base checks plus surface detail, absolute-dose, context, focus, camera and guide checks). Windows installer and packaged-runtime verification are recorded below.

The Direct3D 11 renderer uses depth peeling and retains full-detail geometry during orbit, zoom and linked MPR scrolling. Actual GPU pixel tests retained all 8,594 red target pixels inside a 6% skin surface. Reversing draw order produced a mean byte difference of 0.0000. Transparent emissive slice guides and portrait/landscape camera projection also passed. The synthetic fine-cylinder fixture measured 0.255 mm maximum boundary error with 199,992 triangles; this is one engineering fixture, not a general accuracy guarantee.

On the authorized larger local CT/RT dataset, the default 24 selected ROI surfaces plus skin produced 6,539,298 triangles, without a contour fallback. First mesh preparation took 11,554 ms. Twenty hardware Direct3D 11 orbit frames after three warm-up frames at 1000 × 800 measured 9.33 ms median / 19.64 ms p95 including GPU rendering and bitmap readback. Full geometry remained unchanged. This excludes file/volume loading and does not establish Explorer startup time or a guaranteed desktop frame rate. No private anatomy was exported. The initial mesh cost remains material for large scenes.

Actual UI captures of the approved nonpatient benchmark were inspected after the final changes: absolute Gy dose legend, blue controls, ISO jump, full/compact GPU 3D, focused brainstem, nested tag search and copy popup. Capture uses native GPU bitmap readback inside the actual WPF visual tree; no anatomy is generated. Small ROIs use 0.5 mm target spacing; other ROIs start at 1 mm, with explicit axis/triangle bounds and larger-object retries. Maximum smoothing displacement is 0.65 mm, or 0.325 mm at 0.5 mm spacing; total reconstruction error is not bounded by this smoothing limit.

Physical dose contexts generate whole-Gy defaults: a 27.699 Gy maximum produces 2, 4, …, 26 Gy; a 68 Gy maximum produces 5, 10, …, 65 Gy. Manual input supports 1–24 values with up to two decimal places. Tests verify actual 2D contour coordinates and 3D 5.25 Gy surface coordinates, shared physical thresholds across dose grids, matching colors, decimal precision rejection, above-maximum levels and relative/nonphysical unit handling. Colorwash remains percentage-based. Global preference checks cover two open viewers, changed plan/dose, reopened viewer, fresh on-disk settings reader, separate Gy/percentage lists, invalid precision and malformed files. Tests use an isolated temporary preference store and never change user settings.

Tag tests exercise actual WPF selection, duplicate tags, nested search, retained selected ancestors/scroll after clearing search, individually selectable popup fields and copy payloads. Busy clipboard handling is tested without overwriting the user's clipboard. The reader's existing text-prefix cap and binary omission remain unchanged.

The existing offline HTML tour remains version 0.2.2; this release updates the viewer and Windows installer first. Source DICOM files remain read-only. Engineering tests do not establish clinical commissioning or TPS equivalence.

# Version 0.2.3 — interaction and rendering update

All seven engineering suites pass: Core (10 groups), RT (130 assertions), rendering (341 checks plus 45 contour checks), playback, DVH (71), WPF interaction (66), and 3D (44 base checks plus context, framing, focus and slice-guide suites). The native-coronal regression exercises the actual WPF route. Sampling optimizations retain byte-exact reference parity, including zoom and MONOCHROME1.

The linac table terminates at the schematic head for head-first/feet-first and supine/prone contexts. Its permanent collimator dial follows directed control-point interpolation, including the zero-degree crossing, and clears unavailable angles. The dial is labeled source view; its sign follows DICOM beam-limiting-device rotation conventions. Public benchmark MLC screenshots and the complete 90-frame capture were rendered from the actual viewer and visually inspected.

Selecting an ROI name centers its mapped contour representative point and adds an emissive highlight in full and compact 3D. A second selection clears emphasis. A selected nondefault ROI can be admitted without widening the type filter; EXTERNAL stays excluded. Tests cover registered centering, cache preservation, rapid selection, frozen model reuse, timer cleanup and restoration after interaction.

Native image planes that cross a parallel RT contour stack now use the same labeled approximate boundary reconstruction as MPR; contours already coplanar with the displayed image retain their original points. Unsupported stacks explicitly fall back to intersections. This removes stacked-chord artifacts without modifying the source contours.

## Warm public-benchmark measurements

The same approved nonpatient benchmark and machine were used before and after. These are component measurements, not Explorer startup or a guaranteed on-screen frame rate.

| Workload | 0.2.2 baseline | 0.2.3 |
| --- | ---: | ---: |
| CPU native raster, 512 x 512, dose wash | 29–30 ms | 18 ms |
| CPU native raster, wash + isodoses | 47–52 ms | 20–21 ms |
| Native navigation, 1200 x 800, hidden tags, 20 warm frames: dispatch median | 1.33 ms | 1.21 ms |
| Same navigation: prepared-frame completion median | 46.33 ms | 31.16 ms |

Both raster paths retain 1,719 contour segments. Cold first raster was 48 vs 50 ms; no cold-start improvement is claimed. Navigation waits for completed raster tasks and excludes final WPF paint. Hidden tags no longer parse every source slice; opening the panel refreshes the current attributes.

The 1000 x 800 WPF RenderTargetBitmap orbit profile used 20 timed frames after three warm-up frames. Baseline software paint was 415.603 ms median (434.916 ms p95), with 169,716 triangles. Current quality paint was 255.065 ms (266.695 p95); interaction preview was 175.988 ms (187.525 p95), with 27,936 triangles; idle-restored quality was 248.248 ms (262.420 p95), restoring all 169,716 triangles. Camera update alone remained about 0.027 ms. Repeat capture-run medians were 256.812 / 171.832 / 241.355 ms, respectively. Full-quality timings vary despite the preserved geometry; the preview's lower workload is deliberate, not an equal-quality speedup.

During orbit, zoom or linked MPR scrolling, cached coarse meshes are used and an interaction-preview label appears. Enclosing skin/large-organ context is temporarily omitted when interior objects are visible; a skin-only scene retains its context. ROI preview becomes opaque; dose stays transparent and a selected ROI retains its full mesh/highlight. After 180 ms idle the exact cached full scene returns. Public quality, preview, restored and focused-brainstem captures were visually reviewed. No DICOM files were modified.

The existing offline HTML tour remains version 0.2.2; this release updates the tool and Windows installer first.

# 0.2.2 orientation and context update

The current update passed 130 RT assertions, playback checks, 56 WPF interaction checks, and 44 base 3D checks plus context and slice-guide suites. Tests cover referenced patient-setup selection, eight supported recumbent positions, missing/ambiguous metadata, LPS image-badge orientation, no sum offered for one dose, skin-opacity independence, EXTERNAL exclusion and geometry/cache preservation.

The generic human uses DICOM LPS axes and stable left/right colors. The linac uses the beam's uniquely referenced PatientPosition; unknown or unsupported positions do not display an invented body orientation. The anatomical anchor uses an associated image's BodyPartExamined when recognized. Otherwise head for a noncoplanar couch, or chest, is explicitly labeled assumed. These normalized anchors are not anatomical registration, and the glyph is not patient anatomy. A dashed yellow ray replaces the field cone. Context changes reuse the patient model during playback.

CT skin is an optional context surface, initially enabled at 6% opacity, separately adjustable from structures. EXTERNAL is excluded even when all ROI types are enabled; exact BODY/EXTERNAL names are used only for missing-type fallback. Large ORGAN surfaces use a display-only extent heuristic to reduce occlusion. PTV opacity is unchanged. Source contours, dose and image geometry are unchanged. The 0.2.1 benchmarks below are historical; the default 0.2.2 scene additionally includes CT skin and orientation glyphs.

# Version 0.2.1 verification — 2026-09-26

## Scope and engineering checks

Targeted builds completed without compiler warnings or errors. Synthetic fixtures remain part of automated tests; they are not presentation material. These checks do not establish clinical commissioning, machine validity or TPS equivalence.

| Area | Evidence |
| --- | --- |
| Core | 10 synthetic test groups |
| RT | 65 assertions, including independent vendor-suffixed layers, duplicate device occurrences, inherited positions, geometry mismatch and ambiguous-update rejection |
| Playback | Directed rotations, full turns, beam boundaries, independent layer interpolation, unchanged inputs and cardinal IEC schematic directions |
| WPF / RT-only loading | 49 checks, including RTPLAN without image or dose, RTSTRUCT-only 3D scene, dose-plus-structures DVH without plan/CT, no automatic sum, and independent sidebar ROI focus/visibility interactions |
| Rendering | 333 renderer checks plus 39 reformat checks; original native contours retained |
| DVH | 71 checks covering bounded calculation, coverage, cancellation, curve focus and cache reuse |
| 3D | Type-based default selection, geometry, bounded smoothing, mesh caching, input limits and lifecycle checks |

The RT-only scenarios generate minimal synthetic fo-dicom RP/RS/RD fixtures inside a uniquely named temporary directory and remove that directory afterward. They require neither ignored local fixture folders nor Python. Source DICOM files remain read-only. Initial RTPLAN opens MLC; RTSTRUCT and RTDOSE open 3D. An absent CT or dose does not block independent RT capabilities. A dose sum requires an explicit user selection.

## Public nonpatient benchmark

The supplied benchmark contains 194 files: 191 CT instances plus RTSTRUCT, RTPLAN and RTDOSE, with 55 ROIs. Updated measurements were made locally with warm data and bounded preview settings:

| Workload | Result | Measured time |
| --- | --- | ---: |
| Updated DVH calculation | 55 complete curves, full sampled dose coverage, 2,048 dose intervals | 1,983 ms |
| New default 3D selection | 52 PTV/ORGAN ROIs, 119,936 triangles | 615 ms |
| Reuse of cached default 3D scene | Unchanged geometry | 0 ms at millisecond timer resolution |
| Updated all-object 3D workload | 55 ROIs plus dose and CT context, 223,716 triangles | 1,204 ms |
| Earlier all-object 3D workload | 420,340 triangles, including additional object types/context | 1,673 ms |

Default and all-object 3D workloads contain different objects and triangle counts. Their timings are not an equal-work speedup measurement. A reported 0 ms means below timer resolution, not zero computational cost. Calculation/preparation timings exclude QuickLook startup and are not a live display frame rate. DVH remains an approximate contour-slab estimate; increasing histogram resolution and interpolating its display do not establish agreement with a TPS.

The earlier 0.2.0 CT-opening measurement observed initial image preparation at 31 ms, first RT at 290 ms, first plan at 1,448 ms and indexing at 2,199 ms. Those historical warm-cache values are not remeasured 0.2.1 loading results. Image preparation is recorded before redraw, not when pixels become visible.

## MLC acceptance and approved capture

The authorized read-only clinical MLC check parsed one plan, three beams and 184 control points, including 182 dual-layer control points. The observed layer definitions have 28 and 29 leaf pairs with separate supplied boundary arrays. No missing geometry was invented. Enhanced beam-limiting-device sequences remain explicitly unsupported; ambiguous partial duplicate-type updates are rejected.

Separate explicit authorization permits MLC-only presentation capture from this source. A whitelist display model contains generic plan/beam labels, technical layer geometry and angles only. It has no source entry, original free text, patient/study/frame identifiers, source path, image data, tag tree or original isocenter coordinates. Source files are unchanged; no DICOM copies are produced. The resulting still and 90 unique frames were verified at 1600 × 900, with the still and endpoint frames visually inspected. Playback is 15 fps for six seconds of presentation time, not actual delivery timing.

The synchronized linac/couch view is an IEC fixed-frame schematic with an explicitly unregistered patient glyph. It is not a TPS, collision model or machine simulation.

## Geometry and interaction boundaries

Native-plane contours remain original. Reformatted planes draw a labeled approximate boundary between supported parallel contour planes; unsupported stacks fall back to actual plane intersections. Interpolated reformat outlines and bounded 3D smoothing are display approximations, not replacement structures. PTV/ORGAN defaults use DICOM interpreted types without guessing from names; other types, CT context and dose are opt-in.

Ctrl-wheel zoom retains the current viewport. Compact width/level readouts and plan ISO crosses use the displayed scene; off-plane isocenters are dotted and include signed distance. DVH curve focus is separate from checkbox visibility and reuses compatible cached calculation results.

## Presentation and release acceptance

The 0.2.1 presentation uses only approved public nonpatient data and the separately authorized sanitized MLC-only capture. Synthetic fixtures remain confined to engineering tests. Raw DICOM files and private identifiers are not release assets. Captures use actual WPF controls in offscreen windows, not fabricated UI.

Offscreen tests and component builds do not establish Explorer Space-key acceptance, installer-button behavior, or cold-start performance in an installed QuickLook host. Final package hashes, installer payload checks, presentation asset checks and interactive host acceptance must be recorded separately for the final release; earlier 0.2.0 packaging results do not prove 0.2.1 installation.

Final 0.2.1 packaging: clean Release builds, 32 installer checks and embedded payload verification passed. Installed plugin payload hashes were matched to the package manifest, a previous-version backup retained, and the restarted QuickLook process confirmed responsive. This verifies the deployment files/process, not a manual Explorer Space-key interaction.

The linked 2 × 2 MPR view includes a compact fourth 3D pane. Its LPS slice planes intersect the oriented image volume at the current focus. Tests cover oblique volumes, camera fit, unchanged camera and cached surface geometry during focus/source-slice updates, and layout reuse. The guide contains fewer than 150 triangles; it does not rebuild structure meshes while scrolling.

Presentation acceptance: 14 slides and seven six-second videos passed 133 headless-browser assertions at 1600 × 900 and 1280 × 720, including autoplay, navigation, media enlargement, local asset loading and overflow checks. The hero and MPR/3D slides were also visually inspected.

Final 0.2.2 package: clean build, 32 installer checks and standalone embedded-payload verification passed. All 10 installed package files (including manifest) were hash-verified; QuickLook restarted and responded. The previous plugin backup is retained. Public/nonpatient and sanitized MLC captures were visually inspected. Full 3D camera-fit tests additionally cover wide/tall viewports and camera preservation after manual adjustment.

The final 0.2.2 English tour passed 133 browser assertions across 14 slides at 1600 × 900 and 1280 × 720. All seven videos were freshly rendered and verified as 1600 × 900, six seconds and 180 frames. Hero, MPR, skin/3D and sanitized dual-MLC slides were visually inspected.


Final 0.2.3 package: warning-free Release build, 32 installer checks and standalone embedded-payload verification passed. All 10 installed files, including manifest, matched package hashes; a previous-version backup was retained and restarted QuickLook responded. This verifies deployment files and process health, not manual Explorer Space-key acceptance. The controlled full/restored 3D screenshots were also SHA-256 identical.

## 0.2.4 distribution verification

The strict installer passed 32 archive/path/checksum checks and verified its embedded 32-file payload. Actual Direct3D pixel tests also passed using only the staged runtime DLL closure, then using the installed QuickLook host binding configuration and overlapping host DLLs. No .NET facade bundle is required. Live Explorer Space-key interaction is not part of these automated checks.

## 0.2.5 public preload observation

The approved public capture reported prepared geometry before the first 3D activation. Activation plus the capture harness settle delay took 1,041 ms, including first visible GPU setup/upload; this is not a desktop frame-time measurement. Full and compact screenshots were visually inspected. No private anatomy was exported.

The 0.2.5 installer passed all 32 archive/path/checksum checks and verified its embedded 32-file payload. Local installation matched every staged file hash and QuickLook was restarted. Live Explorer Space-key interaction was not re-tested in this run.

## 0.2.10 — 27 September 2026

- Builds completed with zero warnings/errors. Interaction suite: 212 checks, including selected-dose identity, safe filenames, four-decimal exports, curves-only schema, contour retention at unchanged geometry, stale-angle rejection, LRU pressure and isolated PTV budget.
- RT: 133 assertions plus analytic DRR/geometry scenarios. Render: 368 coordinate/render checks and 48 contour-boundary checks. DVH: 82 checks. Directed-angle/full-turn/MLC playback checks passed.
- 3D suite: 44 core assertions plus surface, context, focus, slice guides, preload, camera retention and field-overlay scenarios. Field toggle preserves meshes; paths are reused during camera movement and cleared on context changes.
- The user-approved public benchmark was opened through the actual WPF viewer: CT-derived DRR, 24 PTV outlines, 25 outlines after adding an organ, noncoplanar field, linked MPR, full 3D fields, DVH and image exports. PTVs remained present at the category toggle.
- Local 60-sample MLC interaction test with DRR/PTV/organs enabled: input dispatch median 0.57 ms, p95 3.02 ms; frame loop including a requested 16 ms delay p95 61.44 ms. Complete uncached DRR scrub settled median 293 ms; cached revisit 48 ms. These are local engineering measurements, not universal frame-rate or clinical-performance claims. Pending anatomy remains asynchronous.
- Public benchmark CSV readback: 51,250 rows each; exactly 18 detailed or 4 simple columns, no numeric field with more than four decimal places. Patient and plan labels were verified in the generated filenames. PNG/3D captures were visually inspected.
- All projection and dose calculations remain inspection previews. Angular meterset density is not temporal or measured delivered dose rate. No clinical approval is implied by these tests.

## 0.2.11 — 27 September 2026

- Added WPF integration scenarios for standalone 3D field selection and CP navigation without CT, recorded-point mouse wheel, fractional CP synchronization with MLC, active-marker movement, static-field leaf changes, double-layer data, neutral selection and exact surface retention.
- Interaction suite: 224 checks, including fail-closed miniature behavior for incompatible CP geometry. Existing 3D suite and the all-fields guide/camera/cache tests passed (44 core assertions plus scenario suites).
- Actual WPF public-benchmark capture verified the moving MLC at start and middle CPs, synchronized view switches and linked 2 × 2 controls. Both prepared ROI meshes and arc-track arrays remained the same objects during 60 CP moves.
- Local 60-sample CP dispatch: median 1.72 ms, p95 3.59 ms. This measures input processing in that run, not complete-frame latency or universal performance. Public screenshots were visually inspected; private clinical data were not captured or published.
- The miniature reuses the main MLC renderer and remains a schematic, screen-facing BEV. No physical detector size or delivery time is implied.
