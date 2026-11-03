# Finder previews in arbitrary folders

## Requested behavior and design

Selecting one DICOM file and pressing Space should automatically resolve the related plan, image series, structures and doses in its containing folder, regardless of where that folder is stored. Preserve the original viewer and staged loading. Do not require a folder picker for ordinary readable locations.

The previous preview sandbox allowed only the dedicated local library or a user-selected folder bookmark. Finder supplies access to the selected file, which does not imply access to its siblings. A signed sandbox probe reproduced that difference: directory enumeration and sibling reads failed under the old entitlements.

Use Apple's documented `com.apple.security.temporary-exception.files.absolute-path.read-only` entitlement with `/` in the directly distributed development preview. This gives the extension read-only filesystem access wherever normal user permissions and macOS privacy controls permit it. The extension remains sandboxed; unrelated writes remain denied. This is a deliberate broader read permission for the requested workflow, not a per-selected-folder sandbox grant. The reader continues to scan only the selected containing folder unless the user explicitly starts a wider search. No indexing of the whole disk is introduced.

Alternatives: saved folder bookmarks still require the unwanted first picker; an unsandboxed helper introduces IPC and process lifetime complexity; Full Disk Access is a separate user-managed privacy permission and does not by itself remove App Sandbox restrictions. Keep the existing contextual folder picker as a fallback when macOS actually denies access. Protected locations, filesystem ACLs, offline shares and cloud files not downloaded locally can still prevent reading. App Store acceptance of the temporary exception is not assumed.

Reference: [Apple's file access temporary exceptions](https://developer.apple.com/library/archive/documentation/Miscellaneous/Reference/EntitlementKeyReference/Chapters/AppSandboxTemporaryExceptionEntitlements.html#//apple_ref/doc/uid/TP40011195-CH5-SW7).

## Implementation and verification plan

- Add a regression probe signed with the actual preview entitlement file. Require enumeration and sibling reads in two never-granted locations, including a path with spaces; reject opening a sibling for writing or creating a new file.
- Replace the dedicated-library exception with the read-only absolute-path exception. Preserve user-selected export destinations, bookmarks and sandboxing.
- Test native staged loading from a CT file without bookmarks using relocated, byte-identical copies of all three original cases. Check full plan/image/structure/dose context and automatic Target/Organ DVHs. Verify source and copied files remain unchanged.
- Build and strictly verify signing, install the new version, and check the installed Finder extension using Space in a folder outside the dedicated library. Record the actual tested scope, update the Mac README, commit and push.

## Verification results

Verified locally on 2026-11-03 with version 0.2.46, bundle 2026110306:

- Regression test failed under the previous entitlements: sibling reads and directory enumeration were denied. With the actual new preview entitlements, two never-granted locations passed both operations; opening the fixture for writing and creating a new file remained denied.
- Three byte-identical original cases were relocated outside the dedicated library into folders with spaces. Starting with one CT file loaded 173/155/184 files, one plan and dose per case, 49/43/62 structures, and 47/42/51 automatically computed Target/Organ DVHs. All cases displayed staged first content and needed no folder picker.
- Starting from RTPLAN, RTSTRUCT and RTDOSE independently also loaded the full first-case context and all 47 automatic DVHs, without a picker.
- The installed Finder extension was invoked with Space on one relocated CT. The original 2×2 viewer showed all 43 structures, the plan and dose; opening DVH showed the populated curves without a calculation click or folder dialog. These screenshots were reviewed locally, not published.
- All 512 original files and 512 copies matched their pre-test SHA256 hashes. Strict app/extension signing, version, installed sandbox/read-only entitlements, active extension registration and installed Web assets matching source passed.

The permission probe covers ordinary local paths in the user home and temporary storage. Native tests and Finder verification cover relocated original DICOM data in temporary storage. Protected Documents/Desktop/Downloads, removable media, network mounts and cloud-provider behavior were not exhaustively tested; TCC and filesystem permissions remain independent limits. No new LINAC compatibility claim is made.
