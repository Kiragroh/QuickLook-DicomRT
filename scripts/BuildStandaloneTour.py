"""Pack the complete feature tour into one HTML file, including its MP4 audio.

Every asset is embedded once as base64. Browser Blob URLs are created lazily
from those bytes; no fetch, server, companion directory or remote media needed.
Intentional GitHub/installer/attribution hyperlinks remain normal links.
"""
from pathlib import Path
import base64
import hashlib
import json
import mimetypes

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'artifacts/presentation/v025-tour/output'
DESTINATION = ROOT / 'artifacts/release/QuickLook-DicomRT-Feature-Tour-0.2.6-Standalone.html'

BOOTSTRAP = r'''
const embeddedNode=document.getElementById('embedded-media');
const embeddedMedia=JSON.parse(embeddedNode.textContent);
embeddedNode.remove();
const mediaCache=new Map();
function mediaUrl(name){
  if(mediaCache.has(name))return mediaCache.get(name);
  const item=embeddedMedia[name];
  if(!item)throw new Error('Missing embedded media: '+name);
  const parts=[];
  // Decode in bounded chunks, rather than copying a whole movie repeatedly.
  for(let offset=0;offset<item.base64.length;offset+=1048576){
    const binary=atob(item.base64.slice(offset,offset+1048576));
    const bytes=new Uint8Array(binary.length);
    for(let i=0;i<binary.length;i++)bytes[i]=binary.charCodeAt(i);
    parts.push(bytes);
  }
  const url=URL.createObjectURL(new Blob(parts,{type:item.type}));
  mediaCache.set(name,url);
  delete embeddedMedia[name];
  return url;
}
document.getElementById('zoom-image').src=mediaUrl('screens/rt-overview.png');
'''


def build():
    manifest = json.loads((SOURCE/'tour-assets.json').read_text(encoding='utf-8'))
    embedded, hashes = {}, {}
    for name in manifest['required']:
        raw = (SOURCE/name).read_bytes()
        embedded[name] = dict(type=mimetypes.guess_type(name)[0] or 'application/octet-stream',
                              base64=base64.b64encode(raw).decode('ascii'))
        hashes[name] = hashlib.sha256(raw).hexdigest()
    html = (SOURCE/'index.html').read_text(encoding='utf-8')

    def replace_once(old, new):
        nonlocal html
        if html.count(old) != 1:
            raise ValueError(f'Expected one HTML insertion point: {old[:80]}')
        html = html.replace(old, new, 1)

    replace_once('src="screens/rt-overview.png" alt="Enlarged viewer capture"',
                 'alt="Enlarged viewer capture"')
    replace_once("'use strict';", "'use strict';\n"+BOOTSTRAP)
    replace_once('poster="screens/${esc(s.poster)}"', 'poster="${mediaUrl(\'screens/\'+s.poster)}"')
    replace_once('src="media/${esc(s.video)}"', 'src="${mediaUrl(\'media/\'+s.video)}"')
    replace_once('src="screens/${esc(source)}"', 'src="${mediaUrl(\'screens/\'+source)}"')
    replace_once("openMedia('media/feature-tour.mp4',true,", "openMedia(mediaUrl('media/feature-tour.mp4'),true,")
    replace_once("'art/space-magic.png',true)", "mediaUrl('art/space-magic.png'),true)")
    replace_once('`url("art/${s.art}")`', '`url("${mediaUrl(\'art/\'+s.art)}")`')
    replace_once("link.download=src.split('/').pop();",
                 "link.download=film?'QuickLook-DicomRT-Feature-Tour.mp4':(title.replace(/[^a-z0-9]+/gi,'-')+'.mp4');")
    # A sandboxed srcdoc frame cannot rewrite browser history. Chapter navigation
    # itself is independent of the optional address-bar fragment.
    replace_once("if(updateHash)history.replaceState(null,'','#'+(index+1));",
                 "if(updateHash){try{history.replaceState(null,'','#'+(index+1));}catch(error){if(error.name!=='SecurityError')throw error;}}")
    payload = '<script type="application/json" id="embedded-media">'+json.dumps(embedded,separators=(',',':'))+'</script>\n'
    replace_once('<script>\n', payload+'<script>\n')
    DESTINATION.parent.mkdir(parents=True,exist_ok=True)
    DESTINATION.write_text(html,encoding='utf-8')
    report = dict(file=DESTINATION.name, bytes=DESTINATION.stat().st_size,
                  assets=len(embedded), assetSha256=hashes,
                  sha256=hashlib.sha256(DESTINATION.read_bytes()).hexdigest())
    (ROOT/'artifacts/presentation/v025-tour/qa/standalone-build.json').write_text(
        json.dumps(report,indent=2),encoding='utf-8')
    print(f'Built {DESTINATION} ({report["bytes"]/1024/1024:.1f} MiB; {report["assets"]} embedded assets)')


if __name__ == '__main__':
    build()
