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
