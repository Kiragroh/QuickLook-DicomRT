# Offline demonstration capture — 0.2.2

Build the English Viewer and DemoCapture, then run:

```powershell
DicomRT.DemoCapture.exe --approved-public-demo <approved-public-nonpatient-source> <media-output>
DicomRT.DemoCapture.exe --mlc-only <explicitly-authorized-source-folder> <mlc-output>
```

The public mode accepts three arguments including its mode flag. It captures actual settled ViewerControl states from the approved public nonpatient dataset. It has no synthetic supplementary capture mode. Synthetic fixtures remain available in test projects only.

WPF is hosted offscreen; RenderTargetBitmap captures the owned control without desktop automation or generated UI. Source files remain read-only. Identifying tag values are scrubbed only in memory before tag screenshots. Public sequences contain 90 actual 1600 × 900 frames; the DVH sequence changes curve emphasis using the actual focus control. The 3D default uses PTV/ORGAN types, with other ROI types and CT/dose context opt-in. Selected subsets are described in captions.

The separately authorized clinical MLC-only mode reads plan metadata and constructs a whitelist display model with generic plan and beam labels. It passes no source DicomEntry, patient/study/frame identifiers, original free text, source paths, images, tag tree or original isocenter coordinates to the control. Only technical aperture geometry, angles, normalized meterset and control-point progression are displayed. It captures MlcPlaybackControl alone, including the layers, global timeline and synchronized schematic linac. This approval does not authorize broader clinical screenshots or private DICOM distribution.

The MLC-only output contains `dual-layer-mlc.png`, `frames/frame-000.png` through `frames/frame-089.png`, and a neutral `manifest.json`. Check labels and pixel dimensions before curating the approved capture into presentation media. Do not store a source case path in scripts or manifests.

The effective input playback is 15 fps for six seconds. It represents scrubbed UI states, not rendering latency or actual treatment delivery timing. `scripts/video/build-clips.mjs` composes six final clips at 30 fps/180 frames with no title-only introduction. The main tour has 14 slides, five feature videos and a video hero; see [video build instructions](../scripts/video/README.md).

Current raw outputs belong under ignored `artifacts/presentation/v022/media` and the separate `artifacts/presentation/v022/private-mlc` capture directory. Keep raw frames and DICOM files out of source control. Only explicitly approved public nonpatient assets and the approved sanitized MLC-only result may be curated into `docs/demo`.
