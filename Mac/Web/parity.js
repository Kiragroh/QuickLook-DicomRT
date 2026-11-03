'use strict';
// Completion of the original viewer's interactions. The original layout and numerical engine stay shared.
let workClock=0,contextWork=0,projectionWork=0,viewTouched=false,partialInteraction=false;
let doseStates=new Map(),dvhCaches=new Map(),dvhJobs=new Map(),displayDoseId=-1,dvhDoseId=-1;
let tagSourceId=-1,fieldRequest=0,trackRequest=null,contextCard=null,bufferRequest=null;
const preparedBeamVersions=new Map();
const engineRpc=rpc;
const doseMeta=id=>(model?.activeDoses||[]).find(d=>d.id===id);
const cloneDisplay=s=>({...s,levels:[...s.levels],hidden:[...s.hidden],colors:{...s.colors}});

class ProjectionFrames extends Map {
 constructor(budget=64*1024*1024,limit=24000){super();this.bytes=0;this.sizes=new Map();this.budget=budget;this.limit=limit;}
 measure(value){return (value.drr?.gray?.length||0)*5+(value.outlines||[]).reduce((n,o)=>n+o.boundary.reduce((sum,loop)=>sum+loop.length*16,0),0)+8192;}
 set(key,value){const bytes=this.measure(value);if(bytes>this.budget)return this;this.delete(key);while(this.size&&(this.size>=this.limit||this.bytes+bytes>this.budget))this.delete(this.keys().next().value);super.set(key,value);this.sizes.set(key,bytes);this.bytes+=bytes;return this;}
 delete(key){if(!super.has(key))return false;this.bytes-=this.sizes.get(key)||0;this.sizes.delete(key);return super.delete(key);}
 clear(){super.clear();this.sizes?.clear();this.bytes=0;}
}
class MeshFrames extends ProjectionFrames {constructor(){super(256*1024*1024,96);}measure(value){return ((value.positions?.length||0)+(value.normals?.length||0)+(value.indices?.length||0))*2+4096;}}
preparedViews=new ProjectionFrames();meshResults=new MeshFrames();
rpc=function(op,args={}){
 if(model&&!model.partial&&['beam','dvh','mesh','volume','doseReference','render','fields'].includes(op)){
  const group=op==='beam'&&(args.outlines||args.drr)?'projection':op==='dvh'?'dvh':'context';
  args={...args,workGroup:group,workRevision:group==='projection'?projectionWork:contextWork};
 }
 return engineRpc(op,args);
};
function advanceGroup(group,revision){engineRpc('cancelWork',{group,revision}).catch(()=>{});}
window.cancelProjectionWork=function(){projectionWork=++workClock;preparedBeamVersions.clear();bufferRequest=null;advanceGroup('projection',projectionWork);};

