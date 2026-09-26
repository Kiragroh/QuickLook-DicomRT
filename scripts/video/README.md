# Viewer demonstration clips

These local [HyperFrames](https://hyperframes.app/docs/5-packages/cli) compositions produce four English, 1600 × 900 MP4 clips. Each contains a one-second title followed by six seconds of actual viewer captures, with a small caption. HyperFrames renders the final composition. FFmpeg encodes the input image sequence; it does not replace the HyperFrames composition step.

## Rebuild

Install Node.js (22 or newer), FFmpeg and FFprobe. Run from this directory:

```powershell
npm ci --no-audit --no-fund
node build-clips.mjs
```

Dependencies are pinned in `package-lock.json` and installed locally. Do not run `hyperframes init` or `hyperframes skills install`: the source composition is already in `build-clips.mjs`. Rendering uses local Chromium and local media. The first render may download the Chromium runtime; no media is uploaded. Telemetry and update checks are disabled for the render subprocesses.

The default input is `artifacts/presentation/media`, relative to the repository root. It must contain exactly 90 consecutive 1600 × 900 PNG frames in each of `scroll`, `mlc`, `orbit`, and `dose`, named `frame-000.png` through `frame-089.png`. The original viewer frames are interpreted at 15 frames per second. They are settled UI states, not a performance measurement or treatment delivery timing.

Place `manifest.json` beside these folders and mark each sequence complete only after all English frames have been approved:

```json
{
  "artifacts": [
    { "file": "scroll/frame-%03d.png", "uiLanguage": "en" },
    { "file": "mlc/frame-%03d.png", "uiLanguage": "en" },
    { "file": "orbit/frame-%03d.png", "uiLanguage": "en" },
    { "file": "dose/frame-%03d.png", "uiLanguage": "en" }
  ]
}
```

The supplied captions describe the public benchmark captures used for this presentation. Only use approved public or synthetic inputs; if adapting the script for another source, update its provenance caption accurately. Do not put clinical data or identifying screenshots into a public repository.

Optional arguments use `--name=value` syntax; relative paths are resolved from the current directory:

```powershell
node build-clips.mjs --clip=03-3d
node build-clips.mjs --input=./approved-frames --output=./clips --work=./render-work
node build-clips.mjs --prepare
```

`--prepare` generates the compositions, encodes source videos, and runs the native HyperFrames linter without final rendering. Generated HTML, local assets, render logs, lint JSON and FFprobe JSON are retained in the work directory. The default work and output directories are under ignored `artifacts/`. The script verifies every final clip as 1600 × 900, 7 seconds and 210 frames at 30 fps.

Outputs: `01-scroll.mp4`, `02-mlc.mp4`, `03-3d.mp4`, and `04-dose.mp4` in `artifacts/presentation/output/media`. Raw captures, generated videos, caches and `node_modules` are not source files.
