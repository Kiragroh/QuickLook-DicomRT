using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;
using Dicom;
using Dicom.Imaging;
using Dicom.IO.Buffer;
using QuickLook.DicomRT;
internal static class Program
{
    static int checks;
    static void Assert(bool condition, string label) { checks++; if (!condition) throw new Exception(label); }
    static void Near(double a, double b, string label) => Assert(Math.Abs(a-b)<1e-6,label);
    static Matrix4 Translate(double x,double y,double z) => new Matrix4(new double[]{1,0,0,x,0,1,0,y,0,0,1,z,0,0,0,1});
    static void MatrixTests()
    {
        var r = new Matrix4(new double[]{0,-1,0,0,1,0,0,0,0,0,1,0,0,0,0,1});
        var t = Translate(10,0,0);
        var p = Matrix4.Multiply(r,t).Transform(new Vec3(1,2,3));
        Near(p.X,-2,"composition x"); Near(p.Y,11,"composition y");
        var q = Matrix4.Multiply(r,t).Inverse().Transform(p);
        Near(q.X,1,"inverse x"); Near(q.Y,2,"inverse y"); Near(q.Z,3,"inverse z");
        bool singular=false;
        try { new Matrix4(new double[]{0,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1}).Inverse(); } catch(ArgumentException) { singular=true; }
        Assert(singular,"singular matrix rejected");
    }
    static DicomDataset MatrixItem(Matrix4 m) => new DicomDataset().Add(DicomTag.FrameOfReferenceTransformationMatrix,m.Values).Add(DicomTag.FrameOfReferenceTransformationMatrixType,"AFFINE");
    static DicomDataset RegItem(string frame, params Matrix4[] matrices)
    {
        var ms=new List<DicomDataset>(); foreach(var m in matrices) ms.Add(MatrixItem(m));
        return new DicomDataset().Add(DicomTag.FrameOfReferenceUID,frame).Add(new DicomSequence(DicomTag.MatrixRegistrationSequence,new DicomDataset().Add(new DicomSequence(DicomTag.MatrixSequence,ms.ToArray()))));
    }
    static void RegistrationTests()
    {
        Assert(RegistrationReader.Resolve(new List<RegistrationLink>(),"1.1","1.1")!=null,"equal known frame identity");
        Assert(RegistrationReader.Resolve(new List<RegistrationLink>(),"","")==null,"empty frame is not identity");
        var r = new Matrix4(new double[]{0,-1,0,0,1,0,0,0,0,0,1,0,0,0,0,1});
        var ds=new DicomDataset().Add(DicomTag.FrameOfReferenceUID,"1.3").Add(new DicomSequence(DicomTag.RegistrationSequence,RegItem("1.1",Translate(10,0,0),r),RegItem("1.2",Translate(0,5,0))));
        var cat=new DicomCatalog(); cat.Files.Add(new DicomEntry { Modality="REG",Dataset=ds });
        var links=RegistrationReader.Read(cat);
        var mapping=RegistrationReader.Resolve(links,"1.1","1.2"); Assert(mapping!=null,"same object registered coordinate mapping");
        var p=mapping.Transform(new Vec3(1,2,3)); Near(p.X,-2,"registration order x"); Near(p.Y,6,"registration order y");
        var q=RegistrationReader.Resolve(links,"1.2","1.1").Transform(p); Near(q.X,1,"registration reverse x"); Near(q.Y,2,"registration reverse y");
        Assert(RegistrationReader.Resolve(links,"1.4","1.2")==null,"unrelated frame rejected");
        links.Add(new RegistrationLink { SourceFrame="1.1",TargetFrame="1.2",SourceToTarget=Translate(999,0,0) });
        Assert(RegistrationReader.Resolve(links,"1.1","1.2")==null,"conflicting registration rejected");
        var chain=new List<RegistrationLink> { new RegistrationLink { SourceFrame="1.1",TargetFrame="1.2",SourceToTarget=Translate(1,0,0) },new RegistrationLink { SourceFrame="1.2",TargetFrame="1.3",SourceToTarget=Translate(1,0,0) } };
        Assert(RegistrationReader.Resolve(chain,"1.1","1.3")==null,"no cross-object transitivity");
        var imageRef=RegItem("1.1",Translate(2,0,0)); imageRef.Remove(DicomTag.FrameOfReferenceUID); imageRef.Add(new DicomSequence(DicomTag.ReferencedImageSequence,new DicomDataset().Add(DicomTag.ReferencedSOPInstanceUID,"1.5")));
        var refCat=new DicomCatalog(); refCat.Files.Add(new DicomEntry { SopUid="1.5",FrameUid="1.1",Modality="CT" }); refCat.Files.Add(new DicomEntry {Modality="REG",Dataset=new DicomDataset().Add(DicomTag.FrameOfReferenceUID,"1.2").Add(new DicomSequence(DicomTag.RegistrationSequence,imageRef)) });
        Assert(RegistrationReader.Resolve(RegistrationReader.Read(refCat),"1.1","1.2")!=null,"frame inferred from exact image reference");
        refCat.Files[1].Dataset.Add(new DicomSequence(new DicomTag(0x0064,0x0002),new DicomDataset()));
        Assert(RegistrationReader.Read(refCat).Count==0,"deformable registration unsupported");
        var invalidRigid=MatrixItem(new Matrix4(new double[]{2,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1}));invalidRigid.AddOrUpdate(DicomTag.FrameOfReferenceTransformationMatrixType,"RIGID");
        var rigidCat=new DicomCatalog();rigidCat.Files.Add(new DicomEntry{Modality="REG",Dataset=new DicomDataset().Add(DicomTag.FrameOfReferenceUID,"1.2").Add(new DicomSequence(DicomTag.RegistrationSequence,
            new DicomDataset().Add(DicomTag.FrameOfReferenceUID,"1.1").Add(new DicomSequence(DicomTag.MatrixRegistrationSequence,new DicomDataset().Add(new DicomSequence(DicomTag.MatrixSequence,invalidRigid))))))});
        Assert(RegistrationReader.Read(rigidCat).Count==0,"invalid declared rigid scaling rejected");
    }
    static DicomDataset DoseDataset(double[] offsets,string units="GY")
    {
        var d=new DicomDataset().Add(DicomTag.SOPClassUID,DicomUID.RTDoseStorage).Add(DicomTag.SOPInstanceUID,"1.9")
            .Add(DicomTag.FrameOfReferenceUID,"1.1").Add(DicomTag.Rows,(ushort)2).Add(DicomTag.Columns,(ushort)2)
            .Add(DicomTag.BitsAllocated,(ushort)16).Add(DicomTag.BitsStored,(ushort)16).Add(DicomTag.HighBit,(ushort)15)
            .Add(DicomTag.PixelRepresentation,(ushort)0).Add(DicomTag.SamplesPerPixel,(ushort)1).Add(DicomTag.PhotometricInterpretation,"MONOCHROME2")
            .Add(DicomTag.ImagePositionPatient,0d,0d,10d).Add(DicomTag.ImageOrientationPatient,1d,0d,0d,0d,1d,0d)
            .Add(DicomTag.PixelSpacing,2d,3d).Add(DicomTag.GridFrameOffsetVector,offsets).Add(DicomTag.DoseGridScaling,0.01d).Add(DicomTag.DoseUnits,units);
        var px=DicomPixelData.Create(d,true);
        for(int z=0;z<offsets.Length;z++)
        {
            var bytes=new byte[8]; for(int i=0;i<4;i++) {var n=BitConverter.GetBytes((ushort)(100*(z+1)+10*i)); Buffer.BlockCopy(n,0,bytes,i*2,2);}
            px.AddFrame(new MemoryByteBuffer(bytes));
        }
        return d;
    }
    static void DoseTests()
    {
        var dose=DoseGrid.Load(new DicomEntry {Dataset=DoseDataset(new double[]{0,2,5})});
        Near(dose.Sample(new Vec3(0,0,10)),1,"dose scaling"); Near(dose.Maximum,3.3,"dose maximum");
        Near(dose.Sample(new Vec3(1.5,1,13.5)),2.65,"irregular trilinear dose");
        Assert(float.IsNaN(dose.Sample(new Vec3(0,0,16))),"outside dose");
        Assert(dose.Volume==null,"irregular dose cannot expose uniform volume");
        Assert(dose.Units=="GY","physical dose unit");
        var relative=DoseGrid.Load(new DicomEntry {Dataset=DoseDataset(new double[]{0,2,4},"RELATIVE")});
        Assert(relative.Units=="RELATIVE","relative unit retained"); Assert(relative.Volume!=null,"regular dose volume");
        var absolute=DoseGrid.Load(new DicomEntry {Dataset=DoseDataset(new double[]{10,12,15})}); Near(absolute.Sample(new Vec3(0,0,12)),2,"absolute axial offset");
        var descending=DoseGrid.Load(new DicomEntry {Dataset=DoseDataset(new double[]{0,-2,-5})}); Near(descending.Sample(new Vec3(0,0,6.5)),2.5,"descending irregular offset");
        bool rejected=false; try {DoseGrid.Load(new DicomEntry {Dataset=DoseDataset(new double[]{0,2,1})});} catch(ArgumentException){rejected=true;} Assert(rejected,"nonmonotonic offsets rejected");
    }
    static DicomDataset ContourItem(double x) => new DicomDataset().Add(DicomTag.ContourGeometricType,"CLOSED_PLANAR").Add(DicomTag.NumberOfContourPoints,4).Add(DicomTag.ContourData,x,0d,0d,x+2,0d,0d,x+2,2d,0d,x,2d,0d);
    static void StructureTests()
    {
        var d=new DicomDataset().Add(new DicomSequence(DicomTag.StructureSetROISequence,new DicomDataset().Add(DicomTag.ROINumber,1).Add(DicomTag.ROIName,"Synthetic").Add(DicomTag.ReferencedFrameOfReferenceUID,"1.1")))
            .Add(new DicomSequence(DicomTag.ROIContourSequence,new DicomDataset().Add(DicomTag.ReferencedROINumber,1).Add(DicomTag.ROIDisplayColor,255,0,128).Add(new DicomSequence(DicomTag.ContourSequence,ContourItem(0),ContourItem(100)))));
        var s=StructureSet.Load(new DicomEntry {Dataset=d}); Assert(s.Rois.Count==1,"ROI parsed"); var roi=s.Rois[0];
        Assert(roi.Contours.Count==2,"all contours retained"); Assert(roi.Red==255 && roi.Blue==128,"ROI color");
        Assert((roi.Center.X>=0 && roi.Center.X<=2) || (roi.Center.X>=100 && roi.Center.X<=102),"jump on ROI rather than between disjoint components");
        Assert(roi.Center.Y>=0 && roi.Center.Y<=2,"jump within contour"); Assert(roi.FrameUid=="1.1","ROI frame retained");
        Near(roi.Center.Y,1,"interior ROI navigation point when available");
    }
    static DicomDataset Device(string type,params double[] position)=>new DicomDataset().Add(DicomTag.RTBeamLimitingDeviceType,type).Add(DicomTag.LeafJawPositions,position);
    static void PlanTests()
    {
        var cp0=new DicomDataset().Add(DicomTag.ControlPointIndex,0).Add(DicomTag.GantryAngle,10d).Add(DicomTag.BeamLimitingDeviceAngle,20d).Add(DicomTag.PatientSupportAngle,30d).Add(DicomTag.IsocenterPosition,1d,2d,3d).Add(DicomTag.CumulativeMetersetWeight,0d)
            .Add(new DicomSequence(DicomTag.BeamLimitingDevicePositionSequence,Device("ASYMX",-50,50),Device("ASYMY",-40,40),Device("MLCX",-10,-20,10,20)));
        var cp1=new DicomDataset().Add(DicomTag.ControlPointIndex,1).Add(DicomTag.GantryAngle,15d).Add(DicomTag.CumulativeMetersetWeight,1d)
            .Add(new DicomSequence(DicomTag.BeamLimitingDevicePositionSequence,Device("MLCX",-15,-25,15,25)));
        var beam=new DicomDataset().Add(DicomTag.BeamNumber,1).Add(DicomTag.BeamName,"Synthetic").Add(DicomTag.TreatmentDeliveryType,"TREATMENT")
            .Add(new DicomSequence(DicomTag.BeamLimitingDeviceSequence,new DicomDataset().Add(DicomTag.RTBeamLimitingDeviceType,"MLCX").Add(DicomTag.NumberOfLeafJawPairs,2).Add(DicomTag.LeafPositionBoundaries,-20d,0d,20d)))
            .Add(new DicomSequence(DicomTag.ControlPointSequence,cp0,cp1));
        var d=new DicomDataset().Add(DicomTag.FrameOfReferenceUID,"1.1").Add(new DicomSequence(DicomTag.BeamSequence,beam))
            .Add(new DicomSequence(DicomTag.FractionGroupSequence,new DicomDataset().Add(new DicomSequence(DicomTag.ReferencedBeamSequence,new DicomDataset().Add(DicomTag.ReferencedBeamNumber,1).Add(DicomTag.BeamMeterset,200d)))));
        var plan=PlanData.Load(new DicomEntry {Dataset=d}); Assert(plan.Beams.Count==1,"beam parsed"); var b=plan.Beams[0]; Assert(b.ControlPoints.Count==2,"control points retained");Assert(b.TreatmentDeliveryType=="TREATMENT","beam delivery type retained for preview ordering");
        var c=b.ControlPoints[1]; Near(c.Gantry,15,"updated gantry"); Near(c.Collimator,20,"inherited collimator"); Near(c.Couch,30,"inherited couch"); Near(c.Isocenter.Z,3,"inherited isocenter");
        Near(c.XJaws[0],-50,"inherited X jaws"); Near(c.YJaws[1],40,"inherited Y jaws"); Near(c.MlcPositions[0],-15,"updated MLC"); Near(c.MlcBoundaries[2],20,"leaf boundaries"); Near(c.MetersetWeight,1,"control point cumulative weight"); Near(b.Meterset,200,"beam meterset");
        c.MlcPositions[0]=999; Near(b.ControlPoints[0].MlcPositions[0],-10,"control point arrays independent");
        cp1.AddOrUpdate(new DicomSequence(DicomTag.BeamLimitingDevicePositionSequence,Device("MLCX",-15,15)));
        bool invalid=false;try {PlanData.Load(new DicomEntry {Dataset=d});}catch(ArgumentException){invalid=true;}
        Assert(invalid,"explicit malformed MLC update cannot inherit old aperture");
        cp1.AddOrUpdate(new DicomSequence(DicomTag.BeamLimitingDevicePositionSequence,Device("ASYMX",-15,15,-20,20)));
        invalid=false;try {PlanData.Load(new DicomEntry {Dataset=d});}catch(ArgumentException){invalid=true;}
        Assert(invalid,"explicit malformed jaw update cannot inherit old aperture");
        cp1.Remove(DicomTag.BeamLimitingDevicePositionSequence);
        beam.AddOrUpdate(new DicomSequence(DicomTag.BeamLimitingDeviceSequence,
            new DicomDataset().Add(DicomTag.RTBeamLimitingDeviceType,"MLCX").Add(DicomTag.NumberOfLeafJawPairs,2).Add(DicomTag.LeafPositionBoundaries,-20d,0d,20d),
            new DicomDataset().Add(DicomTag.RTBeamLimitingDeviceType,"MLCY").Add(DicomTag.NumberOfLeafJawPairs,2).Add(DicomTag.LeafPositionBoundaries,-20d,0d,20d)));
        invalid=false;try {PlanData.Load(new DicomEntry {Dataset=d});}catch(ArgumentException){invalid=true;}
        Assert(invalid,"missing initial layer rejected");
    }
    static void PatientOrientationTests()
    {
        var beam=new DicomDataset().Add(DicomTag.BeamNumber,1).Add(DicomTag.ReferencedPatientSetupNumber,2);
        var setup1=new DicomDataset().Add(DicomTag.PatientSetupNumber,1).Add(DicomTag.PatientPosition,"HFP");
        var setup2=new DicomDataset().Add(DicomTag.PatientSetupNumber,2).Add(DicomTag.PatientPosition,"FFS");
        var data=new DicomDataset().Add(new DicomSequence(DicomTag.BeamSequence,beam)).Add(new DicomSequence(DicomTag.PatientSetupSequence,setup1,setup2));
        Func<string> position=()=>PlanData.Load(new DicomEntry{Dataset=data}).Beams[0].PatientPosition;
        Assert(position()=="FFS","beam selects referenced patient setup rather than first setup");
        beam.Remove(DicomTag.ReferencedPatientSetupNumber);Assert(position()=="","unreferenced patient setup is not guessed");
        beam.Add(DicomTag.ReferencedPatientSetupNumber,99);Assert(position()=="","missing referenced patient setup stays unknown");
        beam.AddOrUpdate(DicomTag.ReferencedPatientSetupNumber,2);
        data.AddOrUpdate(new DicomSequence(DicomTag.PatientSetupSequence,setup2,setup2));Assert(position()=="","duplicate setup number is ambiguous");
        Assert(PatientOrientation.ToIec("")==null&&PatientOrientation.ToIec("SITTING")==null,"unsupported setup never falls back to HFS");
        foreach(var code in new[]{"HFS","HFP","FFS","FFP","HFDR","HFDL","FFDR","FFDL"})
        {
            var m=PatientOrientation.ToIec(code);var l=m.Transform(new Vec3(1,0,0));var p=m.Transform(new Vec3(0,1,0));var h=m.Transform(new Vec3(0,0,1));
            Near(l.X*l.X+l.Y*l.Y+l.Z*l.Z,1,"unit patient left axis "+code);
            Near(p.X*p.X+p.Y*p.Y+p.Z*p.Z,1,"unit patient posterior axis "+code);
            Near(l.Y*p.Z-l.Z*p.Y,h.X,"right handed x "+code);Near(l.Z*p.X-l.X*p.Z,h.Y,"right handed y "+code);Near(l.X*p.Y-l.Y*p.X,h.Z,"right handed z "+code);
            Near(h.Y,code.StartsWith("HF")?1:-1,"head versus feet first "+code);
            if(code.EndsWith("DR"))Near(l.Z,1,"right side down "+code);
            if(code.EndsWith("DL"))Near(l.Z,-1,"left side down "+code);
            if(code.EndsWith("S"))Near(p.Z,-1,"supine back down "+code);
            if(code.EndsWith("P"))Near(p.Z,1,"prone back up "+code);
        }
        string cue;Near(PatientOrientation.SchematicAnchor("BRAIN",false,out cue),.55,"head metadata anchor");
        Near(PatientOrientation.SchematicAnchor("CHEST",true,out cue),0,"explicit chest metadata takes priority over noncoplanar heuristic");
        Near(PatientOrientation.SchematicAnchor(null,true,out cue),.55,"noncoplanar schematic head anchor");Assert(cue.Contains("assumed"),"fallback cue explicitly marked assumed");
    }
    static DicomDataset Definition(string type,params double[] bounds)=>new DicomDataset().Add(DicomTag.RTBeamLimitingDeviceType,type).Add(DicomTag.NumberOfLeafJawPairs,bounds.Length-1).Add(DicomTag.LeafPositionBoundaries,bounds);
    static PlanData LayerPlan(DicomDataset[] definitions,params DicomDataset[] points)=>PlanData.Load(new DicomEntry {Dataset=new DicomDataset().Add(new DicomSequence(DicomTag.BeamSequence,new DicomDataset().Add(DicomTag.BeamNumber,1).Add(new DicomSequence(DicomTag.BeamLimitingDeviceSequence,definitions)).Add(new DicomSequence(DicomTag.ControlPointSequence,points))))});
    static DicomDataset Aperture(params DicomDataset[] devices)=>new DicomDataset().Add(new DicomSequence(DicomTag.BeamLimitingDevicePositionSequence,devices));
    static void LayerTests()
    {
        var definitions=new[]{Definition("MLCX1",-20,0,20),Definition("MLCX2",-30,-10,10,30)};
        var p=LayerPlan(definitions,Aperture(Device("MLCX1",-5,-6,5,6),Device("MLCX2",-7,-8,-9,7,8,9)),Aperture(Device("MLCX2",-10,-11,-12,10,11,12)),new DicomDataset());
        var cp=p.Beams[0].ControlPoints;
        Assert(cp[0].MlcLayers.Count==2,"vendor double layer retained");Near(cp[1].MlcLayers[0].Positions[0],-5,"first layer independently inherited");Near(cp[1].MlcLayers[1].Positions[0],-10,"second layer updated");
        Near(cp[2].MlcLayers[1].Positions[1],-11,"whole sequence omission inherits layers");cp[2].MlcLayers[1].Positions[1]=100;Near(cp[1].MlcLayers[1].Positions[1],-11,"layer arrays independent");
        var duplicate=new[]{Definition("MLCX",-20,0,20),Definition("MLCX",-30,-10,10,30)};
        p=LayerPlan(duplicate,Aperture(Device("MLCX",-5,-6,5,6),Device("MLCX",-7,-8,-9,7,8,9)),Aperture(Device("MLCX",-10,-11,-12,10,11,12)));
        Assert(p.Beams[0].ControlPoints[0].MlcLayers.Select(l=>l.Key).Distinct().Count()==2,"duplicate types preserve occurrence identity");Near(p.Beams[0].ControlPoints[1].MlcLayers[0].Positions[0],-5,"unique pair count partial update inherits other duplicate layer");
        p=LayerPlan(new[]{Definition("MLCY",-20,0,20),Definition("MLCY",-20,0,20)},Aperture(Device("MLCY",-5,-6,5,6),Device("MLCY",-7,-8,7,8)),Aperture(Device("MLCY",-1,-2,1,2),Device("MLCY",-3,-4,3,4)));
        Near(p.Beams[0].ControlPoints[1].MlcLayers[0].Positions[0],-1,"complete duplicate group first occurrence");Near(p.Beams[0].ControlPoints[1].MlcLayers[1].Positions[0],-3,"complete duplicate group second occurrence");Assert(p.Beams[0].ControlPoints[1].MlcLayers.All(l=>l.IsY),"Y layer orientation preserved");
        bool rejected=false;try{LayerPlan(new[]{Definition("MLCX",-20,0,20),Definition("MLCX",-20,0,20)},Aperture(Device("MLCX",-5,-6,5,6),Device("MLCX",-7,-8,7,8)),Aperture(Device("MLCX",-1,-2,1,2)));}catch(ArgumentException){rejected=true;}Assert(rejected,"ambiguous duplicate partial update rejected");
        rejected=false;try{LayerPlan(definitions,Aperture(Device("MLCX1",-5,-6,5,6),Device("MLCX2",-7,-8,-9,7,8,9)),Aperture(Device("MLCX2",-1,1)));}catch(ArgumentException){rejected=true;}Assert(rejected,"malformed second layer rejected");
    }
    static void PrivateMlcAcceptance(string folder)
    {
        int plans=0,beams=0,cps=0,dual=0;
        foreach(var path in Directory.EnumerateFiles(folder))
        {
            DicomFile file;try {file=DicomFile.Open(path,FileReadOption.ReadLargeOnDemand);}catch {continue;}
            if(file.Dataset.GetSingleValueOrDefault(DicomTag.Modality,"")!="RTPLAN")continue;
            // Dataset retained only in memory; never print paths, names, UIDs or arbitrary metadata.
            var plan=PlanData.Load(new DicomEntry{Dataset=file.Dataset});plans++;
            foreach(var beam in plan.Beams){beams++;foreach(var cp in beam.ControlPoints){cps++;if(cp.MlcLayers.Count==2)dual++;foreach(var layer in cp.MlcLayers)Assert(layer.Positions.Length==2*(layer.Boundaries.Length-1),"private layer geometry valid");}}
        }
        Assert(plans>0 && dual>0,"private double-layer data exercised");
        Console.WriteLine("PASS private MLC only: plans="+plans+" beams="+beams+" control_points="+cps+" dual_layer_control_points="+dual);
    }
    static void PrivateAcceptance(string folder)
    {
        string first=Directory.EnumerateFiles(folder).First(); var cat=DicomCatalog.Scan(first,CancellationToken.None);
        var links=RegistrationReader.Read(cat);
        var ct=cat.Files.Where(e=>e.Modality=="CT").Select(e=>e.FrameUid).Where(s=>!string.IsNullOrEmpty(s)).Distinct().ToArray();
        var mr=cat.Files.Where(e=>e.Modality=="MR").Select(e=>e.FrameUid).Where(s=>!string.IsNullOrEmpty(s)).Distinct().ToArray();
        int matched=0; foreach(var a in ct)foreach(var b in mr)if(RegistrationReader.Resolve(links,a,b)!=null)matched++;
        int rois=0,contours=0,doses=0,beams=0,cps=0,mlc=0;
        foreach(var entry in cat.Files)
        {
            if(entry.Modality=="RTSTRUCT") {var s=StructureSet.Load(entry);rois+=s.Rois.Count;contours+=s.Rois.Sum(r=>r.Contours.Count);}
            if(entry.Modality=="RTDOSE") {var d=DoseGrid.Load(entry);Assert(d.Maximum>0,"positive private dose maximum");doses++;}
            if(entry.Modality=="RTPLAN") {var p=PlanData.Load(entry);beams+=p.Beams.Count;cps+=p.Beams.Sum(b=>b.ControlPoints.Count);mlc+=p.Beams.Sum(b=>b.ControlPoints.Count(c=>c.MlcPositions.Length>0));}
        }
        Console.WriteLine("PASS private read-only counts: files="+cat.Files.Count+" REG="+cat.Files.Count(e=>e.Modality=="REG")+" links="+links.Count+" CT_MR_frame_pairs="+matched+" rois="+rois+" contours="+contours+" doses="+doses+" beams="+beams+" control_points="+cps+" MLC_control_points="+mlc);
    }
    public static int Main(string[] args)
    {
        if(args.Length==2 && args[0]=="--private-beam"){try{BeamProjectionScenarios.Private(args[1]);return 0;}catch(Exception ex){Console.WriteLine("FAIL private beam: "+ex.GetType().Name);return 1;}}
        if(args.Length==2 && args[0]=="--private-mlc") {try {PrivateMlcAcceptance(args[1]);return 0;}catch(Exception ex){Console.WriteLine("FAIL private MLC: "+ex.GetType().Name);return 1;}}
        if(args.Length==2 && args[0]=="--private") {try {PrivateAcceptance(args[1]);return 0;}catch(Exception ex){Console.WriteLine("FAIL private acceptance: "+ex.GetType().Name);return 1;}}
        try { BeamProjectionScenarios.Run();MatrixTests(); RegistrationTests(); DoseTests(); StructureTests(); PlanTests(); PatientOrientationTests(); LayerTests(); Console.WriteLine("PASS RT assertions: " + checks); return 0; }
        catch(Exception ex) { Console.WriteLine("FAIL RT assertion: " + ex.Message); return 1; }
    }
}