function saveDoseDisplay(){const state=doseStates.get(displayDoseId);if(state){state.display=cloneDisplay(isoState);state.manual=isoState.manualReference;}}
function doseReferenceValue(id){const meta=doseMeta(id),state=doseStates.get(id);if(!meta||!state)return null;return meta.units==='GY'?QuickLookRTDose.reference(state.info,isoState.preset,id===displayDoseId?isoState.manualReference:state.manual).value:meta.units==='RELATIVE'?100:null;}
window.visibleDoseOverlays=()=>[...(model?.activeDoses||[])].filter(d=>doseStates.get(d.id)?.visible&&d.registered).map(d=>({id:d.id,reference:doseReferenceValue(d.id)}));
window.doseSurfaceKey=(overlay,level)=>'dose:'+overlay.id+':'+String(isoState.absolute?level:level/100*overlay.reference);
window.changeDoseUnits=function(absolute){
 if(absolute===isoState.absolute)return;
 if(!model?.hasDose){preferences.relative=!absolute;savePreferences();return;}
 if(!isoState.reference||absolute&&visibleDoseOverlays().some(d=>doseMeta(d.id)?.units!=='GY')){error('Gy-Anzeige benötigt eine Dosisreferenz und ausschließlich sichtbare physikalische Gy-Dosen.');doseControls();return;}
 const convert=value=>Number((absolute?QuickLookRTDose.absolute(value,isoState.reference):QuickLookRTDose.percent(value,isoState.reference)).toPrecision(15));
 const colors={};for(const level of isoState.levels)colors[convert(level)]=window.isoLineColor(level);
 isoState.levels=isoState.levels.map(convert);isoState.hidden=isoState.hidden.map(convert);isoState.colors=colors;surfaceState.level=convert(surfaceState.level);isoState.absolute=absolute;preferences.relative=!absolute;savePreferences();doseControls();surfaceControls();surfaceAppearance();if(surfaceState.dose)loadSurfaces();scheduleRender();
};
const baseRenderOptions=window.renderOptions;
window.renderOptions=kind=>{const options=baseRenderOptions(kind),overlays=(model?.activeDoses||[]).map(d=>({id:d.id,visible:!!doseStates.get(d.id)?.visible&&d.registered,reference:doseReferenceValue(d.id)||0}));return{...options,overlays,wash:$('wash').checked&&overlays.some(d=>d.visible&&d.reference>0),isodoses:$('isodoses').checked&&overlays.some(d=>d.visible&&d.reference>0),absolute:isoState.absolute&&overlays.filter(d=>d.visible).every(d=>doseMeta(d.id)?.units==='GY')};};
const baseDoseControls=doseControls;
doseControls=function(){baseDoseControls();const root=$('doseControls'),doses=model?.activeDoses||[];if(doses.length>1){const choose=picker('Referenz bearbeiten',doses.map(d=>[d.id,d.label]),displayDoseId,id=>switchDoseDisplay(+id));choose.id='displayDosePicker';root.prepend(choose);}const list=$('doseOverlays');list.replaceChildren();for(const d of doses){const state=doseStates.get(d.id);if(!state)continue;const row=check(d.label,state.visible,value=>{state.visible=value;if(isoState.absolute&&visibleDoseOverlays().some(d=>doseMeta(d.id)?.units!=='GY'))changeDoseUnits(false);scheduleRender();surfaceAppearance();if(surfaceState.dose)loadSurfaces();});row.querySelector('input').disabled=!d.registered;row.querySelector('input').dataset.dose=d.id;row.title=d.registered?'Dosisgitter unabhängig ein- oder ausblenden':'Keine eindeutige Registrierung zur Bildserie';list.append(row,text('small','Dmax '+number(d.maximum,1)+' '+d.units+(d.registered?'':' · Keine Registrierung'),'muted'));}$('doseMaximum').disabled=!doses.some(d=>d.maximumPosition);};
function switchDoseDisplay(id){saveDoseDisplay();const state=doseStates.get(id);if(!state)return;displayDoseId=id;const meta=doseMeta(id);model.doseInfo={maximum:meta.maximum,units:meta.units,type:meta.type};if(state.display)isoState=cloneDisplay(state.display);else{isoState.referenceInfo=state.info;isoState.manualReference=state.manual;isoState.reference=meta.units==='RELATIVE'?100:null;isoState.absolute=meta.units==='GY'&&!preferences.relative;rebuildIsodoses();}isoState.referenceInfo=state.info;doseControls();surfaceControls();scheduleRender();}
loadDoseReference=async function(){
 if(!model?.hasDose||model.partial)return;const context=model;
 for(const d of model.activeDoses||[]){if(d.units!=='GY')continue;const task='reference:'+contextWork+':'+d.id;previewProgress.update(task,'Dosisreferenz D0,035 cm³',0,0,30);
  try{const info=await rpc('doseReference',{doseId:d.id});if(context!==model)return;const state=doseStates.get(d.id);state.info=info;if(displayDoseId===d.id){isoState.referenceInfo=info;refreshIsodoses();}else scheduleRender();}
  catch(e){if(context===model&&!e.cancelled)error(e);}finally{previewProgress.finish(task);}
 }
};
const changePresetBeforeParity=changeDosePreset;
changeDosePreset=function(key){saveDoseDisplay();for(const state of doseStates.values())state.display=null;changePresetBeforeParity(key);};

requestDvh=function(roi,background=false){
 const selected=dvhDoseId,context=model,cache=dvhCaches.get(selected)||new Map();dvhCaches.set(selected,cache);
 if(cache.has(roi.id))return Promise.resolve(cache.get(roi.id));const key=selected+':'+roi.id;if(dvhRequests.has(key))return dvhRequests.get(key);
 const requests=dvhRequests,task=rpc('dvh',{roi:roi.id,doseId:selected,...(background?{background:true,priority:'dvh'}:{})}).then(result=>{if(context===model){cache.set(roi.id,result);if(dvhDoseId===selected){dvhs=cache;if(view==='dvh')drawDvh();}}return result;}).finally(()=>requests.delete(key));requests.set(key,task);return task;
};
startAutomaticDvh=async function(){
 if(!preferences.autoDvh||!model?.hasDose||model.partial||dvhDoseId===-1||dvhJobs.has(dvhDoseId))return;
 const context=model,selected=dvhDoseId,rois=model.rois.filter(r=>r.category==='Target'||r.category==='Organ'),cache=dvhCaches.get(selected)||new Map(),id='autoDvh:'+contextWork+':'+selected;
 dvhCaches.set(selected,cache);const marker={};dvhJobs.set(selected,marker);previewProgress.update(id,'DVH Targets / Organe',0,rois.length,15);
 try{for(let i=0;i<rois.length;i++){if(context!==model||!preferences.autoDvh||selected!==dvhDoseId)return;await requestDvh(rois[i],true);if(context!==model)return;previewProgress.update(id,'DVH Targets / Organe',i+1,rois.length,15);}}
 catch(e){if(context===model)error(e);}finally{previewProgress.finish(id);if(dvhJobs.get(selected)===marker)dvhJobs.delete(selected);if(context===model&&selected===dvhDoseId){if(view==='dvh')drawDvh();invalidateProjections();if(view==='mlc')prepareProjections(preparationGeneration);}}
};
function chooseDvhDose(id){if(!doseMeta(id))return;generation++;dvhDoseId=id;dvhs=dvhCaches.get(id)||new Map();dvhCaches.set(id,dvhs);window.dvhFocus=null;drawDvh();startAutomaticDvh();}
const showDvhBeforeParity=showDvh;
showDvh=function(){showDvhBeforeParity();const bar=$('stage').querySelector('.dvhpanel .toolbar');if(model.activeDoses?.length){const selector=picker('DVH-Dosis',model.activeDoses.map(d=>[d.id,d.label]),dvhDoseId,id=>chooseDvhDose(+id));selector.id='dvhDosePicker';bar.prepend(selector);}};
exportCsv=async function(simple){try{const result=await rpc('save',{kind:'csv',doseId:dvhDoseId,filename:filename(simple?'DVH-curves':'DVH'),simple,rois:model.rois.filter(r=>!hidden.has(r.id)&&!xHidden(r)).map(r=>r.id)});if(result.saved){if($('modal').open)$('modal').close();status('DVH-CSV gespeichert');}}catch(e){error(e);}};

