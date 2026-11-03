#!/bin/bash
set -euo pipefail
MAC_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ROOT="$(cd "$MAC_ROOT/.." && pwd)"
ARCH="${ARCH:-$(uname -m)}"
APP="$ROOT/artifacts/macos/$ARCH/QuickLookRT.app"
VERSION="$(/usr/libexec/PlistBuddy -c 'Print CFBundleShortVersionString' "$APP/Contents/Info.plist")"
OUT="$ROOT/artifacts/macos/releases"
mkdir -p "$OUT"
STAGE="$(mktemp -d "$OUT/.package-XXXXXX")"
trap 'rm -rf "$STAGE"' EXIT
codesign --verify --deep --strict "$APP"
ditto "$APP" "$STAGE/QuickLookRT.app"
cp "$MAC_ROOT/Distribution/Install.command" "$MAC_ROOT/Distribution/README.txt" "$STAGE/"
cp "$MAC_ROOT/Distribution/THIRD_PARTY.txt" "$STAGE/"
mkdir -p "$STAGE/licenses"
cp "$ROOT/licenses/fo-dicom-MS-PL.html" "$ROOT/licenses/Microsoft-"* "$STAGE/licenses/"
cp "$MAC_ROOT/Distribution/Clipper2-LICENSE.txt" "$MAC_ROOT/Web/THREE-LICENSE.txt" "$STAGE/licenses/"
DOTNET="${DOTNET:-$HOME/.local/share/quicklookrt-dotnet/dotnet}"
DOTNET_ROOT_DIR="$(dirname "$DOTNET")"
if [[ -f "$DOTNET_ROOT_DIR/LICENSE.txt" ]]; then cp "$DOTNET_ROOT_DIR/LICENSE.txt" "$STAGE/licenses/Dotnet-LICENSE.txt"; fi
if [[ -f "$DOTNET_ROOT_DIR/ThirdPartyNotices.txt" ]]; then cp "$DOTNET_ROOT_DIR/ThirdPartyNotices.txt" "$STAGE/licenses/Dotnet-ThirdPartyNotices.txt"; fi
chmod +x "$STAGE/Install.command"
ZIP="$OUT/QuickLookRT-macOS-$VERSION-$ARCH.zip"
ditto -c -k --sequesterRsrc "$STAGE" "$ZIP"
(cd "$OUT" && shasum -a 256 "$(basename "$ZIP")" > "$(basename "$ZIP").sha256")
printf 'Packaged: %s\n' "$ZIP"
