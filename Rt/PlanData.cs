using System.Collections.Generic;
using System;
using System.Linq;
using Dicom;
namespace QuickLook.DicomRT
{
    public sealed class ControlPoint
    {
        public int Index; public double Gantry,Collimator,Couch,MetersetWeight; public Vec3 Isocenter;
        public double[] MlcPositions,MlcBoundaries,XJaws,YJaws; public string MlcType;
        public string GantryRotationDirection,CollimatorRotationDirection,CouchRotationDirection;
    }
    public sealed class PlanBeam
    {
        public int Number; public string Name; public Vec3 Isocenter; public double Gantry,Collimator,Couch,Meterset;
        public List<ControlPoint> ControlPoints=new List<ControlPoint>();
        public double FinalCumulativeMetersetWeight;
        public override string ToString()=>Name;
    }
    public sealed class PlanData
    {
        public DicomEntry Entry; public string FrameUid,StructureSopUid; public string Label {get;set;} public List<PlanBeam> Beams=new List<PlanBeam>();
        public override string ToString()=>Label;
        public static PlanData Load(DicomEntry entry)
        {
            var d=RtDicom.Full(entry); var result=new PlanData {Entry=entry,FrameUid=RtDicom.Text(d,DicomTag.FrameOfReferenceUID),Label=RtDicom.Text(d,DicomTag.RTPlanLabel,"RTPLAN"),
                StructureSopUid=RtDicom.Items(d,DicomTag.ReferencedStructureSetSequence).Select(i=>RtDicom.Text(i,DicomTag.ReferencedSOPInstanceUID)).FirstOrDefault()??""};
            var metersets=new Dictionary<int,List<double>>();
            foreach(var group in RtDicom.Items(d,DicomTag.FractionGroupSequence))foreach(var beam in RtDicom.Items(group,DicomTag.ReferencedBeamSequence))
            {int number=RtDicom.Int(beam,DicomTag.ReferencedBeamNumber,-1);double value=RtDicom.Number(beam,DicomTag.BeamMeterset); if(!metersets.ContainsKey(number))metersets[number]=new List<double>();metersets[number].Add(value);}
            foreach(var item in RtDicom.Items(d,DicomTag.BeamSequence))
            {
                int number=RtDicom.Int(item,DicomTag.BeamNumber,-1);
                var beam=new PlanBeam {Number=number,Name=RtDicom.Text(item,DicomTag.BeamName,"Beam "+number),Meterset=double.NaN,FinalCumulativeMetersetWeight=RtDicom.Number(item,DicomTag.FinalCumulativeMetersetWeight)};
                List<double> mu; if(metersets.TryGetValue(number,out mu) && mu.Count>0 && mu.All(v=>RtDicom.Finite(v) && Math.Abs(v-mu[0])<1e-6))beam.Meterset=mu[0];
                var leafDefinitions=new Dictionary<string,double[]>();
                foreach(var device in RtDicom.Items(item,DicomTag.BeamLimitingDeviceSequence))
                {
                    string type=RtDicom.Text(device,DicomTag.RTBeamLimitingDeviceType);
                    if(type=="MLCX" || type=="MLCY")
                    {
                        if(leafDefinitions.Count>0)throw new NotSupportedException("Multiple MLC layers are not supported.");
                        var boundaries=RtDicom.Numbers(device,DicomTag.LeafPositionBoundaries); int pairs=RtDicom.Int(device,DicomTag.NumberOfLeafJawPairs);
                        if(pairs<1 || boundaries.Length!=pairs+1 || !boundaries.All(RtDicom.Finite) || !Enumerable.Range(1,boundaries.Length-1).All(i=>boundaries[i]>boundaries[i-1])) throw new ArgumentException("Invalid MLC leaf boundaries.");
                        leafDefinitions[type]=boundaries;
                    }
                }
                ControlPoint previous=null;
                foreach(var cp in RtDicom.Items(item,DicomTag.ControlPointSequence))
                {
                    var current=new ControlPoint {Index=RtDicom.Int(cp,DicomTag.ControlPointIndex,beam.ControlPoints.Count),
                        Gantry=RtDicom.Number(cp,DicomTag.GantryAngle,previous?.Gantry??double.NaN),Collimator=RtDicom.Number(cp,DicomTag.BeamLimitingDeviceAngle,previous?.Collimator??double.NaN),Couch=RtDicom.Number(cp,DicomTag.PatientSupportAngle,previous?.Couch??double.NaN),
                        MetersetWeight=RtDicom.Number(cp,DicomTag.CumulativeMetersetWeight),Isocenter=RtDicom.Vector(RtDicom.Numbers(cp,DicomTag.IsocenterPosition),previous?.Isocenter??new Vec3(double.NaN,double.NaN,double.NaN)),
                        XJaws=Copy(previous?.XJaws),YJaws=Copy(previous?.YJaws),MlcPositions=Copy(previous?.MlcPositions),MlcBoundaries=Copy(previous?.MlcBoundaries),MlcType=previous?.MlcType??"",
                        GantryRotationDirection=RtDicom.Text(cp,DicomTag.GantryRotationDirection,previous?.GantryRotationDirection??""),
                        CollimatorRotationDirection=RtDicom.Text(cp,DicomTag.BeamLimitingDeviceRotationDirection,previous?.CollimatorRotationDirection??""),
                        CouchRotationDirection=RtDicom.Text(cp,DicomTag.PatientSupportRotationDirection,previous?.CouchRotationDirection??"")};
                    foreach(var device in RtDicom.Items(cp,DicomTag.BeamLimitingDevicePositionSequence))
                    {
                        string type=RtDicom.Text(device,DicomTag.RTBeamLimitingDeviceType); var positions=RtDicom.Numbers(device,DicomTag.LeafJawPositions);
                        if(positions.Any(v=>!RtDicom.Finite(v)))throw new ArgumentException("Nonfinite aperture coordinates.");
                        if(type=="X" || type=="ASYMX") {if(positions.Length!=2)throw new ArgumentException("Invalid X jaw positions.");current.XJaws=positions;}
                        else if(type=="Y" || type=="ASYMY") {if(positions.Length!=2)throw new ArgumentException("Invalid Y jaw positions.");current.YJaws=positions;}
                        else if(type=="MLCX" || type=="MLCY")
                        {
                            double[] boundaries;
                            if(!leafDefinitions.TryGetValue(type,out boundaries) || positions.Length!=2*(boundaries.Length-1))throw new ArgumentException("Invalid MLC positions.");
                            current.MlcType=type;current.MlcPositions=positions;current.MlcBoundaries=Copy(boundaries);
                        }
                        else throw new NotSupportedException("Unsupported beam limiting device.");
                    }
                    beam.ControlPoints.Add(current); previous=current;
                }
                if(beam.ControlPoints.Count>0)
                {var first=beam.ControlPoints[0];beam.Isocenter=first.Isocenter;beam.Gantry=first.Gantry;beam.Collimator=first.Collimator;beam.Couch=first.Couch;}
                else {beam.Isocenter=new Vec3(double.NaN,double.NaN,double.NaN);beam.Gantry=beam.Collimator=beam.Couch=double.NaN;}
                result.Beams.Add(beam);
            }
            return result;
        }
        static double[] Copy(double[] values)=>values==null?new double[0]:(double[])values.Clone();
    }
}