// Dedicated chart export retains all enabled structures and equal curve opacity.
function dvhReportCanvas(){
 const rois=model.rois.filter(r=>!hidden.has(r.id)&&!xHidden(r)),canvas=document.createElement('canvas');canvas.width=1500;canvas.height=Math.max(900,rois.length*98+150);const ctx=canvas.getContext('2d'),w=canvas.width,h=canvas.height;
 ctx.fillStyle='#111314';ctx.fillRect(0,0,w,h);ctx.fillStyle='#edf3f9';ctx.font='24px -apple-system';ctx.fillText('Cumulative DVH · active structures',24,42);const left=75,top=100,right=1030,bottom=h-85,curves=rois.map(roi=>({roi,...dvhs.get(roi.id)})),max=Math.max(1,...curves.map(c=>c.x?.at(-1)||0));ctx.font='14px -apple-system';
 for(let i=0;i<=5;i++){const y=top+(bottom-top)*i/5,x=left+(right-left)*i/5;ctx.strokeStyle='#30373c';ctx.beginPath();ctx.moveTo(left,y);ctx.lineTo(right,y);ctx.stroke();ctx.fillStyle='#a9b5bc';ctx.fillText((100-i*20)+'%',20,y+4);ctx.fillText(number(max*i/5,1),x,bottom+24);}
 ctx.fillText(doseMeta(dvhDoseId)?.units||'Dose',right-40,bottom+48);
 for(let i=0;i<curves.length;i++){const c=curves[i],y=top+i*98;ctx.fillStyle=c.roi.color;ctx.font='15px -apple-system';const name=window.roiLabel(c.roi);const lines=wrapCanvasText(ctx,name,390);for(let j=0;j<Math.min(2,lines.length);j++)ctx.fillText(lines[j],1070,y+j*18);ctx.fillStyle='#a9b5bc';ctx.font='12px -apple-system';if(!c.x){ctx.fillText('NotCalculated · no curve available',1070,y+40);continue;}
  ctx.fillText(number(c.volume,2)+' cm³ · '+c.units+' · '+c.status,1070,y+40);ctx.fillText('Mean '+number(c.mean,2)+' · Median '+number(c.median,2)+' · Min '+number(c.min,2),1070,y+58);ctx.fillText('Max '+number(c.max,2)+' · D98 '+number(c.d98,2)+' · D2 '+number(c.d2,2),1070,y+75);
  ctx.strokeStyle=c.roi.color;ctx.lineWidth=2;ctx.setLineDash(c.status==='PartialCoverage'?[6,4]:[]);ctx.beginPath();c.x.forEach((x,j)=>{const px=left+x/max*(right-left),py=top+(1-c.y[j]/100)*(bottom-top);j?ctx.lineTo(px,py):ctx.moveTo(px,py);});ctx.stroke();
 }
 ctx.setLineDash([]);ctx.fillStyle='#a9b5bc';ctx.font='13px -apple-system';ctx.fillText('Approximate inspection DVH · dashed curves: partial dose coverage · metrics require known coverage.',24,h-24);canvas._dvhReport=true;return canvas;
}
function wrapCanvasText(ctx,value,width){const words=String(value).split(/\s+/),lines=[];let line='';for(const word of words){if(line&&ctx.measureText(line+' '+word).width>width){lines.push(line);line=word;}else line+=(line?' ':'')+word;}if(line)lines.push(line);return lines;}
const savePngBeforeParity=savePng;
savePng=async function(canvas=null,name=view){if(canvas===dvhCanvas&&view==='dvh'){try{const report=dvhReportCanvas(),result=await rpc('save',{kind:'png',filename:filename('DVH'),caption:caption(),image:report.toDataURL('image/png')});if(result.saved)status('DVH-PNG gespeichert');}catch(e){error(e);}return;}return savePngBeforeParity(canvas,name);};

