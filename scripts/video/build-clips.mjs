import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';

const toolRoot = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(toolRoot, '../..');
const option = (name, fallback) => {
  const value = process.argv.find(a => a.startsWith(`--${name}=`));
  return value ? path.resolve(value.slice(name.length + 3)) : fallback;
};
if (process.argv.includes('--help')) {
  console.log('node build-clips.mjs [--input=PATH] [--output=PATH] [--work=PATH] [--clip=01-scroll|02-mlc|03-3d|04-dose|05-dvh|06-dual-mlc|07-mpr] [--prepare]');
  console.log('Defaults: artifacts/presentation/v021/media, artifacts/presentation/output/media and artifacts/presentation/v021/hyperframes. Requires 90 approved English UI frames per clip and manifest.json. No title-only intro.');
  process.exit(0);
}
const root = option('work', path.join(repoRoot, 'artifacts/presentation/v021/hyperframes'));
const input = option('input', path.join(repoRoot, 'artifacts/presentation/v021/media'));
const output = option('output', path.join(repoRoot, 'artifacts/presentation/output/media'));
const cli = path.join(toolRoot, 'node_modules/hyperframes/bin/hyperframes.mjs');
const logs = path.join(root, 'logs');
const env = { ...process.env, HYPERFRAMES_NO_TELEMETRY: '1', HYPERFRAMES_NO_UPDATE_CHECK: '1', NODE_TLS_REJECT_UNAUTHORIZED: '1' };
const clips = [
  { name: '01-scroll', source: 'scroll', title: 'Navigate CT slices', caption: 'CT with contours · axial slice navigation' },
  { name: '02-mlc', source: 'mlc', title: 'Explore the plan timeline', caption: 'Plan-wide timeline · interpolated control points, not delivery time' },
  { name: '03-3d', source: 'orbit', title: 'Explore structures in 3D', caption: 'Selected structures and dose · interactive 3D preview' },
  { name: '04-dose', source: 'dose', title: 'Dose visualization controls', caption: 'Colorwash and isodoses · independent display controls' },
  { name: '05-dvh', source: 'dvh', title: 'Compare structure DVHs', caption: 'Focus a curve · retain dose coverage and sampling context' },
  { name: '06-dual-mlc', source: 'dual-mlc', title: 'Inspect both MLC layers', caption: 'Dual-layer aperture · interpolated control points, not delivery time', provenance: 'approved-sanitized-mlc' },
  { name: '07-mpr', source: 'mpr', title: 'Navigate synchronized MPR and 3D', caption: 'Axial, coronal, sagittal and 3D · linked coordinate planes' }
];
const requested = process.argv.find(a => a.startsWith('--clip='))?.split('=')[1];
const selected = requested ? clips.filter(c => c.name === requested) : clips;
if (!selected.length) throw new Error('Unknown clip selection');
fs.mkdirSync(output, { recursive: true }); fs.mkdirSync(logs, { recursive: true });

function run(binary, args, name, cwd = root) {
  const result = spawnSync(binary, args, { cwd, env, encoding: 'utf8', windowsHide: true, maxBuffer: 32 * 1024 * 1024 });
  fs.writeFileSync(path.join(logs, name + '.log'), (result.stdout || '') + (result.stderr || ''));
  if (result.error || result.status !== 0) throw new Error(`${name} failed: ${result.error?.message || (result.stderr || result.stdout).slice(-2500)}`);
  return result.stdout;
}
function html(c) {
  return `<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=1600,height=900">
<title>${c.title}</title><script src="assets/gsap.min.js"></script>
<style>
*{box-sizing:border-box;margin:0;padding:0}html,body{width:1600px;height:900px;overflow:hidden;background:#0d1318;color:#ecf5f5;font-family:"Segoe UI",Arial,sans-serif}
#root{position:relative;width:1600px;height:900px;background:#0d1318}.clip{position:absolute}
#footage{left:45px;top:0;width:1510px;height:849.375px;object-fit:contain}
#footer{left:0;top:849px;width:1600px;height:51px;background:#131f25;border-top:1px solid #29434b;display:flex;align-items:center;padding:0 45px;gap:20px}
#footer .line{width:30px;height:3px;background:#68d0bb;flex:none}#footer .caption{font-size:19px;font-weight:450;letter-spacing:.05px}
#footer .source{margin-left:auto;white-space:nowrap;font-size:14px;color:#9fb4bb}
</style></head><body>
<main id="root" data-composition-id="main" data-start="0" data-duration="6" data-width="1600" data-height="900" data-fps="30">
<video id="footage" class="clip" data-start="0" data-duration="6" data-track-index="0" data-media-start="0" src="assets/source.mp4" muted playsinline preload="auto"></video>
<div id="footer" class="clip" data-start="0" data-duration="6" data-track-index="1"><span class="line"></span><span class="caption">${c.caption}</span><span class="source">${c.provenance === 'approved-sanitized-mlc' ? 'Sanitized MLC-only viewer capture' : 'Public benchmark · actual viewer capture'}</span></div>
</main>
<script>
window.__timelines=window.__timelines||{};
const tl=gsap.timeline({paused:true});
tl.to('#footage',{opacity:1,duration:6,ease:'none'},0);
window.__timelines.main=tl;
</script></body></html>`;
}

