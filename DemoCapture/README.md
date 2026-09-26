# Offline demonstration capture

Run after building the current English Viewer:

`DicomRT.DemoCapture.exe --approved-public-demo <approved-public-nonpatient-source> <media-output> <synthetic-fusion-folder> <synthetic-two-plan-folder>`

This creates actual settled ViewerControl screenshots and four 90-frame sequences at1600×900. WPF is hosted in an offscreen window; RenderTargetBitmap captures the owned component without desktop automation. Source files are read-only. Identifying tag values are scrubbed only in memory before tag screenshots. The separate fusion-picker PNG is the actual WPF Popup.Child visual, suitable as a labelled detail inset.

The manifest distinguishes the supplied public nonpatient benchmark from synthetic CT/MR/REG and genuine synthetic two-plan sum cases. Sequence entries include `uiLanguage: en` only once all90 current frames are complete. The effective15fps/6-second presentation playback is not a live rendering benchmark. A selected subset makes the 3D illustration legible; a separate scene-budget regression verifies all55 source structures and dose fit the renderer.

Outputs belong in ignored `artifacts/presentation/media`; do not commit generated media or DICOM files.
