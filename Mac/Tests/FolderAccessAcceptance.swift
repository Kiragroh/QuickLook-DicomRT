import AppKit

// No file panel or bookmark: exercise Finder's shared controller from one DICOM.
@main final class FolderAccessAcceptance: NSObject, NSApplicationDelegate {
    var window: NSWindow!
    var controller: PreviewController!
    var attempts = 0
    var partialSeen = false
    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.accessory)
        window = NSWindow(contentRect: NSRect(x:0,y:0,width:1400,height:900), styleMask:[.titled], backing:.buffered, defer:false)
        controller = PreviewController();window.contentViewController = controller;window.orderFront(nil)
        controller.openFile(URL(fileURLWithPath:CommandLine.arguments[1]));poll()
    }
    func poll() {
        attempts += 1
        controller.evaluateForTesting("typeof model!=='undefined'&&model?{partial:!!model.partial,ready:!model.partial&&model.files>1&&model.mpr&&model.plan>=0&&model.hasDose&&model.rois.length>0&&model.failures===0}:{}") { value,_ in
            let state = value as? [String:Any] ?? [:]
            if state["partial"] as? Bool == true { self.partialSeen = true }
            if state["ready"] as? Bool == true { self.check() }
            else if self.attempts < 300 { DispatchQueue.main.asyncAfter(deadline:.now()+0.1) { self.poll() } }
            else { print("FAIL folder access: complete CT/RT context unavailable without picker");self.finish(1) }
        }
    }
    func check() {
        controller.executeAsyncForTesting("""
        const wait=async test=>{const end=performance.now()+120000;while(performance.now()<end){if(test())return;await new Promise(r=>setTimeout(r,50));}throw new Error('automatic DVH timeout');};
        if(!['CT','RTPLAN','RTSTRUCT','RTDOSE'].includes(model.modality))throw new Error('expected selected DICOM');
        if($('notice').querySelector('button'))throw new Error('unexpected folder picker action');
        preferences.autoDvh=true;startAutomaticDvh();
        const rois=model.rois.filter(r=>r.category==='Target'||r.category==='Organ');
        if(!rois.length)throw new Error('missing targets/organs');await wait(()=>rois.every(r=>dvhs.has(r.id)));
        if(rois.some(r=>!['Complete','PartialCoverage'].includes(dvhs.get(r.id).status)))throw new Error('invalid automatic DVH status');
        return JSON.stringify({files:model.files,plans:model.plans.length,doses:model.doses.length,structures:model.rois.length,automaticDVHs:rois.length,folderPicker:false});
        """) { result,error in
            if error != nil { print("FAIL folder access: full context or automatic DVH assertion");self.finish(1);return }
            guard self.partialSeen else { print("FAIL folder access: staged content not observed");self.finish(1);return }
            print("PASS folder access from selected DICOM: staged content=true; \(result ?? "")");self.finish(0)
        }
    }
    func finish(_ status: Int32) { controller.closePreview();exit(status) }
    static func main() { let delegate=FolderAccessAcceptance();NSApplication.shared.delegate=delegate;withExtendedLifetime(delegate){NSApplication.shared.run()} }
}
