#!/bin/bash
set -euo pipefail
MAC_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
REPO_ROOT="$(cd "$MAC_ROOT/.." && pwd)"
DOTNET="${DOTNET:-$HOME/.local/share/quicklookrt-dotnet/dotnet}"
if [[ ! -x "$DOTNET" ]]; then DOTNET="$(command -v dotnet)"; fi
ARCH="${ARCH:-$(uname -m)}"
case "$ARCH" in arm64) RID=osx-arm64;; x86_64) RID=osx-x64;; *) exit 1;; esac
IDENTITY="${SIGN_IDENTITY:--}"
SIGN_OPTIONS=(--timestamp=none)
if [[ "$IDENTITY" != "-" ]]; then SIGN_OPTIONS=(--options runtime --timestamp); fi
APP="$REPO_ROOT/artifacts/macos/$ARCH/QuickLookRT.app"
EXT="$APP/Contents/PlugIns/QuickLookRTPreview.appex"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources" "$EXT/Contents/MacOS" "$EXT/Contents/Resources"
ICONSET="$REPO_ROOT/artifacts/macos/$ARCH/QuickLookRT.iconset"
mkdir -p "$ICONSET"
for SIZE in 16 32 128 256 512; do
  sips -z "$SIZE" "$SIZE" "$REPO_ROOT/assets/dicom-rt.png" --out "$ICONSET/icon_${SIZE}x${SIZE}.png" >/dev/null
  DOUBLE=$((SIZE * 2))
  sips -z "$DOUBLE" "$DOUBLE" "$REPO_ROOT/assets/dicom-rt.png" --out "$ICONSET/icon_${SIZE}x${SIZE}@2x.png" >/dev/null
done
iconutil -c icns "$ICONSET" -o "$APP/Contents/Resources/QuickLookRT.icns"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
"$DOTNET" publish "$MAC_ROOT/Worker" -c Release -r "$RID" --self-contained true -p:PublishSingleFile=false -o "$APP/Contents/Resources/Worker"
ditto "$MAC_ROOT/Web" "$APP/Contents/Resources/Web"
ditto "$APP/Contents/Resources/Worker" "$EXT/Contents/Resources/Worker"
ditto "$MAC_ROOT/Web" "$EXT/Contents/Resources/Web"
cp "$MAC_ROOT/Native/App-Info.plist" "$APP/Contents/Info.plist"
cp "$MAC_ROOT/Native/Preview-Info.plist" "$EXT/Contents/Info.plist"
SDK="$(xcrun --sdk macosx --show-sdk-path)"
xcrun clang -arch "$ARCH" -mmacosx-version-min=13.0 -isysroot "$SDK" -c "$MAC_ROOT/Native/DotNetHost.c" -o "$REPO_ROOT/artifacts/macos/$ARCH/DotNetHost.o"
xcrun swiftc -swift-version 5 -O -sdk "$SDK" -target "$ARCH-apple-macosx13.0" -framework AppKit -framework WebKit \
  -import-objc-header "$MAC_ROOT/Native/DotNetHost.h" "$REPO_ROOT/artifacts/macos/$ARCH/DotNetHost.o" \
  "$MAC_ROOT/Native/PreviewController.swift" "$MAC_ROOT/Native/App.swift" -o "$APP/Contents/MacOS/QuickLookRT"
xcrun swiftc -swift-version 5 -O -sdk "$SDK" -target "$ARCH-apple-macosx13.0" -D QUICKLOOK_EXTENSION -application-extension \
  -framework AppKit -framework WebKit -framework QuickLookUI -parse-as-library -Xlinker -e -Xlinker _NSExtensionMain \
  -import-objc-header "$MAC_ROOT/Native/DotNetHost.h" "$REPO_ROOT/artifacts/macos/$ARCH/DotNetHost.o" \
  "$MAC_ROOT/Native/PreviewController.swift" "$MAC_ROOT/Native/QuickLookController.swift" -o "$EXT/Contents/MacOS/QuickLookRTPreview"
for RES in "$APP/Contents/Resources" "$EXT/Contents/Resources"; do
  for LIB in "$RES"/Worker/*.dylib; do
    codesign --force --sign "$IDENTITY" "${SIGN_OPTIONS[@]}" "$LIB"
  done
  ENTITLEMENTS="$MAC_ROOT/Native/App.entitlements"
  if [[ "$RES" == "$EXT/Contents/Resources" ]]; then ENTITLEMENTS="$MAC_ROOT/Native/Worker.entitlements"; fi
  codesign --force --sign "$IDENTITY" "${SIGN_OPTIONS[@]}" --entitlements "$ENTITLEMENTS" "$RES/Worker/QuickLookRT.Worker"
done
codesign --force --sign "$IDENTITY" "${SIGN_OPTIONS[@]}" --entitlements "$MAC_ROOT/Native/Preview.entitlements" "$EXT"
codesign --force --sign "$IDENTITY" "${SIGN_OPTIONS[@]}" --entitlements "$MAC_ROOT/Native/App.entitlements" "$APP"
codesign --verify --deep --strict "$APP"
printf 'Built: %s\n' "$APP"
