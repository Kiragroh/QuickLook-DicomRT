import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import {fileURLToPath,pathToFileURL} from 'node:url';
import puppeteer from 'puppeteer-core';
const repo=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..');
const source=path.join(repo,'artifacts/release/QuickLook-DicomRT-Feature-Tour-0.2.6-Standalone.html');
const isolated=fs.mkdtempSync(path.join(os.tmpdir(),'dicomrt-standalone-'));
const html=path.join(isolated,'presentation.html');fs.copyFileSync(source,html);
const qa=path.join(repo,'artifacts/presentation/v025-tour/qa');
const manifest=JSON.parse(fs.readFileSync(path.join(repo,'artifacts/presentation/v025-tour/output/tour-assets.json')));
const cache=path.join(process.env.USERPROFILE,'.cache/puppeteer/chrome');
const version=fs.readdirSync(cache).sort((a,b)=>parseInt(b.slice(6))-parseInt(a.slice(6)))[0];
const browser=await puppeteer.launch({executablePath:path.join(cache,version,'chrome-win64/chrome.exe'),headless:true,args:['--autoplay-policy=no-user-gesture-required']});
const page=await browser.newPage(),failures=[],results=[];
page.on('pageerror',e=>failures.push(e.message));
await page.setRequestInterception(true);
const allowedFile=pathToFileURL(html).href;
page.on('request',r=>{
  const url=r.url();
  if(url.split('#')[0]===allowedFile||/^(blob:|data:|about:)/.test(url))r.continue();
  else {failures.push('External dependency attempted: '+url.slice(0,200));r.abort();}
});
await page.setOfflineMode(true);
try{
  await page.setViewport({width:1600,height:1000});
  await page.goto(allowedFile,{waitUntil:'load',timeout:60000});
  await page.waitForSelector('#explore');
  await page.screenshot({path:path.join(qa,'standalone-opening.png')});
  for(const name of manifest.required){
    const result=await page.evaluate(async name=>{
      const url=mediaUrl(name);
      if(!url.startsWith('blob:'))throw new Error('Non-embedded media '+name);
      if(name.endsWith('.mp4')){
        const video=document.createElement('video');video.preload='metadata';video.src=url;
        await new Promise((resolve,reject)=>{video.onloadedmetadata=resolve;video.onerror=()=>reject(new Error('Video decode failed '+name));});
        const result={name,width:video.videoWidth,duration:video.duration};video.removeAttribute('src');video.load();return result;
      }
      const image=new Image();image.src=url;await image.decode();return {name,width:image.naturalWidth};
    },name);
    if(!result.width)failures.push('Asset decode failed: '+name);
    results.push(result);
  }
  for(let chapter=0;chapter<12;chapter++){
    await page.evaluate(n=>show(n),chapter);
    await page.waitForFunction(()=>[...document.querySelectorAll('main img')].every(i=>i.complete&&i.naturalWidth>0));
    if(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth))failures.push('Overflow: '+chapter);
  }
  await page.click('#closing-film');
  await page.waitForFunction(()=>document.getElementById('zoom-video').readyState>=2,{timeout:30000});
  await page.evaluate(()=>new Promise(resolve=>{const v=document.getElementById('zoom-video');v.addEventListener('seeked',resolve,{once:true});v.currentTime=100;}));
  await new Promise(resolve=>setTimeout(resolve,350));
  const film=await page.evaluate(()=>{const v=document.getElementById('zoom-video');return {duration:v.duration,position:v.currentTime,blob:v.currentSrc.startsWith('blob:'),audioBytes:v.webkitAudioDecodedByteCount,download:document.getElementById('download').download};});
  if(film.duration!==112||film.position<100||!film.blob||!film.download.endsWith('.mp4'))failures.push('Embedded film seek/download failed');
  if(typeof film.audioBytes==='number'&&film.audioBytes===0)failures.push('Film audio was not decoded');
  results.push({film});await page.screenshot({path:path.join(qa,'standalone-film.png')});
  await page.keyboard.press('Escape');
  await page.setViewport({width:430,height:932});await page.evaluate(()=>show(0));
  if(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth))failures.push('Mobile overflow');
  await page.screenshot({path:path.join(qa,'standalone-mobile.png')});
  // Opaque-origin iframe resembles a restrictive portal preview. No server/assets.
  await page.goto('about:blank');
  await page.setContent('<iframe id="portal" sandbox="allow-scripts" style="width:100%;height:850px;border:0"></iframe>');
  await page.$eval('#portal',(frame,html)=>{frame.srcdoc=html;},fs.readFileSync(html,'utf8'));
  const frame=await (await page.$('#portal')).contentFrame();
  await frame.waitForSelector('#explore',{timeout:60000});
  await frame.click('#explore');
  const position=await frame.$eval('#position',e=>e.textContent);
  if(!position.startsWith('02'))failures.push('Sandboxed frame navigation failed');
  await frame.evaluate(()=>show(11));await frame.click('#closing-film');
  await frame.waitForFunction(()=>document.getElementById('zoom-video').readyState>=1,{timeout:30000});
  results.push({sandboxedFrame:{position,filmDuration:await frame.$eval('#zoom-video',v=>v.duration)}});
  fs.writeFileSync(path.join(qa,'standalone-verification.json'),JSON.stringify({isolated,bytes:fs.statSync(html).size,results,failures},null,2));
  if(failures.length)throw new Error(failures.join('\n'));
  console.log('PASS: 33 embedded assets, 12 slides, offline film/audio/seek, mobile, sandboxed iframe; no external asset requests');
}finally{await browser.close();}
