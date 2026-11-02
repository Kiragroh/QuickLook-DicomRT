import * as T from 'three';
import {OrbitControls} from 'three/addons/controls/OrbitControls.js';

const vertex=`varying vec3 n; void main(){n=normalize(normalMatrix*normal);gl_Position=projectionMatrix*modelViewMatrix*vec4(position,1.0);}`;
const fragment=`precision highp float;varying vec3 n;uniform vec3 tint;uniform float alpha;uniform sampler2D prior;uniform vec2 viewport;uniform bool first;
void main(){if(!first&&gl_FragCoord.z<=texture2D(prior,gl_FragCoord.xy/viewport).r+0.0000003)discard;
vec3 normal=normalize(n)*(gl_FrontFacing?1.0:-1.0);float light=.36+.48*max(0.,dot(normal,normalize(vec3(-.8,-.5,1.))))+.16*max(0.,dot(normal,normalize(vec3(.5,1.,.3))));gl_FragColor=vec4(tint*light*alpha,alpha);}`;
const decode=(text,Type)=>new Type(Uint8Array.from(atob(text),x=>x.charCodeAt(0)).buffer);
const vec=a=>new T.Vector3(...a);
function material(color,opacity=1){return new T.MeshPhongMaterial({color,transparent:opacity<1,opacity,side:T.DoubleSide,shininess:24});}
function ellipsoid(parent,p,size,color){const m=new T.Mesh(new T.SphereGeometry(1,20,12),material(color));m.position.set(...p);m.scale.set(...size);parent.add(m);return m;}
function box(parent,p,size,color){const m=new T.Mesh(new T.BoxGeometry(...size),material(color));m.position.set(...p);parent.add(m);return m;}
function rod(parent,a,b,r,color,r2=r){const d=vec(b).sub(vec(a)),m=new T.Mesh(new T.CylinderGeometry(r2,r,d.length(),16),material(color));m.position.copy(vec(a).add(vec(b)).multiplyScalar(.5));m.quaternion.setFromUnitVectors(new T.Vector3(0,1,0),d.normalize());parent.add(m);return m;}
function panel(parent,points,color,opacity=1){const g=new T.BufferGeometry();const pos=[],indices=[];points.forEach(p=>pos.push(...p));for(let i=1;i<points.length-1;i++)indices.push(0,i,i+1);g.setAttribute('position',new T.Float32BufferAttribute(pos,3));g.setIndex(indices);g.computeVertexNormals();const m=new T.Mesh(g,material(color,opacity));parent.add(m);return m;}
function avatar(id){
 const g=new T.Group(),human=id==='human',ice=id==='elsa',alien=id==='frieza',guardian=id==='obelisk';
 const skin=human?'#d9d2c0':guardian?'#287eb8':alien?'#eeeff8':'#f1cab0',suit=human?skin:guardian?'#1670af':alien?'#eeeff8':ice?'#50cbed':'#f8d438',broad=guardian?1.35:ice?.83:1;
 ellipsoid(g,[0,0,.55],[.12,.115,.15],skin);ellipsoid(g,[0,-.122,.56],[.025,.032,.026],skin);ellipsoid(g,[0,0,.36],[.055,.058,.095],skin);
 ellipsoid(g,[0,0,.10],[.18*broad,.11,.25],suit);ellipsoid(g,[0,0,-.18],[.14*broad,.10,.15],suit);
 for(const side of [-1,1]){const x=side*.205*broad,accent=side>0?'#53c9cf':'#f0a15e';
  ellipsoid(g,[x,0,.12],[.06*broad,.067,.16],suit);ellipsoid(g,[side*.25*broad,0,-.075],[.046*broad,.055,.14],ice?skin:suit);
  ellipsoid(g,[side*.25*broad,-.01,-.25],[.05*broad,.045,.077],human?accent:alien||guardian||ice?skin:'#ce343b');
  ellipsoid(g,[side*.084,0,-.43],[.074,.075,.24],suit);ellipsoid(g,[side*.087,0,-.73],[.058,.06,.19],human?skin:ice?'#83cef2':alien||guardian?skin:'#ce343b');
  ellipsoid(g,[side*.087,-.05,-.91],[.064,.11,.06],human?accent:ice?'#83cef2':alien||guardian?skin:'#ce343b');
  if(!human){ellipsoid(g,[side*.25*broad,-.002,-.20],[.054*broad,.052,.022],accent);ellipsoid(g,[side*.087,-.13,-.91],[.059,.037,.027],accent);ellipsoid(g,[side*.047,-.108,.58],[.033,.016,.019],'#fafafa');ellipsoid(g,[side*.047,-.124,.58],[.011,.007,.013],guardian?'#ff3448':alien?'#c42a62':'#285989');}
 }
 if(alien){ellipsoid(g,[0,.001,.645],[.099,.10,.059],'#8249bc');ellipsoid(g,[0,-.108,.13],[.10,.025,.115],'#8249bc');for(const side of [-1,1]){ellipsoid(g,[side*.19,-.025,.225],[.076,.065,.07],'#8249bc');ellipsoid(g,[side*.117,.008,.555],[.037,.044,.075],skin);}const tail=[[0,.08,-.2],[0,.22,-.30],[.17,.28,-.47],[.34,.18,-.62],[.40,-.02,-.67],[.34,-.17,-.64]];for(let i=1;i<tail.length;i++)rod(g,tail[i-1],tail[i],.066-i*.009,i===tail.length-1?'#8249bc':skin,.057-i*.009);}
 else if(ice){rod(g,[0,0,-.85],[0,0,-.12],.29,suit,.13);ellipsoid(g,[0,.034,.615],[.125,.108,.105],'#fae9ba');ellipsoid(g,[-.067,-.087,.653],[.077,.045,.048],'#fae9ba');for(let i=0;i<8;i++)ellipsoid(g,[.115+i*.003,-.07-i*.011,.48-i*.067],[.035-i*.0017,.032,.048],'#fae9ba');panel(g,[[-.14,.075,.27],[.14,.075,.27],[.42,.22,-.90],[-.42,.22,-.90]],'#a3dcf3');}
 else if(guardian){for(const side of [-1,1]){panel(g,[[side*.018,-.14,.28],[side*.22,-.09,.25],[side*.20,-.16,.11],[side*.03,-.17,.075]],'#83cef2');for(let i=0;i<3;i++)ellipsoid(g,[side*.073,-.10,.04-i*.065],[.075,.025,.045],i%2===0?'#478fbf':'#236394');rod(g,[side*.085,.01,.64],[side*.17,.015,.83],.045,'#83cef2',.004);panel(g,[[side*.18,.065,.24],[side*.54,.13,.76],[side*.69,.15,.40],[side*.50,.15,-.18],[side*.34,.12,-.37]],suit);rod(g,[side*.18,.05,.24],[side*.54,.12,.76],.025,'#83cef2',.016);rod(g,[side*.54,.12,.76],[side*.50,.13,-.18],.016,'#83cef2',.010);rod(g,[side*.27,0,.24],[side*.39,0,.36],.07,'#83cef2',.002);}ellipsoid(g,[0,-.107,.66],[.047,.025,.065],'#83cef2');}
 else if(!human){panel(g,[[-.21,.055,.30],[.21,.055,.30],[.32,.20,-.62],[-.32,.20,-.62]],'#f4f2e9');ellipsoid(g,[0,-.014,-.15],[.148,.105,.031],'#203648');ellipsoid(g,[0,-.123,-.15],[.035,.013,.032],'#e9dd9a');rod(g,[0,-.112,.30],[0,-.12,.04],.006,'#203648');}
 return g;
}
function disposeGroup(g){g.traverse(o=>{o.geometry?.dispose();if(o.material){const ms=Array.isArray(o.material)?o.material:[o.material];ms.forEach(m=>m.dispose());}});g.clear();}