// Fast field geometry is drawn on the exact frame that produced the underlying image.
async function updateImageFields(){
 if(!model||model.partial)return;const request=++fieldRequest,context=model,position=cursorLocation(),frames=tiles().filter(t=>t._frame&&t.dataset.kind!=='3D').map(t=>({tile:t,frame:t._frame}));
 if(contextCard?.frame&&surfaceState.fields)frames.push({tile:contextCard.slice,frame:contextCard.frame});if(!frames.length)return;
 try{const result=await rpc('fields',{fields:surfaceState.fields,beam:neutralFields?-1:position?.beam??-1,cp:position?.local??0,frames:frames.map(({frame},i)=>({key:String(i),center:frame.center,right:frame.right,down:frame.down,widthMm:frame.widthMm,heightMm:frame.heightMm}))});if(request!==fieldRequest||context!==model)return;
  for(const item of result){const {tile,frame}=frames[+item.key];if(tile===contextCard?.slice){if(contextCard.frame!==frame)continue;frame.fields=item.fields;paintContextSlice();}else if(tile._frame===frame){frame.fields=item.fields;drawImage(tile.querySelector('canvas'),frame);}}
 }catch(e){if(context===model)error(e);}
}
async function loadFieldTracks(){if(lastTracks||trackRequest||!model?.beams.length||model.partial)return;const context=model,position=cursorLocation()||{beam:model.beams[0].id,local:0};const task=rpc('beam',{beam:position.beam,cp:position.local,drr:false,outlines:false,tracks:true,background:true});trackRequest=task;try{const result=await task;if(context===model){lastTracks=result.tracks;if(beam){beam.tracks=lastTracks;surfaceView?.setBeam(beam,surfaceState.fields,preferences.avatar);}}}catch(e){if(context===model)error(e);}finally{if(trackRequest===task)trackRequest=null;}}
let neutralFields=false;
const publishBeforeParity=publishBeam;
publishBeam=function(result){result.neutral=neutralFields;publishBeforeParity(result);updateImageFields();updateBeamControls();};
const baseCursorLocation=cursorLocation;
cursorLocation=function(value=+$('cp').value){if(+$('beam').value===-3)return null;return baseCursorLocation(value);};
function updateBeamControls(){const allowed=!!model?.beams.length&&['image','quad','mlc','3d'].includes(view)&&(view==='mlc'||surfaceState.fields);$('beamtools').classList.toggle('hidden',!allowed);$('play').disabled=neutralFields;$('cp').classList.toggle('hidden',neutralFields);$('cpreadout').classList.toggle('hidden',neutralFields);if(beam&&!neutralFields)$('cpreadout').textContent='CP '+number(beam.cp+1,1)+' / '+beam.count+' · '+(beam.modulation?.text||'');}
function timelineMarkers(){const prior=$('beamMarkers');prior?.remove();$('cp').removeAttribute('list');if(!model?.beams.length||+$('beam').value!==-2)return;const marks=document.createElement('datalist');marks.id='beamMarkers';let offset=0;for(const b of model.beams){offset+=b.count;const option=document.createElement('option');option.value=offset-1;option.label=b.label;marks.append(option);}$('cp').setAttribute('list','beamMarkers');$('beamtools').append(marks);}
const beamChangeBeforeParity=changeBeam;
changeBeam=function(){neutralFields=+$('beam').value===-3;if(neutralFields){pause();beamRevision++;cancelProjectionWork();if(beam){beam.neutral=true;surfaceView?.setBeam(beam,surfaceState.fields,preferences.avatar);}updateImageFields();updateBeamControls();return;}cancelProjectionWork();preparing=false;beamChangeBeforeParity();timelineMarkers();updateBeamControls();};$('beam').onchange=changeBeam;
$('restart').onclick=()=>{pause();neutralFields=false;$('beam').value=model.beams.length>1?'-2':String(model.beams[0]?.id??'');changeBeam();};
const fieldToggleBeforeParity=$('fieldsToggle').onchange;
$('fieldsToggle').onchange=()=>{fieldToggleBeforeParity();updateBeamControls();updateImageFields();updateMlcContext(tiles().find(t=>t.dataset.kind==='MLC')?.querySelector('canvas'));if(surfaceState.fields)loadFieldTracks();};
const surfaceControlsBeforeParity=surfaceControls;
surfaceControls=function(){surfaceControlsBeforeParity();for(const input of $('surfacebar').querySelectorAll('input[type=checkbox]')){if(input.parentElement.textContent.trim()!=='Felder')continue;input.onchange=()=>{surfaceState.fields=input.checked;$('fieldsToggle').checked=input.checked;surfaceAppearance();updateBeamControls();updateImageFields();};}};
$('beamtools').addEventListener('wheel',e=>{if(e.target.closest('select'))return;e.preventDefault();pause();const delta=wheelSteps($('beamtools'),e);if(delta)setCp(+$('cp').value+delta*(e.shiftKey?.1:1));},{passive:false});

