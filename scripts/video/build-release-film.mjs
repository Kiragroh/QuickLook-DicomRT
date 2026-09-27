import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {spawnSync} from 'node:child_process';
const tools=path.dirname(fileURLToPath(import.meta.url)),repo=path.resolve(tools,'../..'),base=path.join(repo,'artifacts/presentation/v0220'),capture=path.join(base,'capture'),out=path.join(base,'output'),work=path.join(base,'film');
const cli=path.join(tools,'node_modules/hyperframes/bin/hyperframes.mjs');
for(const p of [work,work+'/assets',work+'/compositions',work+'/logs',out+'/media'])fs.mkdirSync(p,{recursive:true});
const env={...process.env,HYPERFRAMES_NO_TELEMETRY:'1',HYPERFRAMES_NO_UPDATE_CHECK:'1'};
function run(exe,args,name){const fd=fs.openSync(`${work}/logs/${name}.log`,'w');const result=spawnSync(exe,args,{cwd:work,env,stdio:['ignore',fd,fd],windowsHide:true});fs.closeSync(fd);if(result.status!==0)throw Error(name+': '+fs.readFileSync(`${work}/logs/${name}.log`,'utf8').slice(-2200));}
const scenes=[
 {time:4,file:'hero-quad-fields.png',title:'Select. Space. See the plan.',detail:'DICOM RT for QuickLook  /  Windows  /  No TPS import.',kind:'image'},
 {time:4,file:'mpr.mp4',title:'Four views. One position.',detail:'Images + dose + structures + active fields.',kind:'video'},
 {time:4,file:'mlc.mp4',title:'MLC meets anatomy.',detail:'CT-derived DRR  /  projected outlines  /  clear structure selection.',kind:'video'},
 {time:4,file:'orbit.mp4',title:'The plan, in 3D.',detail:'Shared control points  /  field paths  /  moving aperture.',kind:'video'},
 {time:4,file:'dvh.mp4',title:'DVH. Focus. Export.',detail:'Inspect curves and dose metrics. Export what you need.',kind:'video'},
 {time:4,file:'tags.mp4',title:'Full DICOM tag search.',detail:'Find nested values. Copy details. Keep your place.',kind:'video'},
 {time:6,file:'hero-quad-fields.png',title:'Install it now on your Windows PC.',detail:'github.com/Kiragroh/QuickLook-DicomRT',kind:'ending'}
];
let at=0;for(const s of scenes){s.start=at;at+=s.time}if(at!==30)throw Error('Duration must be exactly 30 seconds');
if(!process.argv.includes('--render-only')){
 for(const [name,count] of [['mpr',60],['orbit',60],['mlc',60],['dvh',120],['tags',90]]){
  const frames=fs.readdirSync(capture+'/'+name).filter(f=>/^frame-\d{3}\.png$/.test(f));if(frames.length!==count)throw Error('Incomplete '+name);
  run('ffmpeg',['-hide_banner','-loglevel','warning','-y','-framerate','15','-i',`${capture}/${name}/frame-%03d.png`,'-frames:v',String(count),'-c:v','libx264','-preset','medium','-crf','17','-pix_fmt','yuv420p','-an','-movflags','+faststart',`${out}/media/${name}.mp4`],'encode-'+name);
  // Film timings are editorial. DVH and tag search use the clearest relevant section.
  const trim=name==='dvh'?['-ss','1']:name==='tags'?['-ss','1.4']:[];
  run('ffmpeg',['-hide_banner','-loglevel','warning','-y',...trim,'-i',`${out}/media/${name}.mp4`,'-t','4','-vf',name==='tags'?'crop=560:820:1040:40,scale=546:800:flags=lanczos,pad=1600:900:980:0:color=0x0c1015':'scale=1422:800:flags=lanczos,pad=1600:900:89:0:color=0x0c1015','-c:v','libx264','-crf','16','-an',`${work}/assets/${name}.mp4`],'film-'+name);
 }
 fs.copyFileSync(capture+'/hero-quad-fields.png',work+'/assets/hero-quad-fields.png');
 fs.copyFileSync(tools+'/node_modules/gsap/dist/gsap.min.js',work+'/assets/gsap.min.js');
 run('python',[tools+'/compose-soundtrack.py',base+'/one-key.wav','--duration','30'],'music');
}
const esc=s=>s.replaceAll('&','&amp;').replaceAll('<','&lt;').replace(/[^\x00-\x7f]/g,c=>'&#'+c.codePointAt(0)+';');
const children=scenes.map((s,i)=>{
 const a=`data-start="0" data-duration="${s.time}"`;
 const image=s.kind==='video'?`<video id="media${i}" class="clip footage" ${a} data-track-index="0" src="assets/${s.file}" muted playsinline></video>`:`<img id="media${i}" class="clip footage ${s.kind==='ending'?'backdrop':''}" ${a} data-track-index="0" src="assets/${s.file}">`;
 const caption=s.kind==='ending'?`<div id="ending" class="clip end" ${a} data-track-index="1"><span>AVAILABLE NOW  /  OPEN SOURCE</span><h1>${esc(s.title)}</h1><p>${esc(s.detail)}</p><div class="download">Windows installer + source + feature tour</div><small>Requires QuickLook for Windows  /  .NET Framework<br>Research and inspection preview</small></div>`:s.file==='tags.mp4'?`<div id="tag-title" class="clip tagtitle" ${a} data-track-index="1"><span>BEYOND THE IMAGE</span><h1>Full DICOM<br>tag search.</h1><p>Numbers. Names. Values.<br>Nested sequences.</p><small>Double-click to copy.<br>Clear search. Keep your place.</small></div>`:`<div id="cap${i}" class="clip caption" ${a} data-track-index="1"><h1>${esc(s.title)}</h1><p>${esc(s.detail)}</p></div>`;
 fs.writeFileSync(`${work}/compositions/s${i}.html`,`<template id="s${i}-template"><div data-composition-id="s${i}" data-width="1600" data-height="900">${image}${caption}<script>window.__timelines=window.__timelines||{};{const tl=gsap.timeline({paused:true});tl.to({},{duration:${s.time}},0);tl.from('#cap${i} h1,#cap${i} p',{y:8,opacity:0,duration:.25},0);window.__timelines.s${i}=tl;}</script></div></template>`);
 return `<div id="scene-${i}" data-composition-id="s${i}" data-composition-src="compositions/s${i}.html" data-start="${s.start}" data-duration="${s.time}" data-track-index="${i}"></div>`;
}).join('');
fs.writeFileSync(work+'/index.html',`<!doctype html><html><head><meta charset="utf-8"><script src="assets/gsap.min.js"></script><style>*{box-sizing:border-box}html,body{margin:0;width:1600px;height:900px;background:#0c1015;color:#eef6ff;font-family:'Segoe UI',sans-serif;overflow:hidden}.footage{position:absolute;left:0;top:0;width:1600px;height:900px;object-fit:contain}img.footage{width:1422px;height:800px;left:89px}.caption{position:absolute;top:800px;left:0;width:1600px;height:100px;background:#0c1015;padding:14px 55px;display:flex;align-items:center;gap:40px;border-top:2px solid #355976}h1{font-size:36px;line-height:1.1;letter-spacing:-1px;margin:0}.caption p{font-size:22px;color:#b4c9da;margin-left:auto;max-width:750px}.backdrop{opacity:.13;filter:blur(4px)}.end{position:absolute;inset:0;padding:150px 90px;background:#0c1015c9}.end span,.tagtitle span{font-size:20px;color:#72c3ff;letter-spacing:4px}.end h1{font-size:60px;max-width:1100px;margin-top:35px}.end p{font-size:48px;color:#8bccff;font-weight:600;letter-spacing:-1px;margin-top:40px}.download{font-size:27px;color:#d0e3f3;margin-top:35px}.end small{display:block;font-size:20px;line-height:1.6;color:#a2b4c4;margin-top:65px}.tagtitle{position:absolute;left:85px;top:130px;width:750px}.tagtitle h1{font-size:85px;margin:45px 0}.tagtitle p{font-size:34px;color:#c3d7e7;line-height:1.5}.tagtitle small{font-size:26px;color:#9cb4c8;line-height:1.6}</style></head><body><main id="film" data-composition-id="main" data-start="0" data-duration="30" data-width="1600" data-height="900" data-fps="30">${children}</main><script>window.__timelines=window.__timelines||{};window.__timelines.main=gsap.timeline({paused:true}).to({},{duration:30});</script></body></html>`);
fs.writeFileSync(work+'/hyperframes.json',JSON.stringify({media:{autoProxy:false}}));fs.writeFileSync(base+'/storyboard.json',JSON.stringify({duration:30,bpm:120,scenes,source:'Approved public nonpatient benchmark; current actual viewer captures; editorial timing, not latency measurement'},null,2));
run(process.execPath,[cli,'lint',work,'--json'],'lint');
run(process.execPath,[cli,'render',work,'--output',work+'/silent.mp4','--fps','30','--quality','high','--workers','2','--video-frame-format','png','--strict-all','--frames-cache-dir',work+'/frames-cache'],'render');
run('ffmpeg',['-hide_banner','-loglevel','warning','-y','-i',work+'/silent.mp4','-i',base+'/one-key.wav','-map','0:v','-map','1:a','-c:v','copy','-c:a','aac','-b:a','192k','-af','loudnorm=I=-15:TP=-1.5:LRA=7','-t','30','-movflags','+faststart',out+'/media/announcement-30s.mp4'],'mux');
console.log('BUILT 30-second announcement film');
