"""Wholly synthetic, Accela-inspired C-arm / dual-layer / dynamic-collimator fixture.
No patient material, vendor machine model, original Accela export or calculated dose.
Run with a NEW output directory. Never alters the existing Accela laboratory data.
"""
from pathlib import Path
import argparse, hashlib, json, math, uuid
import numpy as np
import pydicom
from pydicom.dataset import Dataset, FileDataset, FileMetaDataset
from pydicom.sequence import Sequence
from pydicom.uid import ExplicitVRLittleEndian, CTImageStorage, RTPlanStorage, RTDoseStorage, RTStructureSetStorage

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('output',type=Path)
args=parser.parse_args(); root=args.output
if root.exists() and any(root.iterdir()):
    raise SystemExit('Use an empty output directory; existing data will not be overwritten.')
root.mkdir(parents=True,exist_ok=True)
namespace=uuid.UUID('56c37f65-d3ed-4c67-aed5-76858db13671')
def uid(key): return '2.25.'+str(uuid.uuid5(namespace,key).int)
def item(**values):
    d=Dataset()
    for k,v in values.items(): setattr(d,k,v)
    return d
def code(value,meaning): return Sequence([item(CodeValue=value,CodingSchemeDesignator='DCM',CodeMeaning=meaning)])
def ds(mod,key,sop):
    meta=FileMetaDataset();meta.TransferSyntaxUID=ExplicitVRLittleEndian;meta.MediaStorageSOPClassUID=sop;meta.MediaStorageSOPInstanceUID=uid(key);meta.ImplementationClassUID=uid('implementation')
    d=FileDataset(None,{},file_meta=meta,preamble=b'\0'*128);d.is_little_endian=True;d.is_implicit_VR=False
    values=dict(SOPClassUID=sop,SOPInstanceUID=uid(key),StudyInstanceUID=uid('study'),SeriesInstanceUID=uid('series-'+mod),FrameOfReferenceUID=uid('frame'),
        Modality=mod,PatientName='SYNTHETIC^ACCELA_PHANTOM',PatientID='SYNTH-ACCELA-PHANTOM',PatientBirthDate='',PatientSex='',
        StudyDate='20260930',StudyTime='120000',StudyID='TEST ONLY',AccessionNumber='',ReferringPhysicianName='',
        SeriesNumber={'CT':1,'RTSTRUCT':2,'RTPLAN':3,'RTDOSE':4}[mod],InstanceNumber=1,SpecificCharacterSet='ISO_IR 192',
        SeriesDescription='SYNTHETIC - NOT FOR TREATMENT',Manufacturer='SYNTHETIC - NOT VARIAN',ManufacturerModelName='Accela-inspired SYNTHETIC',
        PositionReferenceIndicator='',SoftwareVersions='QuickLook test phantom 1')
    for k,v in values.items():setattr(d,k,v)
    return d
def ref(d):return item(ReferencedSOPClassUID=d.SOPClassUID,ReferencedSOPInstanceUID=d.SOPInstanceUID)
def save(d,name):d.save_as(root/name,write_like_original=False)
def pixels(d,a,signed=False):
    d.Rows=a.shape[-2];d.Columns=a.shape[-1];d.SamplesPerPixel=1;d.PhotometricInterpretation='MONOCHROME2'
    d.BitsAllocated=16;d.BitsStored=16;d.HighBit=15;d.PixelRepresentation=int(signed);d.PixelData=a.astype('<i2' if signed else '<u2').tobytes()

# Smooth geometric head/neck phantom, with unequal targets and asymmetric organs.
structures=[('External','EXTERNAL',(0,0,0),(83,69,98),(170,170,185)),
 ('PTV_A','PTV',(-23,-12,22),(19,16,24),(255,65,135)),('PTV_B','PTV',(26,6,-23),(15,18,20),(255,180,50)),
 ('SpinalCord','ORGAN',(0,34,-12),(6,7,69),(240,220,70)),
 ('Parotid_L','ORGAN',(45,-3,20),(15,18,28),(85,190,255)),('Parotid_R','ORGAN',(-45,-3,20),(15,18,28),(100,235,170)),
 ('Brainstem','ORGAN',(0,23,39),(11,12,24),(180,120,250))]
