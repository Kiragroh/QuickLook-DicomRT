import AppKit

@main final class DiscoveryAcceptance: NSObject, NSApplicationDelegate {
    var window:NSWindow!,controller:PreviewController!,attempts=0
    func applicationDidFinishLaunching(_ notification:Notification){
        NSApp.setActivationPolicy(.accessory)
        window=NSWindow(contentRect:NSRect(x:0,y:0,width:1400,height:900),styleMask:[.titled],backing:.buffered,defer:false)
        controller=PreviewController();window.contentViewController=controller;window.orderFront(nil)
        controller.openFile(URL(fileURLWithPath:CommandLine.arguments[1]));poll()
    }
    func poll(){attempts+=1;controller.evaluateForTesting("typeof model!=='undefined'&&['scope','plans'].includes(model?.discovery?.kind)&&$('modal').open") { value,_ in
        if value as? Bool == true {self.check()}
        else if self.attempts<300 {DispatchQueue.main.asyncAfter(deadline:.now()+0.1){self.poll()}}
        else{print("FAIL discovery: automatic plan choice did not open");self.finish(1)}
    }}
    func check(){controller.executeAsyncForTesting("""
    const checks=[];const assert=(v,s)=>{if(!v)throw new Error(s);checks.push(s);};
    const wait=async f=>{for(let i=0;i<1200;i++){if(f())return;await new Promise(r=>setTimeout(r,50));}throw new Error('UI timeout');};
    if(model.discovery.kind==='scope'){
      const next=model.discovery.choices.find(c=>c.kind==='continue');assert(next,'large archive offers an explicit continuation');
      $('discoveryChoice').value=String(next.id);$('discoverySubmit').click();
      await wait(()=>model.discovery?.kind==='plans'&&$('modal').open&&!selectionBusy);
      assert($('modalContent').querySelector('h2').textContent==='Plan auswählen','continued search changes to the matching-plan choice without an OS picker');
    }
    assert(model.mpr&&model.nativeCount===33&&model.plan===-1,'complete split CT is available before plan selection');
    assert($('discoveryChoice').options.length===2,'only the two linked plans are offered');
    assert(document.activeElement.id==='discoveryChoice','choice has keyboard focus');
    $('discoveryCancel').click();assert(!$('modal').open&&model.plan===-1&&model.mpr,'cancel retains image context without guessing');
    $('discoveryNotice').click();assert($('modal').open,'choice can be reopened from inline notice');
    const selected=model.plans.find(p=>p.label==='SECOND PLAN');$('discoveryChoice').value=String(selected.id);
    $('discoveryChoice').dispatchEvent(new KeyboardEvent('keydown',{key:'Enter',bubbles:true,cancelable:true}));
    await wait(()=>model.plan===selected.id&&!selectionBusy);
    assert(!model.discovery&&!$('modal').open&&model.hasDose&&model.rois.length===2,'Enter loads the selected plan and its references without a folder dialog');
    const curve=await rpc('dvh',{roi:model.rois[0].id,doseId:model.dvhDose});assert(['Complete','PartialCoverage'].includes(curve.status),'selected split plan supports a real dose/structure DVH');
    assert(model.mpr&&model.nativeCount===33,'plan choice retains the merged source image series');
    return JSON.stringify({checks:checks.length,files:model.files,plans:model.plans.length,structures:model.rois.length});
    """){result,error in
        if let error=error{print("FAIL discovery acceptance: \((error as NSError).userInfo["WKJavaScriptExceptionMessage"] ?? error.localizedDescription)");self.finish(1);return}
        print("PASS native discovery choice: \(result ?? "")");self.finish(0)
    }}
    func finish(_ status:Int32){controller.closePreview();exit(status)}
    static func main(){let delegate=DiscoveryAcceptance();NSApplication.shared.delegate=delegate;withExtendedLifetime(delegate){NSApplication.shared.run()}}
}
