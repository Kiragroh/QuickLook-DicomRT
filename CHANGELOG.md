# Changelog

Feature highlights for DICOM RT for QuickLook. [Download the latest Windows installer](https://github.com/Kiragroh/QuickLook-DicomRT/releases/latest) · [Project home](https://github.com/Kiragroh/QuickLook-DicomRT)

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
