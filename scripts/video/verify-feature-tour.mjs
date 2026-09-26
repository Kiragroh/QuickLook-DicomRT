import fs from 'node:fs';
import path from 'node:path';
import http from 'node:http';
import {fileURLToPath} from 'node:url';
import puppeteer from 'puppeteer-core';
const repo=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..');
const base=path.join(repo,'artifacts/presentation/v025-tour');
const root=path.join(base,'output'),qa=path.join(base,'qa');fs.mkdirSync(qa,{recursive:true});
const mime={'.html':'text/html','.js':'text/javascript','.css':'text/css','.png':'image/png','.mp4':'video/mp4','.json':'application/json'};
const server=http.createServer((req,res)=>{
 const p=path.resolve(root,'.'+decodeURIComponent(new URL(req.url,'http://localhost').pathname));
 if(!p.startsWith(root+path.sep)&&p!==root){res.writeHead(403).end();return;}
 const f=p===root?path.join(p,'index.html'):p;
 if(!fs.existsSync(f)||!fs.statSync(f).isFile()){res.writeHead(404).end();return;}
 const n=fs.statSync(f).size;const match=/bytes=(\d+)-(\d*)/.exec(req.headers.range||'');
 res.setHeader('Content-Type',mime[path.extname(f)]||'application/octet-stream');res.setHeader('Accept-Ranges','bytes');
 if(match){const start=+match[1],end=Math.min(match[2]?+match[2]:n-1,n-1);res.writeHead(206,{'Content-Range':`bytes ${start}-${end}/${n}`,'Content-Length':end-start+1});fs.createReadStream(f,{start,end}).pipe(res);}
 else{res.writeHead(200,{'Content-Length':n});fs.createReadStream(f).pipe(res);}
});await new Promise(r=>server.listen(0,'127.0.0.1',r));
const cache=path.join(process.env.USERPROFILE,'.cache/puppeteer/chrome');
const versions=fs.readdirSync(cache).sort((a,b)=>parseInt(b.slice(6))-parseInt(a.slice(6)));
const browser=await puppeteer.launch({executablePath:path.join(cache,versions[0],'chrome-win64/chrome.exe'),headless:true,args:['--autoplay-policy=no-user-gesture-required']});
const page=await browser.newPage(),failures=[],logs=[];page.on('pageerror',e=>failures.push(e.message));page.on('response',r=>{if(r.status()>=400&&!r.url().endsWith('/favicon.ico'))failures.push(`${r.status()} ${r.url()}`);});
const url=process.env.FEATURE_TOUR_URL||`http://127.0.0.1:${server.address().port}/index.html`;
try{
 for(const [w,h] of [[1600,1000],[1280,720],[430,932]]){
  await page.setViewport({width:w,height:h});await page.goto(url,{waitUntil:'networkidle0'});
  await new Promise(r=>setTimeout(r,450));
  await page.waitForFunction(()=>[...document.querySelectorAll('main video')].every(v=>v.readyState>=1),{timeout:15000});
  const result=await page.evaluate(()=>({title:document.title,width:innerWidth,scrollWidth:document.documentElement.scrollWidth,video:[...document.querySelectorAll('video')].map(v=>({src:v.getAttribute('src')||v.querySelector('source')?.getAttribute('src'),ready:v.readyState,width:v.videoWidth,paused:v.paused})),images:[...document.images].filter(i=>!i.complete||i.naturalWidth===0).map(i=>i.src)}));
  if(result.scrollWidth>w)failures.push(`Horizontal overflow ${w}`);if(result.images.length)failures.push(...result.images);logs.push(result);
  await page.screenshot({path:path.join(qa,`hero-${w}.png`),fullPage:true});
  if(await page.$('main video'))failures.push('Opening must not autoplay a video');
  if(await page.$('#watch-film'))failures.push('Film action must be reserved for the final chapter');
  const artLoaded=await page.evaluate(async()=>{const image=new Image();image.src='art/space-magic.png';await image.decode();return image.naturalWidth>0;});
  if(!artLoaded)failures.push('Opening artwork missing');
 }
 await page.setViewport({width:1600,height:1000});await page.goto(url,{waitUntil:'networkidle0'});
 for(let i=0;i<13;i++){
  await page.keyboard.press('ArrowRight');await new Promise(r=>setTimeout(r,150));
  logs.push(await page.evaluate(()=>({hash:location.hash,title:document.title,missing:[...document.images].filter(i=>i.offsetParent&&!i.naturalWidth).map(i=>i.src),overflow:document.documentElement.scrollWidth>innerWidth})));
  if([1,3,5,8,11].includes(i))await page.screenshot({path:path.join(qa,`slide-${i+2}.png`),fullPage:true});
 }
 for(const l of logs){if(l.missing?.length)failures.push(...l.missing);if(l.overflow)failures.push(`Slide overflow ${l.hash}`);}
 await page.click('#closing-film');await page.waitForFunction(()=>document.querySelector('#zoom-video').readyState>=1,{timeout:15000});
 const film=await page.evaluate(()=>{const v=document.querySelector('#zoom-video');return {open:document.querySelector('dialog').open,duration:v.duration,width:v.videoWidth,source:v.currentSrc};});
 if(!film.open||film.width!==1600||Math.abs(film.duration-112)>.1)failures.push('Film dialog/metadata mismatch');logs.push({film});
 await page.keyboard.press('Escape');if(await page.$eval('dialog',d=>d.open))failures.push('Escape did not close film');
 await page.emulateMediaFeatures([{name:'prefers-reduced-motion',value:'reduce'}]);await page.goto(url,{waitUntil:'networkidle0'});
 await page.click('#explore');if(await page.evaluate(()=>location.hash)!=='#2')failures.push('Explore button did not advance');
 await page.keyboard.press('ArrowRight');await page.waitForFunction(()=>document.querySelector('main video')?.readyState>=1,{timeout:15000});
 await page.click('#motion');if(!await page.$eval('main video',v=>v.paused))failures.push('Explicit Pause should stop chapter playback');
 await page.goto('file:///'+path.join(root,'index.html').replaceAll('\\','/'),{waitUntil:'networkidle0'});
 const offlineArt=await page.evaluate(async()=>{const image=new Image();image.src='art/space-magic.png';await image.decode();return {width:image.naturalWidth,videoCount:document.querySelectorAll('main video').length};});
 logs.push({offlineArt});if(!offlineArt.width||offlineArt.videoCount)failures.push('Offline opening artwork failed');
 await page.keyboard.press('ArrowRight');await page.keyboard.press('ArrowRight');
 await page.waitForFunction(()=>document.querySelector('main video')?.readyState>=1,{timeout:15000});
 logs.push({offlineFile:await page.$eval('main video',v=>({width:v.videoWidth,ready:v.readyState}))});
 fs.writeFileSync(path.join(qa,'browser-verification.json'),JSON.stringify({logs,failures},null,2));
 if(failures.length)throw new Error(failures.join('\n'));
 console.log('PASS: desktop, compact and mobile slides, local images/video, keyboard navigation, no JavaScript/network errors');
}finally{await browser.close();await new Promise(r=>server.close(r));}
