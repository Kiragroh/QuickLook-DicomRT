import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {spawnSync} from 'node:child_process';

const tools = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(tools, '../..');
const base = path.join(repo, 'artifacts/presentation/v025-tour');
const capture = path.join(base, 'capture');
const out = path.join(base, 'output');
const work = path.join(base, 'film');
const cli = path.join(tools, 'node_modules/hyperframes/bin/hyperframes.mjs');
for (const p of [work, `${work}/assets`, `${work}/logs`, `${out}/media`, `${out}/screens`, `${out}/art`]) fs.mkdirSync(p,{recursive:true});
const env={...process.env,HYPERFRAMES_NO_TELEMETRY:'1',HYPERFRAMES_NO_UPDATE_CHECK:'1',NODE_TLS_REJECT_UNAUTHORIZED:'1'};
function run(exe,args,label){
  const log=`${work}/logs/${label}.log`,fd=fs.openSync(log,'w');
  const r=spawnSync(exe,args,{env,cwd:work,encoding:'utf8',windowsHide:true,stdio:['ignore',fd,fd]});
  fs.closeSync(fd);const result=fs.readFileSync(log,'utf8');
  if(r.error||r.status!==0)throw new Error(`${label}: ${r.error?.message||result.slice(-3000)}`);
  return result;
}
const scenes=[];
const add=(duration,source,title,detail,kind='video',offset=0)=>scenes.push({duration,source,title,detail,kind,offset});
// A real viewer montage starts on the very first frame, before any title card.
add(3,'scroll','Radiotherapy colleagues: meet your Space key.','Images, structures, dose and plans — directly from a DICOM file on Windows.');
add(3,'orbit','A familiar idea from the Mac.','Select a file. Press Space. Get a preview.');
add(3,'dvh','QuickLook brings that idea to Windows.','DICOM image previews already exist. I wanted the RT context, too.');
add(3,'mlc','So I built DICOM RT for QuickLook.','Structures, dose, DVH and MLC — without importing into a TPS.');
add(4,'space-key.png','Select a DICOM file. Press Space.','The goal: understand what is in the folder, quickly.','art');
add(8,'scroll','Keep moving through the study.','CT scrolling, contours and dose together. Ctrl + wheel zooms; the view stays put.');
add(8,'mpr','Four panes. One position.','Axial, coronal, sagittal and 3D share a position, with linked coordinate planes.');
add(8,'orbit','See every target in context.','PTVs first. Add organs or skin, select a structure, and orbit around the isocenter.');
add(4,'three-d-focused.png','Keep the geometry ready.','Background mesh preparation and retained meshes support quick returns to 3D.','still');
add(8,'dose','Choose how dose is shown.','Colorwash and isodose lines have independent visibility and display controls.');
add(5,'dose-levels-edited.png','Make the legend your own.','Edit levels and colors. Gy settings stay local to the dose; relative settings apply globally.','still');
add(3,'relative-isodoses.png','A clear way back.','Default restores the selected mode. Percentages refer to the dose object’s maximum.','still');
add(8,'dvh','Focus without losing the comparison.','Click a structure name to emphasize its curve. Click again to restore all curves.');
add(10,'mlc','Follow the entire plan.','MLC treatment fields first; imaging fields last. Beam-end markers organize the timeline.');
add(4,'mlc.png','Know the setup at a glance.','The linac cue follows gantry, couch and collimator. Playback is not delivery timing.','still');
add(6,'tags','Search the complete parsed tag tree.','Nested matches open automatically. Clear the search and keep the selected entry in context.');
add(4,'tag-details.png','Inspect. Copy. Continue.','Double-click a tag to copy its number, label or value from a compact detail popup.','still');
add(5,'rt-only.png','Start with the RT object you have.','Plans, structures and dose can open without CT. Matching images are added when available.','still');
add(5,'direct-files.png','Less waiting to understand a dataset.','Image-first loading · RT-priority discovery · background preparation · retained 3D meshes','art');
add(4,'space-key.png','Bring RT to your Space key.','github.com/Kiragroh/QuickLook-DicomRT\nWindows installer + source · Requires QuickLook for Windows','art');
let time=0; for(const s of scenes){s.start=time;time+=s.duration;}
fs.writeFileSync(`${base}/storyboard.json`,JSON.stringify({duration:time,source:'Approved public nonpatient multimets benchmark; actual ViewerControl captures',note:'Edited demonstration; not a latency measurement or clinical validation. No synthetic medical visuals.',scenes},null,2));
fs.writeFileSync(`${work}/DESIGN.md`,'# DICOM RT feature film\n\n## Style Prompt\nReal, readable viewer footage leads. Graphite canvas and the viewer’s blue accent. Compact editorial captions, calm precise entrances and direct cuts. Space-key conceptual art is confined to interludes.\n\n## Colors\n- #101316 canvas\n- #F0F4F8 foreground\n- #A7B6C6 supporting text\n- #64B5F6 blue accent\n\n## Typography\nBahnschrift headlines; Segoe UI supporting text, system installed.\n\n## What NOT to Do\nNo fabricated anatomy, neon grids, tiny multi-tile footage, gradient text, clinical claims or fake performance counters.\n');

