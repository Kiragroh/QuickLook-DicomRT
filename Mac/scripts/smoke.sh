#!/bin/bash
set -euo pipefail
MAC_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ROOT="$(cd "$MAC_ROOT/.." && pwd)"
SMOKE="$ROOT/artifacts/macos/Smoke.app"
APP="$ROOT/artifacts/macos/$(uname -m)/QuickLookRT.app"
mkdir -p "$SMOKE/Contents/MacOS"
ditto "$APP/Contents/Resources" "$SMOKE/Contents/Resources"
cp "$MAC_ROOT/Native/App-Info.plist" "$SMOKE/Contents/Info.plist"
/usr/libexec/PlistBuddy -c 'Set CFBundleExecutable Smoke' "$SMOKE/Contents/Info.plist"
/usr/libexec/PlistBuddy -c 'Set CFBundleIdentifier de.kiragroh.QuickLookRT.Smoke' "$SMOKE/Contents/Info.plist"
/usr/libexec/PlistBuddy -c 'Delete CFBundleDocumentTypes' "$SMOKE/Contents/Info.plist"
/usr/libexec/PlistBuddy -c 'Delete UTExportedTypeDeclarations' "$SMOKE/Contents/Info.plist"
xcrun swiftc -swift-version 5 -D PREVIEW_TESTS -framework AppKit -framework WebKit \
  -import-objc-header "$MAC_ROOT/Native/DotNetHost.h" "$ROOT/artifacts/macos/$(uname -m)/DotNetHost.o" \
  "$MAC_ROOT/Native/PreviewController.swift" "$MAC_ROOT/Tests/Smoke.swift" -o "$SMOKE/Contents/MacOS/Smoke"
codesign --force --sign - --entitlements "$MAC_ROOT/Native/App.entitlements" "$SMOKE"
"$SMOKE/Contents/MacOS/Smoke" "$ROOT/artifacts/macos/Synthetic Plan mit Leerzeichen/RTPLAN.dcm" "$ROOT/artifacts/macos/smoke-quad.png"
