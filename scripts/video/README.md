# Viewer demonstration clips — 0.2.1

These local HyperFrames compositions produce seven English 1600 × 900 MP4 clips. Actual viewer footage begins on the first frame, without a title-only introduction. Each clip lasts seven seconds and contains 180 output frames at 30 fps, with a compact caption. HyperFrames renders the composition; FFmpeg prepares the input sequence.

## Rebuild

Install Node.js 22 or newer, FFmpeg and FFprobe. From this directory:

```powershell
npm ci --no-audit --no-fund
node build-clips.mjs
```

Dependencies are pinned in `package-lock.json`. The composition is already defined in `build-clips.mjs`; initialization or skill installation is unnecessary. Rendering uses local Chromium and local media. The first run may download Chromium; media is not uploaded. Render subprocess telemetry and update checks are disabled.

Default input: `artifacts/presentation/v021/media`, relative to the repository root. Each of `scroll`, `mlc`, `orbit`, `dose`, `dvh` and `dual-mlc` must contain exactly 90 consecutive 1600 × 900 PNG frames, named `frame-000.png` through `frame-089.png`. Input footage is interpreted at 15 fps. These settled UI states are not live rendering benchmarks or treatment delivery times.

Place `manifest.json` alongside the folders. Mark each completed sequence with its path and `uiLanguage: en` only after all frames are verified. The first five sequences use the approved public nonpatient benchmark. The seventh uses the separately approved actual clinical MLC-only capture with a whitelist display model and generic labels. It contains no source images, identifiers, original names, tag tree or source paths. Synthetic inputs are not used in this presentation; synthetic fixtures remain confined to tests.

The seventh clip's provenance is `approved-sanitized-mlc`. Its caption identifies sanitized MLC-only viewer capture; do not describe it as nonpatient data or synthetic data. This authorization does not extend to other clinical views.

Optional arguments use `--name=value`; relative paths resolve from the current directory:

```powershell
node build-clips.mjs --clip=05-dvh
node build-clips.mjs --clip=06-dual-mlc
node build-clips.mjs --input=./approved-frames --output=./clips --work=./render-work
node build-clips.mjs --prepare
```

`--prepare` creates compositions, encodes source videos and runs the native HyperFrames linter without final rendering. Generated HTML, local assets, logs and FFprobe JSON remain in the ignored work directory, by default `artifacts/presentation/v021/hyperframes`. Final metadata checks require 1600 × 900, seven seconds and 180 frames at 30 fps.

Outputs in `artifacts/presentation/output/media`:

- `01-scroll.mp4`: image navigation.
- `02-mlc.mp4`: public-plan MLC playback.
- `03-3d.mp4`: 3D orbit.
- `04-dose.mp4`: dose display controls.
- `05-dvh.mp4`: curve focus and comparison.
- `06-dual-mlc.mp4`: approved sanitized dual-layer MLC playback.

The 14-slide tour uses five feature videos plus a video hero. Raw captures, generated clips, caches and `node_modules` are not source files.
