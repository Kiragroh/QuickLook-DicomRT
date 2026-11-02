'use strict';
window.previewProgress=(()=>{
 const jobs=new Map();let poll=null,clock=0;
 const panel=document.getElementById('loadingPanel'),label=document.getElementById('loadingLabel'),bar=document.getElementById('loadingBar'),detail=document.getElementById('loadingDetail');
 function paint(){const work=[...jobs.values()].sort((a,b)=>b.priority-a.priority||b.updated-a.updated);panel.classList.toggle('busy',work.length>0);if(!work.length){panel.title='';label.textContent='Bereit';bar.value=1;bar.max=1;detail.textContent='';return;}const active=work[0];label.textContent=active.label;if(active.total>0){bar.max=active.total;bar.value=active.current;}else bar.removeAttribute('value');detail.textContent=work.map(job=>job.label+(job.total>0?' '+job.current+'/'+job.total:' …')).join(' · ');panel.title=detail.textContent;}
 function update(id,label,current=0,total=0,priority=0){jobs.set(id,{label,current,total,priority,updated:++clock});paint();}
 function finish(id){jobs.delete(id);paint();}
 function loaded(){clearInterval(poll);poll=null;finish('open');}
 function opening(){jobs.clear();clearInterval(poll);update('open','DICOM-Dateien lesen',0,0,100);poll=setInterval(async()=>{try{const state=await rpc('progress');if(jobs.has('open'))update('open',state.phase,state.current,state.total,100);}catch{}},250);}
 paint();return{update,finish,opening,loaded};
})();
