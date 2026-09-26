# Component API contract — 0.2.1

Namespace: `QuickLook.DicomRT`. Target: `net462`, WPF in Viewer only, fo-dicom.Desktop 4.0.8 (`Dicom` namespace). Source data stays local and read-only. Console tests and benchmarks report aggregates rather than private identifiers or paths. Actual metadata is displayed in the local viewer.

## Core

`Core/Core.csproj` has no WPF dependency.

- `Vec3`: immutable coordinates, arithmetic, `Dot`, `Cross`, `Length`, `Normalized`.
- `DicomEntry`: parsed dataset, identifiers, geometry and windowing. Pixel-center geometry is `Origin + AxisX*x*SpacingX + AxisY*y*SpacingY`.
- `ImageStack`: ordered entries and geometry suitability. Incompatible orientation, size, spacing and echo/time groups stay separate; duplicate positions cannot form an MPR volume.
- `DicomCatalog.ReadEntry(path)` reads a local header. `Scan(selectedFile, token, progress, entryFound, phaseProgress, seed)` scans the containing folder only. Optional callbacks run on the scan worker. A short SOP-class/modality pass prioritizes RT/REG independently of filenames; remaining files follow. The seed can reuse the opened entry.
- `PixelPlane.Load(entry, frame=0)` returns rescaled intensities, dimensions, range and photometric inversion. Unsupported color/encoding is rejected explicitly.
- `VolumeData.Load(stack, token, progress)` validates regular geometry and caps intensity payload at 512 MiB. `WorldAt`, `Sample` and `SampleNearest` use physical coordinates; out-of-grid sampling returns NaN. Irregular spacing or in-plane drift is rejected rather than presented as regular MPR.
- `TagReader.Read(dataset)` returns every metadata element, sequence and item as `TagRow` paths. Text display/search is limited to an 8,192-character prefix plus a truncation marker. Binary and pixel contents are omitted. This cap does not remove nested sequence rows.

## RT

`Rt/Rt.csproj` references Core.

- `Matrix4`: affine row-major 4×4 matrix; `Transform`, `Inverse`, `Multiply(a,b)` representing `a*b`.
- `RegistrationReader.Read(catalog)` returns spatial registration links. `Resolve(links, sourceFrame, targetFrame)` returns an unambiguous mapping or null; identity requires matching nonempty frames. Exact referenced image instances may establish a missing frame. Matrices compose within one REG object's coordinate system, not arbitrary cross-object paths. Deformable REG is unsupported. The viewer reparses REG against the final patient-filtered catalog.
- `StructureSet.Load(entry)` retains ROIs, original contour points/types and image/series references. `StructureRoi.Center` is a navigation point on/in an actual component where possible.
- `DoseGrid.Load(entry)` retains `FrameUid`, `Units`, `PlanUid`, `DoseType`, `SummationType`, `ReferencedPlanCount`, label and maximum. `Volume` exists only for regular grids. `Sample(world)` supports irregular/descending offsets, returns NaN outside support and preserves units. Zero-weight NaN neighbors do not erase valid nodes; coordinate roundoff is snapped at nodes.
- `PlanData.Load(entry)` returns conventional beams and inherited control points. `ControlPoint.MlcLayers` holds independent `MlcLayer` objects (`Key`, `Type`, `Boundaries`, `Positions`, `IsY`). Numeric-suffix vendor forms such as `MLCX1` / `MLCX2` are accepted. Duplicate classic device types retain occurrence identity; partial duplicate groups require an unambiguous leaf-pair count. Initial geometry and positions are mandatory for each defined layer. Legacy MLC fields expose the first layer. Enhanced `(3008,00A1/A2)` devices remain explicitly unsupported. Unknown scalars stay NaN; malformed explicit updates do not inherit stale positions.
- `DvhCalculator.Calculate(roi, dose, roiToDose, token)` returns `DvhResult`: status/message, dose units, cumulative dose/volume arrays, estimated/covered volume, coverage and spacing. The denominator includes the entire sampled ROI, including unknown dose regions; partial curves are lower bounds. CLOSED_PLANAR loops use union with internal even-odd keyhole fill; CLOSEDPLANAR_XOR uses XOR. Nonparallel/single-plane geometry and nonrigid mappings are unsupported.
- `DoseSum.Calculate(doses, registrations, token)` returns `DoseSumResult`: derived dose, message, included/excluded counts, ambiguous-plan count, reference/included plans and grid-point coverage. Eligibility requires physical Gy PLAN doses with one referenced plan, unique SOPs and unambiguous plan-dose selection. Known patient mismatches and unsupported mappings are excluded. Sampling uses the unchanged first suitable regular reference grid. Missing contributions remain NaN. No fraction scaling, biological conversion or file export is performed; the derived-grid factory invents no SOP/patient identity.

