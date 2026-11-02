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
        const wait=async test=>{for(let i=0;i<600;i++){if(test())return;await pause(100);}throw new Error('UI update timeout');};
        preparationGeneration++;preferences.outlinesFirst=false;preferences.drrOnDemand=false;
        $('plane').value='Axial';setView('image');
        await wait(()=>tiles()[0]?._frame);
        const tile=tiles()[0],canvas=tile.querySelector('canvas'),start=focus[2],initial=tile._frame.rgba;
        canvas.dispatchEvent(new WheelEvent('wheel',{deltaY:120,bubbles:true,cancelable:true}));
        await wait(()=>focus[2]!==start&&!renderBusy&&tile._frame.rgba!==initial);
        assert(focus[2]!==start,'wheel moves the selected slice');
        assert(tile._frame.rgba!==initial,'wheel publishes a different complete image');
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
        mlcState.outlines=false;preferences.outlinesFirst=false;preferences.drrOnDemand=false;
        beamRevision++;setView('mlc');await wait(()=>beam?.beam===p.beam&&tiles()[0]?.querySelector('canvas')?._mlcScale>0);
        const mlc=tiles()[0].querySelector('canvas');
        const pixels=()=>mlc.getContext('2d').getImageData(0,0,mlc.width,mlc.height).data;
        const edgePixels=()=>{const data=pixels();let count=0;for(let i=0;i<data.length;i+=4)if(Math.abs(data[i]-169)<8&&Math.abs(data[i+1]-198)<8&&Math.abs(data[i+2]-214)<8)count++;return count;};
        assert(edgePixels()>20,'actual MLC bank tips are drawn in the canvas');
        const beforeEdges=edgePixels(),savedOpenings=beam.rectangles;beam.rectangles=[];redrawMlc();
        assert(edgePixels()===beforeEdges,'MLC bank tips remain visible without open aperture rectangles');beam.rectangles=savedOpenings;
        $('drr').checked=true;$('drr').onchange();
        await wait(()=>lastDrr&&lastDrr.size>=384);
        assert(new Set(atob(lastDrr.gray)).size>32,'DRR toggle loads a nonuniform actual CT projection');
        mlcState.leafOpacity=.1;mlcState.drrOpacity=1;redrawMlc();const withDrr=pixels();
        mlcState.drrOpacity=0;redrawMlc();const withoutDrr=pixels();let changed=0;for(let i=0;i<withDrr.length;i+=4)if(Math.abs(withDrr[i]-withoutDrr[i])>8)changed++;
        assert(changed>1000,'DRR is visibly composited into the native MLC canvas');
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
