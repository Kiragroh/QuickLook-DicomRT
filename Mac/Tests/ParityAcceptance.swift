import AppKit

@main final class ParityAcceptance: NSObject, NSApplicationDelegate {
    var window: NSWindow!
    var controller: PreviewController!
    var attempts=0
    var partialSeen=false
    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.accessory)
        window=NSWindow(contentRect:NSRect(x:0,y:0,width:1500,height:950),styleMask:[.titled],backing:.buffered,defer:false)
        controller=PreviewController();window.contentViewController=controller;window.orderFront(nil)
        controller.openFile(URL(fileURLWithPath:CommandLine.arguments[1]));poll()
    }
    func poll() {
        attempts += 1
        controller.evaluateForTesting("typeof model!=='undefined'&&model?{partial:!!model.partial,ready:model.files>1&&model.canReformat&&model.plan>=0&&model.hasDose}:{}") { value,_ in
            let state=value as? [String:Any] ?? [:]
            if state["partial"] as? Bool == true { self.partialSeen=true }
            if state["ready"] as? Bool == true { self.check() }
            else if self.attempts<1200 { DispatchQueue.main.asyncAfter(deadline:.now() + 0.1){self.poll()} }
            else { print("FAIL parity acceptance: full context unavailable");self.controller.closePreview();exit(1) }
        }
    }
    func check() {
        controller.executeAsyncForTesting("""
        const checks=[],skipped=[];
        const assert=(value,label)=>{if(!value)throw new Error(label);checks.push(label);};
        const pause=ms=>new Promise(r=>setTimeout(r,ms));
        const wait=async(test,ms=60000)=>{const end=performance.now()+ms;while(performance.now()<end){if(test())return;await pause(20);}throw new Error('UI update timeout: '+test.toString());};
        preferences.drrOnDemand=true;preferences.outlinesFirst=false;preferences.autoDvh=true;cancelProjectionWork();preparing=false;
        await wait(()=>[...doseStates.values()].every(d=>d.info||model.activeDoses.find(m=>doseStates.get(m.id)===d)?.units!=='GY'));
        const target=model.rois.find(r=>r.category==='Target')||model.rois[0];await requestDvh(target);
        assert(model.activeDoses.length>0&&dvhDoseId!==-1,'associated dose model is available');
        assert(model.isocenters.length>0&&$('iso').disabled===false,'source isocenter navigation is enabled');
        if(model.isocenters.length>1){$('iso').click();assert(document.querySelectorAll('.iso-choices button').length===model.isocenters.length,'multiple source isocenters have separate choices');document.querySelector('.iso-choices button:last-child').click();assert(focus.every((v,i)=>Math.abs(v-model.isocenters.at(-1)[i])<1e-6),'selected isocenter is reached');}
        else skipped.push('multiple isocenters: source has one');
        $('doseMaximum').click();if($('modal').open){$('modalContent').querySelector('button').click();}
        assert(model.activeDoses.some(d=>d.maximumPosition?.every((v,i)=>Math.abs(v-focus[i])<1e-6)),'dose maximum action navigates to an actual mapped point');
        $('plane').value='Axial';setView('image');await wait(()=>tiles()[0]?._frame&&!renderBusy&&renderTimer===null);
        assert(document.querySelector('.orientation-badge canvas')&&orientationViews.size===1,'image has a selectable orientation character');
        const frameBefore=tiles()[0]._frame,rightBefore=[...frameBefore.right],isoBefore=[...focus];
        $('isodoses').checked=true;scheduleRender();await wait(()=>!renderBusy&&renderTimer===null&&document.querySelector('.image-isodose-levels'));
        const legendButton=[...document.querySelectorAll('.image-isodose-levels button')].find(b=>b.textContent.trim().startsWith('95'));
        assert(legendButton,'95 percent appears in the in-image isodose legend');legendButton.click();await wait(()=>!renderBusy&&renderTimer===null);
        assert(isoState.hidden.includes(95),'in-image legend controls shared isodose visibility');isoState.hidden=[];scheduleRender();await wait(()=>!renderBusy&&renderTimer===null);
        const savedDisplay=cloneDisplay(isoState);isoState.levels=[42,95,107];isoState.hidden=[95];isoState.colors={42:'#123456',95:'#006400',107:'#ff0000'};const reference=isoState.reference;
        changeDoseUnits(true);assert(Math.abs(isoState.levels[0]-reference*.42)<1e-9&&window.isoLineColor(isoState.levels[0])==='#123456','percent to Gy preserves custom thresholds and colors');
        changeDoseUnits(false);assert(isoState.levels.every((v,i)=>Math.abs(v-[42,95,107][i])<1e-9)&&isoState.hidden.some(v=>Math.abs(v-95)<1e-9)&&window.isoLineColor(isoState.levels[0])==='#123456','Gy to percent preserves custom levels visibility and colors');isoState=savedDisplay;doseControls();surfaceControls();scheduleRender();await wait(()=>!renderBusy&&renderTimer===null);
        const hiddenBefore=new Set(hidden),focusBefore=[...focus],zoomBefore=zoom,displayBefore=displayDoseId;
        if(model.activeDoses.length>1){
            const a=model.activeDoses[0].id,b=model.activeDoses[1].id;chooseDvhDose(a);const resultA=await requestDvh(target);chooseDvhDose(b);const resultB=await requestDvh(target);
            assert(Math.abs(resultA.mean-resultB.mean)>1e-5,'native DVH dose switch changes numeric dose metrics');
            assert(focus.every((v,i)=>v===focusBefore[i])&&zoom===zoomBefore&&displayDoseId===displayBefore&&hidden.size===hiddenBefore.size,'DVH dose switch preserves image and isodose context');
            setView('dvh');assert(+$('dvhDosePicker').querySelector('select').value===b,'DVH dose choice is visible above chart');
            chooseDvhDose(a);assert(dvhs.get(target.id)===resultA,'switching back restores the matching cached DVH');
            setView('image');await wait(()=>tiles()[0]?._frame&&!renderBusy&&renderTimer===null);const image=tiles()[0]._frame.rgba,overlay=$('doseOverlays').querySelector('input');overlay.checked=false;overlay.onchange();await wait(()=>!renderBusy&&renderTimer===null&&tiles()[0]._frame.rgba!==image);
            assert(tiles()[0]._frame.rgba!==image,'individual dose visibility changes native image pixels');overlay.checked=true;overlay.onchange();
        }else skipped.push('multiple dose UI: source has one associated dose');
        surfaceState.fields=true;$('fieldsToggle').checked=true;$('fieldsToggle').onchange();await wait(()=>!renderBusy&&renderTimer===null&&!beamBusy&&tiles()[0]._frame.fields);
        assert(!$('beamtools').classList.contains('hidden'),'field playback controls are available in single-image workspace');
        const tile=tiles()[0],raster=tile._frame,rgba=raster.rgba,oldFields=JSON.stringify(raster.fields),beamId=model.beams[0].id;
        $('beam').value=String(beamId);changeBeam();await wait(()=>beam?.beam===beamId&&!beamBusy);
        let rasterCalls=0;const beforeRpc=rpc;rpc=function(op,args){if(op==='render')rasterCalls++;return beforeRpc(op,args);};
        setCp(Math.min(10,+$('cp').max));await wait(()=>!beamBusy&&beam.cp===Math.min(10,+$('cp').max)&&JSON.stringify(tile._frame.fields)!==oldFields);
        rpc=beforeRpc;assert(tile._frame===raster&&tile._frame.rgba===rgba&&rasterCalls===0,'field movement changes overlays without rerasterizing CT or dose (same frame '+(tile._frame===raster)+', same raster '+(tile._frame.rgba===rgba)+', raster calls '+rasterCalls+')');
        $('beam').value='-3';changeBeam();await wait(()=>tile._frame.fields.fields.every(f=>!f.active));assert($('play').disabled,'neutral field state has no active highlight');
        $('beam').value=String(beamId);changeBeam();
        setView('mlc');await wait(()=>contextCard?.frame&&contextCard.rig.rigParts&&!beamBusy);
        const card=contextCard,position=card.card.offsetLeft,width=card.card.offsetWidth;card.card.dispatchEvent(new KeyboardEvent('keydown',{key:'ArrowLeft',bubbles:true}));
        assert(card.card.offsetLeft<position,'orientation card can be moved with keyboard');card.card.dispatchEvent(new KeyboardEvent('keydown',{key:'ArrowLeft',shiftKey:true,bubbles:true}));assert(card.card.offsetWidth<width,'orientation card can be resized');
        const contextRaster=card.frame,builds=card.rig.rigBuilds,geometries=card.rig.renderer.info.memory.geometries,current={...beam};
        for(let i=0;i<40;i++)card.rig.set({...current,cp:current.cp+i+1,gantry:(current.gantry+i)%360},preferences.avatar);
        assert(card.rig.rigBuilds===builds&&card.rig.renderer.info.memory.geometries===geometries,'LINAC animation reuses GPU geometry across control points');card.rig.set(current,preferences.avatar);
        setCp(Math.min(12,+$('cp').max));await wait(()=>!beamBusy&&beam.cp===Math.min(12,+$('cp').max));
        if(beam.iso.every((v,i)=>Math.abs(v-current.iso[i])<1e-6))assert(card.frame===contextRaster,'field-arrangement CT is reused when isocenter is unchanged');else skipped.push('context reuse: isocenter moves');
        assert(card.card.querySelector('.context-slice canvas')&&card.card.querySelector('.context-rig'),'movable context contains both axial field arrangement and LINAC');
        setView('image');await wait(()=>tiles()[0]?._frame&&!renderBusy&&renderTimer===null);const canvas=tiles()[0].querySelector('canvas'),savedFocus=[...focus],frames=[],beforeDraw=drawImage,dir=model.max[2]-focus[2]>focus[2]-model.min[2]?1:-1;
        drawImage=function(c,f){beforeDraw(c,f);if(c===canvas)frames.push(performance.now());};
        const started=performance.now();for(let i=0;i<20;i++){canvas.dispatchEvent(new WheelEvent('wheel',{deltaY:dir*120,bubbles:true,cancelable:true}));await pause(22);}
        const during=frames.length,ended=performance.now();await wait(()=>!renderBusy&&renderTimer===null&&Math.max(tiles()[0]._frame.width,tiles()[0]._frame.height)===512&&Math.abs(tiles()[0]._frame.center[2]-focus[2])<.01);const settle=performance.now()-ended;drawImage=beforeDraw;
        assert(during>=4,'at least four frames publish during continuous 440ms scrolling');assert(settle<500,'latest requested slice settles at full resolution within 500ms');focus=savedFocus;updateSlider();
        $('tagsToggle').click();await wait(()=>$('tagSourcePicker'));
        const opened=model.tagSources.find(s=>s.opened);assert(+$('tagSourcePicker').querySelector('select').value===opened.id,'opened RT object is the initial metadata source');
        const tagRows=await rpc('tags',{sourceId:opened.id});assert(tagRows.some(t=>t.Tag.toLowerCase()==='(300a,0002)'),'selected metadata source exposes RT plan attributes');
        await openTagSearch();assert(document.activeElement.id==='tagSearch','tag search command focuses search field');
        $('settings').click();const beforeView=view,input=$('isodoseLineWidth');input.focus();input.dispatchEvent(new KeyboardEvent('keydown',{key:'2',metaKey:true,bubbles:true}));assert(view===beforeView,'view shortcuts do not escape settings or text editors');$('modal').close();
        setView('dvh');await requestDvh(target);window.dvhFocus=null;const unfocused=dvhReportCanvas().toDataURL();window.dvhFocus=target.id;const focused=dvhReportCanvas().toDataURL();
        assert(unfocused===focused,'dedicated DVH PNG is independent of focus dimming');assert(dvhReportCanvas().height>=900,'dedicated DVH PNG contains a full metrics legend');
        const directMesh=await rpc('mesh',{kind:'roi',roi:target.id});assert(directMesh.available,'direct target mesh available before 3D');setView('3d');await wait(()=>surfaceView&&!surfaceLoading&&renderTimer===null&&!renderBusy);await loadSurfaces();surfaceAppearance();await pause(150);
        let draws=0;const renderer=surfaceView.renderer,renderNow=renderer.render;renderer.render=function(...args){draws++;return renderNow.apply(this,args);};surfaceAppearance();surfaceAppearance();surfaceAppearance();await new Promise(requestAnimationFrame);renderer.render=renderNow;
        assert(draws>0&&draws<=11,'3D appearance changes coalesce into one complete eight-layer composition ('+draws+' renders; '+surfaceView.nodes.size+' nodes; connected '+surfaceView.canvas.isConnected+'; pending '+!!surfaceView.drawRequest+'; disposed '+!!surfaceView.disposed+'; failure '+surfaceFailure+'; meshes '+meshResults.size+'/'+[...meshResults.values()].filter(m=>m.available).length+'; image '+model.hasImage+'; skin '+surfaceState.skin+'; targets '+surfaceState.Target+'; selected '+model.rois.filter(r=>surfaceState[r.category]&&!hidden.has(r.id)).length+')' );
        const savedManual=isoState.manualReference;isoState.absolute=false;surfaceState.dose=true;surfaceState.level=80;const overlayBefore=visibleDoseOverlays().find(d=>d.id===displayDoseId),keyBefore=doseSurfaceKey(overlayBefore,surfaceState.level);loadSurfaces();await wait(()=>surfaceView.nodes.has(keyBefore));
        const positionsBefore=surfaceView.nodes.get(keyBefore).geometry.getAttribute('position').array;
        isoState.manualReference=Math.round(isoState.reference*.8*10)/10;refreshIsodoses();const overlayAfter=visibleDoseOverlays().find(d=>d.id===displayDoseId),keyAfter=doseSurfaceKey(overlayAfter,surfaceState.level);await wait(()=>surfaceView.nodes.has(keyAfter));const positionsAfter=surfaceView.nodes.get(keyAfter).geometry.getAttribute('position').array;
        assert(keyBefore!==keyAfter&&(positionsBefore.length!==positionsAfter.length||positionsBefore.some((v,i)=>v!==positionsAfter[i])),'manual reference changes rebuild percent 3D isodose at the new Gy threshold');
        isoState.manualReference=savedManual;surfaceState.dose=false;refreshIsodoses();surfaceAppearance();
        const vertexCount=surfaceView.renderer.info.memory.geometries;
        for(let i=0;i<5;i++){setView('mlc');await pause(25);setView('3d');await pause(25);}
        assert(document.querySelectorAll('.mlc-context').length===0&&orientationViews.size===0,'view changes release hidden orientation contexts');assert(surfaceView.renderer.info.memory.geometries<=vertexCount+5,'repeated workspace changes keep GPU geometry bounded');
        const navigated=focus.map((v,i)=>Math.min(model.max[i],Math.max(model.min[i],v+(i===2?2:0))));focus=navigated;await $('series').onchange();assert(focus.every((v,i)=>Math.abs(v-navigated[i])<1e-6),'series selection preserves the latest frontend crosshair after snapshot rendering');
        if(model.plans.length>1){
            setView('mlc');mlcState.outlines=false;preferences.drrOnDemand=true;preferences.progressiveDrr=false;mlcState.speed=5;$('drr').checked=true;$('beam').value=String(model.beams[0].id);changeBeam();await wait(()=>preparedBeamVersions.get(model.beams[0].id)===projectionWork);
            assert(preparedViews.has(model.beams[0].id+':0.4'),'nonprogressive DRR buffer includes original 80ms fractional CP step');$('play').click();await wait(()=>beam.cp>0&&!Number.isInteger(beam.cp));$('play').click();await wait(()=>lastDrr?.size>=384);
            assert(!playing&&lastDrr.size>=384,'fractional DRR playback pauses on an exact refined frame');preferences.progressiveDrr=true;$('drr').checked=false;$('drr').onchange();
            const next=model.plans.find(p=>p.id!==model.plan);$('plans').value=String(next.id);$('plans').onchange();await wait(()=>!selectionBusy&&model.plan===next.id);assert(model.activeDoses.length===1,'second plan selects only its explicitly referenced dose');}
        else skipped.push('multiple plan UI: source has one plan');
        return JSON.stringify({passed:checks.length,skipped:skipped.length,scrollFrames:during,settleMs:Math.round(settle),gpuDraws:draws,structures:model.rois.length});
        """) { result,error in
            if let error=error { print("FAIL parity UI acceptance: \((error as NSError).userInfo["WKJavaScriptExceptionMessage"] ?? error.localizedDescription)");self.controller.closePreview();exit(1) }
            print("PASS parity UI acceptance: first content before full context=\(self.partialSeen); \(result ?? "")")
            if !self.partialSeen {self.controller.closePreview();exit(1)}
            self.controller.closePreview();exit(0)
        }
    }
    static func main(){let delegate=ParityAcceptance();NSApplication.shared.delegate=delegate;withExtendedLifetime(delegate){NSApplication.shared.run()}}
}