export class SurfaceView {
 constructor(){
  this.renderer=new T.WebGLRenderer({antialias:true,alpha:false,preserveDrawingBuffer:true});this.renderer.setPixelRatio(Math.min(2,devicePixelRatio));this.renderer.setClearColor('#080d14',1);this.renderer.autoClear=false;
  this.canvas=this.renderer.domElement;this.canvas.style.cssText='position:absolute;width:100%;height:100%;touch-action:none';
  this.camera=new T.PerspectiveCamera(38,1,.1,10000);this.camera.up.set(0,0,1);
  this.controls=new OrbitControls(this.camera,this.canvas);this.controls.enableDamping=false;this.controls.addEventListener('change',()=>this.draw());
  this.surfaces=new T.Scene();this.guides=new T.Scene();this.staticGuides=new T.Group();this.currentGuides=new T.Group();this.planes=new T.Group();this.guides.add(this.staticGuides,this.currentGuides,this.planes);this.slicePlanes=new Map();this.staticBuilt=false;this.nodes=new Map();this.targets=[];this.generation=0;
  this.rig=new T.Scene();this.rig.add(new T.AmbientLight('#9099a6',1.3));const l=new T.DirectionalLight('#f0f6ff',2);l.position.set(-.5,-1,3);this.rig.add(l);this.rigHost=new T.Group();this.rig.add(this.rigHost);
  this.rigCamera=new T.OrthographicCamera(-2.1,2.1,1.82,-1.82,.1,50);this.rigCamera.position.set(4,-6,3.2);this.rigCamera.up.set(0,0,1);this.rigCamera.lookAt(0,.2,-.05);
  this.quad=new T.Scene();this.quadCamera=new T.OrthographicCamera(-1,1,1,-1,0,1);
  const uniforms={background:{value:new T.Color('#080d14')}};for(let i=0;i<8;i++)uniforms['layer'+i]={value:null};
  const samplers=Array.from({length:8},(_,i)=>`uniform sampler2D layer${i};`).join('');
  const compose=Array.from({length:8},(_,i)=>`c=texture2D(layer${i},vUv);color+=(1.-a)*c.rgb;a+=(1.-a)*c.a;`).join('');
  this.compositor=new T.ShaderMaterial({uniforms,depthTest:false,depthWrite:false,vertexShader:'varying vec2 vUv;void main(){vUv=position.xy*.5+.5;gl_Position=vec4(position.xy,0.,1.);}',fragmentShader:`precision highp float;varying vec2 vUv;${samplers}uniform vec3 background;void main(){vec3 color=vec3(0.);float a=0.;vec4 c;${compose}gl_FragColor=vec4(color+(1.-a)*background,1.);}`});
  this.quad.add(new T.Mesh(new T.PlaneGeometry(2,2),this.compositor));
  this.canvas.addEventListener('dblclick',()=>this.fit());
 }
 mount(tile){if(this.canvas.parentElement!==tile){tile.querySelector('canvas')?.remove();tile.append(this.canvas);}this.resize();}
 reset(model){this.generation++;disposeGroup(this.surfaces);disposeGroup(this.staticGuides);disposeGroup(this.currentGuides);disposeGroup(this.planes);this.slicePlanes.clear();this.staticBuilt=false;this.nodes.clear();this.model=model;this.triangles=0;this.fit();}
 fit(){if(!this.model)return;const min=vec(this.model.min),max=vec(this.model.max),center=min.clone().add(max).multiplyScalar(.5),size=Math.max(50,max.clone().sub(min).length());this.controls.target.copy(center);this.camera.position.copy(center.clone().add(new T.Vector3(1,-1,.7).normalize().multiplyScalar(size*1.8)));this.camera.near=Math.max(.1,size/1000);this.camera.far=size*15+1000;this.camera.updateProjectionMatrix();this.controls.update();this.draw();}
 focus(point){if(!point)return;const delta=vec(point).sub(this.controls.target);this.controls.target.add(delta);this.camera.position.add(delta);this.controls.update();this.draw();}
 resize(){const p=this.canvas.parentElement;if(!p)return;const w=Math.max(2,p.clientWidth),h=Math.max(2,p.clientHeight);this.renderer.setSize(w,h,false);this.camera.aspect=w/h;this.camera.updateProjectionMatrix();const size=this.renderer.getDrawingBufferSize(new T.Vector2());if(this.targets[0]?.width!==size.x||this.targets[0]?.height!==size.y){this.targets.forEach(t=>t.dispose());this.targets=Array.from({length:8},()=>{const target=new T.WebGLRenderTarget(size.x,size.y,{format:T.RGBAFormat,type:T.HalfFloatType,minFilter:T.NearestFilter,magFilter:T.NearestFilter,depthBuffer:true});target.depthTexture=new T.DepthTexture(size.x,size.y,T.UnsignedIntType);return target;});}this.draw();}
 add(key,data,style){if(this.nodes.has(key))return;const triangles=data.triangles||0;if(this.triangles+triangles>8000000)throw new Error('3D-Szenenbudget erreicht');const geometry=new T.BufferGeometry();geometry.setAttribute('position',new T.BufferAttribute(decode(data.positions,Float32Array),3));geometry.setIndex(new T.BufferAttribute(decode(data.indices,Uint32Array),1));if(data.normals)geometry.setAttribute('normal',new T.BufferAttribute(decode(data.normals,Float32Array),3));else geometry.computeVertexNormals();const shader=new T.ShaderMaterial({uniforms:{tint:{value:new T.Color(style.color)},alpha:{value:style.opacity??.7},prior:{value:null},viewport:{value:new T.Vector2()},first:{value:true}},vertexShader:vertex,fragmentShader:fragment,side:T.DoubleSide,depthTest:true,depthWrite:true});const node=new T.Mesh(geometry,shader);node.userData=style;node.frustumCulled=false;this.surfaces.add(node);this.nodes.set(key,node);this.triangles+=triangles;this.draw();}
 appearance(key,{visible,opacity,color}){const node=this.nodes.get(key);if(!node)return;if(visible!==undefined)node.visible=visible;if(opacity!==undefined)node.material.uniforms.alpha.value=opacity;if(color)node.material.uniforms.tint.value.set(color);this.draw();}
 setFocus(id){for(const [key,node] of this.nodes){if(!key.startsWith('roi:'))continue;node.material.uniforms.tint.value.set(node.userData.color);if(key==='roi:'+id)node.material.uniforms.tint.value.lerp(new T.Color('#ffffff'),.3);}this.draw();}
 setBeam(beam,showFields=true,avatarId='human',centerCollimator=false){
  this.beam=beam;this.showFields=showFields;disposeGroup(this.currentGuides);
  if(beam?.iso){const iso=vec(beam.iso),span=Math.max(50,...this.model.max.map((v,i)=>v-this.model.min[i]));const material=new T.LineBasicMaterial({color:'#ffda79',depthTest:false});for(let axis=0;axis<3;axis++){const a=iso.clone(),b=iso.clone();a.setComponent(axis,a.getComponent(axis)-span*.025);b.setComponent(axis,b.getComponent(axis)+span*.025);this.currentGuides.add(new T.Line(new T.BufferGeometry().setFromPoints([a,b]),material));}
   if(showFields&&beam.source){const direction=vec(beam.source).sub(iso).normalize(),head=iso.clone().addScaledVector(direction,span*.65);this.currentGuides.add(new T.Line(new T.BufferGeometry().setFromPoints([head,iso]),new T.LineDashedMaterial({color:'#f8d16c',dashSize:span*.025,gapSize:span*.015})));this.currentGuides.children.at(-1).computeLineDistances();
    if(beam.right&&beam.up){const right=vec(beam.right),up=vec(beam.up);const scale=.25;for(const opening of beam.rectangles){const p=[[opening[0],opening[1]],[opening[2],opening[1]],[opening[2],opening[3]],[opening[0],opening[3]],[opening[0],opening[1]]].map(([x,y])=>head.clone().addScaledVector(right,x*scale).addScaledVector(up,y*scale));this.currentGuides.add(new T.Line(new T.BufferGeometry().setFromPoints(p),new T.LineBasicMaterial({color:'#f8d16c'})));}}
   }
  }
  if(beam?.tracks&&!this.staticBuilt){disposeGroup(this.staticGuides);const span=Math.max(50,...this.model.max.map((v,i)=>v-this.model.min[i])),radius=span*.55;this.fieldNodes=[];
   for(const track of beam.tracks){if(track.setup)continue;const positions=[],samples=track.samples||[],maximum=Math.max(0,...samples.map(s=>s.angular||0));let prior=null;
    for(const sample of samples){const center=vec(sample.iso),direction=vec(sample.direction),point=center.addScaledVector(direction,radius);if(prior&&!sample.split)positions.push(...prior.toArray(),...point.toArray());if(sample.tick&&sample.angular!==null&&maximum>0){const end=point.clone().addScaledVector(direction,radius*.22*sample.angular/maximum);positions.push(...point.toArray(),...end.toArray());}prior=point;}
    if(!samples.length&&track.iso&&track.direction){const iso=vec(track.iso),from=iso.clone().addScaledVector(vec(track.direction),radius);positions.push(...iso.toArray(),...from.toArray());}
    const geometry=new T.BufferGeometry();geometry.setAttribute('position',new T.Float32BufferAttribute(positions,3));const line=new T.LineSegments(geometry,new T.LineBasicMaterial({color:'#62b0e7',transparent:true,opacity:.7}));line.userData.beam=track.id;this.staticGuides.add(line);this.fieldNodes.push(line);
   }this.staticBuilt=true;
  }
  this.staticGuides.visible=showFields;for(const line of this.fieldNodes||[])line.material.color.set(line.userData.beam===beam?.beam?'#ffd752':'#62b0e7');
  this.buildRig(beam,avatarId);this.draw();
 }
 buildRig(beam,id){disposeGroup(this.rigHost);if(!beam||beam.gantry===null||beam.couch===null)return;const g=this.rigHost,steel='#607484',edge='#8c9da9',dark='#293943',accent='#70b6c9',gold='#ffe16b';const gantry=new T.Group();gantry.rotation.y=beam.gantry*Math.PI/180;
  if(beam.machine?.form==='Ring'){
   for(let i=0;i<48;i++){const a=i*Math.PI/24,b=(i+1)*Math.PI/24;for(const y of [-.36,.38]){if(y<0&&i<14)continue;rod(g,[1.28*Math.sin(a),y,1.28*Math.cos(a)],[1.28*Math.sin(b),y,1.28*Math.cos(b)],.025,'#7189a8');}if(i>=14)panel(g,[[1.30*Math.sin(a),-.36,1.30*Math.cos(a)],[1.30*Math.sin(b),-.36,1.30*Math.cos(b)],[1.30*Math.sin(b),.38,1.30*Math.cos(b)],[1.30*Math.sin(a),.38,1.30*Math.cos(a)]],'#6fb9f4',.15);}
   box(g,[0,.10,-1.33],[1.6,.9,.18],dark);box(gantry,[0,0,1.04],[.45,.42,.31],edge);box(gantry,[0,0,.86],[.28,.25,.05],accent);box(gantry,[0,0,-1.05],[.65,.20,.10],dark);
  }else{box(g,[0,1.28,-1.24],[.94,.73,.14],dark);box(g,[0,1.34,-.65],[.56,.50,1.08],steel);box(g,[0,1.05,-.26],[.67,.12,.53],edge);ellipsoid(g,[0,1.23,0],[.33,.18,.33],dark);box(gantry,[0,1.28,.51],[.39,.35,1.16],steel);box(gantry,[0,.69,1.06],[.46,1.28,.35],steel);box(gantry,[0,.17,1.025],[.62,.57,.44],edge);box(gantry,[0,-.02,.86],[.41,.28,.17],dark);box(gantry,[0,-.02,.765],[.28,.22,.035],accent);}
  for(let z=-.18;z<.75;z+=.105)rod(gantry,[0,0,z],[0,0,Math.min(z+.06,.75)],.012,gold);const collimator=new T.Group();collimator.rotation.z=(beam.collimator||0)*Math.PI/180;for(const [a,b] of [[[-.12,-.08,.74],[.12,-.08,.74]],[[.12,-.08,.74],[.12,.08,.74]],[[.12,.08,.74],[-.12,.08,.74]],[[-.12,.08,.74],[-.12,-.08,.74]]])rod(collimator,a,b,.009,gold);gantry.add(collimator);g.add(gantry);
  const couch=new T.Group();couch.rotation.z=beam.couch*Math.PI/180;box(couch,[0,-.15,-.20],[.65,1.75,.12],'#38515a');box(couch,[0,-.15,-.13],[.62,1.75,.035],dark);box(couch,[0,-.15,-.80],[.34,.38,1.08],steel);box(couch,[0,-.15,-1.25],[.76,.72,.13],dark);
  if(beam.machine?.patientToIec){const person=avatar(id);person.applyMatrix4(new T.Matrix4().fromArray(beam.machine.patientToIec).transpose());couch.add(person);}g.add(couch);for(let i=0;i<3;i++){const a=[0,0,0],b=[0,0,0];a[i]=-.105;b[i]=.105;rod(g,a,b,.011,'#f2cb6a');}
 }
 setSlice(kind,frame,canvas){let node=this.slicePlanes.get(kind);if(!node){const geometry=new T.BufferGeometry();geometry.setIndex([0,1,2,0,2,3]);geometry.setAttribute('uv',new T.Float32BufferAttribute([0,1,1,1,1,0,0,0],2));node=new T.Mesh(geometry,new T.MeshBasicMaterial({transparent:true,opacity:.18,side:T.DoubleSide,depthWrite:false}));this.planes.add(node);this.slicePlanes.set(kind,node);}const center=vec(frame.center),right=vec(frame.right),down=vec(frame.down),positions=[];for(const [u,v] of [[-.5,-.5],[.5,-.5],[.5,.5],[-.5,.5]])positions.push(...center.clone().addScaledVector(right,u*frame.widthMm).addScaledVector(down,v*frame.heightMm).toArray());node.geometry.setAttribute('position',new T.Float32BufferAttribute(positions,3));node.geometry.computeBoundingSphere();node.material.map?.dispose();node.material.map=new T.CanvasTexture(canvas);node.material.map.colorSpace=T.SRGBColorSpace;node.material.needsUpdate=true;this.draw();}
 draw(){if(!this.canvas.isConnected||!this.targets.length||this.drawing)return;this.drawing=true;try{const size=this.renderer.getDrawingBufferSize(new T.Vector2());const logical=this.renderer.getSize(new T.Vector2());this.renderer.setScissorTest(false);this.renderer.setViewport(0,0,logical.x,logical.y);
  for(let i=0;i<8;i++){for(const node of this.nodes.values()){const u=node.material.uniforms;u.first.value=i===0;u.prior.value=i?this.targets[i-1].depthTexture:null;u.viewport.value.copy(size);}this.renderer.setRenderTarget(this.targets[i]);this.renderer.setClearColor('#000000',0);this.renderer.clear(true,true,true);this.renderer.render(this.surfaces,this.camera);this.compositor.uniforms['layer'+i].value=this.targets[i].texture;}
  this.renderer.setRenderTarget(null);this.renderer.setViewport(0,0,logical.x,logical.y);this.renderer.setClearColor('#080d14',1);this.renderer.clear(true,true,true);this.renderer.render(this.quad,this.quadCamera);this.renderer.clearDepth();this.renderer.render(this.guides,this.camera);
  if(this.beam){const w=Math.min(logical.x*.36,260),h=w*.88;this.renderer.setViewport(logical.x-w-8,logical.y-h-8,w,h);this.renderer.clearDepth();this.renderer.render(this.rig,this.rigCamera);}this.renderer.setViewport(0,0,logical.x,logical.y);
 }finally{this.drawing=false;}}
 dispose(){this.controls.dispose();disposeGroup(this.surfaces);disposeGroup(this.guides);disposeGroup(this.rigHost);this.targets.forEach(t=>t.dispose());this.renderer.dispose();}
}

export class RigView {
 constructor(canvas){this.renderer=new T.WebGLRenderer({canvas,alpha:true,antialias:true,preserveDrawingBuffer:true});this.renderer.setPixelRatio(Math.min(2,devicePixelRatio));this.scene=new T.Scene();this.scene.add(new T.AmbientLight('#b0bac5',1.5));const light=new T.DirectionalLight('#ffffff',2);light.position.set(-1,-2,3);this.scene.add(light);this.camera=new T.OrthographicCamera(-2.1,2.1,1.82,-1.82,.1,50);this.camera.position.set(4,-6,3.2);this.camera.up.set(0,0,1);this.camera.lookAt(0,.2,-.05);this.rigHost=new T.Group();this.scene.add(this.rigHost);}
 set(beam,id){SurfaceView.prototype.buildRig.call(this,beam,id);const canvas=this.renderer.domElement;this.renderer.setSize(Math.max(100,canvas.clientWidth),Math.max(80,canvas.clientHeight),false);this.renderer.render(this.scene,this.camera);}
 dispose(){disposeGroup(this.rigHost);this.renderer.dispose();}
}
