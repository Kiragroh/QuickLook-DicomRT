using System;
using System.Linq;
using System.Threading;
using System.IO;
using System.Diagnostics;
using Dicom;
using Dicom.Imaging;
using Dicom.IO.Buffer;
using QuickLook.DicomRT;

internal static class Program
{
    static int checks;
    static void Assert(bool condition,string name){checks++;if(!condition)throw new Exception(name);}
    static void Near(double actual,double expected,string name)=>Assert(Math.Abs(actual-expected)<1e-5,name+": "+actual);
    static Contour Rectangle(double x,double y,double width,double height,double z,string type="CLOSED_PLANAR")=>new Contour {GeometricType=type,Points=new System.Collections.Generic.List<Vec3>{new Vec3(x,y,z),new Vec3(x+width,y,z),new Vec3(x+width,y+height,z),new Vec3(x,y+height,z)}};
    static StructureRoi Roi(double x=0,double width=10,string type="CLOSED_PLANAR")=>new StructureRoi {Name="Synthetic",Contours=new System.Collections.Generic.List<Contour>{Rectangle(x,0,width,10,2,type),Rectangle(x,0,width,10,4,type)}};
    static DoseGrid Dose(bool gradient=false,string units="GY")
    {
        var d=new DicomDataset().Add(DicomTag.SOPClassUID,DicomUID.RTDoseStorage).Add(DicomTag.SOPInstanceUID,"1.2.3")
            .Add(DicomTag.FrameOfReferenceUID,"1.1").Add(DicomTag.Rows,(ushort)21).Add(DicomTag.Columns,(ushort)21)
            .Add(DicomTag.BitsAllocated,(ushort)16).Add(DicomTag.BitsStored,(ushort)16).Add(DicomTag.HighBit,(ushort)15)
            .Add(DicomTag.PixelRepresentation,(ushort)0).Add(DicomTag.SamplesPerPixel,(ushort)1).Add(DicomTag.PhotometricInterpretation,"MONOCHROME2")
            .Add(DicomTag.ImagePositionPatient,0d,0d,0d).Add(DicomTag.ImageOrientationPatient,1d,0d,0d,0d,1d,0d)
            .Add(DicomTag.PixelSpacing,1d,1d).Add(DicomTag.GridFrameOffsetVector,0d,10d).Add(DicomTag.DoseGridScaling,.01d).Add(DicomTag.DoseUnits,units);
        var px=DicomPixelData.Create(d,true);
        for(int z=0;z<2;z++)
        {var bytes=new byte[21*21*2];for(int i=0;i<441;i++){var raw=BitConverter.GetBytes((ushort)(gradient?(i%21)*100:200));Buffer.BlockCopy(raw,0,bytes,i*2,2);}px.AddFrame(new MemoryByteBuffer(bytes));}
        return DoseGrid.Load(new DicomEntry {Dataset=d});
    }
    static DvhResult Calc(StructureRoi r,DoseGrid d,Matrix4 m=null)=>DvhCalculator.Calculate(r,d,m??Matrix4.Identity,CancellationToken.None);
    static void Main(string[] args)
    {
        if(args.Length==2 && args[0]=="--private"){PrivateSmoke(args[1]);return;}
        SumTests();
        var dose=Dose();var result=Calc(Roi(),dose);
        Assert(result.Status==DvhStatus.Complete,"constant full coverage");Near(result.EstimatedVolumeCc,.4,"half-spacing slab volume");Near(result.SampledVolumeCc,.4,"covered cc");Near(result.CoverageFraction,1,"coverage");
        Near(result.CumulativeVolumePercent[0],100,"V0");Near(result.CumulativeVolumePercent[256],100,"inclusive threshold at constant max");Near(result.CumulativeVolumePercent[257],0,"tail zero");
        Assert(result.CumulativeVolumePercent.Zip(result.CumulativeVolumePercent.Skip(1),(a,b)=>a>=b).All(x=>x),"monotone cumulative");
        var holes=Roi(type:"CLOSEDPLANAR_XOR");holes.Contours.Add(Rectangle(3,3,4,4,2,"CLOSEDPLANAR_XOR"));holes.Contours.Add(Rectangle(3,3,4,4,4,"CLOSEDPLANAR_XOR"));
        Near(Calc(holes,dose).EstimatedVolumeCc,.336,"XOR excludes hole");
        holes.Contours.Add(Rectangle(4,4,2,2,2,"CLOSEDPLANAR_XOR"));holes.Contours.Add(Rectangle(4,4,2,2,4,"CLOSEDPLANAR_XOR"));
        Near(Calc(holes,dose).EstimatedVolumeCc,.352,"XOR island inside hole");
        var rotated=Roi();double angle=.4;foreach(var c in rotated.Contours)for(int i=0;i<c.Points.Count;i++){var p=c.Points[i];c.Points[i]=new Vec3(5+p.X*Math.Cos(angle)-p.Y*Math.Sin(angle),p.X*Math.Sin(angle)+p.Y*Math.Cos(angle),p.Z);}
        Near(Calc(rotated,dose).EstimatedVolumeCc,.4,"oblique in-plane contour basis");
        var irregular=Roi();irregular.Contours.Add(Rectangle(0,0,10,10,8));Near(Calc(irregular,dose).EstimatedVolumeCc,.9,"irregular contour spacing weighted volume");
        var union=Roi();union.Contours.Add(Rectangle(5,0,10,10,2));union.Contours.Add(Rectangle(5,0,10,10,4));Near(Calc(union,dose).EstimatedVolumeCc,.6,"CLOSED_PLANAR union overlap");
        var keyhole=Roi();foreach(var c in keyhole.Contours){double z=c.Points[0].Z;c.Points.Add(c.Points[0]);c.Points.AddRange(new[]{new Vec3(3,3,z),new Vec3(3,7,z),new Vec3(7,7,z),new Vec3(7,3,z),new Vec3(3,3,z),c.Points[0]});}
        Near(Calc(keyhole,dose).EstimatedVolumeCc,.336,"keyhole excludes inner ring");
        var partial=Calc(Roi(15),dose);Assert(partial.Status==DvhStatus.PartialCoverage,"partial explicit");Near(partial.CoverageFraction,.5,"partial coverage fraction");Near(partial.CumulativeVolumePercent[0],50,"uncovered not normalized away or counted as zero");
        var outside=Calc(Roi(30),dose);Assert(outside.DoseValues.Length==0 && outside.Status==DvhStatus.PartialCoverage,"no invented dose outside");
        var mapped=Calc(Roi(30),dose,new Matrix4(new double[]{1,0,0,-30,0,1,0,0,0,0,1,0,0,0,0,1}));Near(mapped.CoverageFraction,1,"roi to dose transform");
        Assert(Calc(Roi(),dose,new Matrix4(new double[]{2,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1})).Status==DvhStatus.Unsupported,"nonrigid rejected");
        var one=Roi();one.Contours.RemoveAt(1);Assert(Calc(one,dose).Status==DvhStatus.Unsupported,"single plane explicit");
        var invalid=Roi();invalid.Contours[1].GeometricType="CLOSEDPLANAR_XOR";Assert(Calc(invalid,dose).Status==DvhStatus.Unsupported,"mixed types rejected");
        invalid=Roi();invalid.Contours[1].Points[0]=new Vec3(0,0,5);Assert(Calc(invalid,dose).Status==DvhStatus.Unsupported,"nonparallel rejected");
        Assert(Calc(Roi(),Dose(units:"RELATIVE")).DoseUnits=="RELATIVE","relative units retained");
        var gradient=Calc(Roi(),Dose(true));int threshold=Array.FindIndex(gradient.DoseValues,x=>x>=5);Near(gradient.CumulativeVolumePercent[threshold],50,"linear gradient V5");
        var cancelled=new CancellationTokenSource();cancelled.Cancel();bool threw=false;try{DvhCalculator.Calculate(Roi(),dose,Matrix4.Identity,cancelled.Token);}catch(OperationCanceledException){threw=true;}Assert(threw,"cancellation observed");
        var large=Calc(Roi(0,200000),dose);Assert(large.SamplingStepMm>1,"adaptive bounded grid");
        Console.WriteLine("DVH checks passed: "+checks);
    }
    static void SumTests()
    {
        DoseGrid Input(string plan,string sop){var d=Dose();d.PlanUid=plan;d.Entry.SopUid=sop;d.DoseType="PHYSICAL";d.SummationType="PLAN";d.ReferencedPlanCount=1;return d;}
        DoseSumResult Sum(params DoseGrid[] inputs)=>DoseSum.Calculate(inputs,new RegistrationLink[0],CancellationToken.None);
        var a=Input("1.2.1","1.3.1");var b=Input("1.2.2","1.3.2");var sum=Sum(a,b);
        Assert(sum.Dose!=null&&sum.IncludedCount==2&&sum.ExcludedCount==0,"two physical plans included");
        Near(sum.Dose.Sample(new Vec3(10,10,5)),4,"constant voxelwise sum");Near(sum.CoverageFraction,1,"sum full coverage");
        Assert(sum.Dose.Units=="GY"&&sum.Dose.DoseType=="PHYSICAL"&&sum.Dose.SummationType=="MULTI_PLAN","derived dose metadata");
        Assert(string.IsNullOrEmpty(sum.Dose.Entry.SopUid)&&string.IsNullOrEmpty(sum.Dose.Entry.PatientKey),"derived dose no invented identity");
        Assert(sum.Dose.Volume.Width==a.Volume.Width&&sum.Dose.Volume.SpacingZ==a.Volume.SpacingZ,"reference resolution preserved");
        var duplicate=Sum(a,b,a);Assert(duplicate.IncludedCount==2&&duplicate.ExcludedCount==1,"duplicate SOP counted once");Near(duplicate.Dose.Maximum,4,"duplicate not added twice");
        var alternative=Input("1.2.1","1.3.3");var ambiguous=Sum(a,b,alternative);Assert(ambiguous.AmbiguousPlanCount==1&&ambiguous.IncludedCount==1&&ambiguous.ExcludedCount==2,"ambiguous plan excluded entirely");
        var beam=Input("1.2.3","1.3.4");beam.SummationType="BEAM";var relative=Input("1.2.4","1.3.5");relative.Units="RELATIVE";
        var filtered=Sum(a,b,beam,relative,sum.Dose);Assert(filtered.IncludedCount==2&&filtered.ExcludedCount==3,"beam relative and existing sum excluded");
        var biological=Input("1.2.5","1.3.6");biological.DoseType="EFFECTIVE";Assert(Sum(a,biological).ExcludedCount==1,"biological dose excluded");
        var manyRefs=Input("1.2.6","1.3.7");manyRefs.ReferencedPlanCount=2;Assert(Sum(a,manyRefs).ExcludedCount==1,"multiple plan references excluded");
        a.Entry.PatientKey="Synthetic A";b.Entry.PatientKey="Synthetic B";Assert(Sum(a,b).ExcludedCount==1,"known patient mismatch excluded");a.Entry.PatientKey=b.Entry.PatientKey=null;
        b.FrameUid="1.9";Assert(Sum(a,b).ExcludedCount==1,"unregistered frame excluded");
        var mapping=new RegistrationLink {SourceFrame=a.FrameUid,TargetFrame=b.FrameUid,SourceToTarget=new Matrix4(new double[]{1,0,0,5,0,1,0,0,0,0,1,0,0,0,0,1})};
        var shifted=DoseSum.Calculate(new[]{a,b},new[]{mapping},CancellationToken.None);Near(shifted.CoverageFraction,16d/21,"registered overlap coverage");
        Near(shifted.Dose.Sample(new Vec3(15,10,5)),4,"valid node beside NaN remains available");Assert(float.IsNaN(shifted.Dose.Sample(new Vec3(15.5,10,5))),"unknown interpolation remains NaN");Assert(float.IsNaN(shifted.Dose.Sample(new Vec3(20,10,5))),"missing coverage never zero");
        Near(shifted.Dose.Sample(new Vec3(15+1e-12,10,5)),4,"coordinate roundoff near valid node tolerated");
        mapping.SourceToTarget=new Matrix4(new double[]{2,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1});Assert(DoseSum.Calculate(new[]{a,b},new[]{mapping},CancellationToken.None).ExcludedCount==1,"nonrigid sum excluded");
        var cancelled=new CancellationTokenSource();cancelled.Cancel();bool threw=false;try{DoseSum.Calculate(new[]{a,b},new RegistrationLink[0],cancelled.Token);}catch(OperationCanceledException){threw=true;}Assert(threw,"sum cancellation");
    }
    static void PrivateSmoke(string folder)
    {
        // Deliberately aggregate-only: no patient fields, names, identifiers, paths or exception text.
        try
        {
            using(var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(90)))
            {
                var watch=Stopwatch.StartNew();var first=Directory.EnumerateFiles(folder,"*.dcm").First();
                var catalog=DicomCatalog.Scan(first,timeout.Token);
                var sets=catalog.Files.Where(e=>e.Modality=="RTSTRUCT").Select(StructureSet.Load).ToArray();
                var doseEntries=catalog.Files.Where(e=>e.Modality=="RTDOSE").ToArray();
                Console.WriteLine("Private aggregate: structure sets="+sets.Length+", doses="+doseEntries.Length+", rois="+sets.Sum(s=>s.Rois.Count));
                if(doseEntries.Length!=1){Console.WriteLine("Private smoke requires exactly one dose to avoid ambiguous selection.");Environment.ExitCode=2;return;}
                var dose=DoseGrid.Load(doseEntries[0]);var links=RegistrationReader.Read(catalog);
                var results=sets.SelectMany(s=>s.Rois).Select(r=>DvhCalculator.Calculate(r,dose,RegistrationReader.Resolve(links,r.FrameUid,dose.FrameUid),timeout.Token)).ToArray();
                foreach(DvhStatus state in Enum.GetValues(typeof(DvhStatus)))Console.WriteLine(state+"="+results.Count(r=>r.Status==state));
                var computed=results.Where(r=>r.EstimatedVolumeCc>0).ToArray();
                Console.WriteLine("Curves="+results.Count(r=>r.DoseValues.Length>0)+", elapsed_ms="+watch.ElapsedMilliseconds+", units="+dose.Units);
                if(computed.Length>0)Console.WriteLine("Coverage_min="+computed.Min(r=>r.CoverageFraction).ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+", coverage_max="+computed.Max(r=>r.CoverageFraction).ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+", largest_sampling_mm="+computed.Max(r=>r.SamplingStepMm).ToString("F2",System.Globalization.CultureInfo.InvariantCulture));
            }
        }
        catch(OperationCanceledException){Console.WriteLine("Private smoke exceeded its 90 second aggregate budget.");Environment.ExitCode=2;}
        catch(Exception){Console.WriteLine("Private smoke failed; details intentionally suppressed.");Environment.ExitCode=1;}
    }
}
