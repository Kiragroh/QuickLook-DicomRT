# QuickLookRT for macOS (development)

Native AppKit application and Finder Quick Look preview extension, using the original Windows DICOM/RT algorithms with a local WKWebView interface. No network service is involved. The application uses the original QuickLookRT icon and viewer colors/layout.

## Build and install

Requires macOS 13+, Apple Command Line Tools and .NET SDK 10. Set `DOTNET` if the SDK is not on the default project-local tool path. Run `Mac/scripts/build.sh`, then `Mac/scripts/install.sh`. Builds are self-contained; the installed application does not need a separate .NET installation. `ARCH=arm64` and `ARCH=x86_64` are supported build targets. A Developer ID identity can be supplied with `SIGN_IDENTITY`; the default local build is ad-hoc signed.

The application is installed in `~/Applications/QuickLookRT.app`. The installer backs up the previous application and registers the bundled Quick Look extension. Close an existing preview before reopening it with Space. A sandboxed Finder preview can request access to the containing folder to discover related images, plans, structures and doses; folder authorization is retained in a security-scoped bookmark.

## Build 5: completed comparison items

Version 0.2.45 / bundle 2026110305 implements the remaining gaps identified in the Windows source comparison. The [reviewed plan](../docs/macos-parity-plan.md) and [independent source/readiness review](../docs/goldfish_review_report.md) describe the contracts. [Reusable icons and design improvements for the Windows agent](../docs/windows-design-improvements.md) are linked from the repository README.

| Original workflow | macOS implementation |
| --- | --- |
| One selected file, then its related plan | Staged initial native image/RT slice/MLC aperture before folder discovery; completed context preserves user navigation |
| Plans and individual RT objects | Explicit plan/structure/dose choices; exact opened-dose preservation; deterministic standalone associations; ambiguous contexts remain choices |
| Multiple associated doses | Independent registered overlays and per-dose reference state; separate DVH dose choice; registered plan sums |
| Image and RT-only views | Native, axial, coronal, sagittal, linked 2×2 and 3D; wheel/crosshair/zoom/window controls; all source isocenters and dose maxima |
| Structures and descriptions | Original label modes, search, categories, x-prefix filtering, visibility, focus and volumes |
| DVHs | Automatic Targets/Organs; progressive curves, numerical metrics and coverage; independent per-dose caches; manual additional structures |
| Shared field navigation | Image/MLC/3D field controls, neutral selection, whole-plan playback, restart and field-end marks; guide motion does not re-rasterize anatomy |
| MLC and DRR | Recorded and interpolated CPs/layers/jaws; source machine label; exact-geometry overlays; progressive recorded-CP or prepared fractional playback; contours-first and DRR on-demand preferences; current-frame refinement and tone controls |
| Orientation | Selectable LPS figures in image views; movable/resizable axial field arrangement plus LINAC in MLC; reusable scene geometry |
| Tags | Current image, opened RT object, catalog RT/REG source picker; hierarchy, search, element detail and copy |
| Exports | Whole-view/individual PNG; separate full DVH chart PNG with equal-opacity curves/metrics/status; detailed CSV including NotCalculated; curve-only CSV; reference-resolved original DICOM ZIP |
| Native shell | Original app/Dock icon; Settings/Open/Close/Quit/search/export menus; asynchronous open/save sheets; shared viewer preferences |

This remains a development inspection tool. The source comparison and tests cover the workflows above; they do not certify every vendor export, every OS/sandbox configuration or clinical equivalence. Missing/unsupported registration, patient position, SAD and source device coordinates remain unavailable rather than inferred. Existing core gates for unsupported intensity transforms and geometry remain in force. Multiframe MPR remains unsupported by the shared engine. Arbitrary-folder Finder access still requires a local OS folder grant; the dedicated local library has read-only access. Opening the same file in the standalone app rebuilds its complete folder context; in-memory manual view/selection state is not transferred between processes.

## Validation

Tests use original Core/Rt code, generated selection arrangements and three original local datasets. No private data, source names, UIDs or screenshots are tracked.

- Original Core synthetic suite; 194 RT assertions plus analytic DRR checks across eight patient positions; 8 display-math checks; 24 palette/reference checks.
- Worker integration: 130 checks. Additional parity contracts: 123 checks, including numerically distinct same-plan DVHs/references/meshes, mixed units, exact-dose discovery, negative associations, RT-only views, sum transitions and uncomputed CSV rows.
- Original-case native acceptance: 36 checks per case, covering original image/RT context, automatic DVHs, dose presets, descriptions, visible leaves/jaws/DRR/contours, export associations and WebGL.
- Additional native parity acceptance: 30 passed and 3 explicitly skipped per original case. Those sources each have one plan, one associated dose and one isocenter; generated native multi-plan/multi-dose data cover those branches, with 40 passed and zero skipped, including prepared fractional DRR playback, unit-switch color/level preservation and 3D isodose rebuilding after reference edits. Tests also cover source metadata selection, dedicated DVH PNG, field-only updates, orientation move/resize, cancelled-work recovery, bounded geometry reuse and staged first content.
- Original ZIP exports contained 173/155/184 reference-resolved files; every archived file matched original bytes. Detailed CSV retained requested structures and correct calculated metrics/status. All 512 source files matched their pre-export hashes. Temporary exports were removed after verification.
- The original-case harness is signed with the preview sandbox/read-only library entitlement. Generated native fixtures outside that library run in the containing-app configuration. These are shared-view tests. The installed build was also opened from Finder Space on an original CT: the full 43-structure/one-dose/one-plan context appeared, and Open With QuickLookRT retained that complete context in the standalone app. Its DVH populated 38 Complete and four PartialCoverage results automatically. The old permanent folder-access button was absent; a dedicated DVH PNG was saved through the native asynchronous sheet.