if(!process.argv.includes('--render-only')){
  for(const name of ['scroll','dose','mpr','orbit','mlc','dvh','tags']){
    const dir=`${capture}/${name}`;
    const frames=fs.readdirSync(dir).filter(n=>/^frame-\d{3}\.png$/.test(n)).sort();
    const expected=name==='mlc'?150:name==='tags'?90:120;
    if(frames.length!==expected||frames.some((n,i)=>n!==`frame-${String(i).padStart(3,'0')}.png`)) throw new Error(`Incomplete ${name}: ${frames.length}/${expected}`);
    run('ffmpeg',['-hide_banner','-loglevel','warning','-y','-framerate','15','-i',`${dir}/frame-%03d.png`,'-frames:v',String(expected),'-c:v','libx264','-preset','medium','-g','15','-keyint_min','15','-crf','15','-pix_fmt','yuv420p','-an','-movflags','+faststart',`${out}/media/${name}.mp4`],`encode-${name}`);
    fs.copyFileSync(`${out}/media/${name}.mp4`,`${work}/assets/${name}.mp4`);
  }
  for(const f of fs.readdirSync(capture).filter(n=>n.endsWith('.png'))){fs.copyFileSync(`${capture}/${f}`,`${out}/screens/${f}`);fs.copyFileSync(`${capture}/${f}`,`${work}/assets/${f}`);}
  for(const f of ['space-key.png','direct-files.png']){fs.copyFileSync(`${base}/assets/${f}`,`${out}/art/${f}`);fs.copyFileSync(`${base}/assets/${f}`,`${work}/assets/${f}`);}
  fs.writeFileSync(`${out}/art/imagegen-prompts.md`,fs.readFileSync(`${base}/assets/imagegen-prompts.md`,'utf8').replace(/^Source:.*$/gm,'Source: built-in GPT Image output; original retained locally.').replaceAll('0.2.5','0.2.6'));
  fs.copyFileSync(`${tools}/node_modules/gsap/dist/gsap.min.js`,`${work}/assets/gsap.min.js`);
}
const esc=s=>s.replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('"','&quot;');
fs.mkdirSync(`${work}/compositions`,{recursive:true});
const elements=scenes.map((s,i)=>{
  const attrs=`data-start="0" data-duration="${s.duration}"`;
  const media=s.kind==='video'?`<video id="v${i}" ${attrs} data-track-index="0" data-media-start="${s.offset}" src="assets/${s.source}.mp4" muted playsinline preload="auto"></video>`:`<img id="v${i}" class="clip ${s.kind}" ${attrs} data-track-index="0" src="assets/${s.source}" alt="${esc(s.title)}">`;
  fs.writeFileSync(`${work}/compositions/scene-${i}.html`,`<template id="scene-${i}-template"><div data-composition-id="scene-${i}" data-width="1600" data-height="900">${media}<div id="c${i}" class="clip caption" ${attrs} data-track-index="1"><h1>${esc(s.title)}</h1><p>${esc(s.detail)}</p><span class="count">${String(i+1).padStart(2,'0')} / ${scenes.length}</span></div><script>window.__timelines=window.__timelines||{};{const tl=gsap.timeline({paused:true});tl.to({},{duration:${s.duration}},0);tl.from('#c${i} h1, #c${i} p',{y:10,opacity:0,duration:.32,ease:'power2.out'},0);window.__timelines['scene-${i}']=tl;}</script></div></template>`);
  return `<div id="s${i}" data-composition-id="scene-${i}" data-composition-src="compositions/scene-${i}.html" data-start="${s.start}" data-duration="${s.duration}" data-track-index="${i}"></div>`;
}).join('\n');
const animation='';
fs.writeFileSync(`${work}/index.html`,`<!doctype html><html lang="en"><head><meta charset="utf-8"><title>DICOM RT — one key, the whole picture</title><script src="assets/gsap.min.js"></script><style>
@font-face{font-family:Bahnschrift;src:local('Bahnschrift')}*{box-sizing:border-box}html,body{margin:0;width:1600px;height:900px;overflow:hidden;background:#101316;color:#f0f4f8;font-family:'Segoe UI',sans-serif}#film{position:relative;width:1600px;height:900px}video,img{position:absolute;left:114px;top:0;width:1372px;height:771.75px;object-fit:contain}img.art{left:0;width:1600px;object-fit:cover} .caption{position:absolute;left:0;top:772px;width:1600px;height:128px;background:#101316;display:grid;grid-template-columns:1fr auto;grid-template-rows:auto auto;align-items:center;padding:8px 36px 9px;column-gap:16px;border-top:1px solid #2d3a47}.copy{flex:1;min-width:0}h1{font-family:Bahnschrift,'Segoe UI',sans-serif;font-size:38px;font-weight:600;line-height:1.1;margin:0 0 5px;letter-spacing:-.3px}p{color:#a7b6c6;font-size:23px;line-height:1.25;margin:0}.count{grid-column:2;grid-row:1 / 3;font-size:15px;color:#64b5f6;white-space:nowrap}
#c19{top:180px;left:88px;width:1100px;height:470px;display:flex;flex-direction:column;align-items:flex-start;justify-content:center;padding:0;border:0;background:transparent}#c19 h1{font-size:64px;max-width:720px;line-height:1.08;margin-bottom:28px}#c19 p{font-size:30px;white-space:pre-line;line-height:1.6;color:#64b5f6}#c19 .count{display:none}</style></head><body><div id="film" data-composition-id="feature-tour" data-start="0" data-duration="${time}" data-width="1600" data-height="900">${elements}</div><script>window.__timelines=window.__timelines||{};const tl=gsap.timeline({paused:true});tl.to({},{duration:${time}},0);${animation}window.__timelines['feature-tour']=tl;</script></body></html>`);
fs.writeFileSync(`${work}/hyperframes.json`,JSON.stringify({media:{autoProxy:false}},null,2));
fs.writeFileSync(`${work}/meta.json`,JSON.stringify({id:'feature-tour',name:'DICOM RT — one key, the whole picture'},null,2));
run(process.execPath,[cli,'lint',work,'--json'],'lint');
if(process.argv.includes('--prepare')){console.log(`Prepared ${time}s film`);process.exit(0);}
run(process.execPath,[cli,'render',work,'--output',`${out}/media/feature-tour.mp4`,'--fps','30','--quality','high','--workers','2','--video-frame-format','png','--strict-all','--frames-cache-dir',`${work}/frames-cache`],'render');
run('ffmpeg',['-hide_banner','-loglevel','warning','-y','-i',`${out}/media/feature-tour.mp4`,'-t','12','-c:v','copy','-an','-movflags','+faststart',`${out}/media/hero-loop.mp4`],'hero');
const probe=JSON.parse(run('ffprobe',['-v','error','-show_entries','format=duration,size:stream=codec_name,width,height,r_frame_rate,nb_frames','-of','json',`${out}/media/feature-tour.mp4`],'probe'));
if(Math.abs(+probe.format.duration-time)>.06||probe.streams[0].width!==1600)throw new Error('Film metadata mismatch');
fs.writeFileSync(`${base}/film-verification.json`,JSON.stringify(probe,null,2));
console.log(`VERIFIED ${time}s, 1600x900, 30fps HyperFrames feature film`);
