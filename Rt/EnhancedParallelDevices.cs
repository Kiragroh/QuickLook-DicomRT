using System;
using System.Collections.Generic;
using System.Linq;
using Dicom;
namespace QuickLook.DicomRT
{
    // RTPLAN Enhanced devices, not second-generation RT Radiation IODs.
    // C.8.8.14.17: these coordinates are already projected to SAD, as in classic BLD.
    internal static class EnhancedParallelDevices
    {
        static DicomTag A(ushort element)=>new DicomTag(0x300a,element);
        public static List<MlcLayer> Definitions(DicomDataset beam)
        {
            if(RtDicom.Items(beam,DicomTag.BeamLimitingDeviceSequence).Any())throw new NotSupportedException("Mixed classic and Enhanced device definitions.");
            var result=new List<MlcLayer>();
            foreach(var device in RtDicom.Items(beam,new DicomTag(0x3008,0x00a1)))
            {
                int index=RtDicom.Int(device,new DicomTag(0x3010,0x0039),-1);
                if(index<1||result.Any(l=>l.Key=="Enhanced#"+index))throw new ArgumentException("Invalid or duplicate Enhanced device index.");
                var codes=RtDicom.Items(device,new DicomTag(0x3010,0x002e)).ToArray();
                if(codes.Length!=1||RtDicom.Text(codes[0],DicomTag.CodingSchemeDesignator)!="DCM"||RtDicom.Text(codes[0],DicomTag.CodeValue)!="130331")
                    throw new NotSupportedException("Enhanced device is not a supported paired-leaf MLC.");
                double angle=RtDicom.Number(device,A(0x0645));
                if(!RtDicom.Finite(angle)||(Math.Abs(angle)>1e-6&&Math.Abs(angle-90)>1e-6))throw new NotSupportedException("Enhanced MLC orientation must be IEC X or Y.");
                var parallel=RtDicom.Items(device,A(0x0647)).ToArray();
                if(parallel.Length!=1||RtDicom.Text(parallel[0],A(0x064e))!="VARIABLE")throw new NotSupportedException("Enhanced MLC requires variable paired leaves.");
                int pairs=RtDicom.Int(parallel[0],A(0x0648));var boundaries=RtDicom.Numbers(parallel[0],A(0x0649));
                if(pairs<1||boundaries.Length!=pairs+1||!boundaries.All(RtDicom.Finite)||!Enumerable.Range(1,pairs).All(i=>boundaries[i]>boundaries[i-1]))throw new ArgumentException("Invalid Enhanced leaf boundaries.");
                result.Add(new MlcLayer{Key="Enhanced#"+index,Type=angle<45?"MLCX":"MLCY",Boundaries=boundaries});
            }
            if(result.Count==0)throw new ArgumentException("Missing Enhanced device definitions.");
            return result;
        }
        public static void Apply(DicomDataset cp,List<MlcLayer> layers,bool first)
        {
            if(RtDicom.Items(cp,DicomTag.BeamLimitingDevicePositionSequence).Any())throw new NotSupportedException("Mixed classic and Enhanced openings.");
            var seen=new HashSet<string>();
            foreach(var opening in RtDicom.Items(cp,new DicomTag(0x3008,0x00a2)))
            {
                string key="Enhanced#"+RtDicom.Int(opening,A(0x0607),-1);var layer=layers.SingleOrDefault(l=>l.Key==key);
                if(layer==null||!seen.Add(key))throw new ArgumentException("Unknown or duplicate Enhanced device reference.");
                // Offsets need explicit per-device transforms across all aperture consumers.
                // Reject rather than silently place shifted devices at the origin.
                var offset=RtDicom.Numbers(opening,A(0x064b));
                if((offset.Length!=0&&offset.Length!=2)||offset.Any(x=>!RtDicom.Finite(x)||Math.Abs(x)>1e-8))throw new NotSupportedException("Nonzero Enhanced device offsets are not supported.");
                var positions=RtDicom.Numbers(opening,A(0x064a));
                if(positions.Length==0&&!first)continue;
                if(positions.Length!=2*(layer.Boundaries.Length-1)||!positions.All(RtDicom.Finite))throw new ArgumentException("Invalid Enhanced leaf positions.");
                layer.Positions=positions;
            }
            // Omitted devices inherit by stable DeviceIndex; initial completeness is checked by PlanData.
        }
    }
}