spacing=1.5; origin=-95.25; zs=np.arange(-90,91,2.5); yy,xx=np.mgrid[:128,:128];x=origin+xx*spacing;y=origin+yy*spacing
def ellipse(x,y,z,c,r):return ((x-c[0])/r[0])**2+((y-c[1])/r[1])**2+((z-c[2])/r[2])**2
cts=[]
for i,z in enumerate(zs):
    d=ds('CT','ct-'+str(i),CTImageStorage);d.InstanceNumber=i+1;d.ImageType=['ORIGINAL','PRIMARY','AXIAL'];d.PatientPosition='HFS';d.BodyPartExamined='HEADNECK'
    d.ImagePositionPatient=[origin,origin,float(z)];d.ImageOrientationPatient=[1,0,0,0,1,0];d.PixelSpacing=[spacing,spacing];d.SliceThickness=2.5;d.SpacingBetweenSlices=2.5
    body=ellipse(x,y,z,structures[0][2],structures[0][3])<1
    a=np.where(body,35+12*np.cos(x/12)*np.sin(y/14),-1000)
    a=np.where(body&(ellipse(x,y,z,(0,0,0),(79,65,94))>1),780,a)
    vertebra=((x/13)**2+((y-34)/12)**2)<1
    a=np.where(body&vertebra&(abs(z)<65),700,a)
    a=np.where(((x/8)**2+((y-34)/8)**2<1)&(abs(z)<65),25,a)
    a=np.where(ellipse(x,y,z,(0,-28,-30),(9,10,42))<1,-850,a)
    for name,kind,c,r,color in structures[1:]: a=np.where(ellipse(x,y,z,c,r)<1,95 if kind=='PTV' else 55,a)
    d.RescaleSlope=1;d.RescaleIntercept=0;d.RescaleType='HU';d.WindowCenter=40;d.WindowWidth=350;d.KVP=120
    pixels(d,a,True);save(d,f'CT_{i:03d}.dcm');cts.append(d)

rs=ds('RTSTRUCT','rs',RTStructureSetStorage);rs.StructureSetLabel='SYNTH PHANTOM';rs.StructureSetDate='20260930';rs.StructureSetTime='120000'
series=item(SeriesInstanceUID=uid('series-CT'),ContourImageSequence=Sequence([ref(d) for d in cts]))
study=item(ReferencedSOPClassUID='1.2.840.10008.3.1.2.3.1',ReferencedSOPInstanceUID=uid('study'),RTReferencedSeriesSequence=Sequence([series]))
rs.ReferencedFrameOfReferenceSequence=Sequence([item(FrameOfReferenceUID=uid('frame'),RTReferencedStudySequence=Sequence([study]))])
rs.StructureSetROISequence=Sequence();rs.ROIContourSequence=Sequence();rs.RTROIObservationsSequence=Sequence()
for n,(name,kind,c,r,color) in enumerate(structures,1):
    rs.StructureSetROISequence.append(item(ROINumber=n,ROIName=name,ReferencedFrameOfReferenceUID=uid('frame'),ROIGenerationAlgorithm='AUTOMATIC'))
    contours=Sequence()
    for i,z in enumerate(zs):
        q=1-((z-c[2])/r[2])**2
        if q<=0:continue
        theta=np.linspace(0,2*np.pi,96,endpoint=False);xyz=np.column_stack((c[0]+r[0]*math.sqrt(q)*np.cos(theta),c[1]+r[1]*math.sqrt(q)*np.sin(theta),np.full(96,z)))
        contours.append(item(ContourGeometricType='CLOSED_PLANAR',NumberOfContourPoints=96,ContourData=[round(float(v),5) for v in xyz.ravel()],ContourImageSequence=Sequence([ref(cts[i])])))
    rs.ROIContourSequence.append(item(ReferencedROINumber=n,ROIDisplayColor=list(color),ContourSequence=contours))
    rs.RTROIObservationsSequence.append(item(ObservationNumber=n,ReferencedROINumber=n,RTROIInterpretedType=kind,ROIInterpreter=''))
save(rs,'RTSTRUCT_SYNTHETIC.dcm')

