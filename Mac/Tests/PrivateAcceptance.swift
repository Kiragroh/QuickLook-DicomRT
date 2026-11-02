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
        const wait=async test=>{for(let i=0;i<180;i++){if(test())return;await pause(100);}throw new Error('UI update timeout');};
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
        const p=cursorLocation(),first=await rpc('beam',{beam:p.beam,cp:0,tracks:true,drr:false,outlines:false});
        assert(first.layers.length>0,'original MLC layers available');
        const final=await rpc('beam',{beam:p.beam,cp:Math.max(0,first.count-1),drr:false,outlines:false});
        assert(final.count===first.count&&final.rectangles.length>=0,'complete control-point range available');
        const small=model.rois.find(r=>r.category==='Target')||model.rois[0];
        const curve=await rpc('dvh',{roi:small.id});
        assert(['Complete','PartialCoverage'].includes(curve.status)&&curve.x.length===2050,'actual structure/dose DVH calculated');
        dvhs.set(small.id,curve);setView('dvh');drawDvh();
        assert(document.querySelectorAll('.dvhlegend').length===model.rois.length,'DVH legend retains all structures');
        const info=await rpc('exportInfo');
        assert(info.parts.CT>0&&info.parts.RP===1&&info.parts.RS===1&&info.parts.RD>=1,'ZIP resolves actual CT/RP/RS/RD references');
        const mesh=await rpc('mesh',{kind:'roi',roi:small.id});
        assert(mesh.available&&mesh.triangles>0,'original ROI surface algorithm builds actual target surface');
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
