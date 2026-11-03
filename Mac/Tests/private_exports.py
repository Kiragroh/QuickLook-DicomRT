"""Local original-case export/immutability acceptance. Outputs contain aggregates only."""
import csv
import hashlib
import io
import json
import subprocess
import sys
import tempfile
import zipfile
from pathlib import Path

root = Path(__file__).resolve().parents[2]
worker = root / 'artifacts/macos/arm64/QuickLookRT.app/Contents/Resources/Worker/QuickLookRT.Worker'

def digest(path):
    h = hashlib.sha256()
    with open(path, 'rb') as source:
        for part in iter(lambda: source.read(1024 * 1024), b''): h.update(part)
    return h.digest()

def run_case(case):
    process = subprocess.Popen([str(worker)], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True)
    serial = 0
    def call(op, **args):
        nonlocal serial
        serial += 1
        process.stdin.write(json.dumps(dict(id=serial, op=op, **args)) + '\n'); process.stdin.flush()
        response = json.loads(process.stdout.readline())
        assert response.get('id') == serial and 'error' not in response, 'worker export operation failed'
        return response['result']
    try:
        paths = [entry['path'] for entry in case['entries']]
        before = {path: digest(path) for path in paths}
        selected = next(entry['path'] for entry in case['entries'] if entry['modality'] == 'RTPLAN')
        model = call('open', path=selected, folder=True)
        info = call('exportInfo')
        with tempfile.TemporaryDirectory(prefix='quicklookrt private export ') as output:
            archive = Path(output) / 'plan.zip'
            parts = [part for part, count in info['parts'].items() if count]
            call('export', kind='zip', destination=str(archive), parts=parts)
            expected = sum(info['parts'][part] for part in parts)
            with zipfile.ZipFile(archive) as data:
                members = [member for member in data.infolist() if not member.is_dir()]
                assert len(members) == expected, 'ZIP file count differs from resolved reference graph'
                source_hashes = set(before.values())
                for member in members:
                    assert hashlib.sha256(data.read(member)).digest() in source_hashes, 'ZIP changes source DICOM bytes'
            target = next((roi for roi in model['rois'] if roi['category'] == 'Target'), model['rois'][0])
            curve = call('dvh', roi=target['id'], doseId=model['dvhDose'])
            table = Path(output) / 'metrics.csv'
            call('export', kind='csv', destination=str(table), doseId=model['dvhDose'], rois=[roi['id'] for roi in model['rois']])
            rows = list(csv.DictReader(io.StringIO(table.read_text(encoding='utf-8-sig'))))
            assert len({row['Structure'] for row in rows}) == len({roi['name'] for roi in model['rois']}), 'CSV omits requested structures'
            assert any(row['Status'] == 'NotCalculated' for row in rows), 'CSV omits uncomputed status'
            assert any(row['Dmean'] and abs(float(row['Dmean']) - curve['mean']) < .0001 for row in rows), 'CSV changes computed metrics'
        assert all(digest(path) == old for path, old in before.items()), 'source DICOM bytes changed'
        return dict(zipFiles=expected,structures=len(model['rois']),unchangedSources=len(before))
    finally:
        process.stdin.close(); process.wait(timeout=10)

try:
    index = json.loads(Path(sys.argv[1]).read_text())
    for number, case in enumerate(index, 1):
        result = run_case(case)
        print('PASS original export case', number, json.dumps(result), flush=True)
except Exception:
    # Python exception messages can contain source member names or paths.
    print('FAIL original export acceptance: local operation or byte/status comparison failed')
    raise SystemExit(1)
