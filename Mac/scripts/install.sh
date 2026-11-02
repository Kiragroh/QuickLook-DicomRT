#!/bin/bash
set -euo pipefail
MAC_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SOURCE="$MAC_ROOT/../artifacts/macos/$(uname -m)/QuickLookRT.app"
DEST="$HOME/Applications/QuickLookRT.app"
[[ -d "$SOURCE" ]] || { echo 'Run Mac/scripts/build.sh first.'; exit 1; }
mkdir -p "$HOME/Applications"
if [[ -e "$DEST" ]]; then
  /System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister -u "$DEST" || true
  BACKUP="$HOME/Applications/QuickLookRT-backup-$(date +%Y%m%d-%H%M%S).app-backup"
  mv "$DEST" "$BACKUP"
fi
ditto "$SOURCE" "$DEST"
/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister -f "$DEST"
pluginkit -a "$DEST/Contents/PlugIns/QuickLookRTPreview.appex"
pluginkit -e use -i de.kiragroh.QuickLookRT.Preview
qlmanage -r
printf 'Installed: %s\n' "$DEST"
