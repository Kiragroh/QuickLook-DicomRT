# 3D engineering checks — 0.2.5

Run `dotnet run --project ThreeD.Tests -c Release` for physical-coordinate surface, signed-distance contour interpolation, holes/gaps, registration, normals, bounded smoothing, cancellation, cache, focus, context, framing, slice-guide and WPF lifecycle checks. Absolute dose tests check actual 5.25 Gy surface coordinates, matching selector values and rejection of relative grids in Gy mode.

Run `dotnet run --project ThreeD.Tests -c Release -- --direct3d-tests` for actual GPU pixel tests. An enclosing 6% skin surface must retain the internal target; reversing draw order must preserve pixels. Tests also cover transparent emissive slice guides and horizontal/vertical camera-FOV conversion on portrait/landscape viewports.

Full geometry stays in GPU buffers during orbit and MPR scrolling. There is no coarse interaction mesh substitution. ROI fields target 0.5 mm for small objects and 1 mm otherwise, subject to axis (256) and object (1 million triangles) bounds. Failed detail attempts retry explicitly at larger spacing; unsupported stacks use a labeled fallback where possible. Total scene cap is 8 million triangles. Smoothing displacement is capped at 0.65 mm and does not describe the total reconstruction error.

`--gpu-scene <authorized-folder>` loads read-only and emits only aggregate preparation time, triangle counts, hardware feature level and render-plus-readback median/p95. Twenty camera frames follow three warm-up frames at 1000 × 800. This excludes file loading and is not an Explorer startup measurement or a desktop frame-rate guarantee. No images are exported.

`--approved-public-gpu <nonpatient-folder> <output>` also saves actual GPU renders. Use only with an explicitly approved nonpatient source. Automated synthetic fixtures are engineering tests, not presentation imagery.

Older `--frame-benchmark`, `--benchmark` and `--scene-budget` entry points remain available for component investigation. Historical coarse-WPF timings are recorded in VERIFICATION.md; they do not describe the current GPU renderer.
PreloadWorkspaceTests checks hidden preparation before activation, PTV-only defaults, separate Organs/Support/External/Other switches, exact prepared-object reuse across full/2x2/native switches, all controls available in both layouts, retained opacity/dose/camera settings and cancellation on close. Incremental plan-dose discovery must not prematurely enable a 3D dose surface.
