import AppKit
import WebKit

// Both the containing app and Finder extension use this exact view and managed engine.
class PreviewController: NSViewController, WKScriptMessageHandler, WKNavigationDelegate {
    private var web: WKWebView!
    private var sessionID = UUID().uuidString
    private let io = DispatchQueue(label: "de.kiragroh.quicklookrt.worker", qos: .userInteractive)
    private let preparation: OperationQueue = {
        let queue = OperationQueue(); queue.name = "de.kiragroh.quicklookrt.preparation"
        queue.maxConcurrentOperationCount = 2; queue.qualityOfService = .utility; return queue
    }()
    private var fileURL: URL?
    private var folderScope: URL?
    private var ready = false
    private var generation = 0
    private var viewerPreferences: UserDefaults {
        #if PREVIEW_TESTS
        return .standard
        #else
        return UserDefaults(suiteName: "de.kiragroh.QuickLookRT.Settings")!
        #endif
    }
    private var resources: URL { Bundle(for: PreviewController.self).resourceURL! }

    override func loadView() {
        let root = NSView(frame: NSRect(x: 0, y: 0, width: 1180, height: 800))
        root.wantsLayer = true
        root.layer?.backgroundColor = NSColor(calibratedRed: 0.04, green: 0.06, blue: 0.09, alpha: 1).cgColor
        let config = WKWebViewConfiguration()
        config.websiteDataStore = .nonPersistent()
        config.userContentController.add(WeakHandler(self), name: "native")
        config.preferences.javaScriptCanOpenWindowsAutomatically = false
        web = WKWebView(frame: .zero, configuration: config)
        web.navigationDelegate = self
        web.setValue(false, forKey: "drawsBackground")
        web.translatesAutoresizingMaskIntoConstraints = false;root.addSubview(web)
        NSLayoutConstraint.activate([
            web.topAnchor.constraint(equalTo: root.topAnchor),
            web.leadingAnchor.constraint(equalTo: root.leadingAnchor), web.trailingAnchor.constraint(equalTo: root.trailingAnchor),
            web.bottomAnchor.constraint(equalTo: root.bottomAnchor)
        ])
        view = root
        web.loadFileURL(resources.appendingPathComponent("Web/index.html"), allowingReadAccessTo: resources.appendingPathComponent("Web"))
    }
    func openFile(_ url: URL) {
        _ = view
        generation += 1
        stopWorker()
        fileURL = url
        folderScope?.stopAccessingSecurityScopedResource()
        folderScope = nil
        extraScopes.forEach { $0.stopAccessingSecurityScopedResource() };extraScopes.removeAll()
        restoreFolder(for: url)
        if ready { loadDocument() }
    }
    private func restoreFolder(for file: URL) {
        let parent = file.deletingLastPathComponent().standardizedFileURL
        let bookmarks = UserDefaults.standard.dictionary(forKey: "DICOMFolderBookmarks") as? [String: Data] ?? [:]
        let candidates = bookmarks.keys.filter { parent.path == $0 || parent.path.hasPrefix($0 + "/") }.sorted { $0.count > $1.count }
        guard let key = candidates.first, let data = bookmarks[key] else { return }
        var stale = false
        if let url = try? URL(resolvingBookmarkData: data, options: [.withSecurityScope], relativeTo: nil, bookmarkDataIsStale: &stale),
           (parent.path == url.standardizedFileURL.path || parent.path.hasPrefix(url.standardizedFileURL.path + "/")), url.startAccessingSecurityScopedResource() { folderScope = url;if stale { saveFolderBookmark(url) } }
    }
    @objc private func chooseFolder() {
        guard let file = fileURL else { return }
        let panel = NSOpenPanel()
        panel.canChooseDirectories = true; panel.canChooseFiles = false; panel.allowsMultipleSelection = false
        panel.directoryURL = file.deletingLastPathComponent()
        panel.message = "Den lokalen Ordner dieser DICOM-Datei auswählen. QuickLookRT liest darin die zugehörigen Bilder und RT-Objekte. Es wird nichts an andere Personen übertragen."
        panel.prompt = "Ordner auswählen"
        let version = generation
        let complete: (NSApplication.ModalResponse) -> Void = { [weak self] response in
            guard let self = self, self.generation == version, self.fileURL == file, response == .OK, let url = panel.url else { return }
            self.useFolder(url, for: file)
        }
        if let window = view.window { panel.beginSheetModal(for: window, completionHandler: complete) }
        else { panel.begin(completionHandler: complete) }
    }
    private func useFolder(_ url: URL, for file: URL) {
        let parent = file.deletingLastPathComponent().standardizedFileURL.path
        guard parent == url.standardizedFileURL.path || parent.hasPrefix(url.standardizedFileURL.path + "/") else { return }
        folderScope?.stopAccessingSecurityScopedResource()
        _ = url.startAccessingSecurityScopedResource();folderScope = url
        saveFolderBookmark(url)
        generation += 1;stopWorker();loadDocument()
    }
    private func saveFolderBookmark(_ url: URL) {
        if let data = try? url.bookmarkData(options: [.withSecurityScope, .securityScopeAllowOnlyReadAccess], includingResourceValuesForKeys: nil, relativeTo: nil) {
            var bookmarks = UserDefaults.standard.dictionary(forKey: "DICOMFolderBookmarks") as? [String: Data] ?? [:]
            bookmarks[url.standardizedFileURL.path] = data;UserDefaults.standard.set(bookmarks, forKey: "DICOMFolderBookmarks")
        }
    }
    private func canReadSiblings(_ file: URL) -> Bool {
        guard let siblings = try? FileManager.default.contentsOfDirectory(at: file.deletingLastPathComponent(), includingPropertiesForKeys: nil) else { return false }
        let candidates = siblings.filter { $0 != file && ["dcm", "dicom", "ima", ""].contains($0.pathExtension.lowercased()) }
        guard let sibling = candidates.first else { return true }
        guard let handle = try? FileHandle(forReadingFrom: sibling) else { return false }
        try? handle.close();return true
    }
    private func loadDocument() {
        guard let url = fileURL else { return }
        // The standalone app can read sibling files; sandboxed Finder previews need a grant.
        #if QUICKLOOK_EXTENSION
        let scanFolder = canReadSiblings(url)
        #else
        let scanFolder = true
        #endif
        web.evaluateJavaScript("window.loading(\(String(scanFolder)))", completionHandler: nil)
        // Return useful single-file content before indexing and decoding its siblings.
        send(["id": 0, "op": "open", "path": url.path, "folder": false, "partial": scanFolder]) { [weak self] in
            guard let self = self, scanFolder else { return }
            self.send(["id": 0, "op": "open", "path": url.path, "folder": true])
        }
    }
    func userContentController(_ userContentController: WKUserContentController, didReceive message: WKScriptMessage) {
        guard message.frameInfo.isMainFrame, let url = message.frameInfo.request.url, url.isFileURL,
              url.standardizedFileURL.path.hasPrefix(resources.appendingPathComponent("Web/").path),
              let body = message.body as? [String: Any], let op = body["op"] as? String else { return }
        if op == "ready" {
            ready = true
            let preferences = viewerPreferences.dictionary(forKey: "ViewerSettings") ?? UserDefaults.standard.dictionary(forKey: "ViewerSettings") ?? [:]
            if let data = try? JSONSerialization.data(withJSONObject: preferences), let json = String(data: data, encoding: .utf8) { web.evaluateJavaScript("window.applyPreferences(\(json))", completionHandler: nil) }
            loadDocument(); return
        }
        if op == "preferences", let preferences = body["value"] as? [String: Any] { viewerPreferences.set(preferences, forKey: "ViewerSettings"); return }
        if op == "copy", let text = body["text"] as? String { NSPasteboard.general.clearContents(); NSPasteboard.general.setString(text, forType: .string); return }
        if op == "openContainingFolder" { chooseFolder(); return }
        if op == "searchFolder" { searchFolder(body); return }
        if op == "save" { saveExport(body); return }
        guard ["select", "render", "scene", "beam", "dvh", "tags", "mesh", "fusion", "sum", "exportInfo", "histogram", "volume", "doseReference", "progress", "fields", "navigate", "cancelWork"].contains(op), body["id"] as? Int != nil else { return }
        send(body)
    }
    private func reply(_ request: [String: Any], _ result: [String: Any]) {
        let response: [String: Any] = ["id": request["id"] as? Int ?? 0, "result": result]
        if let data = try? JSONSerialization.data(withJSONObject: response), let json = String(data: data, encoding: .utf8) { web.evaluateJavaScript("window.receive(\(json))", completionHandler: nil) }
    }
    private func searchFolder(_ request: [String: Any]) {
        let panel = NSOpenPanel(); panel.canChooseDirectories = true; panel.canChooseFiles = false; panel.allowsMultipleSelection = false
        panel.directoryURL = fileURL?.deletingLastPathComponent()
        panel.message = "DICOMs im gewählten Ordner und seinen Unterordnern suchen. Zuordnung nur über Patientenidentität und DICOM-Referenzen."
        let version = generation
        let complete: (NSApplication.ModalResponse) -> Void = { [weak self] response in
            guard let self = self else { return }
            guard self.generation == version, response == .OK, let root = panel.url else { self.reply(request, ["cancelled": true]); return }
            _ = root.startAccessingSecurityScopedResource()
            var command = request; command["op"] = "discover"; command["root"] = root.path
            self.send(command);self.extraScopes.append(root)
        }
        if let window = view.window { panel.beginSheetModal(for: window, completionHandler: complete) }
        else { panel.begin(completionHandler: complete) }
    }
    private var extraScopes: [URL] = []
    private func saveExport(_ request: [String: Any]) {
        guard let kind = request["kind"] as? String, ["png", "csv", "zip"].contains(kind) else { return }
        let panel = NSSavePanel()
        let name = request["filename"] as? String ?? "QuickLookRT"
        panel.nameFieldStringValue = name.replacingOccurrences(of: "/", with: "_").replacingOccurrences(of: ":", with: "_") + "." + kind
        panel.canCreateDirectories = true
        let version = generation
        let complete: (NSApplication.ModalResponse) -> Void = { [weak self] response in
            guard let self = self else { return }
            guard self.generation == version, response == .OK, let destination = panel.url else { self.reply(request, ["cancelled": true]); return }
            self.writeExport(request, kind: kind, destination: destination)
        }
        if let window = view.window { panel.beginSheetModal(for: window, completionHandler: complete) }
        else { panel.begin(completionHandler: complete) }
    }
    private func writeExport(_ request: [String: Any], kind: String, destination: URL) {
        let version = generation
        if kind != "png" { var command = request; command["op"] = "export"; command["destination"] = destination.path; send(command); return }
        let finish: (NSImage?) -> Void = { [weak self] image in
            guard let self = self, let image = image else { self?.showError(id: request["id"] as? Int ?? 0); return }
            guard self.generation == version else { self.reply(request, ["cancelled": true]);return }
            let caption = request["caption"] as? String ?? "QuickLookRT"
            let stamped = NSImage(size: NSSize(width: image.size.width, height: image.size.height + 32))
            stamped.lockFocus()
            NSColor(calibratedWhite: 0.06, alpha: 1).setFill(); NSRect(origin: .zero, size: stamped.size).fill()
            image.draw(in: NSRect(x: 0, y: 32, width: image.size.width, height: image.size.height))
            (caption as NSString).draw(in: NSRect(x: 10, y: 8, width: image.size.width-20, height: 20), withAttributes: [.font: NSFont.systemFont(ofSize: 12), .foregroundColor: NSColor.white])
            stamped.unlockFocus()
            do {
                guard let tiff = stamped.tiffRepresentation, let bitmap = NSBitmapImageRep(data: tiff), let png = bitmap.representation(using: .png, properties: [:]) else { throw NSError(domain: "Export", code: 1) }
                try png.write(to: destination, options: .atomic); self.reply(request, ["saved": true])
            } catch { self.showError(id: request["id"] as? Int ?? 0) }
        }
        if let encoded = request["image"] as? String, encoded.hasPrefix("data:image/png;base64,"), let data = Data(base64Encoded: String(encoded.dropFirst(22))) { finish(NSImage(data: data)) }
        else {
            var config: WKSnapshotConfiguration? = nil
            if let rect = request["rect"] as? [Double], rect.count == 4, rect.allSatisfy({ $0.isFinite }) {
                let requested = NSRect(x: rect[0], y: rect[1], width: rect[2], height: rect[3]).intersection(web.bounds)
                if requested.width > 0 && requested.height > 0 { let c = WKSnapshotConfiguration(); c.rect = requested; config = c }
            }
            web.takeSnapshot(with: config) { image, _ in finish(image) }
        }
    }
    private func send(_ request: [String: Any], after: (() -> Void)? = nil) {
        var body = request
        body["session"] = sessionID
        guard let data = try? JSONSerialization.data(withJSONObject: body), let json = String(data: data, encoding: .utf8) else { return }
        let version = generation, directory = resources.appendingPathComponent("Worker").path
        let work: () -> Void = { [weak self] in
            guard let self = self, version == self.generation else { return }
            let reply = json.withCString { requestPtr in directory.withCString { directoryPtr in qlrt_call(requestPtr, directoryPtr) } }
            guard let reply = reply else {
                DispatchQueue.main.async { [weak self] in self?.showError(id: request["id"] as? Int ?? 0) }
                return
            }
            let result = String(cString: reply)
            qlrt_release(reply)
            DispatchQueue.main.async { [weak self] in
                guard let self = self, version == self.generation else { return }
                self.web.evaluateJavaScript("window.receive(\(result))") { _, _ in after?() }
            }
        }
        let apertureOnly = request["op"] as? String == "beam" && request["drr"] as? Bool != true && request["outlines"] as? Bool != true && request["tracks"] as? Bool != true
        if ["progress", "cancelWork"].contains(request["op"] as? String ?? "") { DispatchQueue.global(qos: .utility).async(execute: work) }
        else if !apertureOnly && ["mesh", "beam", "dvh", "volume", "doseReference"].contains(request["op"] as? String ?? "") {
            let operation = BlockOperation(block: work)
            operation.queuePriority = request["background"] as? Bool == true ? (request["priority"] as? String == "dvh" ? .normal : .low) : .veryHigh
            preparation.addOperation(operation)
        } else { io.async(execute: work) }
    }
    private func showError(id: Int) {
        web.evaluateJavaScript("window.receive({id:\(id),error:'DICOM-Prozess nicht verfügbar. App neu öffnen und Ordnerzugriff prüfen.'})", completionHandler: nil)
    }
    private func stopWorker() {
        preparation.cancelAllOperations()
        let old = sessionID
        sessionID = UUID().uuidString
        DispatchQueue.global(qos: .userInitiated).async { old.withCString { qlrt_close($0) } }
    }
    func performViewerCommand(_ command: String) {
        let commands = ["settings":"$('settings').click()", "tags":"$('tagsToggle').click()", "rt":"$('rtToggle').click()", "png":"savePng()", "zip":"$('zipButton').click()", "fit":"$('fit').click()", "help":"$('help').click()", "find":"openTagSearch()"]
        if let script = commands[command] { web.evaluateJavaScript(script, completionHandler: nil) }
    }
    func webView(_ webView: WKWebView, decidePolicyFor navigationAction: WKNavigationAction, decisionHandler: @escaping (WKNavigationActionPolicy) -> Void) {
        let url = navigationAction.request.url
        decisionHandler(url?.isFileURL == true && url!.standardizedFileURL.path.hasPrefix(resources.appendingPathComponent("Web/").path) ? .allow : .cancel)
    }
    func webView(_ webView: WKWebView, didFailProvisionalNavigation navigation: WKNavigation!, withError error: Error) {
        showError(id: 0)
    }
    func webViewWebContentProcessDidTerminate(_ webView: WKWebView) {
        showError(id: 0)
    }
    func closePreview() { generation += 1; stopWorker(); folderScope?.stopAccessingSecurityScopedResource(); folderScope = nil; extraScopes.forEach { $0.stopAccessingSecurityScopedResource() }; extraScopes.removeAll() }
    #if PREVIEW_TESTS
    func evaluateForTesting(_ script: String, completion: @escaping (Any?, Error?) -> Void) { web.evaluateJavaScript(script, completionHandler: completion) }
    func executeAsyncForTesting(_ script: String, completion: @escaping (Any?, Error?) -> Void) {
        web.callAsyncJavaScript(script, arguments: [:], in: nil, in: .page) { result in
            switch result { case .success(let value): completion(value, nil); case .failure(let error): completion(nil, error) }
        }
    }
    func snapshotForTesting(_ url: URL, completion: @escaping () -> Void) {
        web.takeSnapshot(with: nil) { image, _ in
            if let data = image?.tiffRepresentation, let rep = NSBitmapImageRep(data: data), let png = rep.representation(using: .png, properties: [:]) { try? png.write(to: url) }
            completion()
        }
    }
    #endif
    deinit { sessionID.withCString { qlrt_close($0) }; folderScope?.stopAccessingSecurityScopedResource() }
}
private final class WeakHandler: NSObject, WKScriptMessageHandler {
    weak var owner: PreviewController?
    init(_ owner: PreviewController) { self.owner = owner }
    func userContentController(_ userContentController: WKUserContentController, didReceive message: WKScriptMessage) { owner?.userContentController(userContentController, didReceive: message) }
}