rp=ds('RTPLAN','rp',RTPlanStorage);rp.RTPlanLabel='SYNTH-DMAT';rp.RTPlanName='Synthetic dual-layer C-arm';rp.RTPlanDescription='Entirely synthetic Accela-inspired geometry. Not a vendor export. Analytic display dose, NOT a treatment calculation.'
rp.RTPlanDate='20260930';rp.RTPlanTime='120000';rp.RTPlanGeometry='PATIENT';rp.ApprovalStatus='UNAPPROVED';rp.ReferencedStructureSetSequence=Sequence([ref(rs)])
rp.PatientSetupSequence=Sequence([item(PatientSetupNumber=1,PatientPosition='HFS')]);rp.BeamSequence=Sequence()
rp.FractionGroupSequence=Sequence([item(FractionGroupNumber=1,NumberOfFractionsPlanned=1,NumberOfBeams=2,NumberOfBrachyApplicationSetups=0,
    ReferencedBeamSequence=Sequence([item(ReferencedBeamNumber=i,BeamMeterset=700+100*i,BeamDose=0) for i in (1,2)]))])
boundaries=[np.linspace(-115,115,47),np.linspace(-117.5,117.5,48)]
for bi in (1,2):
    b=item(BeamNumber=bi,BeamName=f'SYNTH ARC {bi}',BeamType='DYNAMIC',RadiationType='PHOTON',TreatmentDeliveryType='TREATMENT',PrimaryDosimeterUnit='MU',
        TreatmentMachineName='SYNTH-CARM',Manufacturer='SYNTHETIC - NOT VARIAN',ManufacturerModelName='Accela-inspired SYNTHETIC',SourceAxisDistance=1000,
        ReferencedPatientSetupNumber=1,FinalCumulativeMetersetWeight=1,NumberOfControlPoints=121,NumberOfWedges=0,NumberOfCompensators=0,NumberOfBoli=0,NumberOfBlocks=0,
        EnhancedRTBeamLimitingDeviceDefinitionFlag='YES',EnhancedRTBeamLimitingDeviceSequence=Sequence(),ControlPointSequence=Sequence())
    for li,bounds in enumerate(boundaries,1):
        b.EnhancedRTBeamLimitingDeviceSequence.append(item(DeviceIndex=li,DeviceLabel=f'SYNTH_LAYER_{li}',DeviceTypeCodeSequence=code('130331','Leaf Pairs'),
            BeamModifierOrientationAngle=0.,RTBeamLimitingDeviceProximalDistance=None,RTBeamLimitingDeviceDistalDistance=None,
            ParallelRTBeamDelimiterDeviceSequence=Sequence([item(NumberOfParallelRTBeamDelimiters=len(bounds)-1,ParallelRTBeamDelimiterDeviceOrientationLabelCodeSequence=code('130334','X Orientation'),
            ParallelRTBeamDelimiterOpeningMode='VARIABLE',ParallelRTBeamDelimiterBoundaries=bounds.tolist())])))
    ts=np.linspace(0,1,121);progress=ts+.06*np.sin(2*np.pi*ts)
    gantries=340+230*progress if bi==1 else 215-250*progress;colls=350+85*ts if bi==1 else 65-110*ts
    increments=np.diff(np.abs(gantries-gantries[0]))*(.6+.4*np.sin(np.arange(120)*.08+bi)**2);weights=np.r_[0,np.cumsum(increments)];weights/=weights[-1]
    for i,(g,k,t,w) in enumerate(zip(gantries,colls,ts,weights)):
        cp=item(ControlPointIndex=i,CumulativeMetersetWeight=round(float(w),10),GantryAngle=round(float(g%360),7),GantryRotationDirection=('CW' if bi==1 else 'CC') if i<120 else 'NONE',
            BeamLimitingDeviceAngle=round(float(k%360),7),BeamLimitingDeviceRotationDirection=('CC' if bi==1 else 'CW') if i<120 else 'NONE',PatientSupportAngle=0,PatientSupportRotationDirection='NONE',
            IsocenterPosition=[0.,0.,0.],DoseRateSet=int(2500+1500*math.sin(math.pi*t)**2),NominalBeamEnergy=8,
            EnhancedRTBeamLimitingOpeningSequence=Sequence())
        gr=math.radians(g);kr=math.radians(k);right=np.array([math.cos(gr),math.sin(gr),0]);up=np.array([0,0,1]);source=np.array([math.sin(gr),-math.cos(gr),0])
        rx=right*math.cos(kr)+up*math.sin(kr);uy=up*math.cos(kr)-right*math.sin(kr)
        for li,bounds in enumerate(boundaries,1):
            mid=(bounds[:-1]+bounds[1:])/2;left=np.full(len(mid),2.);right_bank=left.copy()
            for name,kind,c,r,color in structures[1:3]:
                c=np.array(c);mag=1000/(1000-np.dot(c,source));px=np.dot(c,rx)*mag;py=np.dot(c,uy)*mag;radius=max(r)+4
                span=radius**2-(mid-py)**2;active=span>0;half=np.sqrt(np.maximum(0,span))*(.85+.1*math.sin(t*5+li))
                lo=px-half;hi=px+half;closed=left==right_bank
                left=np.where(active,np.where(closed,lo,np.minimum(left,lo)),left);right_bank=np.where(active,np.where(closed,hi,np.maximum(right_bank,hi)),right_bank)
            cp.EnhancedRTBeamLimitingOpeningSequence.append(item(ReferencedDeviceIndex=li,RTBeamLimitingDeviceOffset=[0.,0.],ParallelRTBeamDelimiterPositions=np.r_[left,right_bank].round(6).tolist()))
        b.ControlPointSequence.append(cp)
    rp.BeamSequence.append(b)
