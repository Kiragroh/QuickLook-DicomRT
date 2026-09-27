using System.Collections.Generic;
using System;
using System.Linq;
using Dicom;
namespace QuickLook.DicomRT
{
    public sealed class MlcLayer
    {
        public string Key, Type;
        public double[] Boundaries = new double[0], Positions = new double[0];
        public bool IsY => Type != null && Type.StartsWith("MLCY", StringComparison.Ordinal);
        public MlcLayer Copy() => new MlcLayer {Key=Key,Type=Type,Boundaries=(double[])Boundaries.Clone(),Positions=(double[])Positions.Clone()};
    }
    public sealed class ControlPoint
    {
        public List<BeamBlock> Blocks=new List<BeamBlock>();
        public double DoseRateSet=double.NaN;
        public double GantryPitch,TablePitch,TableRoll,TableEccentric;
        public int Index; public double Gantry,Collimator,Couch,MetersetWeight; public Vec3 Isocenter;
        public double[] MlcPositions,MlcBoundaries,XJaws,YJaws; public string MlcType;
        public List<MlcLayer> MlcLayers=new List<MlcLayer>();
        public string GantryRotationDirection,CollimatorRotationDirection,CouchRotationDirection;
    }
    public sealed class PlanBeam
    {
        public int Number; public string Name; public Vec3 Isocenter; public double Gantry,Collimator,Couch,Meterset;
        // Empty when the referenced setup is absent or ambiguous; never assume HFS.
        public string PatientPosition="",TreatmentDeliveryType="";
        public List<ControlPoint> ControlPoints=new List<ControlPoint>();
        public double SourceAxisDistance=double.NaN;
        public string PrimaryDosimeterUnit;
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
                if(RtDicom.Text(item,new DicomTag(0x3008,0x00a3))=="YES")throw new NotSupportedException("Enhanced beam limiting device geometry is not supported.");
                int number=RtDicom.Int(item,DicomTag.BeamNumber,-1);
                var beam=new PlanBeam {Number=number,Name=RtDicom.Text(item,DicomTag.BeamName,"Beam "+number),PrimaryDosimeterUnit=RtDicom.Text(item,DicomTag.PrimaryDosimeterUnit),SourceAxisDistance=RtDicom.Number(item,DicomTag.SourceAxisDistance),Meterset=double.NaN,FinalCumulativeMetersetWeight=RtDicom.Number(item,DicomTag.FinalCumulativeMetersetWeight),PatientPosition=PatientSetupPosition(d,item),TreatmentDeliveryType=RtDicom.Text(item,DicomTag.TreatmentDeliveryType)};
                List<double> mu; if(metersets.TryGetValue(number,out mu) && mu.Count>0 && mu.All(v=>RtDicom.Finite(v) && Math.Abs(v-mu[0])<1e-6))beam.Meterset=mu[0];
                var leafDefinitions=new List<MlcLayer>();
                foreach(var device in RtDicom.Items(item,DicomTag.BeamLimitingDeviceSequence))
                {
                    string type=RtDicom.Text(device,DicomTag.RTBeamLimitingDeviceType);
                    if(IsMlc(type))
                    {
                        var boundaries=RtDicom.Numbers(device,DicomTag.LeafPositionBoundaries); int pairs=RtDicom.Int(device,DicomTag.NumberOfLeafJawPairs);
                        if(pairs<1 || boundaries.Length!=pairs+1 || !boundaries.All(RtDicom.Finite) || !Enumerable.Range(1,boundaries.Length-1).All(i=>boundaries[i]>boundaries[i-1])) throw new ArgumentException("Invalid MLC leaf boundaries.");
                        leafDefinitions.Add(new MlcLayer {Key=type+"#"+leafDefinitions.Count(x=>x.Type==type),Type=type,Boundaries=boundaries});
                    }
                }
                var blocks=RtDicom.Items(item,DicomTag.BlockSequence).Select(b=>BeamBlock.Create(RtDicom.Text(b,DicomTag.BlockType),RtDicom.Numbers(b,DicomTag.BlockData),RtDicom.Int(b,DicomTag.BlockNumberOfPoints))).ToList();
                ControlPoint previous=null;
                foreach(var cp in RtDicom.Items(item,DicomTag.ControlPointSequence))
                {
                    var current=new ControlPoint {Blocks=blocks,Index=RtDicom.Int(cp,DicomTag.ControlPointIndex,beam.ControlPoints.Count),
                        DoseRateSet=RtDicom.Number(cp,DicomTag.DoseRateSet,previous?.DoseRateSet??double.NaN),
                        Gantry=RtDicom.Number(cp,DicomTag.GantryAngle,previous?.Gantry??double.NaN),Collimator=RtDicom.Number(cp,DicomTag.BeamLimitingDeviceAngle,previous?.Collimator??double.NaN),Couch=RtDicom.Number(cp,DicomTag.PatientSupportAngle,previous?.Couch??double.NaN),
                        GantryPitch=RtDicom.Number(cp,new DicomTag(0x300a,0x014a),previous?.GantryPitch??0),TablePitch=RtDicom.Number(cp,new DicomTag(0x300a,0x0140),previous?.TablePitch??0),TableRoll=RtDicom.Number(cp,new DicomTag(0x300a,0x0144),previous?.TableRoll??0),TableEccentric=RtDicom.Number(cp,new DicomTag(0x300a,0x0125),previous?.TableEccentric??0),
                        MetersetWeight=RtDicom.Number(cp,DicomTag.CumulativeMetersetWeight),Isocenter=RtDicom.Vector(RtDicom.Numbers(cp,DicomTag.IsocenterPosition),previous?.Isocenter??new Vec3(double.NaN,double.NaN,double.NaN)),
                        XJaws=Copy(previous?.XJaws),YJaws=Copy(previous?.YJaws),
                        MlcLayers=(previous==null?leafDefinitions:previous.MlcLayers).Select(x=>x.Copy()).ToList(),
                        GantryRotationDirection=RtDicom.Text(cp,DicomTag.GantryRotationDirection,previous?.GantryRotationDirection??""),
                        CollimatorRotationDirection=RtDicom.Text(cp,DicomTag.BeamLimitingDeviceRotationDirection,previous?.CollimatorRotationDirection??""),
                        CouchRotationDirection=RtDicom.Text(cp,DicomTag.PatientSupportRotationDirection,previous?.CouchRotationDirection??"")};
                    var updates=RtDicom.Items(cp,DicomTag.BeamLimitingDevicePositionSequence).ToArray();
                    var occurrences=new Dictionary<string,int>();
                    foreach(var device in updates)
                    {
                        string type=RtDicom.Text(device,DicomTag.RTBeamLimitingDeviceType); var positions=RtDicom.Numbers(device,DicomTag.LeafJawPositions);
                        if(positions.Any(v=>!RtDicom.Finite(v)))throw new ArgumentException("Nonfinite aperture coordinates.");
                        if(type=="X" || type=="ASYMX") {if(positions.Length!=2)throw new ArgumentException("Invalid X jaw positions.");current.XJaws=positions;}
                        else if(type=="Y" || type=="ASYMY") {if(positions.Length!=2)throw new ArgumentException("Invalid Y jaw positions.");current.YJaws=positions;}
                        else if(IsMlc(type))
                        {
                            var candidates=current.MlcLayers.Where(x=>x.Type==type).ToArray();
                            int updateCount=updates.Count(x=>RtDicom.Text(x,DicomTag.RTBeamLimitingDeviceType)==type);
                            int occurrence;occurrences.TryGetValue(type,out occurrence);occurrences[type]=occurrence+1;
                            MlcLayer layer=null;
                            // Classic RTPLAN has no layer reference. Complete duplicate-type groups use
                            // definition/position occurrence order; partial groups require unique geometry.
                            if(updateCount==candidates.Length && occurrence<candidates.Length)layer=candidates[occurrence];
                            else if(updateCount==1) {var matching=candidates.Where(x=>positions.Length==2*(x.Boundaries.Length-1)).ToArray();if(matching.Length==1)layer=matching[0];}
                            if(layer==null)throw new ArgumentException("Ambiguous or undefined MLC layer update.");
                            if(positions.Length!=2*(layer.Boundaries.Length-1))throw new ArgumentException("Invalid MLC positions.");
                            layer.Positions=positions;
                        }
                        else throw new NotSupportedException("Unsupported beam limiting device.");
                    }
                    if(current.MlcLayers.Any(x=>x.Positions.Length!=2*(x.Boundaries.Length-1)))throw new ArgumentException("Missing initial MLC layer positions.");
                    // Legacy single-layer consumers retain the first layer; the viewer uses MlcLayers.
                    var firstLayer=current.MlcLayers.FirstOrDefault();
                    current.MlcType=firstLayer?.Type??"";current.MlcPositions=firstLayer?.Positions??new double[0];current.MlcBoundaries=firstLayer?.Boundaries??new double[0];
                    beam.ControlPoints.Add(current); previous=current;
                }
                if(beam.ControlPoints.Count>0)
                {var first=beam.ControlPoints[0];beam.Isocenter=first.Isocenter;beam.Gantry=first.Gantry;beam.Collimator=first.Collimator;beam.Couch=first.Couch;}
                else {beam.Isocenter=new Vec3(double.NaN,double.NaN,double.NaN);beam.Gantry=beam.Collimator=beam.Couch=double.NaN;}
                result.Beams.Add(beam);
            }
            return result;
        }
        static string PatientSetupPosition(DicomDataset plan,DicomDataset beam)
        {
            int reference=RtDicom.Int(beam,DicomTag.ReferencedPatientSetupNumber,-1);
            if(reference<0)return "";
            var matches=RtDicom.Items(plan,DicomTag.PatientSetupSequence).Where(s=>RtDicom.Int(s,DicomTag.PatientSetupNumber,-2)==reference).ToArray();
            return matches.Length==1?RtDicom.Text(matches[0],DicomTag.PatientPosition).Trim().ToUpperInvariant():"";
        }
        // Numeric suffixes are an explicit vendor extension (e.g. MLCX1/MLCX2), not standard enumerated values.
        static bool IsMlc(string type) => type=="MLCX" || type=="MLCY" ||
            (type!=null && type.Length>4 && (type.StartsWith("MLCX",StringComparison.Ordinal)||type.StartsWith("MLCY",StringComparison.Ordinal)) && type.Substring(4).All(char.IsDigit));
        static double[] Copy(double[] values)=>values==null?new double[0]:(double[])values.Clone();
    }
}
