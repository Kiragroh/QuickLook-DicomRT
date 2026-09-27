# Changelog

Feature highlights for DICOM RT for QuickLook. [Download the latest Windows installer](https://github.com/Kiragroh/QuickLook-DicomRT/releases/latest) · [Project home](https://github.com/Kiragroh/QuickLook-DicomRT)

## 0.2.19 — 27 September 2026

### Load the images you need

- **Search more images** loads matching image series for the selected plan, dose, structure set or generated sum. Known deferred images are loaded directly by their paths; otherwise choose a search folder. The search is cancellable.
- Opening an image fully catalogs its own series. Other image series retain lightweight patient/series/frame identities for later discovery; unrelated pixels and volumes are not loaded. Opening an RT file catalogs images associated with that object.
- The RT-first scan reuses the same bounded header pass for deferred identities. Large folders still require file enumeration and header reads; this is not a claim of constant-time discovery.
- **Search subfolders** finds additional RT while deferring image series. It remains available after RT has already been found.

### Explicit RT selection, with or without CT

- The RT dropdown lists plans with indented entries for their individual doses and structure sets, plus standalone entries for unassociated RT files.
- Plans are marked **images available**, **images not loaded**, or **no matching images**. When opening an image, matching plans are preferred over unrelated ones. Opening an RT file keeps that specific object selected.
- Selecting a plan without loaded matching images clears unrelated anatomy from the display and keeps MLC available. RTDOSE and RTSTRUCT remain usable independently in 3D and available dose analysis; an RTPLAN does not require a CT or dose for MLC playback.
- Image matching uses patient identity, explicit referenced series when present, frame identity and existing unambiguous REG mappings. Deferred image identities can resolve REG image references without pixel loading.
- The selected dose or structure set can be inspected independently even when an associated plan is available. For a standalone structure set, dose is automatically associated only when a single compatible dose is available; select an individual dose when several alternatives exist.

### Clearer shortcut reference

- The Info / F1 panel presents keyboard and mouse controls in grouped two-column tables, with individual keycaps and one action per row. Navigation, views, image/MLC controls and export are easy to scan.

## 0.2.18 — 27 September 2026

- **Search subfolders remains available after every initial scan**, including when plans, structures or doses have already been found. Search a common parent to add more matching RT objects from sibling directories without closing the current case.
- Existing source-instance identities prevent repeated discovery from loading the same RT object twice. The current image and patient filtering are retained.

## 0.2.17 — 27 September 2026

### Selectable plan sums

- Compatible physical PLAN doses are offered as separate spatial groups. An unrelated first dose no longer hides a sum in another frame of reference.
- The dropdown names the group members. The Dose panel lists every plan with a checkbox and **Generate sum**. The displayed sum explicitly names its included plans; changing checkboxes keeps the previous result until regeneration.
- Same-frame or unambiguously rigid-registered doses can be combined. Unrelated frames, ambiguous duplicate plan doses and incompatible units are excluded. There is no inferred clinical course grouping or fraction scaling; users choose which compatible plans contribute.

### Isocenter and dose-maximum navigation

- Initial compatible dose loading and individual-plan changes navigate to the mapped isocenter, with **Dmax as the fallback** when no isocenter is available. Initial loading still respects prior manual navigation.
- Generated plan sums navigate to their maximum finite dose voxel. A **Dose maximum** button provides the same action on demand.
- Maximum voxel locations are prepared during dose loading, including nonuniform and descending frame offsets.

### Custom electron apertures

- Individual DICOM APERTURE and SHIELDING block contours now contribute to image-plane field intersections, the beam-eye preview and the active 3D aperture miniature.
- Concave cutouts retain their true boundary. Block coordinates are interpreted in their recorded IEC beam-limiting-device isocentric plane, including collimator rotation, without applying source-to-tray scaling twice.
- These are geometric field guides, not an electron transport or penumbra calculation. Missing or unsupported block geometry is not replaced with a fabricated jaw aperture.

### Compact controls and subfolder discovery

- **Opacity** opens vertically stacked ROI and skin controls in 3D. Colorwash opacity and thresholds are grouped in a collapsed section of the Dose panel.
- With no RT objects found, a bottom-right **Search subfolders** button lets you select the common parent of image and RT directories, including sibling folders.
- Recursive discovery remains cancellable, prioritizes RT, filters loaded RT to the opened patient, skips inaccessible folders and directory links, and retains the displayed image. Source files remain unchanged.

## 0.2.16 — 27 September 2026

### Field geometry across the full image pane

- Gantry arcs, angular-modulation ticks and fixed-field aperture intersections can extend **past the CT image boundary into the free pane area**. The pane boundary still prevents drawing into adjacent views or controls.
- Anatomy and dose contours keep their original image clipping and spatial mapping.

### Prepared MLC anatomy during playback

- The background cache includes **fractional control points** used by default playback, with additional exact-position lookahead for other playback speeds and manual navigation.
- DRR preparation has its **own background worker**, running alongside structure preparation. PTV, organ and other categories remain prepared before their checkboxes are enabled.
- Completed projections survive cursor changes. Unchanged cached overlays do not repeatedly redraw as unrelated background work completes.
- **192-pixel DRR previews** keep the preparation and memory cost bounded; stationary views refine to **384 pixels** in place. Angles and projection geometry are unchanged between preview and refinement.
- Playback advances with complete enabled overlays. If the next exact-angle frame is not ready, the Play/Pause button briefly shows **Preparing**, retaining the complete current CP. Scrolling, scrubbing and pausing remain available; no old-angle DRR or contours are attached to a newer aperture.

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
