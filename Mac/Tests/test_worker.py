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
    placeholder = pydicom.dcmread(str(folder / 'RTPLAN.dcm'))
    placeholder.RTPlanLabel = 'None'
    for point in placeholder.BeamSequence[1].ControlPointSequence:
        for device in point.BeamLimitingDevicePositionSequence:
            if device.RTBeamLimitingDeviceType == 'X':
                device.LeafJawPositions = [-35 + point.ControlPointIndex * .5, 35 - point.ControlPointIndex * .5]
    placeholder.save_as(str(folder / 'RTPLAN.dcm'), write_like_original=False)
    contours = pydicom.dcmread(str(folder / 'RTSTRUCT.dcm'))
    body = pydicom.Dataset()
    body.ObservationNumber = 1
    body.ReferencedROINumber = 1
    body.RTROIInterpretedType = 'EXTERNAL'
    contours.RTROIObservationsSequence = pydicom.Sequence([body])
    contours.save_as(str(folder / 'RTSTRUCT.dcm'), write_like_original=False)
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
        check(m['plans'][m['plan']]['label'].lower() != 'none' and 'None' in m['plans'][m['plan']]['detail'], 'placeholder plan label remains identifiable with original metadata retained')
        reference = request('doseReference')
        check(reference['prescriptions'] == [] and 0 < reference['d0035'] < m['doseInfo']['maximum'], 'without prescription the hottest 0.035 cc is computed inside the body instead of the point maximum')
        check(request('progress')['finished'], 'completed opening reports real progress completion')
        palette = [[10, '#8c5a2b'], [95, '#006400'], [100, '#90ee90'], [105, '#ffff00'], [107, '#ff0000'], [110, '#ff00ff'], [120, '#b000e0']]
        relative = request('render', plane='Axial', focus=m['focus'], size=96, wash=True, isodoses=True, reference=8, palette=palette, maximumPercent=120, washMax=120, levels=[95, 100, 105, 107, 110, 120])
        absolute = request('render', plane='Axial', focus=m['focus'], size=96, wash=True, isodoses=True, reference=8, palette=palette, maximumPercent=120, washMax=120, absolute=True, levels=[7.6, 8, 8.4, 8.56, 8.8, 9.6])
        check(relative['rgba'] == absolute['rgba'], 'Gy conversion preserves the normalized colorwash')
        check(any(line['level'] == 105 for line in relative['isolines']), 'relative 105-percent contour is retained')
        check(len(relative['isolines']) == len(absolute['isolines']), 'absolute and relative isodose geometry agree')
        hotspot = request('render', plane='Axial', focus=m['focus'], size=64, wash=True, reference=8, opacity=.9, palette=[[10, '#8c5a2b'], [100, '#ff00ff']], washMax=100)
        pixels = base64.b64decode(hotspot['rgba'])
        check(any(pixels[i] > 180 and pixels[i + 2] > 180 and pixels[i + 1] < 70 for i in range(0, len(pixels), 4)), 'hotter-than-100-percent voxels remain visible in the final palette color')
        prescribed = pydicom.dcmread(str(folder / 'RTPLAN.dcm'))
        rx = pydicom.Dataset()
        rx.DoseReferenceNumber = 1
        rx.DoseReferenceType = 'TARGET'
        rx.DoseReferenceStructureType = 'SITE'
        rx.TargetPrescriptionDose = 60
        prescribed.DoseReferenceSequence = pydicom.Sequence([rx])
        prescribed.save_as(str(folder / 'RTPLAN.dcm'), write_like_original=False)
        request('open', path=str(folder / 'RTPLAN.dcm'), folder=True)
        check(request('doseReference')['prescriptions'] == [60], 'an explicit target prescription is retained in Gy')
        other_rx = pydicom.Dataset()
        other_rx.DoseReferenceNumber = 2
        other_rx.DoseReferenceType = 'TARGET'
        other_rx.TargetPrescriptionDose = 54
        prescribed.DoseReferenceSequence.append(other_rx)
        prescribed.save_as(str(folder / 'RTPLAN.dcm'), write_like_original=False)
        request('open', path=str(folder / 'RTPLAN.dcm'), folder=True)
        check(request('doseReference')['prescriptions'] == [54, 60], 'distinct prescriptions remain explicit instead of guessing one')
        (folder / 'RTPLAN.dcm').write_bytes(before['RTPLAN.dcm'])
        del contours.RTROIObservationsSequence
        contours.save_as(str(folder / 'RTSTRUCT.dcm'), write_like_original=False)
        request('open', path=str(folder / 'RTPLAN.dcm'), folder=True)
        check(request('doseReference')['d0035'] is None, 'a missing body contour is not replaced by an unlabelled point maximum')
        (folder / 'RTSTRUCT.dcm').write_bytes(before['RTSTRUCT.dcm'])
        m = request('open', path=str(folder / 'RTPLAN.dcm'), folder=True)
        for plane in ('Axial', 'Coronal', 'Sagittal'):
            f = request('render', plane=plane, focus=m['focus'], size=96, wash=True, isodoses=True)
            check(len(base64.b64decode(f['rgba'])) == f['width'] * f['height'] * 4, 'complete raster payload')
            check(len(f['outlines']) == 2, 'structures in each reformatted pane')
            check(f['width'] != f['height'] if plane != 'Axial' else True, 'physical image aspect preserved')
        f = request('render', plane='Axial', focus=m['focus'], size=64, hidden=[1, 2])
        check(not f['outlines'], 'per-ROI visibility is independent')
        check(len(request('scene')['rois']) == 2, '3D retains both original contour stacks')
        b = request('beam', beam=1, cp=10.5)
        check(b['jaws']['x'] == [-29.75, 29.75], 'moving jaws interpolate at the actual fractional control point')
        check(b['cp'] == 10.5 and b['count'] == 21 and len(b['rectangles']) > 0, 'second field and fractional MLC aperture')
        b = request('beam', beam=0, cp=0, drr=True)
        check(b['drr'] is None and b['reason'], 'missing SAD/patient position never fabricated for DRR')
        dvh = request('dvh', roi=1)
        check(dvh['status'] == 'Complete' and dvh['coverage'] > .999 and dvh['max'] > 0, 'complete synthetic DVH with metrics')
        check(request('dvh', roi=1) == dvh, 'unchanged DVH result reused')
        tags = request('tags', source=True)
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