for (const clip of selected) {
  const manifest = JSON.parse(fs.readFileSync(path.join(input, 'manifest.json'), 'utf8').replace(/^\uFEFF/, ''));
  const capture = manifest.artifacts.find(a => a.file === `${clip.source}/frame-%03d.png`);
  if (clip.provenance && capture?.provenance !== clip.provenance) throw new Error(`${clip.source}: approved sanitized MLC-only provenance must be explicit in the manifest`);
  if (capture?.uiLanguage !== 'en') throw new Error(`${clip.source}: completed English UI capture not yet confirmed in source manifest`);
  const sourceDir = path.join(input, clip.source);
  const frames = fs.readdirSync(sourceDir).filter(n => /^frame-\d{3}\.png$/.test(n)).sort();
  if (frames.length !== 90 || frames.some((name, i) => name !== `frame-${String(i).padStart(3, '0')}.png`)) throw new Error(`${clip.source}: expected exactly 90 consecutive approved source frames`);
  const dir = path.join(root, 'compositions', clip.name), assets = path.join(dir, 'assets');
  fs.mkdirSync(assets, { recursive: true });
  fs.copyFileSync(path.join(toolRoot, 'node_modules/gsap/dist/gsap.min.js'), path.join(assets, 'gsap.min.js'));
  fs.writeFileSync(path.join(dir, 'index.html'), html(clip));
  fs.writeFileSync(path.join(dir, 'hyperframes.json'), JSON.stringify({ media: { autoProxy: false } }, null, 2));
  fs.writeFileSync(path.join(dir, 'meta.json'), JSON.stringify({ name: clip.title, id: clip.name }, null, 2));
  run('ffmpeg', ['-hide_banner','-loglevel','warning','-y','-framerate','15','-i',path.join(sourceDir,'frame-%03d.png'),'-frames:v','90','-c:v','libx264','-preset','medium','-crf','14','-pix_fmt','yuv420p','-an','-movflags','+faststart',path.join(assets,'source.mp4')], clip.name+'-source-encode');
  const lint = run(process.execPath,[cli,'lint',dir,'--json'],clip.name+'-lint');
  fs.writeFileSync(path.join(logs,clip.name+'-lint.json'),lint);
  if (process.argv.includes('--prepare')) { console.log('PREPARED '+clip.name); continue; }
  const target = path.join(output,clip.name+'.mp4');
  run(process.execPath,[cli,'render',dir,'--output',target,'--fps','30','--quality','high','--workers','2','--video-frame-format','png','--strict-all','--frames-cache-dir',path.join(root,'frames-cache')],clip.name+'-render');
  const probe = JSON.parse(run('ffprobe',['-v','error','-show_entries','format=duration,size:stream=codec_name,width,height,r_frame_rate,nb_frames','-of','json',target],clip.name+'-probe'));
  const video = probe.streams.find(s=>s.width);
  if (video.width!==1600 || video.height!==900 || Math.abs(Number(probe.format.duration)-6)>.05 || video.nb_frames!=='180') throw new Error('Unexpected rendered metadata for '+clip.name);
  fs.writeFileSync(path.join(logs,clip.name+'-probe.json'),JSON.stringify(probe,null,2));
  console.log('VERIFIED '+clip.name+': 1600×900, 6 seconds, 180 frames, final composition rendered by HyperFrames');
}
