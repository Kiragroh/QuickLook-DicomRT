# Automatic discovery across export subfolders

Requested: selecting one DICOM file should resolve a split CT/RTPLAN/RTSTRUCT/RTDOSE export without selecting a search folder. Ask when several compatible plans exist or the neighborhood is too large. Keep the original viewer; publish a separate macOS GitHub release.

## Design and implementation plan

Keep staged single-file display. On the full-context request, inspect descendants of the selected containing folder and progressively expand through at most three parent folders. Stop at user-home, common collection folders or volume roots. Never follow directory symlinks. Automatic enumeration is bounded to 4,096 files, 128 directories and six descendant levels. Stop once complete referenced context or a meaningful ambiguity is found.

Use the original DICOM catalog and patient/reference matching; merge split slices into their real series before decoding. No association based on folder names, matching frame alone or file ordering. Explicitly opened RT objects retain priority. Multiple plans explicitly linked to the selected image/structure produce a plan-choice sheet; no plan is silently selected. A larger/incomplete search produces an in-view scope-choice sheet using discovered folders and a bounded continuation action, not a new OS folder picker. Cancel leaves the available selected-file/image context visible.

Steps: add failing generated worker contracts; implement bounded discovery and its request/model contract in `Mac/Worker/AutomaticDiscovery.cs`; connect the full native request and existing HTML modal; verify generated ambiguity/limit/foreign-patient cases and all three byte-identical originals split across nested folders; verify installed Finder behavior and signing; package the self-contained arm64 app with installation instructions and SHA256; push the branch and create a separately tagged macOS release. Keep source data and private evidence out of Git and release assets.

The existing manual search stays available for unsupported geometry, denied access, missing references and exports outside the bounded neighborhood. macOS privacy/ACL limits remain in force. Initial release validation targets Apple silicon; no untested Intel support claim.

## Evidence

Verified on 2026-11-03 with 0.2.47 / bundle 2026110307:

- The generated regression initially failed because only one CT subfolder was loaded. It now covers a 33-plane CT split into two parts, separate RT/registration folders, all four CT/RT entry points, exact versus ambiguous plans, foreign-patient exclusion, duplicate SOP instances, skipped directory symlinks, required registration outside the case folder, a 4,100-file noisy search area, explicit continuation and rejection of stale choices.
- Native choice acceptance passed 10 checks: a large-area prompt continues into a two-plan choice; initial keyboard focus, cancel, reopen, Enter, correct selected plan/dose/structures, retained merged CT and a calculated DVH all work. This caught and fixed a missing native request allowlist entry before publication.
- All three original cases were copied byte-for-byte and split into separate CT parts and RT object folders. Native opening from one CT loaded 173/155/184 source instances, 49/43/62 structures and 47/42/51 automatic Target/Organ DVHs. No folder picker was needed, and staged first content remained visible.
- Installed Finder Space was tested on a CT in a deeply nested part folder. It showed the complete 43-structure/one-plan/one-dose context and already populated DVH curves. Private screenshots were reviewed locally and were not included in Git or release assets.
- Original Core synthetic checks, 130 worker integration checks, 123 parity checks and the actual-entitlement read-only sandbox probe passed. Unrelated files encountered during filtered discovery now handle the DICOM reader's null result safely; deduplicated instances no longer discard their remaining valid image stack.
- The self-contained arm64 ZIP was extracted, its checksum/signature/payload/installer verified, and installed using that extracted installer. The package includes instructions and runtime license notices; no DICOM fixtures, private exports or screenshots are shipped. The build is ad-hoc signed, not notarized.

These checks cover local storage and the described synthetic/original arrangements. Offline cloud files, remote filesystem latency, protected locations and exports outside the bounded neighborhood remain explicit limits. No new LINAC compatibility claim is made; the original examples are unchanged.
