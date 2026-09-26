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
// 120 BPM / four beats per bar: every scene boundary is a two-second bar boundary.
add(6,'space-magic.png','A little Space.\nA whole new view.','Images · Structures · Dose · Plans','magic');
add(2,'space-key.png','Select a file.\nPress Space.','DICOM RT previews for Windows.','key');
add(4,'dose','DICOM RT for QuickLook.','Your images, structures, dose and plan. No TPS import.','reveal');
add(2,'orbit','Targets. In context.','Explore the actual multi-metastasis benchmark in 3D.');
add(2,'mlc','The plan. In motion.','Follow MLC apertures, gantry, couch and collimator.');
add(8,'scroll','Keep moving through the study.','CT scrolling, contours and dose together. Ctrl + wheel zooms; the view stays put.');
add(8,'mpr','Four panes. One position.','Axial, coronal, sagittal and 3D share a position, with linked coordinate planes.');
add(8,'orbit','See every target in context.','PTVs first. Add organs or skin, select a structure, and orbit around the isocenter.');
add(4,'three-d-focused.png','Keep the geometry ready.','Background mesh preparation and retained meshes support quick returns to 3D.','still');
add(8,'dose','Choose how dose is shown.','Colorwash and isodose lines have independent visibility and display controls.');
add(4,'dose-levels-edited.png','Make the legend your own.','Edit levels and colors. Gy settings stay local to the dose; relative settings apply globally.','still');
add(2,'relative-isodoses.png','A clear way back.','Default restores the selected mode. Percentages refer to the dose object’s maximum.','still');
add(10,'dvh-focus','DVH. See the dose–volume picture.','Click a structure name to focus its curve. Click again to compare.','dvh');
add(8,'mlc','Follow the entire plan.','MLC treatment fields first; imaging fields last. Beam-end markers organize the timeline.');
add(4,'mlc.png','Know the setup at a glance.','The linac cue follows gantry, couch and collimator. Playback is not delivery timing.','still');
add(10,'tags-focus','Full DICOM\ntag search.','Numbers. Names. Values. Nested sequences.','tags');
add(4,'tag-details.png','Inspect. Copy.\nContinue.','Double-click a tag. Copy its number, label or value.','tagdetail');
add(4,'rt-only.png','Start with the RT object you have.','Plans, structures and dose can open without CT. Matching images are added when available.','still');
add(4,'direct-files.png','Less waiting to understand a dataset.','Image-first loading · RT-priority discovery · background preparation · retained 3D meshes','art');
add(10,'space-magic.png','Available now\non GitHub.','github.com/Kiragroh/QuickLook-DicomRT','ending');
let time=0; for(const s of scenes){s.start=time;time+=s.duration;}
if(time!==112||scenes.some(s=>s.start%2||s.duration%2))throw new Error('Music / scene bar alignment mismatch');
fs.writeFileSync(`${base}/storyboard.json`,JSON.stringify({duration:time,source:'Approved public nonpatient multimets benchmark; actual ViewerControl captures',note:'Edited demonstration; not a latency measurement or clinical validation. No synthetic medical visuals.',scenes},null,2));
fs.writeFileSync(`${work}/DESIGN.md`,'# DICOM RT feature film\n\n## Style Prompt\nQuiet GPT Image Space-key art introduces the workflow, followed by real viewer footage. DVH and tag inspection receive explicit chapter headings and enlarged views. The ten-second closing card prominently links to GitHub. An original sample-free 120 BPM instrumental bed aligns every scene change to a two-second bar. Graphite canvas and the viewer’s blue accent. Compact editorial captions, calm precise entrances and direct cuts. All conceptual imagery is separate from actual viewer captures.\n\n## Colors\n- #101316 canvas\n- #F0F4F8 foreground\n- #A7B6C6 supporting text\n- #64B5F6 blue accent\n\n## Typography\nBahnschrift headlines; Segoe UI supporting text, system installed.\n\n## What NOT to Do\nNo fabricated anatomy, neon grids, tiny multi-tile footage, gradient text, clinical claims or fake performance counters.\n');

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
  for(const f of ['space-key.png','direct-files.png','space-magic.png']){fs.copyFileSync(`${base}/assets/${f}`,`${out}/art/${f}`);fs.copyFileSync(`${base}/assets/${f}`,`${work}/assets/${f}`);}
  fs.writeFileSync(`${out}/art/imagegen-prompts.md`,fs.readFileSync(`${base}/assets/imagegen-prompts.md`,'utf8').replace(/^Source:.*$/gm,'Source: built-in GPT Image output; original retained locally.').replaceAll('0.2.5','0.2.6'));
  fs.copyFileSync(`${tools}/node_modules/gsap/dist/gsap.min.js`,`${work}/assets/gsap.min.js`);
}
fs.copyFileSync(`${out}/art/space-magic.png`,`${work}/assets/space-magic.png`);
// Slower editorial playback, explicitly not a measurement of application latency.
for(const [name,length] of [['dvh',8],['tags',6]])run('ffmpeg',['-hide_banner','-loglevel','warning','-y','-i',`${out}/media/${name}.mp4`,'-vf',`setpts=${10/length}*PTS,fps=30`,'-t','10','-c:v','libx264','-crf','16','-g','15','-pix_fmt','yuv420p','-an',`${work}/assets/${name}-focus.mp4`],`focus-${name}`);
run('python',[`${tools}/compose-soundtrack.py`,`${work}/one-key.wav`,'--duration',String(time)],'compose-music');
const esc=s=>s.replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('"','&quot;');
fs.mkdirSync(`${work}/compositions`,{recursive:true});
const elements=scenes.map((s,i)=>{
  const attrs=`data-start="0" data-duration="${s.duration}"`;
  const schematic=`<div id="v0" class="clip file-stage" ${attrs} data-track-index="0">${['CT images','Structures','Dose','Plan'].map((label,k)=>`<div class="file-card" id="file-${k}"><svg viewBox="0 0 180 190" aria-hidden="true"><path class="sheet" d="M25 12 H120 L155 47 V170 H25 Z M120 12 V47 H155"/>${k===0?'<path d="M48 72H130V125H48Z M55 65H137 M62 58H144"/>':k===1?'<ellipse cx="89" cy="101" rx="37" ry="25"/><ellipse cx="96" cy="101" rx="18" ry="12"/>':k===2?'<circle cx="90" cy="101" r="35"/><circle cx="90" cy="101" r="23"/><circle cx="90" cy="101" r="10"/>':'<path d="M48 70H82 M48 82H93 M48 94H75 M48 106H90 M48 118H80 M130 70H103 M130 82H111 M130 94H98 M130 106H107 M130 118H102"/>'}</svg><span>${label}</span></div>`).join('')}<div class="connector"></div><span class="schema-note">Schematic file icons</span></div>`;
  const isVideo=['video','reveal','dvh','tags'].includes(s.kind);
  const rawMedia=s.kind==='schematic'?schematic:isVideo?`<video id="v${i}" ${attrs} data-track-index="0" data-media-start="${s.offset}" src="assets/${s.source}.mp4" muted playsinline preload="auto"></video>`:`<img id="v${i}" class="clip ${s.kind}" ${attrs} data-track-index="0" src="assets/${s.source}" alt="${esc(s.title)}">`;
  const media=s.kind==='reveal'?'<div id="reveal-wrap" style="position:absolute;inset:0">'+rawMedia+'</div>':s.kind==='tags'?'<div class="tag-crop">'+rawMedia+'</div>':rawMedia;
  const press=s.kind==='key'?`<div id="press" class="clip press" ${attrs} data-track-index="2">SPACE</div><div id="pulse" class="clip pulse" ${attrs} data-track-index="3"></div>`:'';
  const requirements=i===19?'<div class="install-now">You can install it now on any Windows PC.</div><small>Windows installer + source<br>Requires QuickLook for Windows · .NET Framework 4.8 · Direct3D 11</small>':i===15?'<div class="tag-copy">Expand the tree yourself.<br>Search opens matching branches.<br>Clear search. Keep your place.</div><small>Searches retained text; binary payloads are excluded.<br>Actual viewer capture · enlarged detail</small>':'';
  const motion=s.kind==='schematic'?"tl.from('.file-card',{y:48,opacity:0,stagger:.24,duration:.6,ease:'power3.out'},.35);tl.from('.connector',{scaleX:0,transformOrigin:'left',duration:.85,ease:'power2.inOut'},1.65);tl.to('.file-card',{y:-12,stagger:.06,duration:.7,ease:'sine.inOut'},2.8);":s.kind==='key'?"tl.from('#v1',{opacity:0,scale:1.05,duration:.4,ease:'power2.out'},0);tl.from('#press',{y:30,opacity:0,duration:.3},.2);tl.to('#press',{y:8,scale:.97,backgroundColor:'#64b5f6',color:'#101316',duration:.16},1.1);tl.to('#press',{y:0,scale:1,duration:.24},1.27);tl.fromTo('#pulse',{scale:.1,opacity:0},{scale:1,opacity:.8,duration:.3},1.1);tl.to('#pulse',{scale:2.4,opacity:0,duration:.5},1.41);":s.kind==='reveal'?"tl.from('#reveal-wrap',{clipPath:'inset(43% 42% round 24px)',scale:.94,duration:.8,ease:'power3.inOut'},0);":'';
  fs.writeFileSync(`${work}/compositions/scene-${i}.html`,`<template id="scene-${i}-template"><div data-composition-id="scene-${i}" data-width="1600" data-height="900">${media}${press}<div id="c${i}" class="clip caption" ${attrs} data-track-index="1"><h1>${esc(s.title)}</h1><p>${esc(s.detail)}</p>${requirements}<span class="count">${String(i+1).padStart(2,'0')} / ${scenes.length}</span></div><script>window.__timelines=window.__timelines||{};{const tl=gsap.timeline({paused:true});tl.to({},{duration:${s.duration}},0);tl.from('#c${i} h1, #c${i} p',{y:10,opacity:0,duration:.32,ease:'power2.out'},0);${motion}window.__timelines['scene-${i}']=tl;}</script></div></template>`);
  return `<div id="s${i}" data-composition-id="scene-${i}" data-composition-src="compositions/scene-${i}.html" data-start="${s.start}" data-duration="${s.duration}" data-track-index="${i}"></div>`;
}).join('\n');
const animation='';
fs.writeFileSync(`${work}/index.html`,`<!doctype html><html lang="en"><head><meta charset="utf-8"><title>DICOM RT — one key, the whole picture</title><script src="assets/gsap.min.js"></script><style>
@font-face{font-family:Bahnschrift;src:local('Bahnschrift')}*{box-sizing:border-box}html,body{margin:0;width:1600px;height:900px;overflow:hidden;background:#101316;color:#f0f4f8;font-family:'Segoe UI',sans-serif}#film{position:relative;width:1600px;height:900px}video,img{position:absolute;left:114px;top:0;width:1372px;height:771.75px;object-fit:contain}img.art{left:0;width:1600px;object-fit:cover} .caption{position:absolute;left:0;top:772px;width:1600px;height:128px;background:#101316;display:grid;grid-template-columns:1fr auto;grid-template-rows:auto auto;align-items:center;padding:8px 36px 9px;column-gap:16px;border-top:1px solid #2d3a47}.copy{flex:1;min-width:0}h1{font-family:Bahnschrift,'Segoe UI',sans-serif;font-size:38px;font-weight:600;line-height:1.1;margin:0 0 5px;letter-spacing:-.3px}p{color:#a7b6c6;font-size:23px;line-height:1.25;margin:0}.count{grid-column:2;grid-row:1 / 3;font-size:15px;color:#64b5f6;white-space:nowrap}
#c0,#c1,#c19{top:92px;left:88px;width:1300px;height:230px;display:flex;flex-direction:column;align-items:flex-start;justify-content:center;padding:0;border:0;background:transparent}#c0 h1,#c1 h1,#c19 h1{font-size:64px;max-width:1100px;white-space:pre-line;line-height:1.08;margin-bottom:24px}#c0 p,#c1 p,#c19 p{font-size:30px;white-space:pre-line;line-height:1.6;color:#64b5f6}#c0 .count,#c1 .count,#c19 .count{display:none}#c1{top:100px}#c19{top:180px;height:500px}#c19 small{font-size:21px;color:#b6c2cf;line-height:1.6;margin-top:22px}#v19{opacity:.38}img.key{left:0;top:0;width:1600px;height:900px;object-fit:cover;opacity:.7}.file-stage{position:absolute;inset:0;display:flex;align-items:center;justify-content:center;gap:100px;padding-top:100px}.file-card{width:210px;text-align:center;z-index:1;background:#101316}.file-card svg{width:180px;height:210px;fill:none;stroke:#64b5f6;stroke-width:2.5;stroke-linejoin:round}.file-card .sheet{stroke:#8496a9}.file-card span{display:block;font-size:29px;margin-top:20px}.connector{position:absolute;left:330px;right:330px;top:478px;height:2px;background:#35516d}.schema-note{position:absolute;left:88px;bottom:38px;font-size:18px;color:#8496a9}.press{position:absolute;left:88px;top:450px;width:460px;height:130px;border:2px solid #64b5f6;border-radius:18px;display:flex;align-items:center;justify-content:center;font-size:38px;letter-spacing:7px;background:#18212c;box-shadow:0 12px 0 #071019}.pulse{position:absolute;left:247px;top:443px;width:145px;height:145px;border:2px solid #64b5f6;border-radius:50%;opacity:0;pointer-events:none}
#v0,#v19{left:0;top:0;width:1600px;height:900px;object-fit:cover}#v0{opacity:1}#v19{opacity:.38}
#c0{left:72px;top:220px;width:700px;height:440px}#c0 h1{font-size:64px;max-width:700px}#c0 p{font-size:26px}
#c1{top:100px;height:300px}#c1 h1{white-space:pre-line;font-size:60px;max-width:800px}
#v12{left:32px;top:96px;width:1536px;height:672px;object-fit:fill;object-position:center;clip-path:inset(0);}
#c12{top:0;height:96px;padding:12px 40px}#c12 h1{font-size:40px}#c12 p{font-size:23px}#c12 .count{display:none}
#v12{height:864px;top:-5px;clip-path:inset(110px 0 38px 0)}#c12{z-index:3;display:flex;flex-direction:column;align-items:flex-start;justify-content:center}
#c15,#c16{left:70px;top:130px;width:720px;height:570px;display:flex;flex-direction:column;align-items:flex-start;justify-content:center;border:0;padding:0;background:transparent}
#c15 h1,#c16 h1{font-size:68px;line-height:1.1;white-space:pre-line;margin-bottom:28px}#c15 p,#c16 p{font-size:28px;max-width:600px;line-height:1.5;color:#b6c2cf}#c15 .count,#c16 .count{display:none}
.tag-copy{font-size:27px;line-height:1.8;margin-top:30px;color:#f0f4f8}#c15 small{font-size:17px;color:#8496a9;line-height:1.5;margin-top:34px}
.tag-crop{position:absolute;left:906px;top:46px;width:582px;height:752px;overflow:hidden;border:1px solid #35516d;border-radius:8px;background:#101316}
.tag-crop video{position:absolute;left:-2131.8px;top:-71.4px;width:2720px;height:1530px;object-fit:fill}
#v16{left:870px;top:240px;width:640px;height:434px;object-fit:contain}
#c19{left:76px;top:125px;width:1450px;height:620px}#c19 h1{font-size:78px;line-height:1.06;max-width:950px;margin-bottom:32px}
#c19 p{font-size:40px;color:#83c8ff;white-space:nowrap;font-weight:600}.install-now{font-size:30px;margin-top:32px;color:#f0f4f8}#c19 small{margin-top:28px;font-size:19px;line-height:1.7}
</style></head><body><div id="film" data-composition-id="feature-tour" data-start="0" data-duration="${time}" data-width="1600" data-height="900">${elements}<audio id="soundtrack" src="one-key.wav" data-start="0" data-duration="${time}" data-track-index="21" data-volume="1"></audio></div><script>window.__timelines=window.__timelines||{};const tl=gsap.timeline({paused:true});tl.to({},{duration:${time}},0);${animation}window.__timelines['feature-tour']=tl;</script></body></html>`);
fs.writeFileSync(`${work}/hyperframes.json`,JSON.stringify({media:{autoProxy:false}},null,2));
fs.writeFileSync(`${work}/meta.json`,JSON.stringify({id:'feature-tour',name:'DICOM RT — one key, the whole picture'},null,2));
run(process.execPath,[cli,'lint',work,'--json'],'lint');
if(process.argv.includes('--prepare')){console.log(`Prepared ${time}s film`);process.exit(0);}
run(process.execPath,[cli,'render',work,'--output',`${work}/silent-film.mp4`,'--fps','30','--quality','high','--workers','2','--video-frame-format','png','--strict-all','--frames-cache-dir',`${work}/frames-cache`],'render');
run('ffmpeg',['-hide_banner','-loglevel','warning','-y','-i',`${work}/silent-film.mp4`,'-i',`${work}/one-key.wav`,'-map','0:v:0','-map','1:a:0','-c:v','copy','-c:a','aac','-b:a','192k','-af','loudnorm=I=-20:TP=-2:LRA=8','-t',String(time),'-movflags','+faststart',`${out}/media/feature-tour.mp4`],'soundtrack-mux');
run('ffmpeg',['-hide_banner','-loglevel','warning','-y','-i',`${out}/media/feature-tour.mp4`,'-t','16','-c:v','libx264','-preset','medium','-crf','17','-an','-movflags','+faststart',`${out}/media/hero-loop.mp4`],'hero');
const probe=JSON.parse(run('ffprobe',['-v','error','-show_entries','format=duration,size:stream=codec_name,width,height,r_frame_rate,nb_frames','-of','json',`${out}/media/feature-tour.mp4`],'probe'));
if(Math.abs(+probe.format.duration-time)>.06||probe.streams[0].width!==1600||!probe.streams.some(s=>s.codec_name==='aac'))throw new Error('Film metadata mismatch');
fs.writeFileSync(`${base}/film-verification.json`,JSON.stringify(probe,null,2));
console.log(`VERIFIED ${time}s, 1600x900, 30fps HyperFrames feature film`);
