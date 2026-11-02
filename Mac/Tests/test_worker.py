"""End-to-end tests using generated synthetic DICOMs only; no private dataset input."""
import base64
import json
import subprocess
import sys
import tempfile
from pathlib import Path
import pydicom
from pydicom.uid import generate_uid

root = Path(__file__).resolve().parents[2]
worker = Path(sys.argv[1]) if len(sys.argv) > 1 else root / 'artifacts/macos/arm64/QuickLookRT.app/Contents/Resources/Worker/QuickLookRT.Worker'
checks = 0

def check(value, message):
    global checks
    assert value, message
    checks += 1

with tempfile.TemporaryDirectory(prefix='quicklookrt synthetic ') as folder:
    folder = Path(folder)
    subprocess.run([sys.executable, str(root / 'scripts/CreateSynthetic.py'), str(folder)], check=True, stdout=subprocess.DEVNULL)
    before = {p.name: p.read_bytes() for p in folder.iterdir()}
    process = subprocess.Popen([str(worker)], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
    serial = 0

    def request(op, **args):
        global serial
        serial += 1
        process.stdin.write(json.dumps(dict(id=serial, op=op, **args)) + '\n')
        process.stdin.flush()
        reply = process.stdout.readline()
        check(bool(reply), 'self-contained worker remains alive')
        result = json.loads(reply)
        check(result['id'] == serial, 'responses preserve request identity')
        check('error' not in result, 'operation succeeds')
        return result['result']

    try:
        m = request('open', path=str(folder / 'RTPLAN.dcm'), folder=True)
        check(m['files'] == 70 and m['failures'] == 0, 'folder including spaces indexed')
        check(m['hasImage'] and m['mpr'] and m['plan'] >= 0 and m['dose'] >= 0 and m['structure'] >= 0, 'explicit RT references resolve complete context')
        check(len(m['beams']) == 2 and len(m['rois']) == 2, 'all beams and structures retained')
        for plane in ('Axial', 'Coronal', 'Sagittal'):
            f = request('render', plane=plane, focus=m['focus'], size=96, wash=True, isodoses=True)
            check(len(base64.b64decode(f['rgba'])) == f['width'] * f['height'] * 4, 'complete raster payload')
            check(len(f['outlines']) == 2, 'structures in each reformatted pane')
            check(f['width'] != f['height'] if plane != 'Axial' else True, 'physical image aspect preserved')
        f = request('render', plane='Axial', focus=m['focus'], size=64, hidden=[1, 2])
        check(not f['outlines'], 'per-ROI visibility is independent')
        check(len(request('scene')['rois']) == 2, '3D retains both original contour stacks')
        b = request('beam', beam=1, cp=10.5)
        check(b['cp'] == 10.5 and b['count'] == 21 and len(b['rectangles']) > 0, 'second field and fractional MLC aperture')
        b = request('beam', beam=0, cp=0, drr=True)
        check(b['drr'] is None and b['reason'], 'missing SAD/patient position never fabricated for DRR')
        dvh = request('dvh', roi=1)
        check(dvh['status'] == 'Complete' and dvh['coverage'] > .999 and dvh['max'] > 0, 'complete synthetic DVH with metrics')
        check(request('dvh', roi=1) == dvh, 'unchanged DVH result reused')
        tags = request('tags')
        check(any('[' in t['Path'] for t in tags), 'nested metadata retained')
        single = request('open', path=str(folder / 'RTPLAN.dcm'), folder=False)
        check(single['files'] == 1 and len(single['beams']) == 2 and not single['hasImage'], 'sandbox single-file fallback supports RT-only MLC')
        check(single['dose'] == -1 and single['structure'] == -1, 'single-file fallback never claims folder access')
        image = request('open', path=str(folder / 'CT_016.dcm'), folder=True)
        check(image['plan'] >= 0 and image['mpr'], 'image entry point discovers associated plan')
        dose = request('open', path=str(folder / 'RTDOSE.dcm'), folder=True)
        check(dose['dose'] >= 0 and dose['plan'] >= 0, 'dose entry point discovers referenced plan')
        structure = request('open', path=str(folder / 'RTSTRUCT.dcm'), folder=True)
        check(structure['structure'] >= 0 and structure['plan'] >= 0, 'structure entry point discovers referencing plan')
        # Conflicting patient with a copied frame must never join this preview.
        foreign = pydicom.dcmread(str(folder / 'RTPLAN.dcm'))
        foreign.PatientID = 'OTHER-SYNTHETIC'
        foreign.SOPInstanceUID = generate_uid()
        foreign.file_meta.MediaStorageSOPInstanceUID = foreign.SOPInstanceUID
        foreign.save_as(str(folder / 'unrelated.dcm'), write_like_original=False)
        m = request('open', path=str(folder / 'CT_016.dcm'), folder=True)
        check(len(m['plans']) == 1 and m['files'] == 70, 'conflicting patient excluded despite matching frame')
        # An alternative plan dose is ambiguous when opening the plan. Opening an exact
        # dose must still preserve that selected instance.
        alternative = pydicom.dcmread(str(folder / 'RTDOSE.dcm'))
        alternative.SOPInstanceUID = generate_uid()
        alternative.file_meta.MediaStorageSOPInstanceUID = alternative.SOPInstanceUID
        alternative.save_as(str(folder / 'alternative.dcm'), write_like_original=False)
        m = request('open', path=str(folder / 'RTPLAN.dcm'), folder=True)
        check(m['dose'] == -1, 'ambiguous plan doses stay unselected')
        m = request('open', path=str(folder / 'RTDOSE.dcm'), folder=True)
        check(m['dose'] >= 0, 'exact opened dose retained among alternatives')
        check(all((folder / name).read_bytes() == data for name, data in before.items()), 'source files unchanged')
    finally:
        process.stdin.close()
        process.wait(timeout=10)
    check(process.returncode == 0, 'clean worker shutdown')
print('PASS macOS worker integration:', checks, 'checks')
