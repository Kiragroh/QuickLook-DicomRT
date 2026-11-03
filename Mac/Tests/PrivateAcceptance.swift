import AppKit

@main final class PrivateAcceptance: NSObject, NSApplicationDelegate {
    var window: NSWindow!
    var controller: PreviewController!
    var attempts=0
    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.accessory)
        window=NSWindow(contentRect:NSRect(x:0,y:0,width:1500,height:950),styleMask:[.titled],backing:.buffered,defer:false)
        controller=PreviewController();window.contentViewController=controller;window.orderFront(nil)
        controller.openFile(URL(fileURLWithPath:CommandLine.arguments[1]));poll()
    }
    func poll() {
        attempts += 1
        controller.evaluateForTesting("typeof model!=='undefined'&&model&&model.files>1&&model.mpr&&model.plan>=0&&model.hasDose&&model.rois.length>0&&model.failures===0") { value,_ in
            if value as? Bool == true { self.check() }
            else if self.attempts<120 { DispatchQueue.main.asyncAfter(deadline:.now() + 0.5){self.poll()} }
            else { print("FAIL private acceptance: complete image/RT context unavailable");self.controller.closePreview();exit(1) }
        }
    }
    func check() {
        controller.executeAsyncForTesting("""
        const checks=[];
        const assert=(value,label)=>{if(!value)throw new Error(label);checks.push(label);};
        const pause=ms=>new Promise(r=>setTimeout(r,ms));
        const wait=async test=>{for(let i=0;i<1800;i++){if(test())return;await pause(100);}throw new Error('UI update timeout: '+test.toString());};
        preparationGeneration++;preferences.outlinesFirst=false;preferences.drrOnDemand=true;preferences.autoDvh=true;startAutomaticDvh();
        const automaticRois=model.rois.filter(r=>r.category==='Target'||r.category==='Organ');await wait(()=>automaticRois.every(r=>dvhs.has(r.id)));
        assert(automaticRois.length>0&&automaticRois.every(r=>dvhs.has(r.id)),'targets and organs populate DVHs automatically without the calculation button');
        await wait(()=>isoState.referenceInfo?.d0035>0);
        assert(isoState.referenceInfo.prescriptions.length!==1||Math.abs(isoState.reference-isoState.referenceInfo.prescriptions[0])<1e-8,'standard display uses the explicit prescription');
        isoState.absolute=false;changeDosePreset('stx');
        assert(Math.abs(isoState.reference-isoState.referenceInfo.d0035)<1e-8,'STX maximum uses body D0.035 cc');
        assert(activeDosePreset().stops.at(-1)[0]===100&&window.isoLineColor(70)==='#006400','STX ends at 100 percent with dark green at 70 percent');
        changeDosePreset('standard');isoState.absolute=false;refreshIsodoses();
        assert(isoState.levels.includes(95)&&window.isoLineColor(95)==='#006400'&&window.isoLineColor(100)==='#90ee90','standard 95 and 100 percent use the requested green colors');
        isoState.absolute=true;refreshIsodoses();
        assert($('doseControls').querySelector('.levelsinput').value.split(';').every(v=>/^[ ]*[0-9]+([.][0-9])?[ ]*$/.test(v)),'absolute isodose labels have at most one decimal');
        isoState.absolute=false;refreshIsodoses();
        $('settings').click();const lineWidth=$('isodoseLineWidth');lineWidth.value=3;lineWidth.oninput();
        assert(preferences.isodoseLineWidth===3,'settings change isodose line thickness');$('modal').close();
        editDosePresets('stx');$('modalContent').querySelector('input[type=color]').value='#123456';[...$('modalContent').querySelectorAll('button')].find(b=>b.textContent==='Presets speichern').click();
        assert(preferences.dosePresets.stx.stops[0][1]==='#123456','preset editor saves an edited color');preferences.dosePresets=QuickLookRTDose.configured();savePreferences();refreshIsodoses();
        assert($('settings').querySelector('svg').getBoundingClientRect().width>=20,'settings symbol has a readable size');
        previewProgress.update('acceptance','Prüfschritt',3,10,200);
        assert($('loadingLabel').textContent==='Prüfschritt'&&$('loadingBar').value===3&&$('loadingBar').max===10,'loading area displays actual work counts');previewProgress.finish('acceptance');
        $('plane').value='Axial';setView('image');
        await wait(()=>tiles()[0]?._frame);
        const tile=tiles()[0],canvas=tile.querySelector('canvas'),start=focus[2],initial=tile._frame.rgba;
        canvas.dispatchEvent(new WheelEvent('wheel',{deltaY:120,bubbles:true,cancelable:true}));
        await wait(()=>focus[2]!==start&&!renderBusy&&tile._frame.rgba!==initial);
        assert(focus[2]!==start,'wheel moves the selected slice');
        assert(tile._frame.rgba!==initial,'wheel publishes a different complete image');
        const savedFocus=[...focus],direction=model.max[2]-focus[2]>focus[2]-model.min[2]?1:-1,frames=[];const drawBeforeScroll=drawImage;
        drawImage=function(c,f){drawBeforeScroll(c,f);if(c===canvas)frames.push(performance.now());};
        for(let i=0;i<20;i++){canvas.dispatchEvent(new WheelEvent('wheel',{deltaY:direction*120,bubbles:true,cancelable:true}));await pause(22);}
        const duringGesture=frames.length;await wait(()=>!renderBusy&&Math.max(tile._frame.width,tile._frame.height)===512&&Math.abs(tile._frame.center[2]-focus[2])<.01);drawImage=drawBeforeScroll;
        assert(duringGesture>0,'continuous wheel movement publishes frames before the gesture ends');
        assert(Math.abs(tile._frame.center[2]-focus[2])<.01,'scrolling settles on the newest requested slice at full resolution');
        focus=savedFocus;updateSlider();$('isodoses').checked=true;scheduleRender();await wait(()=>!renderBusy&&tile._frame.isolines.length>0);
        const strokeFrame=tile._frame;preferences.isodoseLineWidth=.5;drawImage(canvas,strokeFrame);const thin=canvas.getContext('2d').getImageData(0,0,canvas.width,canvas.height).data;
        preferences.isodoseLineWidth=3;drawImage(canvas,strokeFrame);const thick=canvas.getContext('2d').getImageData(0,0,canvas.width,canvas.height).data;let strokeChanges=0;for(let i=0;i<thin.length;i+=4)if(thin[i]!==thick[i]||thin[i+1]!==thick[i+1]||thin[i+2]!==thick[i+2])strokeChanges++;
        assert(strokeChanges>30,'isodose line thickness changes visible image pixels');preferences.isodoseLineWidth=1.5;savePreferences();
        const center=[...tile._frame.center],bounds=canvas.getBoundingClientRect();
        pick(canvas,{clientX:bounds.left+bounds.width*.60,clientY:bounds.top+bounds.height*.55});
        await wait(()=>!renderBusy);
        assert(Math.abs(tile._frame.center[0]-center[0])<.001,'crosshair preserves in-plane viewport');
        preferences.labelMode=3;roiList();
        const described=model.rois.filter(r=>r.description?.trim());
        assert(described.every(r=>window.roiLabel(r)===r.description.trim()),'description-first labels use retained DICOM descriptions');
        assert(+$('plans').value===model.plan&&model.plan>=0&&$('plans').selectedOptions[0].text.toLowerCase()!=='none','selected RT plan has an unambiguous display label');
        assert(['Axial','Coronal','Sagittal','2 × 2'].every(name=>[...$('imageModes').querySelectorAll('button')].some(b=>b.textContent.trim()===name)),'image modes have visible descriptive labels');
        const p=cursorLocation(),first=await rpc('beam',{beam:p.beam,cp:0,tracks:true,drr:false,outlines:false});
        assert(first.layers.length>0,'original MLC layers available');
        const final=await rpc('beam',{beam:p.beam,cp:Math.max(0,first.count-1),drr:false,outlines:false});
        assert(final.count===first.count&&final.rectangles.length>=0,'complete control-point range available');
        mlcState.outlines=false;preferences.outlinesFirst=false;preferences.drrOnDemand=true;
        beamRevision++;setView('mlc');await wait(()=>beam?.beam===p.beam&&tiles()[0]?.querySelector('canvas')?._mlcScale>0);
        const mlc=tiles()[0].querySelector('canvas');
        const pixels=()=>mlc.getContext('2d').getImageData(0,0,mlc.width,mlc.height).data;
        const edgePixels=()=>{const data=pixels();let count=0;for(let i=0;i<data.length;i+=4)if(Math.abs(data[i]-219)<8&&Math.abs(data[i+1]-243)<8&&Math.abs(data[i+2]-255)<8)count++;return count;};
        assert(edgePixels()>20,'actual MLC bank tips are drawn with clear high-contrast edges');
        assert($('linacName').textContent.includes(beam.machine.label),'Linac label displays the actual DICOM machine metadata');
        if((beam.jaws?.x?.length||0)+(beam.jaws?.y?.length||0)>0){mlcState.jaws=false;redrawMlc();const withoutJaws=pixels();mlcState.jaws=true;redrawMlc();const withJaws=pixels();let jawPixels=0;for(let i=0;i<withJaws.length;i+=4)if(withJaws[i]!==withoutJaws[i]||withJaws[i+1]!==withoutJaws[i+1]||withJaws[i+2]!==withoutJaws[i+2])jawPixels++;assert(jawPixels>20,'actual jaw positions are visibly drawn in the MLC view');}
        const beforeEdges=edgePixels(),savedOpenings=beam.rectangles;beam.rectangles=[];redrawMlc();
        assert(edgePixels()===beforeEdges,'MLC bank tips remain visible without open aperture rectangles');beam.rectangles=savedOpenings;
        $('drr').checked=true;$('drr').onchange();
        await wait(()=>lastDrr&&lastDrr.size>=384);
        assert(new Set(atob(lastDrr.gray)).size>32,'DRR toggle loads a nonuniform actual CT projection');
        mlcState.leafOpacity=.1;mlcState.drrOpacity=1;redrawMlc();const withDrr=pixels();
        mlcState.drrOpacity=0;redrawMlc();const withoutDrr=pixels();let changed=0;for(let i=0;i<withDrr.length;i+=4)if(Math.abs(withDrr[i]-withoutDrr[i])>8||Math.abs(withDrr[i+1]-withoutDrr[i+1])>8||Math.abs(withDrr[i+2]-withoutDrr[i+2])>8)changed++;
        assert(changed>1000,'DRR is visibly composited into the native MLC canvas ('+changed+' changed pixels; '+mlc.width+' x '+mlc.height+')');
        mlcState.drrOpacity=1;$('drr').checked=false;$('drr').onchange();await wait(()=>!lastDrr);
        const next=Math.min(10,first.count-1);$('cp').value=next;$('cp').oninput();await wait(()=>beam?.cp===next);
        assert(beam.layers.length>0,'control-point navigation updates the displayed MLC geometry');
        const small=model.rois.find(r=>r.category==='Target')||model.rois[0];
        const curve=await rpc('dvh',{roi:small.id});
        assert(['Complete','PartialCoverage'].includes(curve.status)&&curve.x.length===2050,'actual structure/dose DVH calculated');
        dvhs.set(small.id,curve);setView('dvh');drawDvh();
        assert(document.querySelectorAll('.dvhlegend').length===model.rois.length,'DVH legend retains all structures');
        const info=await rpc('exportInfo');
        assert(info.parts.CT>0&&info.parts.RP===1&&info.parts.RS===1&&info.parts.RD>=1,'ZIP resolves actual CT/RP/RS/RD references');
        const mesh=await rpc('mesh',{kind:'roi',roi:small.id});
        assert(mesh.available&&mesh.triangles>0,'original ROI surface algorithm builds actual target surface');
        const projection=await rpc('beam',{beam:p.beam,cp:next,drr:false,outlines:true,categories:['Target','Organ','Other'],hidden:model.rois.filter(r=>r.id!==small.id).map(r=>r.id)});
        assert(projection.outlines.some(o=>o.boundary.some(loop=>loop.length>2)),'actual structure has a nonempty beam-eye contour projection');
        setView('mlc');await wait(()=>tiles()[0]?.querySelector('canvas')?._mlcScale>0);
        beam={...projection,outlines:[]};redrawMlc();const contourCanvas=tiles()[0].querySelector('canvas'),context=contourCanvas.getContext('2d'),beforeContour=context.getImageData(0,0,contourCanvas.width,contourCanvas.height).data;
        beam.outlines=projection.outlines;redrawMlc();const afterContour=context.getImageData(0,0,contourCanvas.width,contourCanvas.height).data;let contourPixels=0;for(let i=0;i<beforeContour.length;i+=4)if(beforeContour[i]!==afterContour[i]||beforeContour[i+1]!==afterContour[i+1]||beforeContour[i+2]!==afterContour[i+2])contourPixels++;
        assert(contourPixels>20,'beam-eye structure contours change visible canvas pixels');
        setView('3d');await pause(300);if(surfaceView&&!surfaceView.nodes.has('roi:'+small.id))surfaceView.add('roi:'+small.id,mesh,{color:small.color,opacity:.7});
        assert(surfaceView&&surfaceView.triangles>0,'native WebGL surface buffers populated');
        assert(surfaceView.renderer.getContext().getError()===0,'native 3D rendering has no WebGL error');
        return JSON.stringify({checks:checks.length,files:model.files,structures:model.rois.length,beams:model.beams.length,dvhStatus:curve.status,descriptions:described.length});
        """) { result,error in
            if let error=error { print("FAIL private UI acceptance: \((error as NSError).userInfo["WKJavaScriptExceptionMessage"] ?? error.localizedDescription)");self.controller.closePreview();exit(1) }
            print("PASS private UI acceptance (aggregates only): \(result ?? "")")
            self.controller.closePreview();exit(0)
        }
    }
    static func main(){let delegate=PrivateAcceptance();NSApplication.shared.delegate=delegate;withExtendedLifetime(delegate){NSApplication.shared.run()}}
}