save(rp,'RTPLAN_SYNTHETIC_DMAT.dcm')

rd=ds('RTDOSE','rd',RTDoseStorage);rd.DoseUnits='GY';rd.DoseType='PHYSICAL';rd.DoseSummationType='PLAN';rd.DoseGridScaling=.001;rd.NumberOfFrames=len(zs)
rd.ImagePositionPatient=[origin,origin,float(zs[0])];rd.ImageOrientationPatient=[1,0,0,0,1,0];rd.PixelSpacing=[spacing,spacing];rd.GridFrameOffsetVector=[float(z-zs[0]) for z in zs]
rd.FrameIncrementPointer=[0x3004000c];rd.ReferencedRTPlanSequence=Sequence([ref(rp)]);rd.SeriesDescription='SYNTHETIC analytic dose - NOT calculated'
dose=[]
for z in zs:
    a=np.zeros_like(x)
    for peak,st in zip((24,20),structures[1:3]):a+=peak*np.exp(-.55*ellipse(x,y,z,st[2],tuple(v*1.25 for v in st[3])))
    dose.append(a)
pixels(rd,np.stack(dose)/.001);save(rd,'RTDOSE_SYNTHETIC_ANALYTIC.dcm')

manifest={'kind':'wholly synthetic display/test fixture','ct_slices':len(cts),'rois':len(structures),'beams':2,'control_points':242,'leaf_pairs':[46,47],
 'dose':'Analytic peaks, not calculated from beams; no machine calibration or clinical validity',
 'geometry':'Accela-inspired C-arm; Enhanced indexed paired leaves; no claim of original Accela export encoding',
 'files':[{ 'name':p.name,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in sorted(root.glob('*.dcm'))]}
(root/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
(root/'README.md').write_text('# SYNTHETIC Accela-inspired DICOM phantom\n\nOpen RTPLAN_SYNTHETIC_DMAT.dcm with Space in QuickLook.\n\n73 CT slices, seven ROIs, two dynamic arcs with 46/47 paired MLC leaves per layer. The collimator and gantry cross zero with explicit directions. All files share exact study/frame/SOP references.\n\nEverything is generated geometry, not patient data or a vendor export. The analytic dose is only for testing display and DVH plumbing. It is NOT beam-calculated or calibrated. Machine dimensions and delivery timing are not simulated. Do not use for treatment or advertise this phantom as a clinical Accela example.\n\nSource generator: scripts/CreateAccelaPhantom.py in QuickLook-DicomRT.\n',encoding='utf-8')
print(json.dumps({k:v for k,v in manifest.items() if k!='files'}))
