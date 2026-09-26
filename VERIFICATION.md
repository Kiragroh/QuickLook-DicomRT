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
