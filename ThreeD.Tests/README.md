# Optional 3D overview

Public entry point: `ThreeDControl.SetScene(RenderScene)`; dispose with `Dispose()`. The control must be visible to build meshes. Window/level, slice focus and zoom changes do not rebuild unchanged geometry. Switching away cancels pending work. Every mesh uses the image frame's physical LPS coordinates.

Controls: CT-derived bone threshold (300 HU), CT-derived skin threshold (-350 HU), selected ROI surfaces, dose isosurface, opacity, relative dose level, mouse orbit/wheel zoom, reset. CT threshold controls are disabled for MR backgrounds. Supply a CT-backed scene with registered overlays to display CT surfaces and MR-associated structures together.

CT skin is enabled initially in both full and compact views at 6% opacity. Its independent slider covers 1–25%; changing either opacity slider reuses the prepared meshes. PTV and ORGAN remain the initial ROI types. “All ROI types” never includes EXTERNAL, or exact BODY/EXTERNAL names when the interpreted type is absent. Other names do not affect selection. Large ORGAN surfaces use 18% of the selected ROI opacity when their patient-coordinate bounding box encloses a PTV with at least eight times its box volume and 1.5 times every extent, or occupies at least 15% of the CT envelope box. This display heuristic changes neither contours nor PTV opacity. The small patient orientation badge follows the orbit camera in LPS coordinates.

## Geometry and limitations

- CT threshold surfaces use marching tetrahedra on a grid bounded to56 samples per axis. These are derived threshold surfaces, not anatomical segmentations; table material/noise can also satisfy a threshold.
- ROI surfaces use actual parallel closed contours in their own plane basis, mapped through `RoiToImage`. Scanline voxelization preserves even-odd holes within a contour and XOR across `CLOSEDPLANAR_XOR` loops. Ordinary closed loops are united. Adjacent contour planes use nearest-plane slabs; missing planes beyond the regular contour interval are not bridged. The adaptive8–24-sample overview grid is deliberately coarse; thin details can disappear. This is a contour-derived approximation, not a convex hull or clinical volume reconstruction.
- Open, nonparallel, mixed or single-plane contours use actual contour-line geometry instead of invented surfaces. Fallback and unavailable-object counts appear in the control.
- Regular dose grids are meshed in dose-frame physical bounds and transformed back through `ImageToDose.Inverse()`. Irregular grids are sampled in the image bounds through `ImageToDose`. Levels20/50/80/95% refer to each grid's own maximum. No dose summation or interpretation of relative units as Gy occurs.
- WPF sorts transparent objects but cannot guarantee perfect triangle ordering for self-intersecting transparent surfaces; the UI labels transparency as approximate.
- Input caps:2,048 contours,400,000 contour points and512 contour planes per voxelized ROI. Geometry caps:120,000 triangles per mesh;80 meshes and600,000 triangles total. Up to64 ROIs and8 dose grids are considered. Exceeded budgets are reported as unavailable objects; partial meshes are not published.

## Verification

`dotnet run --project ThreeD.Tests/ThreeD.Tests.csproj -c Release`

42 checks cover physical threshold interpolation, outward normals, registered ROI placement, XOR-hole retention, explicit unsupported geometry, input limits, cancellation and real WPF background-build/composition using synthetic CT only. No clinical screenshots are produced.

Additional synthetic context tests verify skin defaults in both views, independent skin/ROI transparency, unchanged PTV opacity, the large-organ extent heuristic, EXTERNAL exclusion even with all types selected, and cached skin/ROI geometry reuse. Compact slice-guide checks verify that focus movement preserves the prepared scene, camera orbit and zoom.

`dotnet run --project ThreeD.Tests/ThreeD.Tests.csproj -c Release -- --benchmark <authorized-folder>`

The benchmark reads only and emits aggregate counts/times. On the explicitly authorized local dataset, bone+skin+all23ROIs+one dose surface produced238,228 triangles, with one honest contour-line fallback and no skipped ROI. Geometry preparation took about1.2 seconds including RT reads; CT surfaces took20–24ms each, dose13ms, and the slowest ROI21ms. This does not include initial image-volume loading or guarantee latency on other hardware/data.

`dotnet run --project ThreeD.Tests/ThreeD.Tests.csproj -c Release -- --scene-budget <authorized-folder>`

This scene-level regression verifies that available ROI and dose objects survive the global allocation. CT context and dose have reserved shares; each ROI gets a fair share and progressively reduced sampling resolution. The authorized nonpatient public benchmark retained all55 ROIs, one dose and both CT context surfaces:420,340 triangles, zero skips,822ms mesh preparation (excluding file loading). This is a bounded overview, with adaptive detail explicitly labelled.