window.playbackInterval=()=>$('drr').checked&&view==='mlc'&&preferences.progressiveDrr?1000/mlcState.speed:80;
advancePlayback=function(){
 if(!playing||neutralFields)return;const withDrr=$('drr').checked&&view==='mlc',recorded=withDrr&&preferences.progressiveDrr,step=recorded?1:mlcState.speed*.08,current=cursorLocation();if(!current)return;
 const active=model.beams.find(b=>b.id===current.beam),last=active.count-1;
 let next=current.local<last?Math.min(last,Math.round((Math.floor(Math.round(current.local/step,8))+1)*step*1e8)/1e8):null;
 if(next===null){const at=model.beams.findIndex(b=>b.id===current.beam),following=model.beams[(at+1)%model.beams.length];if(+$('beam').value===-2)next=(at+1)%model.beams.length===0?0:current.offset+active.count;else{$('beam').value=String(following.id);$('cp').max=Math.max(0,following.count-1);next=0;}}
 else next+=+$('beam').value===-2?current.offset:0;
 if(withDrr){
  let position=cursorLocation(next),ready=projectionReady(preparedViews.get(position.beam+':'+position.local));
  if(!ready&&preferences.progressiveDrr){const maximum=+$('cp').max;for(let distance=1;distance<=maximum;distance++){const candidate=(Math.ceil(next)+distance)%(maximum+1),where=cursorLocation(candidate);if(where&&projectionReady(preparedViews.get(where.beam+':'+where.local))){next=candidate;position=where;ready=true;break;}}}
  if(!preferences.progressiveDrr&&preparedBeamVersions.get(position.beam)!==projectionWork)ready=false;
  if(!ready){status('DRR-Puffer wird vorbereitet …');prepareProjections(preparationGeneration,position.beam);
   if(!preferences.progressiveDrr&&preparedBeamVersions.get(position.beam)===projectionWork&&!bufferRequest){const key=position.beam+':'+position.local,revision=projectionWork;bufferRequest=key;rpc('beam',{beam:position.beam,cp:position.local,...window.beamOptions(),drr:true,tracks:false,size:128}).then(result=>{if(revision===projectionWork){result.projectionComplete=true;preparedViews.set(key,result);}}).catch(error).finally(()=>{if(bufferRequest===key)bufferRequest=null;});}
   playTimer=setTimeout(advancePlayback,50);return;}

 }
 $('cp').value=next;updateBeam();
};
prepareProjections=async function(unused=null,targetBeam=null){
 if(!model?.beams.length||model.partial||preparing||view!=='mlc'||neutralFields)return;
 const doDrr=$('drr').checked||!preferences.drrOnDemand,doOutlines=mlcState.outlines;
 if(!doDrr&&!doOutlines)return;
 const context=model,revision=projectionWork,selected=+$('beam').value;
 const beams=!preferences.progressiveDrr&&doDrr?model.beams.filter(b=>b.id===(targetBeam??cursorLocation()?.beam)):selected===-2?model.beams:model.beams.filter(b=>b.id===selected);
 const views=beams.map(b=>{const points=[];
  if(!preferences.progressiveDrr&&doDrr){const step=mlcState.speed*.08;for(let cp=0;cp<b.count-1;cp=Math.min(b.count-1,Math.round((Math.floor(Math.round(cp/step,8))+1)*step*1e8)/1e8))points.push(cp);}
  else for(let step=8;step>=1;step/=2)for(let cp=0;cp<b.count;cp+=step)if(!points.includes(cp))points.push(cp);
  if(!points.includes(b.count-1))points.push(b.count-1);return{beam:b,points};
 });
 const phases=preferences.outlinesFirst&&doOutlines&&doDrr?[{outlines:true,drr:false},{outlines:false,drr:true}]:[{outlines:doOutlines,drr:doDrr}];
 const total=views.reduce((n,v)=>n+v.points.length,0)*phases.length,id='warm:'+revision;let done=0;preparing=true;
 try{for(const phase of phases)for(const {beam:b,points} of views){
  for(const cp of points){if(context!==model||revision!==projectionWork||view!=='mlc')return;const key=b.id+':'+cp,cached=preparedViews.get(key);
   const ready=cached&&(!phase.outlines||cached.outlinesComplete)&&(!phase.drr||cached.drr||cached.reason);
   if(!ready){const result=await rpc('beam',{beam:b.id,cp,...window.beamOptions(),...phase,size:128,tracks:false,background:true});if(context!==model||revision!==projectionWork)return;
    const combined={...cached,...result,outlines:phase.outlines?result.outlines:cached?.outlines||[],drr:phase.drr?result.drr:cached?.drr||null,outlinesComplete:!!phase.outlines||!!cached?.outlinesComplete,projectionComplete:true};preparedViews.set(key,combined);
   }
   previewProgress.update(id,phase.drr?'DRR-Puffer':'Konturpuffer',++done,total,1);
  }
  if(phase===phases.at(-1))preparedBeamVersions.set(b.id,revision);
 }}catch(e){if(context===model&&!e.cancelled)error(e);}finally{previewProgress.finish(id);if(revision===projectionWork)preparing=false;}
};

