# Version 0.2.0 verification — 2026-09-26

## Automated checks

Release builds and synthetic verification passed. The English UI build completed with zero compiler warnings and errors.

| Area | Evidence |
| --- | --- |
| Core | 10 synthetic groups: geometry, intensity decoding, catalog behavior and metadata |
| RT | 53 assertions: dose geometry, registration, structures and plan interpretation |
| Renderer | 333 coordinate, fusion, dose and frame-swap checks |
| Playback | Directed rotations and whole-plan beam boundaries |
| DVH / plan sum | 48 checks: union/XOR/keyholes, coverage, limits, sum eligibility, duplicates, rigid mapping, NaN boundaries and cancellation |
| 3D | 42 geometry/composition checks, including bounded input and lifecycle; popup lifecycle also checked |
| WPF interaction | 16 checks: slider binding, contrast, tag-tree search/expansion, global timeline and sum-only selection |

An RTDOSE-opening test confirmed selection of its explicitly referenced plan rather than the first discovered plan. Final catalog REG inference includes exact referenced images. Source files stayed read-only. These are engineering checks, not clinical commissioning or proof of TPS equivalence.

## Public nonpatient benchmark

The supplied public benchmark contained 194 files: 191 CT instances plus RTSTRUCT, RTPLAN and RTDOSE, with 55 ROIs.

- **DVH:** all 55 ROIs produced complete curves, each with 100% sampled dose coverage. No unsupported, empty or budget-limited results occurred. Scan, loading and all calculations together took 27.259 seconds; maximum adaptive spacing was 3.11 mm. UI limits remain 128 ROIs and 30 seconds per view, with unprocessed ROIs reported.
- **3D:** the full scene check retained all 55 ROIs, one dose surface and both CT context surfaces. Preparation used about 420,000 triangles and 822 ms. This timer excludes catalog and volume loading. Adaptive detail and the 600,000-triangle cap remain active.
- Demo captures may show selected ROI subsets to make overlapping surfaces legible. The UI reports the prepared selected ROI-surface count.

The final CT performance capture used a fresh viewer after prior dataset access, so OS caches were warm:

| Event | CT opening | RTPLAN opening |
| --- | ---: | ---: |
| First image decoded/prepared | 31 ms | 2,391 ms |
| First RT available | 290 ms | 1,039 ms |
| First plan available | 1,448 ms | 1,644 ms |
| Directory index complete | 2,199 ms | 2,371 ms |

**The image metric is recorded before redraw. It measures decoding/preparation, not pixels appearing on screen.** All values exclude QuickLook startup. They are observed timings for this machine and cache state, not general latency guarantees. RT-first loading changes discovery order while still scanning the complete direct folder.

The benchmark data is not bundled with the source or installer. Public demonstration assets remain separate from private local acceptance material.

## Other local observations

A separate authorized read-only local case gave warm observations of 280 ms for initial image preparation, 2,187 ms for first RT, 2,278 ms for first plan and 5,409 ms for indexing. Its DVH run produced 22 curves from 23 ROIs: 21 with full coverage, one partial and one unsupported, in about 5.1 seconds including scan/load/calculation. These are aggregate measurements only; no identifiers or images from that case are released.

Adaptive sampling can be coarse for large ROIs and is displayed. A synthetic 512×512 image/dose/isodose raster took about 41 ms of CPU preparation; this is not an end-to-end display frame-rate claim.

## Acceptance boundaries

Offscreen WPF tests and public demonstration rendering do not establish interactive Explorer Space-key acceptance in an installed QuickLook host. End-to-end interactive host behavior remains a separate acceptance step. Installer publication and installation must be checked independently of component builds.

No private DICOMs, private identifiers or patient screenshots are packaged. Dose sums use stored physical values without a treatment judgment. DVH slab sampling, bounded 3D surfaces and MLC playback remain engineering previews, not clinical or machine validation.

## Release packaging checks

Installer archive/path/host tests: 32 passed. The standalone executable passed `--verify-payload` with all 10 embedded files verified. A separate review checked host location handling, exact current-session process matching, backup/rollback and archive allowlisting. No installer-button installation test was performed; the final plugin was independently copied to the local normal QuickLook installation, all 10 installed-file hashes matched, and the restarted host was responsive.

The English HTML tour has 14 slides, 20 real viewer screenshots and four locally rendered HyperFrames clips (1600 × 900, 30 fps, seven seconds each). Browser checks at 1600 × 900 and 1280 × 720 found no slide overflow, missing images or script errors; video decoding, navigation, image zoom and tag-view switching passed.
