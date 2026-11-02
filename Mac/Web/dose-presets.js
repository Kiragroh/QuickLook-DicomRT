/* Shared display math for the native viewer and its regression tests. */
(function(root){
 'use strict';
 const presets={
  standard:{label:'Standard',maximumPercent:110,stops:[[10,'#8c5a2b'],[20,'#183fb8'],[30,'#2478e5'],[40,'#00b8dd'],[50,'#00af9b'],[60,'#00a96a'],[70,'#008f49'],[80,'#008535'],[90,'#007329'],[95,'#006400'],[100,'#90ee90'],[105,'#ffff00'],[107,'#ff0000'],[110,'#ff00ff'],[120,'#b000e0']]},
  stx:{label:'STX',maximumPercent:100,stops:[[10,'#8c5a2b'],[20,'#183fb8'],[30,'#2478e5'],[40,'#00b8dd'],[50,'#00af9b'],[60,'#008f49'],[70,'#006400'],[80,'#90ee90'],[90,'#ff0000'],[100,'#ff00ff']]}
 };
 const clone=value=>JSON.parse(JSON.stringify(value));
 function validate(stops,key){
  if(!Array.isArray(stops)||stops.length<1||stops.length>24)throw new Error('1–24 Isodosen eingeben.');
  const sorted=stops.map(([percent,color])=>[Number(percent),String(color)]).sort((a,b)=>a[0]-b[0]);
  if(sorted.some(([p,c],i)=>!Number.isFinite(p)||p<=0||p>(key==='stx'?100:200)||Math.abs(p*10-Math.round(p*10))>1e-7||!/^#[0-9a-f]{6}$/i.test(c)||i&&p===sorted[i-1][0]))throw new Error('Eindeutige positive Prozentwerte mit höchstens einer Nachkommastelle und gültige Farben eingeben.');
  if(key==='stx'&&sorted.at(-1)[0]!==100)throw new Error('STX endet immer bei 100 %.');
  return sorted;
 }
 function configured(saved){const result=clone(presets);for(const key of Object.keys(result)){try{if(saved?.[key])result[key].stops=validate(saved[key].stops,key);}catch{}}return result;}
 function reference(info,key,manual=null){
  if(Number.isFinite(manual)&&manual>0)return{value:manual,source:'Manuell: 100 % = '+manual.toFixed(1)+' Gy'};
  if(key==='standard'&&info?.prescriptions?.length===1)return{value:info.prescriptions[0],source:'DICOM-Verschreibung: 100 % = '+info.prescriptions[0].toFixed(1)+' Gy'};
  if(info?.d0035>0){const maximum=presets[key].maximumPercent;return{value:info.d0035*100/maximum,source:'D0,035 cm³ = '+maximum+' % ('+info.d0035.toFixed(1)+' Gy)'+(key==='standard'&&info?.prescriptions?.length>1?' · Verschreibung mehrdeutig':'')};}
  return{value:null,source:info?.message||'Dosisreferenz wird berechnet …'};
 }
 function color(stops,percent){
  let upper=stops.findIndex(s=>s[0]>=percent);if(upper<0)upper=stops.length-1;const lower=Math.max(0,upper-1),a=stops[lower],b=stops[upper],t=upper===lower?0:Math.max(0,Math.min(1,(percent-a[0])/(b[0]-a[0])));
  const rgb=s=>[1,3,5].map(i=>parseInt(s.slice(i,i+2),16));const x=rgb(a[1]),y=rgb(b[1]);return'#'+x.map((v,i)=>Math.round(v*(1-t)+y[i]*t).toString(16).padStart(2,'0')).join('');
 }
 const api={presets,clone,validate,configured,reference,color,absolute:(percent,reference)=>percent*reference/100,percent:(absolute,reference)=>absolute/reference*100};
 if(typeof module==='object'&&module.exports)module.exports=api;else root.QuickLookRTDose=api;
})(typeof window==='undefined'?globalThis:window);
