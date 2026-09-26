# Renderer verification — 0.2.1

Run `dotnet run --project Render.Tests/Render.Tests.csproj -c Release` on Windows with a compatible .NET Framework runtime.

Synthetic checks cover oblique native image axes, pixel-center geometry, anisotropic extent, patient-space bilinear sampling, native out-of-plane rejection, canonical coronal/sagittal orientation, bounded raster dimensions, window/level and MONOCHROME1, registration, dose wash, isodose lines, cancellation and published-frame consistency. Reformat tests cover an interpolated contour-stack boundary and preservation of native geometry. Synthetic data is for tests only, not the public presentation.

`SlicePane` uses the same published physical geometry for pixels, vectors and picking. Background frames carry generation identifiers; only the newest request can publish its frozen bitmap. Ctrl-wheel zoom retains the viewport. Each pane shows compact width/level and zoom values. Gold ISO crosses identify plan isocenters; dotted projected crosses include signed off-plane distances. Renderer code does not open source files or send data.

Native contours are drawn from their original points. For supported parallel contour stacks, reformatted views draw an explicitly labeled approximate boundary interpolated between planes instead of showing every interior chord. Unsupported inputs fall back to actual plane intersections. This does not create or replace an RTSTRUCT. Native tolerance is 0.49 times loaded volume slice spacing, otherwise 0.01 mm. The separate 3D view uses bounded contour voxelization and display smoothing.

Isodoses default to 20/50/80/95 percent of each dose maximum and support custom levels. Dose wash colors span blue through green to red. Relative dose is not treated as Gy. A separately requested eligible physical dose sum can provide an overlay; no sum is selected automatically.

## Read-only performance mode

`dotnet run --project Render.Tests/Render.Tests.csproj -c Release -- --benchmark <authorized-folder>`

This mode emits aggregate counts/timings only, writes no images and leaves source files unchanged. It chooses MPR-capable CT/MR stacks, favoring MR with resolvable ROI registrations, and includes resolvable ROIs/doses. Times cover CPU raster and contour preparation, excluding DICOM volume loading and WPF composition. Historical local raster measurements are not current end-to-end display frame-rate guarantees. See [VERIFICATION.md](../VERIFICATION.md) for current measurement scope and engineering limits.
