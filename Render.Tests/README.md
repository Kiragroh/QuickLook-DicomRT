# Renderer verification — 0.2.1

Run `dotnet run --project Render.Tests/Render.Tests.csproj -c Release` on Windows with a compatible .NET Framework runtime.

Synthetic checks cover oblique native image axes, pixel-center geometry, anisotropic extent, patient-space bilinear sampling, native out-of-plane rejection, canonical coronal/sagittal orientation, bounded raster dimensions, window/level and MONOCHROME1, registration, dose wash, isodose lines, cancellation and published-frame consistency. Reformat tests cover an interpolated contour-stack boundary and preservation of native geometry. Synthetic data is for tests only, not the public presentation.

`SlicePane` uses the same published physical geometry for pixels, vectors and picking. Background frames carry generation identifiers; only the newest request can publish its frozen bitmap. Ctrl-wheel zoom retains the viewport. Each pane shows compact width/level and zoom values. Gold ISO crosses identify plan isocenters; dotted projected crosses include signed off-plane distances. Renderer code does not open source files or send data.

Contours aligned with the displayed plane are drawn from their original points. For supported parallel contour stacks viewed off-axis, both native images and reformatted views draw an explicitly labeled approximate boundary interpolated between planes instead of showing every interior chord. The native image orientation can differ from the RTSTRUCT orientation. Unsupported inputs fall back to actual plane intersections with an explicit label. This does not create or replace an RTSTRUCT. Native tolerance is 0.49 times loaded volume slice spacing, otherwise 0.01 mm. The separate 3D view uses bounded contour voxelization and display smoothing.

Isodoses default to 20/50/80/95 percent of each dose maximum and support custom levels. Dose wash colors span blue through green to red. Relative dose is not treated as Gy. A separately requested eligible physical dose sum can provide an overlay; no sum is selected automatically.

## Read-only performance mode

`dotnet run --project Render.Tests/Render.Tests.csproj -c Release -- --benchmark <authorized-folder>`

This mode emits aggregate counts/timings only, writes no images and leaves source files unchanged. It chooses MPR-capable CT/MR stacks, favoring MR with resolvable ROI registrations, and includes resolvable ROIs/doses. Times cover CPU raster and contour preparation, excluding DICOM volume loading and WPF composition. Historical local raster measurements are not current end-to-end display frame-rate guarantees. See [VERIFICATION.md](../VERIFICATION.md) for current measurement scope and engineering limits.

The 2026-09-26 public M01 benchmark (194 files; CT 440×440×191; 55 mapped ROIs; one mapped dose) used the same 512×512 middle native slice before and after these renderer changes. The two warmed wash-only passes fell from 29–30 ms to 18 ms; warmed wash plus isodose passes fell from 47–52 ms to 20–21 ms. Both produced 1,719 contour segments. First wash-only invocation was 48 ms before and 50 ms after, including JIT/initialization effects. These are local CPU measurements, not end-to-end scrolling frame rates or clinical validation. No MR stack was available in this public case. Optimizations reuse invariant native sampling axes and marching-cell scratch arrays and avoid unnecessary cell construction; raster resolution and final image quality are unchanged.