// The original MLC orientation context groups the axial field arrangement and the LINAC.
function removeContextCard(){if(contextCard){contextCard.rig.dispose();contextCard.card.remove();contextCard=null;}}
function clampContextCard(){if(!contextCard)return;const c=contextCard,host=c.card.parentElement,w=host.clientWidth,h=host.clientHeight;c.card.style.width=Math.max(150,Math.min(parseFloat(c.card.style.width)||300,540,w-16))+'px';if(c.x===null)c.x=w-c.card.offsetWidth-8;const x=Math.max(0,Math.min(c.x,w-c.card.offsetWidth)),y=Math.max(0,Math.min(c.y,h-c.card.offsetHeight));c.card.style.left=x+'px';c.card.style.top=y+'px';}
function paintContextSlice(){if(!contextCard?.frame)return;imageWithWindow(contextCard.canvas,contextCard.frame);drawFields(contextCard.canvas,contextCard.frame);}
window.updateMlcContext=function(canvas){
 if(!canvas||!beam||view!=='mlc'||model?.partial)return;
 if(contextCard?.host!==canvas.parentElement){removeContextCard();const card=text('div','','mlc-context'),header=text('div','','context-header'),title=text('span','ORIENTATION · ziehen'),reset=button('↺',()=>{contextCard.x=null;contextCard.y=8;card.style.width='300px';clampContextCard();}),slice=text('div','','context-slice'),sliceTitle=text('span','FIELD ARRANGEMENT · ISO','context-caption'),image=document.createElement('canvas'),note=text('small','','muted'),rigCanvas=document.createElement('canvas'),grip=text('button','◢','context-grip');
  reset.title='Größe und Position zurücksetzen';header.append(title,reset);slice.append(image,sliceTitle,note);rigCanvas.className='context-rig';card.style.width='300px';card.tabIndex=0;card.setAttribute('role','group');card.setAttribute('aria-label','Orientierung: Schnittbild und Linac. Pfeiltasten verschieben, Umschalt und Pfeiltasten ändern die Größe.');grip.title='Orientierungsbereich vergrößern oder verkleinern';card.append(header,slice,rigCanvas,grip);canvas.parentElement.append(card);const rig=new QuickLookRTGraphics.RigView(rigCanvas);contextCard={card,host:canvas.parentElement,slice,canvas:image,note,rig,x:null,y:8,frame:null,key:null,request:0};
  let drag=null;const start=(e,resizing)=>{if(e.target===reset)return;e.preventDefault();e.stopPropagation();drag={x:e.clientX,y:e.clientY,resizing};e.currentTarget.setPointerCapture(e.pointerId);};header.onpointerdown=e=>start(e,false);grip.onpointerdown=e=>start(e,true);
  const move=e=>{if(!drag)return;const dx=e.clientX-drag.x,dy=e.clientY-drag.y;drag.x=e.clientX;drag.y=e.clientY;if(drag.resizing)card.style.width=Math.max(180,Math.min(540,card.offsetWidth+dx+dy*.4))+'px';else{contextCard.x=(contextCard.x??card.offsetLeft)+dx;contextCard.y+=dy;}clampContextCard();contextCard.rig.set(beam,preferences.avatar);};header.onpointermove=grip.onpointermove=move;header.onpointerup=grip.onpointerup=()=>drag=null;
  card.onkeydown=e=>{if(!['ArrowLeft','ArrowRight','ArrowUp','ArrowDown'].includes(e.key))return;e.preventDefault();e.stopPropagation();const delta=e.key==='ArrowLeft'||e.key==='ArrowUp'?-10:10;if(e.shiftKey)card.style.width=card.offsetWidth+delta+'px';else if(e.key.includes('Left')||e.key.includes('Right'))contextCard.x=(contextCard.x??card.offsetLeft)+delta;else contextCard.y+=delta;clampContextCard();};
  card.addEventListener('wheel',e=>{if(e.metaKey||e.ctrlKey){e.preventDefault();e.stopPropagation();card.style.width=Math.max(180,Math.min(540,card.offsetWidth-e.deltaY*.25))+'px';clampContextCard();contextCard.rig.set(beam,preferences.avatar);}},{passive:false});
 }
 const c=contextCard;c.slice.classList.toggle('hidden',!surfaceState.fields);c.rig.set(beam,preferences.avatar);clampContextCard();if(!surfaceState.fields)return;
 if(!model.mpr||!beam.iso){c.note.textContent='Zugeordnetes Bildvolumen erforderlich';return;}
 const key=[contextWork,...beam.iso,+$('level').value,+$('width').value].join(':');if(c.key===key){c.note.textContent='G '+number(beam.gantry)+'° · T '+number(beam.couch)+'° · C '+number(beam.collimator)+'°';return;}c.key=key;const request=++c.request,context=model;c.note.textContent='Schnitt am Isozentrum laden …';
 rpc('render',{plane:'Axial',focus:beam.iso,viewCenter:beam.iso,zoom:1,size:256,level:+$('level').value,width:+$('width').value,hidden:model.rois.map(r=>r.id),wash:false,isodoses:false,fields:false}).then(f=>{if(contextCard!==c||request!==c.request||context!==model)return;c.frame=f;paintContextSlice();updateImageFields();}).catch(e=>{if(contextCard===c&&!e.cancelled)c.note.textContent='Schnittbild nicht verfügbar';});
};
window.addEventListener('resize',clampContextCard);
const orientationViews=new Map();
function disposeOrientationViews(){for(const widget of orientationViews.values())widget.dispose();orientationViews.clear();}
function chooseAvatar(){const choices=text('div','','avatar-choices');for(const [id,label] of [['human','Human'],['frieza','Freeza'],['obelisk','Obelisk'],['elsa','Elsa'],['saitama','Saitama']])choices.append(button(label,()=>{preferences.avatar=id;savePreferences();choices.remove();for(const [canvas,widget] of orientationViews){const f=canvas.closest('.tile')?._frame;if(f)widget.set(f,id);}surfaceAppearance();redrawMlc();}));return choices;}
const drawImageBeforeParity=drawImage;
drawImage=function(canvas,f){drawImageBeforeParity(canvas,f);const tile=canvas.parentElement;if(!tile.classList.contains('tile')||tile.dataset.kind==='MLC'||model?.partial)return;let badge=tile.querySelector('.orientation-badge');if(!badge){badge=text('button','','orientation-badge');badge.title='Orientierungsfigur wählen · links türkis / rechts orange';badge.setAttribute('aria-label','Orientierungsfigur wählen');const avatar=document.createElement('canvas');badge.append(avatar,text('span','L / R'));tile.append(badge);try{orientationViews.set(avatar,new QuickLookRTGraphics.OrientationView(avatar));}catch{badge.remove();return;}badge.onclick=()=>{const old=tile.querySelector('.avatar-choices');if(old)old.remove();else tile.append(chooseAvatar());};}const avatar=badge.querySelector('canvas');orientationViews.get(avatar)?.set(f,preferences.avatar);
 const help=tile.querySelector('.tile-help');if(help)help.textContent=model.hasImage?'W '+number(f.windowWidth??+$('width').value,0)+' / L '+number(f.windowCenter??+$('level').value,0)+' · ×'+number(f.zoom??zoom,1):'RT only · kein CT/MR · ×'+number(zoom,1);
 let legend=tile.querySelector('.image-isodoses');if($('isodoses').checked&&visibleDoseOverlays().some(d=>d.reference>0)){
  const key=JSON.stringify([isoState.levels,isoState.hidden,isoState.colors,isoState.absolute]);if(!legend){legend=text('div','','image-isodoses');tile.append(legend);}if(legend.dataset.key!==key){legend.dataset.key=key;legend.replaceChildren(text('span','Isodosen · '+(isoState.absolute?'Gy':'%'),'isodose-caption'));const rows=text('div','','image-isodose-levels');for(const level of isoState.levels){const toggle=button(number(level,isoState.absolute?1:Number.isInteger(level)?0:1)+(isoState.absolute?' Gy':' %'),()=>{isoState.hidden=isoState.hidden.includes(level)?isoState.hidden.filter(v=>v!==level):[...isoState.hidden,level];doseControls();surfaceAppearance();scheduleRender();});toggle.style.setProperty('--iso-color',window.isoLineColor(level));toggle.classList.toggle('off',isoState.hidden.includes(level));toggle.setAttribute('aria-pressed',String(!isoState.hidden.includes(level)));toggle.title='Isodose in allen Ansichten ein-/ausblenden';rows.append(toggle);}legend.append(rows);}
 }else legend?.remove();
};

