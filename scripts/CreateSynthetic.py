"""Create a wholly synthetic CT/MR/RT/REG fixture; contains no patient material."""
from pathlib import Path
import sys, math
import numpy as np
from pydicom.dataset import FileDataset, Dataset, FileMetaDataset
from pydicom.sequence import Sequence
from pydicom.uid import generate_uid, ExplicitVRLittleEndian

root=Path(sys.argv[1]); root.mkdir(parents=True,exist_ok=True)
prefix='1.2.826.0.1.3680043.10.543.'
uid=lambda:generate_uid(prefix=prefix)
study=uid(); ctframe=uid(); mrframe=uid(); ctseries=uid(); mrseries=uid()
classes={'CT':'1.2.840.10008.5.1.4.1.1.2','MR':'1.2.840.10008.5.1.4.1.1.4','RTSTRUCT':'1.2.840.10008.5.1.4.1.1.481.3','RTDOSE':'1.2.840.10008.5.1.4.1.1.481.2','RTPLAN':'1.2.840.10008.5.1.4.1.1.481.5','REG':'1.2.840.10008.5.1.4.1.1.66.1'}
def ds(mod,series=None,frame=None):
 m=FileMetaDataset();m.TransferSyntaxUID=ExplicitVRLittleEndian;m.MediaStorageSOPClassUID=classes[mod];m.MediaStorageSOPInstanceUID=uid();m.ImplementationClassUID=uid()
 d=FileDataset(None,{},file_meta=m,preamble=b'\0'*128);d.is_little_endian=True;d.is_implicit_VR=False;d.SOPClassUID=m.MediaStorageSOPClassUID;d.SOPInstanceUID=m.MediaStorageSOPInstanceUID
 d.PatientName='SYNTHETIC^PREVIEW';d.PatientID='SYNTHETIC';d.StudyInstanceUID=study;d.SeriesInstanceUID=series or uid();d.Modality=mod;d.StudyDate='20260926';d.StudyTime='120000';d.SeriesDescription='Synthetic '+mod
 if frame:d.FrameOfReferenceUID=frame
 return d
def ref(d):
 r=Dataset();r.ReferencedSOPClassUID=d.SOPClassUID;r.ReferencedSOPInstanceUID=d.SOPInstanceUID;return r
def save(d,name):d.save_as(root/name,write_like_original=False)
def pixels(d,array,signed=False):
 d.Rows=array.shape[-2];d.Columns=array.shape[-1];d.SamplesPerPixel=1;d.PhotometricInterpretation='MONOCHROME2';d.BitsAllocated=16;d.BitsStored=16;d.HighBit=15;d.PixelRepresentation=int(signed);d.PixelData=array.astype('<i2' if signed else '<u2').tobytes()
cts=[];mrs=[];yy,xx=np.mgrid[:96,:96];x=-47.5+xx;y=-47.5+yy;zs=np.arange(-32,34,2)
for mod,frame,series,items,offset in [('CT',ctframe,ctseries,cts,(0,0,0)),('MR',mrframe,mrseries,mrs,(30,-5,8))]:
 for i,z in enumerate(zs):
  d=ds(mod,series,frame);d.InstanceNumber=i+1;d.ImagePositionPatient=[-47.5+offset[0],-47.5+offset[1],float(z+offset[2])];d.ImageOrientationPatient=[1,0,0,0,1,0];d.PixelSpacing=[1,1];d.SliceThickness=2;d.SpacingBetweenSlices=2
  body=(x*x/37**2+y*y/32**2+z*z/45**2)<1;target=((x-12)**2+(y+5)**2+z*z)<11**2
  image=np.where(body,40+20*np.sin(x/7)*np.cos(y/8),-1000);image=np.where(target,120,image)
  if mod=='MR':image=np.where(body,700+200*np.sin(x/10)*np.cos(y/10),0);image=np.where(target,1600,image)
  d.RescaleSlope=1;d.RescaleIntercept=0;d.WindowCenter=40 if mod=='CT' else 800;d.WindowWidth=400 if mod=='CT' else 1600;pixels(d,image,True);save(d,f'{mod}_{i:03}.dcm');items.append(d)
