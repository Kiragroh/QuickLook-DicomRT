import AppKit
import QuickLookUI

@objc(QuickLookController)
final class QuickLookController: PreviewController, QLPreviewingController {
    func preparePreviewOfFile(at url: URL, completionHandler handler: @escaping (Error?) -> Void) {
        preferredContentSize = NSSize(width: 1180, height: 800)
        openFile(url)
        handler(nil)
    }
    override func viewDidDisappear() { super.viewDidDisappear(); closePreview() }
}
