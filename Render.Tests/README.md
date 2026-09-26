# Renderer verification

`dotnet run --project Render.Tests/Render.Tests.csproj -c Release` (Windows, .NET Framework 4.6.2 runtime)

Synthetic data only: oblique native image axes and pixel-center geometry; anisotropic pixel extent; patient-space bilinear sampling; native out-of-plane rejection; canonical coronal/sagittal orientation; 512-pixel raster bound; window/level and MONOCHROME1; no projection of distant ROI contours; true orthogonal contour-plane intersection; cancellation; local RTDOSE gradient, dose wash, isodose lines and registration transform.

`SlicePane` draws to a physical-aspect rectangle; the render geometry used for pixels is also used for vectors and picking. Background frames carry generation identifiers, and frozen bitmaps are published only for the newest request. No renderer code opens files or sends data.

Limits: RT contours are outlined at their actual plane or intersected as closed planar loops. The renderer does not reconstruct a volumetric structure surface between contours. Native-plane tolerance is 0.49 times loaded volume slice spacing, otherwise a conservative 0.01 mm. Isodoses show 20/50/80/95 percent of each dose grid maximum; dose grids are not summed. Relative units are not interpreted as Gy. Dose wash colors span blue through green to red. These synthetic checks are engineering verification, not clinical commissioning.

## Read-only performance mode

`dotnet run --project Render.Tests/Render.Tests.csproj -c Release -- --benchmark <authorized-folder>`

The benchmark emits aggregate counts and timings only; it does not write images or modify source files. It chooses MPR-capable CT/MR stacks, favoring the MR stack with resolvable ROI registrations, and includes every resolvable ROI and dose grid. Times cover CPU raster generation plus contour preparation, excluding DICOM volume loading and WPF composition.

Measured on the current Windows host with the explicitly authorized local dataset (1,073 instances, 23 ROIs, one dose grid): at 512 x 512 pixels, CT native and registered MR axial/coronal preparation took 35–62 ms without isodose curves and 52–75 ms with four isodose levels. All 23 ROIs and the dose grid were included. These are local engineering measurements, not a general latency guarantee. Native zoom above 1 centers on the selected focus projected onto the image plane.
