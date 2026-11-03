# QuickLookRT macOS completion brief

Preserve the original Windows viewer's compact dark interface and inspection workflow. Finder Space on one DICOM should expose its related plan, structures, doses, DVH, MLC/DRR animation and exports whenever local access and explicit DICOM references permit. The same native viewer is used by Finder and the standalone application. Source data remain unchanged.

The user authorized implementation, tests on three original example folders, installation and pushing the macos-preview branch. Additional source arrangements are tested with generated data. No private identifiers, filenames, UIDs, images or local share paths may enter public evidence.

## Design plan

- Archetype: document inspection window plus Finder preview; original RT sidebar and optional Tags inspector.
- Information architecture: RT context picker; Structures/Doses tabs; Image, MLC, DVH and 3D workspaces.
- Commands: native Open/Close/Quit/Settings; Command 1–6 Native/Axial/Coronal/Sagittal/2×2/3D; Command I isocenters; Command F tags; Command Shift S PNG; Option I/M/D/V workspaces; Home fit.
- Empty states: selected file first while context loads; unavailable association/registration states inline; contextual local folder chooser only when access is missing.
- Feeling: quick, readable inspection of original data. New controls stay within existing groups. The user's original-layout requirement takes precedence over replacing the interface with new platform chrome.

Technical contracts and verification gates: [macOS parity plan](macos-parity-plan.md). Reusable cross-platform changes: [Windows design improvements](windows-design-improvements.md).
