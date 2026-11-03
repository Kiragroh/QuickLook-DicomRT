#!/bin/bash
set -euo pipefail
PACKAGE_DIR="$(cd "$(dirname "$0")" && pwd)"
SOURCE="$PACKAGE_DIR/QuickLookRT.app"
DEST="$HOME/Applications/QuickLookRT.app"
[[ -d "$SOURCE" ]] || { echo 'Keep Install.command next to QuickLookRT.app.'; exit 1; }
codesign --verify --deep --strict "$SOURCE"
mkdir -p "$HOME/Applications"
STAGE="$HOME/Applications/.QuickLookRT-release-$$.app"
trap 'rm -rf "$STAGE"' EXIT
ditto "$SOURCE" "$STAGE"
codesign --verify --deep --strict "$STAGE"
pkill -x QuickLookRTPreview || true
pkill -x QuickLookRT || true
if [[ -e "$DEST" ]]; then
  /System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister -u "$DEST" || true
  mv "$DEST" "$HOME/Applications/QuickLookRT-backup-$(date +%Y%m%d-%H%M%S).app-backup"
fi
mv "$STAGE" "$DEST"
/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister -f "$DEST"
pluginkit -a "$DEST/Contents/PlugIns/QuickLookRTPreview.appex"
pluginkit -e use -i de.kiragroh.QuickLookRT.Preview
qlmanage -r
echo 'QuickLookRT installed. Select a DICOM in Finder and press Space.'