function moveToPoint(point){if(!point)return;focus=[...point];viewCenters={};if(view==='mlc')setView(model.canReformat?'image':'3d');if(view==='image'&&$('plane').value==='Native'&&model.hasImage)engineRpc('navigate',{plane:'Native',point}).then(r=>{model.nativeIndex=r.index;updateSlider();scheduleRender();});updateSlider();scheduleRender();}
$('iso').onclick=()=>{const points=model?.isocenters||[];if(points.length===1){moveToPoint(points[0]);return;}if(points.length<1){error('Kein räumlich zugeordnetes Isozentrum vorhanden.');return;}const old=document.querySelector('.iso-choices');if(old){old.remove();return;}const popup=text('div','','iso-choices');popup.setAttribute('role','group');popup.setAttribute('aria-label','Isozentrum auswählen');for(let i=0;i<points.length;i++)popup.append(button('ISO '+(i+1)+' · '+points[i].map(v=>number(v,1)).join(', ')+' mm',()=>{popup.remove();moveToPoint(points[i]);}));$('modebar').append(popup);};
$('doseMaximum').onclick=()=>{const doses=(model?.activeDoses||[]).filter(d=>d.maximumPosition);if(doses.length===1){moveToPoint(doses[0].maximumPosition);return;}if(doses.length)modal('Dosismaximum auswählen',root=>{for(const d of doses)root.append(button(d.label+' · '+number(d.maximum,1)+' '+d.units,()=>{$('modal').close();moveToPoint(d.maximumPosition);}));});};
window.openTagSearch=async function(){if(!tagsOpen){tagsOpen=true;$('tagsPanel').classList.remove('hidden');$('tagsToggle').classList.add('active');await showTags();}const input=$('tagsPanel').querySelector('input[type=search]');input?.focus();input?.select();};
window.handleViewerKey=function(e){
 const editing=e.target?.closest?.('input,textarea,select,[contenteditable=true]');if($('modal').open)return;
 const command=e.metaKey||e.ctrlKey,key=e.key.toLowerCase();let action=null;
 if(e.key==='F1')action=()=>$('help').click();else if(command&&key===',')action=()=>$('settings').click();else if(command&&e.shiftKey&&key==='s')action=()=>savePng();else if(command&&key==='f')action=openTagSearch;
 else if(!editing){if(command&&key==='i')action=()=>$('iso').click();else if(command&&/^[1-6]$/.test(key))action=()=>{const mode=['Native','Axial','Coronal','Sagittal','quad','3d'][+key-1];if(mode==='quad'||mode==='3d')setView(mode);else{$('plane').value=mode;$('plane').onchange();}};else if(e.altKey&&['i','m','d','v'].includes(key))action=()=>setView({i:'image',m:'mlc',d:'dvh',v:'3d'}[key]);else if(e.key==='Home')action=()=>$('fit').click();}
 if(action){e.preventDefault();action();}
};
const fitBeforeParity=$('fit').onclick;$('fit').onclick=()=>{viewCenters={};fitBeforeParity();};

