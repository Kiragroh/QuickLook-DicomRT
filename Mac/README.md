# QuickLookRT for macOS (development)

Native AppKit application and Finder Quick Look preview extension, using the original Windows DICOM/RT algorithms with a local WKWebView interface. No network service is involved. The application uses the original QuickLookRT icon and viewer colors/layout.

## Build and install

Requires macOS 13+, Apple Command Line Tools and .NET SDK 10. Set `DOTNET` if the SDK is not on the default project-local tool path. Run `Mac/scripts/build.sh`, then `Mac/scripts/install.sh`. Builds are self-contained; the installed application does not need a separate .NET installation. `ARCH=arm64` and `ARCH=x86_64` are supported build targets. A Developer ID identity can be supplied with `SIGN_IDENTITY`; the default local build is ad-hoc signed.

The application is installed in `~/Applications/QuickLookRT.app`. The installer backs up the previous application and registers the bundled Quick Look extension. Close an existing preview before reopening it with Space. A sandboxed Finder preview can request access to the containing folder to discover related images, plans, structures and doses; folder authorization is retained in a security-scoped bookmark.

## Current scope and validation

The development port includes linked image/MPR views, slice scrolling, plan selection, structures and contour-description labels, DVH calculation, MLC control-point playback, DRRs, 3D surfaces, dose/fusion controls, preferences and native export dialogs. Original core and RT regression tests can be run through `Mac/Tests/Core` and `Mac/Tests/Rt`. `Mac/Tests/test_worker.py` checks the JSON worker against synthetic DICOM fixtures. `Mac/scripts/smoke.sh` checks the embedded native/WebKit rendering path. `Mac/Tests/PrivateAcceptance.swift` is a local UI acceptance harness for independently supplied examples and emits aggregate results only.

Full Windows feature parity and Finder integration across all dataset arrangements are still under verification. This branch is a development preview, not a completed parity release. Multiple simultaneous dose overlays, full preparation/loading behavior and some detailed Windows interaction controls still need comparison. Private DICOM files and test artifacts must remain outside tracked source.

Local native WebKit acceptance on three original datasets passed 36 checks per dataset, including the selected plan label, continuous scrolling, configurable dose presets/line width, automatic target/organ DVHs, MLC bank-tip and jaw pixels, machine labels, CT-derived DRR pixels, structure-contour pixels, control-point navigation, export reference resolution and WebGL surfaces. The latest run used the preview sandbox and its read-only library allowance. These tests exercise the shared native viewer; they do not independently certify all Finder sandbox configurations or full Windows parity. Placeholder plan labels such as `None` receive an explicit plan/field-count display label while the original DICOM label is retained. MLC geometry renders before expensive projections, and the bank tips remain visible for closed apertures. Image modes have text labels and plane illustrations.

The bundled `Web/graphics.js` is built from `Graphics/renderer.js` with `npm ci --prefix Mac` and `npm run build --prefix Mac`. Its Three.js license is included in `Web/THREE-LICENSE.txt`.

## Dose display and preparation

The Standard display preset includes 95% dark green, 100% light green, 105% yellow, 107% red and 110%/120% magenta shades, preceded by brown/blue/green lower levels. STX uses ten-percent steps ending at 100%, with dark green at 70%, light green at 80%, red at 90% and magenta at 100%. Both palettes and their levels can be edited in Settings; isodose line width is configurable. Absolute Gy labels use at most one decimal while internal threshold calculations keep their precision. Switching percent/Gy preserves the reference and colors.

For Standard, an unambiguous positive TARGET prescription from DICOM Target Prescription Dose (300A,0026) supplies the 100% reference, as defined in the [DICOM RT Prescription Module](https://dicom.nema.org/medical/Dicom/2017e/output/chtml/part03/sect_C.8.8.10.html). Otherwise D0.035 cm³ in the supported body contour defines 110%. In STX, D0.035 cm³ defines 100%, irrespective of the prescription. The calculation uses native dose-grid centre values, weighted by voxel volumes and actual frame offsets; this is a grid-based approximation rather than a TPS interpolation. The UI identifies the reference and permits an explicit manual 100%-Gy value. Unsupported/missing body contours and insufficient known volume are reported instead of silently substituting the point maximum. Hotter samples retain the palette's final color.

Targets and organs have their DVHs calculated automatically in the background. Each finished curve appears immediately; unsupported or partial results retain their status. The preparation strip reports actual catalog/image counts and background work (DVH, surfaces, volumes, contours and DRRs). Wheel navigation updates during the gesture, using an interaction raster and then restoring full resolution; in 2×2, the actively scrolled plane is prioritized. Coplanar contour geometry is indexed once per context.

The MLC view draws contrasting leaf edges and actual X/Y jaw positions at the selected/interpolated control point. Jaw edge overlays can be toggled. The machine label comes from DICOM metadata; unknown identity remains unknown. The decorative ring is removed.

The development preview has read-only sandbox access to the dedicated `~/DICOM` library so files there retain sibling plan context directly from Finder. Other folders use a contextual local folder picker; the permanent native permission bar is removed. Stale bookmarks are renewed when usable. The standalone app rescans the original file's containing folder, and both app and extension share the viewer-settings domain. Private acceptance tests use their own settings domain and do not alter viewer preferences.
