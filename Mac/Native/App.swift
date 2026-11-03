import AppKit

final class AppDelegate: NSObject, NSApplicationDelegate, NSWindowDelegate {
    private var openingPanel: NSOpenPanel?
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
        appMenu.addItem(withTitle: "Einstellungen …", action: #selector(viewerCommand(_:)), keyEquivalent: ",").representedObject = "settings"
        appMenu.items.last?.target = self
        appMenu.addItem(.separator())
        appMenu.addItem(withTitle: "QuickLookRT beenden", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q")
        let fileItem = NSMenuItem(title: "Ablage", action: nil, keyEquivalent: "");menu.addItem(fileItem)
        let fileMenu = NSMenu(title: "Ablage");fileItem.submenu = fileMenu
        fileMenu.addItem(withTitle: "DICOM öffnen …", action: #selector(openDocument), keyEquivalent: "o").target = self
        for (title,command,key) in [("Vorschau als PNG sichern …","png","S"),("DICOM als ZIP exportieren …","zip","")] {
            let entry=fileMenu.addItem(withTitle:title,action:#selector(viewerCommand(_:)),keyEquivalent:key);entry.target=self;entry.representedObject=command
        }
        fileMenu.addItem(.separator());fileMenu.addItem(withTitle:"Schließen",action:#selector(NSWindow.performClose(_:)),keyEquivalent:"w")
        let editItem = NSMenuItem(title: "Bearbeiten", action: nil, keyEquivalent: "")
        menu.addItem(editItem)
        let editMenu = NSMenu(title: "Bearbeiten"); editItem.submenu = editMenu
        editMenu.addItem(withTitle: "Ausschneiden", action: #selector(NSText.cut(_:)), keyEquivalent: "x")
        editMenu.addItem(withTitle: "Kopieren", action: #selector(NSText.copy(_:)), keyEquivalent: "c")
        editMenu.addItem(withTitle: "Einsetzen", action: #selector(NSText.paste(_:)), keyEquivalent: "v")
        editMenu.addItem(withTitle: "Alles auswählen", action: #selector(NSText.selectAll(_:)), keyEquivalent: "a")
        let viewItem=NSMenuItem(title:"Darstellung",action:nil,keyEquivalent:"");menu.addItem(viewItem);let viewMenu=NSMenu(title:"Darstellung");viewItem.submenu=viewMenu
        for (title,command,key) in [("RT-Bereich ein-/ausblenden","rt",""),("Tags ein-/ausblenden","tags",""),("DICOM-Tags suchen","find","f"),("Einpassen","fit","")] {
            let entry=viewMenu.addItem(withTitle:title,action:#selector(viewerCommand(_:)),keyEquivalent:key);entry.target=self;entry.representedObject=command
        }
        let helpItem=NSMenuItem(title:"Hilfe",action:nil,keyEquivalent:"");menu.addItem(helpItem);let helpMenu=NSMenu(title:"Hilfe");helpItem.submenu=helpMenu;let help=helpMenu.addItem(withTitle:"QuickLookRT-Hilfe",action:#selector(viewerCommand(_:)),keyEquivalent:"");help.target=self;help.representedObject="help"
        NSApp.mainMenu = menu
        if CommandLine.arguments.count > 1 {
            for path in CommandLine.arguments.dropFirst() where !path.hasPrefix("-") { show(URL(fileURLWithPath: path)) }
        } else if windows.isEmpty { openDocument() }
        NSApp.activate(ignoringOtherApps: true)
    }
    func application(_ sender: NSApplication, openFile filename: String) -> Bool { show(URL(fileURLWithPath: filename)); return true }
    @objc private func viewerCommand(_ sender: NSMenuItem) {
        guard let command=sender.representedObject as? String else { return }
        (NSApp.keyWindow?.contentViewController as? PreviewController)?.performViewerCommand(command)
    }
    @objc private func openDocument() {
        let panel = NSOpenPanel(); panel.canChooseDirectories = false; panel.allowsMultipleSelection = false
        panel.message = "Eine DICOM-Datei wählen. Der zugehörige Plan wird im selben Ordner gesucht."
        openingPanel = panel
        panel.begin { [weak self] response in
            self?.openingPanel = nil
            if response == .OK, let url = panel.url { self?.show(url) }
        }
    }
    private func show(_ url: URL) {
        openingPanel?.cancel(nil);openingPanel = nil
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