## Viewer

`Viewer/Viewer.csproj` references Core and RT.

- `ViewerControl.Open(path)` starts asynchronous loading; `LoadCompletion` exposes completion. `Dispose` cancels work and releases view resources. `FirstImageMilliseconds` is recorded before redraw and measures image decoding/preparation, not pixel visibility. First-RT/plan/index timestamps are availability diagnostics.
- `RenderScene` snapshots base-image geometry, focus/windowing, registered fusion, ROI/dose overlays, wash range and isodose levels. Published frames own their geometry and labels; stale jobs cannot replace newer frames.
- `TagNode`/`TagTreeControl` implement the collapsed tree, ancestor-aware search and manual expansion-state restoration.
- `MlcTimeline.Locate` maps global control-point indices to beam-local positions without cross-beam interpolation. `Layers` matches stable layer keys and validates boundaries before independent interpolation. `SourceDirection` and `CouchDirection` return normalized IEC fixed-frame schematic directions; source direction is independent of couch angle. `MlcPlaybackControl.SetPlan` drives the layer selector, aperture, timeline and synchronized mini linac/couch schematic. The patient glyph has no patient registration; no collision or delivery model is implied.
- `DvhControl.SetData(rois, dose, transform)` starts on-demand asynchronous work; `Completion`, `StatusText`, `Cancel` and `Dispose` expose lifecycle state. Limits: 128 structures, 30 seconds per view; each calculator call has a 10-second/600,000-cell cap. Remaining structures are counted.
- `ThreeDControl.SetScene(scene)` prepares physical-coordinate meshes only while visible. Hidden/disposed views cancel pending work. Adaptive allocation caps the scene at 600,000 triangles, 64 ROI inputs and 8 dose inputs. Unsupported/omitted objects are counted. Transparency is approximate.

The internal workspace key `Bild` remains for compatibility; its visible label is `Image`. Plane values are `Native`, `Axial`, `Coronal`, `Sagittal` and `3 planes`.

RT-only loading does not require a matching image or dose. An initial RTPLAN opens MLC; RTSTRUCT/RTDOSE open 3D. `HasImage == false` does not imply RT failure. Matching structures and dose support DVH without a plan. Dose sums require explicit selection.

`DvhCalculator` uses 2,048 dose intervals, with linear display interpolation. `DvhControl.FocusStructure(roi)` toggles emphasis without changing visibility; `FocusedStructure` and `CalculationCount` expose focus and cache verification. Cached results require identical ROI identity, dose identity and transform values; changing dose clears the cache.

`StructureRoi.InterpretedType` retains the DICOM interpreted type. `ThreeDGeometry.DefaultRoi` selects exact `PTV` / `ORGAN` types, without name inference. All ROI types, CT context and dose are opt-in. Unchanged geometry reuses a bounded mesh cache; smoothing displaces ROI vertices by at most 1 mm.

`ReformatContours.Outline` constructs a bounded approximate contour-stack boundary between supported parallel planes. Original native contours remain unchanged; unsupported stacks fall back to actual plane intersections. `SlicePane` uses published geometry for raster, overlays and picking, supports Ctrl-wheel zoom, shows compact width/level information, and draws plan isocenter projections with signed off-plane distances.

## Host and tests

`Plugin` implements QuickLook's `IViewer`; `Harness` embeds the same control for offscreen verification. `QuickLookDir` selects the host assembly location, defaulting to `%LOCALAPPDATA%\Programs\QuickLook`; the host assembly is not redistributed.

Assertion projects cover Core, RT, rendering, playback, DVH/sums, 3D and WPF interaction. Use synthetic cases for reproducible changes. Authorized-folder modes are read-only and output aggregates. See [VERIFICATION.md](VERIFICATION.md) for acceptance boundaries.
