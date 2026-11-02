import AppKit

final class AppDelegate: NSObject, NSApplicationDelegate, NSWindowDelegate {
    private var windows: [NSWindow] = []
    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.regular)
        if let icon = Bundle.main.url(forResource: "QuickLookRT", withExtension: "icns") {
            NSApp.applicationIconImage = NSImage(contentsOf: icon)
        }
        NSApp.activate(ignoringOtherApps: true)
        let menu = NSMenu()
        let item = NSMenuItem(); menu.addItem(item)
        let appMenu = NSMenu(); item.submenu = appMenu
        appMenu.addItem(withTitle: "DICOM öffnen …", action: #selector(openDocument), keyEquivalent: "o").target = self
        appMenu.addItem(.separator())
        appMenu.addItem(withTitle: "QuickLookRT beenden", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q")
        let editItem = NSMenuItem(title: "Bearbeiten", action: nil, keyEquivalent: "")
        menu.addItem(editItem)
        let editMenu = NSMenu(title: "Bearbeiten"); editItem.submenu = editMenu
        editMenu.addItem(withTitle: "Ausschneiden", action: #selector(NSText.cut(_:)), keyEquivalent: "x")
        editMenu.addItem(withTitle: "Kopieren", action: #selector(NSText.copy(_:)), keyEquivalent: "c")
        editMenu.addItem(withTitle: "Einsetzen", action: #selector(NSText.paste(_:)), keyEquivalent: "v")
        editMenu.addItem(withTitle: "Alles auswählen", action: #selector(NSText.selectAll(_:)), keyEquivalent: "a")
        NSApp.mainMenu = menu
        if CommandLine.arguments.count > 1 {
            for path in CommandLine.arguments.dropFirst() where !path.hasPrefix("-") { show(URL(fileURLWithPath: path)) }
        } else if windows.isEmpty { openDocument() }
        NSApp.activate(ignoringOtherApps: true)
    }
    func application(_ sender: NSApplication, openFile filename: String) -> Bool { show(URL(fileURLWithPath: filename)); return true }
    @objc private func openDocument() {
        let panel = NSOpenPanel(); panel.canChooseDirectories = false; panel.allowsMultipleSelection = false
        panel.message = "Eine DICOM-Datei wählen. Der zugehörige Plan wird im selben Ordner gesucht."
        if panel.runModal() == .OK, let url = panel.url { show(url) }
    }
    private func show(_ url: URL) {
        let controller = PreviewController()
        let window = NSWindow(contentRect: NSRect(x: 0,y: 0,width: 1180,height: 800), styleMask: [.titled,.closable,.miniaturizable,.resizable], backing: .buffered, defer: false)
        window.title = "QuickLookRT · " + url.lastPathComponent
        window.contentViewController = controller; window.delegate = self
        window.minSize = NSSize(width: 780,height: 560); window.center(); window.makeKeyAndOrderFront(nil)
        windows.append(window); controller.openFile(url)
    }
    func windowWillClose(_ notification: Notification) {
        guard let window = notification.object as? NSWindow else { return }
        (window.contentViewController as? PreviewController)?.closePreview()
        windows.removeAll { $0 === window }
    }
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { true }
}
@main enum Main {
    static func main() {
        let delegate = AppDelegate()
        NSApplication.shared.delegate = delegate
        withExtendedLifetime(delegate) { NSApplication.shared.run() }
    }
}
