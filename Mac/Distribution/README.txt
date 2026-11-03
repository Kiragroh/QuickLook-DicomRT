QuickLookRT for macOS — development preview

Apple silicon (arm64), macOS 13 or later. Self-contained: no separate .NET installation needed.

INSTALL / UPDATE
1. Extract the ZIP. Keep QuickLookRT.app and Install.command together.
2. Run Install.command to install/update in ~/Applications and register the Finder preview.
3. Close an old preview, select one DICOM file in Finder and press Space.
   You can also open QuickLookRT.app and select a DICOM file.

This development package is ad-hoc signed, not Developer ID signed or notarized.
macOS may block opening a downloaded build. If you trust this project, use the
normal Privacy & Security “Open Anyway” flow; the installer does not disable Gatekeeper.

AUTOMATIC PLAN CONTEXT
Related CT, plan, structures and dose are found in nearby nested export folders
using DICOM patient identity and explicit references. Several matching plans or
a large search area produce a choice. No folder picker is needed for ordinary
readable locations. macOS privacy permissions, filesystem ACLs and unavailable
network/cloud files can still prevent access.

The Finder extension has read-only filesystem access for this workflow. Original
DICOM files are not modified. Only the selected neighborhood is searched; no
whole-disk index, patient upload, server or telemetry is used. Exports write only
to a user-selected destination. Viewer settings are stored locally.

Inspection/development software. The software checks do not constitute clinical
validation or proof of compatibility with every vendor export.

Source, test scope and current documentation:
https://github.com/Kiragroh/QuickLook-DicomRT/tree/macos-preview/Mac