let applyingModel=false;
const setViewBeforeParity=setView;
setView=function(next){if(!applyingModel)viewTouched=true;if(next!==view){disposeOrientationViews();removeContextCard();if(model&&!model.partial){cancelProjectionWork();preparing=false;}}if(model&&!model.hasImage&&next==='image'&&$('plane').value==='Native')$('plane').value='Axial';setViewBeforeParity(next);updateBeamControls();if(next==='mlc'&&!model?.partial){if(neutralFields){neutralFields=false;$('beam').value=String(model.beams[0]?.id??'');changeBeam();}prepareProjections(preparationGeneration);}if(surfaceView)surfaceView.onBeamPick=id=>{$('beam').value=String(id);changeBeam();};};
const modelBeforeParity=applyModel;
applyModel=function(m,initial=false){
 const previous=model,same=previous&&previous.selectionKey===m.selectionKey&&!previous.partial,priorHidden=new Set(previous?.rois.filter(r=>hidden.has(r.id)).map(r=>r.key)||[]),priorWindow=[+$('width').value,+$('level').value],priorZoom=zoom;
 disposeOrientationViews();removeContextCard();contextWork=++workClock;projectionWork=++workClock;preparing=false;trackRequest=null;dvhCaches=new Map();dvhJobs=new Map();doseStates=new Map((m.activeDoses||[]).map(d=>[d.id,{visible:d.registered,info:null,manual:null,display:null}]));displayDoseId=dvhDoseId=m.dvhDose??m.dose;tagSourceId=m.modality.startsWith('RT')?m.tagSources?.find(s=>s.opened)?.id??-1:-1;neutralFields=false;
 applyingModel=true;try{modelBeforeParity(m,initial);}finally{applyingModel=false;}
 dvhs=dvhCaches.get(dvhDoseId)||dvhs;dvhCaches.set(dvhDoseId,dvhs);if(m.beams.length&&!$ ('beam').querySelector('option[value="-3"]'))$('beam').add(new Option('Alle Felder · keine Hervorhebung','-3'),0);
 if(same){hidden=new Set(m.rois.filter(r=>priorHidden.has(r.key)).map(r=>r.id));$('width').value=priorWindow[0];$('level').value=priorWindow[1];zoom=priorZoom;roiList();}
 timelineMarkers();for(const id of ['plans','structures','doses','series'])$(id).disabled=!!m.partial;doseControls();updateBeamControls();$('iso').disabled=!(m.isocenters?.length);$('iso').textContent=(m.isocenters?.length||0)>1?'ISO ▾':'ISO';
 if(m.partial){$('notice').textContent='Ausgewählte Datei sichtbar · zugehöriger Plan-Kontext wird geladen …';$('notice').classList.remove('warning');if(m.firstBeam)publishBeam(m.firstBeam);scheduleRender();}
 else{advanceGroup('context',contextWork);advanceGroup('dvh',contextWork);advanceGroup('projection',projectionWork);if(tagsOpen)showTags();}
};
window.finishStagedOpen=function(m){const preserve=viewTouched||partialInteraction,state={view,focus:[...focus],zoom,width:+$('width').value,level:+$('level').value,hidden:new Set(model.rois.filter(r=>hidden.has(r.id)).map(r=>r.key))};applyModel(m,!preserve);if(preserve){focus=state.focus;zoom=state.zoom;$('width').value=state.width;$('level').value=state.level;hidden=new Set(m.rois.filter(r=>state.hidden.has(r.key)).map(r=>r.id));setView(state.view);updateSlider();roiList();scheduleRender();}viewTouched=false;partialInteraction=false;};
const loadingBeforeParity=window.loading;window.loading=function(folder){disposeOrientationViews();removeContextCard();viewTouched=false;partialInteraction=false;loadingBeforeParity(folder);};
for(const type of ['pointerdown','wheel','keydown'])document.addEventListener(type,()=>{if(model?.partial)partialInteraction=true;},{capture:true,passive:true});
window.addEventListener('pagehide',()=>{disposeOrientationViews();removeContextCard();preparedViews.clear();preparedBeamVersions.clear();meshResults.clear();});

$('series').onchange=async()=>{if(!model||model.partial||selectionBusy)return;selectionBusy=true;pause();previewProgress.opening();try{applyModel(await rpc('select',{plan:model.plan,structure:model.structure,dose:model.dose,series:+$('series').value,keepContext:true,focus:[...focus]}));}catch(e){error(e);}finally{selectionBusy=false;previewProgress.loaded();}};
