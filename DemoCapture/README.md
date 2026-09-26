# Approved public feature tour (0.2.6)

The 12-slide English presentation and 112-second film use only the approved nonpatient multi-metastasis benchmark. No private images or generated medical images are included. Conceptual Space-key artwork was generated with built-in GPT Image; prompts and provenance accompany the presentation. The HTML opening is static artwork; the full film is offered on the final chapter.

```powershell
DicomRT.DemoCapture.exe --approved-public-tour <approved-public-nonpatient-source> <capture-output>
# Optional isolated refreshes after viewer changes:
DicomRT.DemoCapture.exe --approved-public-tour-refresh <approved-public-nonpatient-source> <capture-output>
DicomRT.DemoCapture.exe --approved-public-tour-mlc <approved-public-nonpatient-source> <capture-output>
DicomRT.DemoCapture.exe --approved-public-tour-orbit <approved-public-nonpatient-source> <capture-output>
node scripts/video/build-feature-film.mjs
python scripts/BuildFeatureTour.py --require-assets
node scripts/video/verify-feature-tour.mjs
```

Default film inputs are `artifacts/presentation/v025-tour/capture` and `assets`; output is the sibling `output` directory. Source UI captures contain 120 frames per principal sequence, 150 for MLC and 90 for tags at nominal 15 fps. The final HyperFrames/GSAP composition runs at 30 fps and opens with conceptual Space-key art before revealing actual viewer footage. DVH and tag inspection receive enlarged views and ten seconds each; the final GitHub card stays for ten seconds. `compose-soundtrack.py` uses Python/NumPy to synthesize an original sample-free 120 BPM instrumental bed; all scene changes fall on two-second bar boundaries. DVH and tags are slowed editorially for readability. This is an edited feature demonstration, not measured responsiveness. The case contains CT/RS/RD/RP but no MR/REG, dual-layer MLC or multiple-plan example. Those capabilities are described separately.

The 3D capture intentionally selects all 24 PTVs plus Brainstem. The source labels Brainstem as AVOIDANCE, so Other is explicitly enabled; the viewer default remains PTV only. RT-only capture uses an unchanged approved RTPLAN temporarily isolated from its images. GPU scenes are composited through their actual readback. Global isodose preferences and clipboard content are not changed.

The authoring approach follows [HyperFrames student kit](https://github.com/nateherkai/hyperframes-student-kit): deterministic local media, a registered finite GSAP timeline, clip metadata, lint before render and rendered-frame review. Assets run offline.

---
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
