using System;
using System.IO;
using System.Linq;
using Dicom;
using QuickLook.DicomRT;
internal static class EnhancedDeviceScenarios
{
    static DicomTag A(ushort e)=>new DicomTag(0x300a,e);
    static DicomTag E(ushort e)=>new DicomTag(0x3008,e);
    static DicomDataset Definition(ushort index,double angle=0)=>new DicomDataset()
        .Add(new DicomUnsignedShort(new DicomTag(0x3010,0x0039),index))
        .Add(new DicomSequence(new DicomTag(0x3010,0x002e),new DicomDataset().Add(DicomTag.CodingSchemeDesignator,"DCM").Add(DicomTag.CodeValue,"130331")))
        .Add(new DicomFloatingPointDouble(A(0x0645),angle))
        .Add(new DicomSequence(A(0x0647),new DicomDataset().Add(new DicomUnsignedShort(A(0x0648),2))
            .Add(new DicomCodeString(A(0x064e),"VARIABLE")).Add(new DicomFloatingPointDouble(A(0x0649),-10d,0d,10d))));
    static DicomDataset Opening(ushort index,double x)=>new DicomDataset().Add(new DicomUnsignedShort(A(0x0607),index))
        .Add(new DicomFloatingPointDouble(A(0x064b),0d,0d)).Add(new DicomFloatingPointDouble(A(0x064a),-x,-x,x,x));
    static PlanData Load(DicomDataset[] devices,params DicomDataset[] cps)=>PlanData.Load(new DicomEntry{Dataset=new DicomDataset().Add(new DicomSequence(DicomTag.BeamSequence,
        new DicomDataset().Add(DicomTag.Manufacturer,"Varian").Add(DicomTag.ManufacturerModelName,"Accela").Add(DicomTag.TreatmentMachineName,"TEST")
            .Add(new DicomCodeString(E(0x00a3),"YES")).Add(new DicomSequence(E(0x00a1),devices)).Add(new DicomSequence(DicomTag.ControlPointSequence,cps))))});
    static DicomDataset Cp(params DicomDataset[] openings)=>new DicomDataset().Add(new DicomSequence(E(0x00a2),openings));
    public static void Run(Action<bool,string> check)
    {
        var plan=Load(new[]{Definition(1),Definition(2,90)},Cp(Opening(2,3),Opening(1,7)),Cp(Opening(2,5)),new DicomDataset());
        var beam=plan.Beams[0];var points=beam.ControlPoints;
        check(points[0].MlcLayers[0].Positions[0]==-7&&points[0].MlcLayers[1].Positions[0]==-3,"Enhanced index mapping ignores sequence order");
        check(points[1].MlcLayers[0].Positions[0]==-7&&points[2].MlcLayers[1].Positions[0]==-5,"Enhanced omitted devices inherit independently");
        check(points[0].MlcLayers[1].IsY&&BeamAperture.Rectangles(points[0]).Sum(r=>(r.Right-r.Left)*(r.Top-r.Bottom))==84,"Enhanced orthogonal layers intersect correctly");
        check(BeamMachineInfo.Form(beam)==LinacForm.CArm&&BeamMachineInfo.Label(beam)=="TEST · Accela","Accela dual layer retains C-arm identity");
        beam.ManufacturerModelName="Halcyon 4.0";check(BeamMachineInfo.Form(beam)==LinacForm.Ring,"Halcyon model selects ring");
        beam.ManufacturerModelName="Ethos";check(BeamMachineInfo.Form(beam)==LinacForm.Ring,"Ethos model selects ring");
        beam.ManufacturerModelName="";beam.TreatmentMachineName=" Hal01 ";check(BeamMachineInfo.Form(beam)==LinacForm.Ring&&BeamMachineInfo.UsesLocalRingHint(beam),"User-confirmed Hal prefix selects local ring schematic");
        check(BeamMachineInfo.Detail(beam).Contains("Local machine-name rule"),"Local fallback provenance remains explicit");
        beam.ManufacturerModelName="Accela";check(BeamMachineInfo.Form(beam)==LinacForm.CArm&&!BeamMachineInfo.UsesLocalRingHint(beam),"Explicit C-arm model overrides conflicting local alias");
        beam.ManufacturerModelName="";beam.TreatmentMachineName="DUAL-LAYER";check(BeamMachineInfo.Form(beam)==LinacForm.Unknown,"Dual layer count alone does not infer ring identity");
        beam.TreatmentMachineName="OTHER-Hal01";check(BeamMachineInfo.Form(beam)==LinacForm.Unknown,"Local ring prefix must start the machine name");
        beam.TreatmentMachineName="hal02";check(BeamMachineInfo.Form(beam)==LinacForm.Ring,"Local machine prefix is case insensitive");
        beam.ManufacturerModelName="ARTISTE";check(BeamMachineInfo.Form(beam)==LinacForm.CArm,"ARTISTE model from legacy archive overrides local name hint");
        Action<Action,string> reject=(run,label)=>{bool failed=false;try{run();}catch(ArgumentException){failed=true;}catch(NotSupportedException){failed=true;}check(failed,label);};
        reject(()=>Load(new[]{Definition(1),Definition(1)},Cp(Opening(1,5))),"Duplicate device rejected");
        reject(()=>Load(new[]{Definition(1)},Cp(Opening(2,5))),"Unknown device reference rejected");
        reject(()=>Load(new[]{Definition(1),Definition(2)},Cp(Opening(1,5))),"Missing initial layer rejected");
        reject(()=>Load(new[]{Definition(1)},Cp(Opening(1,5),Opening(1,6))),"Duplicate opening rejected");
        reject(()=>Load(new[]{Definition(1,35)},Cp(Opening(1,5))),"Unsupported orientation rejected");
        var offset=Opening(1,5);offset.AddOrUpdate(new DicomFloatingPointDouble(A(0x064b),2d,0d));
        reject(()=>Load(new[]{Definition(1)},Cp(offset)),"Unsupported offsets never silently dropped");
        var bad=Opening(1,5);bad.AddOrUpdate(new DicomFloatingPointDouble(A(0x064a),1d,2d));reject(()=>Load(new[]{Definition(1)},Cp(bad)),"Truncated positions rejected");
        var c0=Cp(Opening(1,5)).Add(DicomTag.BeamLimitingDeviceAngle,350d).Add(DicomTag.BeamLimitingDeviceRotationDirection,"CC");
        var c1=Cp(Opening(1,6)).Add(DicomTag.BeamLimitingDeviceAngle,10d);
        check(Load(new[]{Definition(1)},c0,c1).Beams[0].DynamicCollimator,"Dynamic collimator detected across zero");
    }
    public static int Inspect(string folder)
    {
        DicomRtDictionary.EnsureLoaded();
        int plans=0,points=0,layers=0;
        try{
            foreach(var path in Directory.EnumerateFiles(folder,"*.dcm",SearchOption.AllDirectories)){
                var d=DicomFile.Open(path).Dataset;if(d.GetSingleValueOrDefault(DicomTag.Modality,"")!="RTPLAN")continue;
                var p=PlanData.Load(new DicomEntry{Dataset=d});int bi=0;
                foreach(var b in p.Beams){var raw=d.GetSequence(DicomTag.BeamSequence).Items[bi++];int ci=0;
                    foreach(var cp in b.ControlPoints){var source=raw.GetSequence(DicomTag.ControlPointSequence).Items[ci++];
                        if(Math.Abs(cp.Collimator-source.GetSingleValue<double>(DicomTag.BeamLimitingDeviceAngle))>1e-8)throw new Exception("Collimator mismatch");
                        var seq=source.GetSequence(b.EnhancedDevices?E(0x00a2):DicomTag.BeamLimitingDevicePositionSequence);int li=0;
                        foreach(var item in seq.Items){var l=b.EnhancedDevices?cp.MlcLayers.Single(x=>x.Key=="Enhanced#"+item.GetSingleValue<int>(A(0x0607))):cp.MlcLayers[li++];
                            var values=item.GetValues<double>(b.EnhancedDevices?A(0x064a):DicomTag.LeafJawPositions);
                            if(!values.SequenceEqual(l.Positions))throw new Exception("Leaf position mismatch");layers++;
                        }points++;
                    }
                }plans++;
            }
            if(plans==0)throw new Exception("No test plans");Console.WriteLine($"PASS fixture parser: {plans} plans, {points} CPs, {layers} layer arrays identical to encoded DICOM");return 0;
        }catch(Exception e){Console.WriteLine("FAIL fixture parser after "+plans+" plans / "+points+" points: "+e);return 1;}
    }
}
