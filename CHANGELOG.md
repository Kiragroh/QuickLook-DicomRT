# Changelog

Feature highlights for DICOM RT for QuickLook. [Download the latest Windows installer](https://github.com/Kiragroh/QuickLook-DicomRT/releases/latest) · [Project home](https://github.com/Kiragroh/QuickLook-DicomRT)

## 0.2.15 — 27 September 2026

### Start at the plan isocenter

- When the first compatible dose, plan and image stack are ready, the image position automatically moves to the **mapped plan isocenter**. Existing frame and rigid-registration mapping is respected.
- The correct native source slice is selected even when opening directly into MLC or 3D. With multiple isocenters, the active field is preferred when available.
- This initial positioning runs once. Scrolling, selecting a slice or image series, dragging the crosshair or zooming during loading keeps your chosen navigation state.

### Whole-viewer and individual-view PNG exports

- The top **PNG** button and **Ctrl+Shift+S** save the whole viewer, including open RT and DICOM Tags sidebars, toolbars, footer and GPU-rendered 3D content.
- Right-click Native, Axial, Coronal, Sagittal, 3D, DVH or MLC to save **only that view**, including individual 2 × 2 tiles.
- Export filenames retain patient ID and relevant plan ID. Tooltips and help explain the two export scopes.

## 0.2.14 — 27 September 2026

### Larger, movable orientation panel

- The MLC view combines the **isocenter slice and a larger LINAC model** in one orientation panel.
- Drag the panel header to move it. Resize with the bottom-right grip or **Ctrl + mouse wheel** over the panel; the reset button restores its default size and position. Both views scale together without rebuilding the source slice.
- The panel stays inside the available view area. The Fields toggle controls its slice arrangement; the LINAC orientation remains available.

### Shared looping playback in 3D and MLC

- A **Play / Pause button beside the 3D CP slider** controls the same plan timeline as the MLC view. It runs from the current position through the fields and loops back at the plan end.
- Playing, pausing, active field and fractional CP position remain synchronized when switching between MLC, standalone 3D and linked 2 × 2. Speed follows the MLC CP/s setting; it is preview speed, not delivery timing.
- The **collimator angle** appears beside the 3D CP slider and next to the moving aperture guide.
- Automatic field fitting no longer marks the camera as manually adjusted. When CT arrives after an early switch to MLC/3D, the context and schematic arc radius update correctly. Explicit reset fits both anatomy and field tracks; genuine manual camera input is retained.

## 0.2.13 — 27 September 2026

### Open aperture in the 3D MLC guide

- The miniature shows only the **actual aperture boundary** after intersecting every MLC layer with the jaws, including separate openings in modulated fields.
- The opening is **fully transparent**: anatomy and scene content remain visible through it. The rectangular backing, outer frame and central cross have been removed.
- Short, translucent bank-side fringes **fade outwards** from the aperture. Their fill is clipped away from every opening, including adjacent small apertures. Shared internal leaf edges are removed.
- The guide retains its gantry-track position, isocenter-facing orientation, collimator rotation and CP synchronization. Aperture boundaries are cached for the current CP without rebuilding anatomy.

## 0.2.12 — 27 September 2026

### MLC geometry on the gantry arc

- The active field now has a **spatial MLC plane on its schematic gantry track, facing the isocenter**. It follows gantry motion and rotates with the recorded **collimator angle**, including couch, patient orientation and rigid registration transforms.
- The jaw-clipped leaf pattern updates at each control point, including multiple MLC layers and static fields. The track is drawn behind the miniature. Only the selected field gets this plane.
- This is a perspective-projected schematic guide, not a physical-sized collimator or a depth-occluded anatomical surface. It can appear edge-on when viewed from the side; rotate the 3D scene to inspect its opening.
- **Fields visibility, selected field and CP position are shared** between images, MLC, standalone 3D and the linked 2 × 2 view. The MLC toolbar includes the same Fields toggle for its arrangement inset.

### Available sooner, with clear panel controls

- The **RT sidebar opens automatically** when matching structures, dose or plan data become available. Closing it manually is respected while subsequent objects load.
- **RT and Tags buttons turn blue when their panel is open**, with Open / Close hover explanations.
- Completing the current image series keeps its displayed pane. Adding the compatible CT volume retains existing 3D surfaces and an adjusted camera while new context is prepared.
- 3D shader/device initialization runs in the background; the hidden Tags panel no longer eagerly reads every attribute during opening. Searchable full tags remain available when the panel is opened.

## 0.2.11 — 27 September 2026

### Interactive control points in 3D

- Enable **Fields** in standalone 3D to select the active field and move through its **control-point slider**. The mouse wheel moves one CP; **Shift + wheel** moves 0.1 CP.
- Field and CP selection stay synchronized with MLC and image views, including fractional slider positions. The same controls are available in the linked 2 × 2 view.
- A small **MLC BEV preview** follows the active source marker along the arc and updates its leaf and jaw opening at each CP. Static fields keep their marker in place while their MLC shape changes.
- The miniature uses the same aperture renderer as the main MLC view, including multiple MLC layers. Stable beam-wide jaw framing makes small SRS openings easier to see. It is a screen-facing schematic BEV, not a detector image or a physical-sized collimator model.
- Only the active field gets a miniature. **All fields · no highlight** removes the miniature while retaining the treatment-track overview.
- CP motion updates the lightweight field guide and miniature while reusing prepared tracks, ROI meshes and camera position.

### One modulation display

- The ring now shows **angular meterset modulation only** (e.g. MU/°); the planned-rate selector has been removed from image and 3D controls.
- Angular modulation retains the segment meterset / directed-angle calculation. It does not infer delivery timing or measured dose rate.

## 0.2.10 — 27 September 2026

### Exports with useful identifiers

- Image and DVH export filenames include **Patient ID**, the relevant **Plan ID (RT Plan Label)** when available, the view and a timestamp. Invalid filename characters are replaced automatically.
- PNG exports include a small Patient ID / Plan ID footer. Detailed DVH CSV files also carry these identifiers in their rows. The DVH uses the plan referenced by its selected dose.
- Dose, volume and metric values in CSV use **at most four decimal places**, with a dot as the decimal separator.
- **Export curves only (CSV)** provides just `Structure,Dose,DoseUnit,VolumePercent` for straightforward import. IDs remain in the filename; there are no metric columns or metadata rows. Only enabled, calculated curves are included. Detailed export continues to report unavailable curves and all metrics.

### VMAT modulation in images and 3D

- Arc bars default to **angular meterset modulation**: segment meterset divided by directed gantry travel. Their lengths show variation even when a plan records a constant dose-rate setting.
- Switch to **Planned rate setting** to see the recorded Dose Rate Set instead. Units distinguish **MU/°** from **MU/min** where the plan declares MU. Missing or invalid values remain unknown.
- Angular modulation is derived from cumulative meterset weights and the beam meterset. It is **not a reconstructed or measured delivery dose rate**; no delivery time is inferred. See the [DICOM RT Beams module](https://dicom.nema.org/medical/dicom/current/output/chtml/part03/sect_c.8.8.14.html).
- **Fields** in 3D shows all treatment tracks and their modulation bars, with an incoming-direction arrow and highlighted active field. The guides rotate with the patient and do not rebuild ROI surfaces. Track radii are schematic orientation aids.
- Field settings are retained between standalone 3D and the linked 2 × 2 view. Setup and imaging fields remain hidden unless explicitly selected.

### More prepared contours, responsive navigation

- Connected silhouette edges now use compact polylines, preserving the original raster boundary and concavities while reducing cache memory.
- PTV projections have a separate cache budget. Background preparation prioritizes targets across control points, then other structures, then DRRs; nearby positions take priority.
- Adding a category retains already available contours at the **same projection geometry**. A new angle, image or registration cannot display contours from an older geometry.
- Caches remain bounded. Missing anatomy is prepared asynchronously, keeping leaf navigation available throughout.

## 0.2.9 — 27 September 2026

### More image, fewer controls

- A single compact image toolbar brings together the image series, fusion, isocenter, help and field controls.
- Recognizable view buttons switch between Native, Axial, Coronal, Sagittal, linked **2 × 2 (MPR + 3D)** and **standalone 3D**. The same overview button is available in MLC and 3D.
- Hover explanations make controls easier to discover. Narrow windows keep every toolbar control accessible through horizontal scrolling.
- Linked crosshairs can be dragged with the left mouse button while keeping the in-plane viewport fixed. **Ctrl + mouse wheel** zooms the image.
- A selected plan can bring up its explicitly referenced compatible planning-image series automatically when the current image cannot be mapped to it. Ambiguous associations remain a manual choice.

### Windowing at a glance

- **Auto** is the initial preset. Choose DICOM, Soft tissue, Lung, Bone, Brain or Liver from the preset dropdown.
- **Custom** reveals a sampled intensity histogram, a grayscale strip and draggable upper/lower window limits. CT limits are shown in HU.
- Right-drag window/level adjustment remains available without opening the controls.

### Fields and arcs with a clear direction

- Fixed fields show their joint **MLC and jaw aperture**, including multiple MLC layers, with an arrow identifying the incoming beam direction.
- Moving gantries show a **projected source track** instead of changing leaf intersections across the slices. The selected track includes ticks scaled to the plan's segment dose-rate setting.
- The ring is a schematic orientation aid; its radius is not source distance. Rates come from **Dose Rate Set**, not inferred delivery data. Missing values remain unknown.
- Choose the active field in the dropdown, or select **All fields — no highlight** for an equal gray overview.
- SETUP and CBCT fields appear only when selected explicitly.

### MLC with optional anatomy

- Beam selection, layer selection, DRR, PTV/organ/other outlines and leaf opacity share one compact settings row.
- The **MLC opacity** slider lets you choose dark leaves over the DRR or a translucent view that reveals more anatomy. DRR still starts off.
- The mouse wheel visits recorded control points; **Shift + wheel** moves in 0.1-CP increments. The timeline slider remains continuous, with beam-end markers.
- PTV, organ and other outlines are prepared in the background before their category toggles are enabled. Individual completed outlines can appear as they become available.
- Nearby positions are prioritized; the viewer retains bounded caches across views and cancels superseded foreground projections. Leaves and navigation remain usable while anatomy is being prepared.

### Compact DVH, useful statistics

- The structure list shows the **name and a small volume label**, leaving more room for structures.
- Each structure has an **info hover** with **Dmean, Dmedian, Dmax, Dmin, D98 and D2**, together with coverage and sampling details.
- Clicking a name emphasizes its curve; clicking again restores equal emphasis. Checkboxes continue to control visibility independently.
- Right-click to export all enabled structures as **CSV**, including curves, volume, every listed dose metric and calculation status. A full-chart **PNG** export includes the active structures and metrics too.
- Summaries use the preview's sampled dose values and interpolated cumulative curve. Whole-structure dose summaries remain unavailable when coverage is incomplete; missing dose is not treated as zero.

### Save and share the view you are looking at

- **Right-click → Save view as image…** is available in Native, Axial, Coronal, Sagittal, 3D, DVH and MLC, including individual tiles in the 2 × 2 layout.
- 3D screenshots include the rendered GPU surfaces.
- A **PNG** button and **Ctrl + Shift + S** save the central workspace.

### Help and shortcuts

The **info button beside ISO**, or **F1**, opens a short introduction, developer details, project/download links, this changelog and a shortcut reference.

| Shortcut | Action |
| --- | --- |
| Space in Explorer | Open the selected file in QuickLook |
| Ctrl + 1 / 2 / 3 / 4 | Native / Axial / Coronal / Sagittal |
| Ctrl + 5 / 6 | Linked 2 × 2 / standalone 3D |
| Alt + I / M / D / V | Image / MLC / DVH / 3D workspace |
| Ctrl + I | Jump to isocenter |
| Ctrl + F | Search all nested DICOM tags |
| Ctrl + Shift + S | Save the central workspace as PNG |
| Home | Fit the image |
| F1 | Help, developer links and shortcuts |

## Earlier releases

The earlier release pages retain their original feature notes and downloads:

- [0.2.8 — Responsive MLC and true field apertures](https://github.com/Kiragroh/QuickLook-DicomRT/releases/tag/v0.2.8)
- [0.2.7 — DRR, field geometry and responsive navigation](https://github.com/Kiragroh/QuickLook-DicomRT/releases/tag/v0.2.7)
- [0.2.6 — Space-key feature tour, MLC-first playback and visible ISO](https://github.com/Kiragroh/QuickLook-DicomRT/releases/tag/v0.2.6)
- [0.2.5 — Shared 3D workspace and background preparation](https://github.com/Kiragroh/QuickLook-DicomRT/releases/tag/v0.2.5)
- [0.2.4 — Detailed GPU surfaces and global isodoses](https://github.com/Kiragroh/QuickLook-DicomRT/releases/tag/v0.2.4)

This is a local research and inspection viewer, not a clinically validated treatment-planning system. [Verification notes](VERIFICATION.md) describe software checks and their limits.