Reproduce generated tests with `Mac/Tests/test_worker.py`, `Mac/Tests/test_parity.py`, and `Mac/scripts/smoke.sh`. `Mac/scripts/private-acceptance.sh <untracked-index.json> [PrivateAcceptance|ParityAcceptance]` builds a native harness and emits aggregates only. The index stays local. A generated fixture can be written with `test_parity.py <worker> --export-fixture <ignored-output-folder>`.

## Measured performance

Alternating requests to the previous installed build and this build on the same 10-core arm64 machine (32 GiB), with other local workloads present. Each raster size used five warm-up pairs and twenty measured pairs; timings include private-pipe IPC and JSON decode. These are aggregate local measurements, not a hardware-independent speed promise.

| Original case | Full 512px axial median, old → new | Interactive 256px p95, old → new | Unchanged context selection median, old → new | Fast aperture median, old → new |
| --- | --- | --- | --- | --- |
| 1 | 40.7 → 35.6 ms | 47.5 → 32.3 ms | 104.7 → 5.0 ms | 13.15 → 0.24 ms |
| 2 | 46.9 → 37.8 ms | 59.8 → 46.8 ms | 125.1 → 4.2 ms | 5.33 → 0.27 ms |
| 3 | 54.6 → 45.0 ms | 47.2 → 40.8 ms | 135.7 → 7.6 ms | 2.36 → 0.26 ms |

All paired raster samples matched the old pixels, source-contour coordinates and isodose segments exactly with identical render options. New display palettes intentionally follow the separate user-confirmed settings. Interactive medians were not uniformly faster; the p95 and real gesture behavior are the stronger evidence. Final native gesture checks published 11, 17 and 12 frames during the 440 ms wheel sequences and settled at full resolution in 220, 167 and 193 ms respectively; the generated native case published 18 frames and settled in 168 ms.

Changes include source-stack and native-plane reuse, cached beam extents, geometry-based DRR/silhouette keys, conservative isodose-row rejection, parallel full-resolution raster rows, optimized Mac-worker compilation from first execution, frame-coalesced 3D rendering, retained LINAC/character geometry and reused slice textures. Serialized frontend meshes are bounded to 256 MiB/96 entries, projections to 64 MiB/24,000 entries and the GPU scene to eight million triangles; hidden geometry can be evicted before rejecting a new visible mesh. A cancelled shared lazy ROI mesh is removed/retried, so it cannot poison later 3D requests.

The bundled `Web/graphics.js` is generated from `Graphics/renderer.js` with `npm ci --prefix Mac` and `npm run build --prefix Mac`. Its Three.js license is included in `Web/THREE-LICENSE.txt`.

## Dose display and preparation

The Standard display preset includes 95% dark green, 100% light green, 105% yellow, 107% red and 110%/120% magenta shades, preceded by brown/blue/green lower levels. STX uses ten-percent steps ending at 100%, with dark green at 70%, light green at 80%, red at 90% and magenta at 100%. Both palettes and their levels can be edited in Settings; isodose line width is configurable. Absolute Gy labels use at most one decimal while internal threshold calculations keep their precision. Switching percent/Gy preserves the reference and colors.

For Standard, an unambiguous positive TARGET prescription from DICOM Target Prescription Dose (300A,0026) supplies the 100% reference, as defined in the [DICOM RT Prescription Module](https://dicom.nema.org/medical/Dicom/2017e/output/chtml/part03/sect_C.8.8.10.html). Otherwise D0.035 cm³ in the supported body contour defines 110%. In STX, D0.035 cm³ defines 100%, irrespective of the prescription. The calculation uses native dose-grid centre values, weighted by voxel volumes and actual frame offsets; this is a grid-based approximation rather than a TPS interpolation. The UI identifies the reference and permits an explicit manual 100%-Gy value. Unsupported/missing body contours and insufficient known volume are reported instead of silently substituting the point maximum. Hotter samples retain the palette's final color.

Targets and organs have their DVHs calculated automatically in the background. Each finished curve appears immediately; unsupported or partial results retain their status. The preparation strip reports actual catalog/image counts and background work (DVH, surfaces, volumes, contours and DRRs). Wheel navigation updates during the gesture, using an interaction raster and then restoring full resolution; in 2×2, the actively scrolled plane is prioritized. Coplanar contour geometry is indexed once per context.

The MLC view draws contrasting leaf edges and actual X/Y jaw positions at the selected/interpolated control point. Jaw edge overlays can be toggled. The machine label comes from DICOM metadata; unknown identity remains unknown. The decorative ring is removed.

The development preview has read-only sandbox access to the dedicated `~/DICOM` library so files there retain sibling plan context directly from Finder. Other folders use a contextual local folder picker; the permanent native permission bar is removed. Stale bookmarks are renewed when usable. The standalone app rescans the original file's containing folder, and both app and extension share the viewer-settings domain. Private acceptance tests use their own settings domain and do not alter viewer preferences.
