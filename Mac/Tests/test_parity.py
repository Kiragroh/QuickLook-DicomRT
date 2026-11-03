"""Additional contracts for generated multi-dose/standalone RT arrangements. No private inputs."""
import base64
import csv
import json
import subprocess
import shutil
import sys
import tempfile
from pathlib import Path
import pydicom
from pydicom.uid import generate_uid

root = Path(__file__).resolve().parents[2]
worker = Path(sys.argv[1]) if len(sys.argv) > 1 else root / 'artifacts/macos/arm64/QuickLookRT.app/Contents/Resources/Worker/QuickLookRT.Worker'
checks = 0

def check(value, label):
    global checks
    assert value, label
    checks += 1

def save(dataset, path, new_uid=False):
    if new_uid:
        dataset.SOPInstanceUID = generate_uid()
        dataset.file_meta.MediaStorageSOPInstanceUID = dataset.SOPInstanceUID
    dataset.save_as(path, write_like_original=False)

with tempfile.TemporaryDirectory(prefix='quicklookrt parity ') as name:
    folder = Path(name)
    subprocess.run([sys.executable, str(root / 'scripts/CreateSynthetic.py'), str(folder)], check=True, stdout=subprocess.DEVNULL)
    structure = pydicom.dcmread(folder / 'RTSTRUCT.dcm')
    body = pydicom.Dataset(); body.ObservationNumber = 1; body.ReferencedROINumber = 1; body.RTROIInterpretedType = 'EXTERNAL'
    structure.RTROIObservationsSequence = pydicom.Sequence([body]); save(structure, folder / 'RTSTRUCT.dcm')
    plan = pydicom.dcmread(folder / 'RTPLAN.dcm')
    setup = pydicom.Dataset(); setup.PatientSetupNumber = 1; setup.PatientPosition = 'HFS'
    plan.PatientSetupSequence = pydicom.Sequence([setup])
    for beam in plan.BeamSequence:
        beam.SourceAxisDistance = 1000; beam.ReferencedPatientSetupNumber = 1
        for point in beam.ControlPointSequence:
            point.BeamLimitingDeviceRotationDirection = 'NONE'; point.PatientSupportRotationDirection = 'NONE'
    # Two source control-point isocenters, no inferred or blended boundary.
    for point in plan.BeamSequence[1].ControlPointSequence:
        point.IsocenterPosition = [22, -5, 0]
    save(plan, folder / 'RTPLAN.dcm')
    original_dose = pydicom.dcmread(folder / 'RTDOSE.dcm')
    other = pydicom.dcmread(folder / 'RTDOSE.dcm'); other.DoseGridScaling = float(other.DoseGridScaling) / 2
    save(other, folder / 'alternative.dcm', new_uid=True)
    if '--export-fixture' in sys.argv:
        destination = Path(sys.argv[sys.argv.index('--export-fixture') + 1]); destination.mkdir(parents=True, exist_ok=True)
        for source in folder.iterdir(): shutil.copy2(source, destination / source.name)
        second_plan = pydicom.dcmread(folder / 'RTPLAN.dcm'); second_plan.RTPlanLabel = 'SYNTHETIC SECOND'
        save(second_plan, destination / 'second-plan.dcm', new_uid=True)
        second_dose = pydicom.dcmread(folder / 'RTDOSE.dcm'); second_dose.ReferencedRTPlanSequence[0].ReferencedSOPInstanceUID = second_plan.SOPInstanceUID
        save(second_dose, destination / 'second-dose.dcm', new_uid=True)
        third_plan = pydicom.dcmread(folder / 'RTPLAN.dcm'); third_plan.RTPlanLabel = 'SYNTHETIC THIRD'
        save(third_plan, destination / 'third-plan.dcm', new_uid=True)
        third_dose = pydicom.dcmread(folder / 'RTDOSE.dcm'); third_dose.ReferencedRTPlanSequence[0].ReferencedSOPInstanceUID = third_plan.SOPInstanceUID
        third_dose.DoseGridScaling = float(third_dose.DoseGridScaling) * .75
        save(third_dose, destination / 'third-dose.dcm', new_uid=True)
    p = subprocess.Popen([str(worker)], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
    serial = 0
    def call(op, expect_error=False, **args):
        global serial
        serial += 1
        p.stdin.write(json.dumps(dict(id=serial, op=op, **args)) + '\n'); p.stdin.flush()
        reply = json.loads(p.stdout.readline()); check(reply['id'] == serial, 'request identity')
        if expect_error:
            check('error' in reply, 'unsupported or stale request is rejected')
            return reply
        check('error' not in reply, 'operation succeeds')
        return reply['result']
    try:
        partial = call('open', path=str(folder / 'RTPLAN.dcm'), folder=False, partial=True)
        check(partial['partial'] and partial['firstBeam']['layers'] and partial['files'] == 1, 'first MLC aperture is embedded before catalog discovery')
        image = call('open', path=str(folder / 'CT_016.dcm'), folder=False, partial=True)
        check(image['firstFrame']['rgba'] and image['files'] == 1, 'first selected image is embedded before catalog discovery')
        m = call('open', path=str(folder / 'RTPLAN.dcm'), folder=True)
        check(m['dose'] == -1 and len(m['activeDoses']) == 2, 'ambiguous source dose stays explicit while both associated overlays are retained')
        check(len(m['isocenters']) == 2, 'all distinct source isocenters available')
        check(all(d['registered'] and len(d['maximumPosition']) == 3 for d in m['activeDoses']), 'mapped dose maxima available')
        hi, lo = sorted(m['activeDoses'], key=lambda d: d['maximum'], reverse=True)
        check(hi['label'] != lo['label'], 'same-plan dose choices have distinguishable display labels')
        def render(overlays, **extra):
            return call('render', plane='Axial', focus=m['focus'], size=96, reference=8, wash=True, isodoses=True, levels=[50, 95, 100], overlays=overlays, **extra)
        first = render([dict(id=hi['id'], visible=True, reference=8)])
        half = render([dict(id=lo['id'], visible=True, reference=8)])
        both = render([dict(id=hi['id'], visible=True, reference=8), dict(id=lo['id'], visible=True, reference=8)])
        hidden = render([dict(id=hi['id'], visible=True, reference=8), dict(id=lo['id'], visible=False, reference=8)])
        equal = render([dict(id=lo['id'], visible=True, reference=4)])
        check(first['rgba'] != half['rgba'] and both['rgba'] != first['rgba'], 'individual and simultaneous overlays have distinct raster results')
        check(hidden['rgba'] == first['rgba'], 'dose visibility is independent')
        check(equal['rgba'] == first['rgba'], 'each overlay uses its own explicit normalization')
        refs = [call('doseReference', doseId=d['id']) for d in (hi, lo)]
        check(abs(refs[0]['d0035'] - refs[1]['d0035'] * 2) < .00001, 'per-dose D0.035 values are independently cached')
        curves = [call('dvh', roi=1, doseId=d['id']) for d in (hi, lo)]
        check(abs(curves[0]['mean'] - curves[1]['mean'] * 2) < .00001, 'DVH dose switching produces independently correct metrics')
        check(call('dvh', roi=1, doseId=hi['id']) == curves[0], 'returning to a dose reuses its own DVH')
        mesh1 = call('mesh', kind='dose', doseId=hi['id'], level=8)
        mesh2 = call('mesh', kind='dose', doseId=lo['id'], level=8)
        check(mesh1.get('positions') != mesh2.get('positions'), 'dose surfaces have independent source keys')
        call('render', plane='Axial', overlays=[dict(id=99999, visible=True, reference=8)], expect_error=True)
        frame = call('fields', fields=True, beam=1, cp=0, frames=[dict(key='axial', center=first['center'], right=first['right'], down=first['down'], widthMm=first['widthMm'], heightMm=first['heightMm'])])
        check(frame[0]['key'] == 'axial' and any(f['active'] for f in frame[0]['fields']['fields']), 'field-only updates preserve exact rendered geometry')
        neutral = call('fields', fields=True, beam=-1, frames=[dict(key='axial', center=first['center'], right=first['right'], down=first['down'], widthMm=first['widthMm'], heightMm=first['heightMm'])])
        check(not any(f['active'] for f in neutral[0]['fields']['fields']), 'neutral selection does not highlight a field')
        call('export', kind='csv', destination=str(folder / 'RTPLAN.dcm'), rois=[1], expect_error=True)
        export = folder / 'metrics.csv'
        call('export', kind='csv', destination=str(export), rois=[1, 2], doseId=lo['id'])
        rows = list(csv.DictReader(export.read_text(encoding='utf-8-sig').splitlines()))
        check(any(r['Status'] == 'NotCalculated' for r in rows), 'detailed CSV retains uncomputed enabled structures')
        check(any(abs(float(r['Dmean']) - curves[1]['mean']) < .0001 for r in rows if r['Dmean']), 'CSV uses the explicit DVH dose')
        exact = call('open', path=str(folder / 'RTDOSE.dcm'), folder=True)
        check(len(exact['activeDoses']) == 1 and exact['dose'] >= 0, 'exact opened dose remains selected among alternatives')
        discovered = call('discover', root=str(folder))
        check(discovered['dose'] == exact['dose'] and len(discovered['activeDoses']) == 1, 'discovery preserves exact opened-dose choice')
        sources = discovered['tagSources']
        rp_source = next(x for x in sources if x['label'].startswith('RTPLAN'))
        tags = call('tags', sourceId=rp_source['id'])
        check(any(t['Tag'].lower() == '(300a,0002)' for t in tags), 'plan tag source is inspectable while CT is loaded')
        call('cancelWork', group='projection', revision=50)
        call('beam', beam=0, cp=0, drr=False, outlines=True, workGroup='projection', workRevision=49, expect_error=True)
        # CLI and native bridge enforce the same job generation contract.
        call('beam', beam=0, cp=0, drr=False, outlines=False, workGroup='projection', workRevision=50)
        # Mixed-unit overlays retain their units and cannot silently claim an absolute Gy scale.
        relative = pydicom.dcmread(folder / 'RTDOSE.dcm'); relative.DoseUnits = 'RELATIVE'
        save(relative, folder / 'relative.dcm', new_uid=True)
        mixed_model = call('open', path=str(folder / 'RTPLAN.dcm'), folder=True)
        rel = next(d for d in mixed_model['activeDoses'] if d['units'] == 'RELATIVE')
        gy = next(d for d in mixed_model['activeDoses'] if d['units'] == 'GY')
        mixed = call('render', plane='Axial', absolute=True, isodoses=True, size=64, overlays=[dict(id=gy['id'], visible=True, reference=8),dict(id=rel['id'], visible=True, reference=100)])
        check(mixed['absolute'] is False, 'mixed physical/relative overlays do not claim absolute Gy isodoses')
        physical = call('render', plane='Axial', absolute=True, isodoses=True, size=64, overlays=[dict(id=gy['id'], visible=True, reference=8),dict(id=rel['id'], visible=False, reference=100)])
        check(physical['absolute'] is True, 'hidden relative grids do not disable physical Gy display')
        (folder / 'relative.dcm').unlink()
        # Two additional unambiguous plans cover sum transitions despite alternatives in the first plan.
        for ordinal, factor in [('second', 1), ('third', .75)]:
            linked_plan = pydicom.dcmread(folder / 'RTPLAN.dcm'); linked_plan.RTPlanLabel = 'SYNTHETIC ' + ordinal.upper()
            save(linked_plan, folder / (ordinal + '-plan.dcm'), new_uid=True)
            linked_dose = pydicom.dcmread(folder / 'RTDOSE.dcm'); linked_dose.DoseGridScaling = float(linked_dose.DoseGridScaling) * factor
            linked_dose.ReferencedRTPlanSequence[0].ReferencedSOPInstanceUID = linked_plan.SOPInstanceUID
            save(linked_dose, folder / (ordinal + '-dose.dcm'), new_uid=True)
        multi = call('open', path=str(folder / 'CT_016.dcm'), folder=True)
        check(multi['plan'] == -1 and len(multi['plans']) == 3, 'multiple explicitly associated plans remain available without guessing an image plan')
        picked = next(p for p in multi['plans'] if p['label'] == 'SYNTHETIC SECOND')
        one_plan = call('select', plan=picked['id'])
        check(len(one_plan['activeDoses']) == 1 and one_plan['rois'], 'explicit plan selection isolates its referenced RT context')
        group = one_plan['sumGroups'][0]
        means = [call('dvh', roi=2, doseId=one_plan['activeDoses'][0]['id'])['mean']]
        expected = max(d['maximum'] for d in one_plan['doses'] if d['id'] in group) * 1.75
        summed = call('sum', doses=group)
        check(summed['isSum'] and summed['dvhDose'] == -2 and len(summed['activeDoses']) == 1, 'sum exposes its derived grid and combined structure context')
        check(abs(summed['doseInfo']['maximum'] - expected) < .0001, 'sum retains physical numeric dose addition')
        check(call('doseReference', doseId=-2)['d0035'] > 0, 'sum reference uses supported combined body contours')
        retained = call('select', series=summed['series'], keepContext=True)
        check(retained['isSum'] and retained['activeDoses'][0]['id'] == -2, 'image-series selection preserves the sum context')
        normal = call('select', plan=picked['id'])
        check(not normal['isSum'] and len(normal['activeDoses']) == 1, 'returning to a source plan clears the sum')
        for ordinal in ['second', 'third']:
            (folder / (ordinal + '-plan.dcm')).unlink(); (folder / (ordinal + '-dose.dcm')).unlink()
        # Standalone objects without images or plans still have deterministic associations and RT bounds.
        for file in folder.glob('CT_*.dcm'): file.unlink()
        (folder / 'RTPLAN.dcm').unlink(); (folder / 'alternative.dcm').unlink()
        dose_only = call('open', path=str(folder / 'RTDOSE.dcm'), folder=True)
        check(dose_only['plan'] == -1 and dose_only['rois'] and dose_only['canReformat'] and not dose_only['mpr'], 'standalone dose associates registered structures and exposes RT-only slices')
        for plane in ('Axial', 'Coronal', 'Sagittal'):
            f = call('render', plane=plane, focus=dose_only['focus'], size=64)
            check(len(base64.b64decode(f['rgba'])) == f['width'] * f['height'] * 4, 'RT-only reformat frame')
        struct_only = call('open', path=str(folder / 'RTSTRUCT.dcm'), folder=True)
        check(struct_only['dose'] >= 0 and struct_only['canReformat'], 'standalone structure chooses one compatible dose')
        unknown = pydicom.dcmread(folder / 'RTDOSE.dcm'); unknown.FrameOfReferenceUID = generate_uid()
        save(unknown, folder / 'unmapped.dcm', new_uid=True)
        struct_only = call('open', path=str(folder / 'RTSTRUCT.dcm'), folder=True)
        check(len(struct_only['activeDoses']) == 1, 'unregistered dose is not invented as a compatible association')
        duplicated = pydicom.dcmread(folder / 'RTDOSE.dcm'); save(duplicated, folder / 'duplicate.dcm', new_uid=True)
        ambiguous = call('open', path=str(folder / 'RTSTRUCT.dcm'), folder=True)
        check(ambiguous['dose'] == -1 and not ambiguous['activeDoses'], 'multiple standalone compatible doses require a choice')
    finally:
        p.stdin.close(); p.wait(timeout=10)
    check(p.returncode == 0, 'clean worker shutdown')
print('PASS macOS parity contracts:', checks, 'checks')