rs=ds('RTSTRUCT',frame=ctframe);rs.StructureSetLabel='SYNTHETIC';rs.StructureSetDate='20260926';rs.StructureSetTime='120000'
f=Dataset();f.FrameOfReferenceUID=ctframe;st=Dataset();st.ReferencedSOPClassUID='1.2.840.10008.3.1.2.3.1';st.ReferencedSOPInstanceUID=study;se=Dataset();se.SeriesInstanceUID=ctseries;se.ContourImageSequence=Sequence([ref(d) for d in cts]);st.RTReferencedSeriesSequence=Sequence([se]);f.RTReferencedStudySequence=Sequence([st]);rs.ReferencedFrameOfReferenceSequence=Sequence([f]);rs.StructureSetROISequence=Sequence();rs.ROIContourSequence=Sequence()
for number,name,radius,center,color in [(1,'Target',11,(12,-5,0),[250,100,85]),(2,'Outer shell',30,(0,0,0),[75,220,180])]:
 roi=Dataset();roi.ROINumber=number;roi.ROIName=name;roi.ReferencedFrameOfReferenceUID=ctframe;rs.StructureSetROISequence.append(roi);rc=Dataset();rc.ReferencedROINumber=number;rc.ROIDisplayColor=color;rc.ContourSequence=Sequence()
 for i,z in enumerate(zs):
  if abs(z-center[2])>=radius:continue
  r=math.sqrt(radius**2-(z-center[2])**2);a=np.linspace(0,2*math.pi,48,endpoint=False);coords=np.column_stack([center[0]+r*np.cos(a),center[1]+r*np.sin(a),np.full(48,z)])
  c=Dataset();c.ContourGeometricType='CLOSED_PLANAR';c.NumberOfContourPoints=48;c.ContourData=[round(float(v),5) for v in coords.ravel()];c.ContourImageSequence=Sequence([ref(cts[i])]);rc.ContourSequence.append(c)
 rs.ROIContourSequence.append(rc)
save(rs,'RTSTRUCT.dcm')
rp=ds('RTPLAN',frame=ctframe);rp.RTPlanLabel='SYNTHETIC ARC';rp.RTPlanGeometry='PATIENT';rp.ReferencedStructureSetSequence=Sequence([ref(rs)]);rp.BeamSequence=Sequence()
for beam_no in (1,2):
 b=Dataset();b.BeamNumber=beam_no;b.BeamName=f'Synthetic arc {beam_no}';b.BeamType='DYNAMIC';b.RadiationType='PHOTON';b.NumberOfControlPoints=21;b.FinalCumulativeMetersetWeight=1;b.BeamLimitingDeviceSequence=Sequence()
 for kind in ('X','Y','MLCX'):
  device=Dataset();device.RTBeamLimitingDeviceType=kind;device.NumberOfLeafJawPairs=20 if kind=='MLCX' else 1
  if kind=='MLCX':device.LeafPositionBoundaries=list(np.linspace(-40,40,21))
  b.BeamLimitingDeviceSequence.append(device)
 b.ControlPointSequence=Sequence()
 for i in range(21):
  c=Dataset();c.ControlPointIndex=i;c.CumulativeMetersetWeight=i/20;c.GantryAngle=(350+i*2)%360;c.GantryRotationDirection='CW';c.BeamLimitingDeviceAngle=15;c.PatientSupportAngle=0;c.IsocenterPosition=[12,-5,0];c.BeamLimitingDevicePositionSequence=Sequence()
  for kind in ('X','Y','MLCX'):
   dev=Dataset();dev.RTBeamLimitingDeviceType=kind
   shift=10*math.sin(i/20*math.pi*2);leaves=np.arange(20)
   vals=np.r_[-18+shift+3*np.sin(leaves/3+i/4),18+shift+3*np.sin(leaves/3+i/4)] if kind=='MLCX' else [-35,35]
   dev.LeafJawPositions=[round(float(v),5) for v in vals];c.BeamLimitingDevicePositionSequence.append(dev)
  b.ControlPointSequence.append(c)
 rp.BeamSequence.append(b)
save(rp,'RTPLAN.dcm')
rd=ds('RTDOSE',frame=ctframe);rd.DoseUnits='GY';rd.DoseType='PHYSICAL';rd.DoseSummationType='PLAN';rd.DoseGridScaling=.001;rd.NumberOfFrames=len(zs);rd.ImagePositionPatient=[-47.5,-47.5,float(zs[0])];rd.ImageOrientationPatient=[1,0,0,0,1,0];rd.PixelSpacing=[1,1];rd.GridFrameOffsetVector=[float(z-zs[0]) for z in zs];rd.ReferencedRTPlanSequence=Sequence([ref(rp)]);dose=np.stack([12000*np.exp(-((x-12)**2+(y+5)**2+z*z)/(2*13**2)) for z in zs]);pixels(rd,dose);save(rd,'RTDOSE.dcm')
reg=ds('REG',frame=ctframe);reg.RegistrationSequence=Sequence()
for frame,images,offset in [(ctframe,cts,(0,0,0)),(mrframe,mrs,(-30,5,-8))]:
 it=Dataset();it.FrameOfReferenceUID=frame;it.ReferencedImageSequence=Sequence([ref(d) for d in images]);mr=Dataset();mat=Dataset();mat.FrameOfReferenceTransformationMatrixType='RIGID';matrix=np.eye(4);matrix[:3,3]=offset;mat.FrameOfReferenceTransformationMatrix=list(matrix.ravel());mr.MatrixSequence=Sequence([mat]);it.MatrixRegistrationSequence=Sequence([mr]);reg.RegistrationSequence.append(it)
save(reg,'REG.dcm');print('Synthetic fixture: 66 images + RTSTRUCT/RTPLAN/RTDOSE/REG')
