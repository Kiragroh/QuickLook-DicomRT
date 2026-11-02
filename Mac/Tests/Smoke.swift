import AppKit

@main final class Smoke: NSObject, NSApplicationDelegate {
    var window: NSWindow!
    var controller: PreviewController!
    var attempts = 0
    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.accessory)
        window = NSWindow(contentRect: NSRect(x: 0,y: 0,width: 1180,height: 800), styleMask: [.titled], backing: .buffered, defer: false)
        controller = PreviewController(); window.contentViewController = controller
        window.orderFront(nil)
        controller.openFile(URL(fileURLWithPath: CommandLine.arguments[1]))
        poll()
    }
    func poll() {
        attempts += 1
        controller.evaluateForTesting("JSON.stringify({title:document.getElementById('title')?.textContent,status:document.getElementById('status')?.textContent,canvas:document.querySelectorAll('canvas').length,files:typeof model!=='undefined'&&model?model.files:0})") { result, error in
            if let s = result as? String, s.contains("\"files\":70"), !s.contains("\"canvas\":0") {
                self.controller.evaluateForTesting("surfaceState.Other=true;setView('quad');loadSurfaces();true") { _, _ in
                    DispatchQueue.main.asyncAfter(deadline: .now()+2) { self.capture() }
                }
            } else if self.attempts < 30 {
                DispatchQueue.main.asyncAfter(deadline: .now()+0.5) { self.poll() }
            } else { print("FAIL native integration: \(result ?? "no result") \(error?.localizedDescription ?? "")"); exit(1) }
        }
    }
    func capture() {
        controller.evaluateForTesting("JSON.stringify({tiles:document.querySelectorAll('.tile').length,frames:tiles().filter(t=>t._frame).length,rois:model.rois.length,beams:model.beams.length})") { result, error in
            guard let text = result as? String, text.contains("\"frames\":3"), text.contains("\"rois\":2"), text.contains("\"beams\":2") else { print("FAIL linked views: \(result ?? "no result")"); exit(1) }
            self.controller.snapshotForTesting(URL(fileURLWithPath: CommandLine.arguments[2])) {
                print("PASS native WebKit + embedded engine integration: 70 files, 3 MPR frames, 2 structures, 2 beams")
                self.controller.closePreview(); exit(0)
            }
        }
    }
    static func main() { let d=Smoke(); NSApplication.shared.delegate=d; withExtendedLifetime(d) { NSApplication.shared.run() } }
}
