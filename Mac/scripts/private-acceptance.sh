#!/bin/bash
set -euo pipefail
MAC_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ROOT="$(cd "$MAC_ROOT/.." && pwd)"
INDEX="${1:?Pass a local, untracked original-example index JSON}"
TEST_SOURCE="${2:-PrivateAcceptance}"
TEST_APP="$ROOT/artifacts/macos/$TEST_SOURCE.app"
APP="$ROOT/artifacts/macos/$(uname -m)/QuickLookRT.app"
mkdir -p "$TEST_APP/Contents/MacOS"
ditto "$APP/Contents/Resources" "$TEST_APP/Contents/Resources"
cp "$MAC_ROOT/Native/App-Info.plist" "$TEST_APP/Contents/Info.plist"
/usr/libexec/PlistBuddy -c "Set CFBundleExecutable $TEST_SOURCE" "$TEST_APP/Contents/Info.plist"
/usr/libexec/PlistBuddy -c "Set CFBundleIdentifier de.kiragroh.QuickLookRT.$TEST_SOURCE" "$TEST_APP/Contents/Info.plist"
/usr/libexec/PlistBuddy -c 'Delete CFBundleDocumentTypes' "$TEST_APP/Contents/Info.plist"
/usr/libexec/PlistBuddy -c 'Delete UTExportedTypeDeclarations' "$TEST_APP/Contents/Info.plist"
xcrun swiftc -swift-version 5 -O -D PREVIEW_TESTS -D QUICKLOOK_EXTENSION -framework AppKit -framework WebKit \
  -import-objc-header "$MAC_ROOT/Native/DotNetHost.h" "$ROOT/artifacts/macos/$(uname -m)/DotNetHost.o" \
  "$MAC_ROOT/Native/PreviewController.swift" "$MAC_ROOT/Tests/$TEST_SOURCE.swift" -o "$TEST_APP/Contents/MacOS/$TEST_SOURCE"
TEST_ENTITLEMENTS="$MAC_ROOT/Native/Preview.entitlements"
if [[ "${TEST_SANDBOX:-1}" == "0" ]]; then TEST_ENTITLEMENTS="$MAC_ROOT/Native/App.entitlements"; fi
codesign --force --sign - --entitlements "$TEST_ENTITLEMENTS" "$TEST_APP" >/dev/null 2>&1
python3 - "$INDEX" "$TEST_APP/Contents/MacOS/$TEST_SOURCE" <<'PY'
import json,os,subprocess,sys
from pathlib import Path
index=json.loads(Path(sys.argv[1]).read_text())
for i,case in enumerate(index,1):
    modality=os.environ.get('TEST_MODALITY', 'CT' if sys.argv[2].endswith('/FolderAccessAcceptance') else 'RTPLAN')
    entry=next(x['path'] for x in case['entries'] if x['modality']==modality)
    result=subprocess.run([sys.argv[2],entry],stdout=subprocess.PIPE,stderr=subprocess.DEVNULL,text=True,timeout=420)
    # The harness emits aggregate counts or fixed assertion labels only, never source paths.
    print('Case',i,modality,':',result.stdout.strip(),flush=True)
    if result.returncode:raise SystemExit(result.returncode)
PY
