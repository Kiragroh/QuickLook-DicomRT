"""Real-worker contracts: split exports, ambiguous references and bounded discovery."""
import json
import subprocess
import shutil
import sys
import tempfile
from pathlib import Path
import pydicom
from pydicom.uid import generate_uid

ROOT = Path(__file__).resolve().parents[2]
WORKER = Path(sys.argv[1]) if len(sys.argv)>1 else ROOT/'artifacts/macos/arm64/QuickLookRT.app/Contents/Resources/Worker/QuickLookRT.Worker'
with tempfile.TemporaryDirectory(prefix='quicklookrt split ') as temporary:
    root=Path(temporary);source=root/'.source';source.mkdir()
    subprocess.run([sys.executable,str(ROOT/'scripts/CreateSynthetic.py'),str(source)],check=True,stdout=subprocess.DEVNULL)
    case=root/'export';case.mkdir();selected=None
    for file in source.iterdir():
        ds=pydicom.dcmread(file,stop_before_pixels=True)
        folder=case/(('Images/series/part A' if int(file.stem.split('_')[-1])<16 else 'Images/series/part B') if ds.Modality=='CT' else ds.Modality)
        folder.mkdir(parents=True,exist_ok=True);dest=folder/file.name;dest.write_bytes(file.read_bytes())
        if file.name=='CT_016.dcm':selected=dest
    process=subprocess.Popen([str(WORKER)],stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True)
    counter=0
    def call(op,expect_error=False,**args):
        global counter
        counter+=1;process.stdin.write(json.dumps(dict(id=counter,op=op,**args))+'\n');process.stdin.flush()
        reply=json.loads(process.stdout.readline())
        if expect_error:assert 'error' in reply;return reply
        assert 'error' not in reply,reply;return reply['result']
    try:
        m=call('open',path=str(selected),folder=True,automatic=True)
        assert m['files']==70 and m['mpr'] and m['nativeCount']==33, ('split image stack and linked RT context must be merged',m['files'],m['mpr'],m['nativeCount'],m.get('discovery'))
        assert m['plan']>=0 and m['hasDose'] and len(m['rois'])==2,'one CT must resolve sibling plan/structures/dose'
        for modality in ['RTPLAN','RTSTRUCT','RTDOSE']:
            path=next((case/modality).glob('*.dcm'))
            m=call('open',path=str(path),folder=True,automatic=True)
            assert m['mpr'] and m['plan']>=0 and m['hasDose'],modality+' must resolve the same split context'
        original_plan=pydicom.dcmread(case/'RTPLAN/RTPLAN.dcm');registered=pydicom.dcmread(case/'RTPLAN/RTPLAN.dcm')
        registered.FrameOfReferenceUID=pydicom.dcmread(next((case/'MR').glob('*.dcm')),stop_before_pixels=True).FrameOfReferenceUID
        registered.save_as(case/'RTPLAN/RTPLAN.dcm',write_like_original=False)
        registration=next((case/'REG').glob('*.dcm'));moved=root/'registration.dcm';registration.rename(moved)
        m=call('open',path=str(case/'RTPLAN/RTPLAN.dcm'),folder=True,automatic=True)
        assert m['mpr'] and m['hasDose'] and m['files']==70,'missing required registration continues search before claiming complete context'
        moved.rename(registration);original_plan.save_as(case/'RTPLAN/RTPLAN.dcm',write_like_original=False)
        duplicate=pydicom.dcmread(next((case/'RTPLAN').glob('*.dcm')))
        duplicate.SOPInstanceUID=generate_uid();duplicate.file_meta.MediaStorageSOPInstanceUID=duplicate.SOPInstanceUID;duplicate.RTPlanLabel='SECOND PLAN'
        duplicate.save_as(case/'RTPLAN/second.dcm',write_like_original=False)
        other=pydicom.dcmread(next((case/'RTDOSE').glob('*.dcm')));other.SOPInstanceUID=generate_uid();other.file_meta.MediaStorageSOPInstanceUID=other.SOPInstanceUID
        other.ReferencedRTPlanSequence[0].ReferencedSOPInstanceUID=duplicate.SOPInstanceUID;other.save_as(case/'RTDOSE/second-dose.dcm',write_like_original=False)
        if '--export-fixture' in sys.argv:
            destination=Path(sys.argv[sys.argv.index('--export-fixture')+1]);shutil.copytree(case,destination,dirs_exist_ok=True)
        m=call('open',path=str(selected),folder=True,automatic=True)
        assert m['plan']==-1 and m['discovery']['kind']=='plans' and len(m['discovery']['choices'])==2,'ambiguous linked plans must ask without guessing'
        chosen=next(p for p in m['plans'] if p['label']=='SECOND PLAN')
        m=call('select',plan=chosen['id']);assert m['plan']==chosen['id'] and m['discovery'] is None,'explicit choice must close the prompt'
        exact=call('open',path=str(case/'RTPLAN/RTPLAN.dcm'),folder=True,automatic=True)
        assert exact['plan']>=0 and exact['discovery'] is None and len(exact['activeDoses'])==1,'an exact opened RT plan must not be replaced by alternatives'
        # Same frame with another patient is not an association.
        foreign=pydicom.dcmread(case/'RTPLAN/second.dcm');foreign.PatientID='FOREIGN PATIENT';foreign.SOPInstanceUID=generate_uid();foreign.file_meta.MediaStorageSOPInstanceUID=foreign.SOPInstanceUID
        foreign.save_as(case/'RTPLAN/foreign.dcm',write_like_original=False)
        outside=root/'outside';outside.mkdir();linked=pydicom.dcmread(case/'RTPLAN/second.dcm')
        linked.SOPInstanceUID=generate_uid();linked.file_meta.MediaStorageSOPInstanceUID=linked.SOPInstanceUID;linked.save_as(outside/'linked-plan.dcm',write_like_original=False)
        (case/'linked-folder').symlink_to(outside,target_is_directory=True)
        m=call('open',path=str(selected),folder=True,automatic=True)
        assert len(m['plans'])==2,'other patients and directory symlink targets are excluded'
        backup=case/'Images/backup';backup.mkdir()
        for file in (case/'Images/series/part A').iterdir():(backup/file.name).write_bytes(file.read_bytes())
        m=call('open',path=str(selected),folder=True,automatic=True)
        assert m['nativeCount']==33 and m['mpr'],'duplicate SOP instances must not discard the remaining valid stack'
        # Avoid silently reading an unbounded patient archive.
        for i in range(4100):(case/'Images'/f'noise-{i:04}.txt').write_text('not dicom')
        m=call('open',path=str(selected),folder=True,automatic=True)
        assert m['discovery']['kind']=='scope' and m['discovery']['choices'],'large automatic search asks before expanding'
        resume=next(c for c in m['discovery']['choices'] if c['kind']=='continue')
        m=call('autoDiscover',choice=resume['id'])
        assert len(m['plans'])==2 and m['mpr'],'confirmed search resumes without an OS folder picker'
        call('autoDiscover',choice=resume['id'],expect_error=True)
        call('open',path=str(case/'RTPLAN/RTPLAN.dcm'),folder=False)
        call('autoDiscover',choice=resume['id'],expect_error=True)
        print('PASS automatic discovery: split stack, four entry points, ambiguous plan choice, patient isolation and bounded/resumed search')
    finally:
        process.stdin.close();process.wait(timeout=10)
