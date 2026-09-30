using Dicom;
namespace QuickLook.DicomRT
{
    public static class DicomRtDictionary
    {
        // fo-dicom 4 predates the RTPLAN Enhanced device sequence tags.
        // Register official public definitions before reading Implicit VR input.
        static DicomRtDictionary()
        {
            Add(0x3008,0x00a1,"Enhanced RT Beam Limiting Device Sequence","EnhancedRTBeamLimitingDeviceSequence",DicomVR.SQ,DicomVM.VM_1);
            Add(0x3008,0x00a2,"Enhanced RT Beam Limiting Opening Sequence","EnhancedRTBeamLimitingOpeningSequence",DicomVR.SQ,DicomVM.VM_1);
            Add(0x3008,0x00a3,"Enhanced RT Beam Limiting Device Definition Flag","EnhancedRTBeamLimitingDeviceDefinitionFlag",DicomVR.CS,DicomVM.VM_1);
            Add(0x3010,0x0039,"Device Index","DeviceIndex",DicomVR.US,DicomVM.VM_1);
            Add(0x3010,0x002e,"Device Type Code Sequence","DeviceTypeCodeSequence",DicomVR.SQ,DicomVM.VM_1);
            Add(0x300a,0x0607,"Referenced Device Index","ReferencedDeviceIndex",DicomVR.US,DicomVM.VM_1);
            Add(0x300a,0x0645,"Beam Modifier Orientation Angle","BeamModifierOrientationAngle",DicomVR.FD,DicomVM.VM_1);
            Add(0x300a,0x0647,"Parallel RT Beam Delimiter Device Sequence","ParallelRTBeamDelimiterDeviceSequence",DicomVR.SQ,DicomVM.VM_1);
            Add(0x300a,0x0648,"Number of Parallel RT Beam Delimiters","NumberOfParallelRTBeamDelimiters",DicomVR.US,DicomVM.VM_1);
            Add(0x300a,0x0649,"Parallel RT Beam Delimiter Boundaries","ParallelRTBeamDelimiterBoundaries",DicomVR.FD,DicomVM.VM_2_n);
            Add(0x300a,0x064a,"Parallel RT Beam Delimiter Positions","ParallelRTBeamDelimiterPositions",DicomVR.FD,DicomVM.VM_2_n);
            Add(0x300a,0x064b,"RT Beam Limiting Device Offset","RTBeamLimitingDeviceOffset",DicomVR.FD,DicomVM.VM_2);
            Add(0x300a,0x064e,"Parallel RT Beam Delimiter Opening Mode","ParallelRTBeamDelimiterOpeningMode",DicomVR.CS,DicomVM.VM_1);
        }
        static void Add(ushort group,ushort element,string name,string keyword,DicomVR vr,DicomVM vm)
        {
            var tag=new DicomTag(group,element);
            if(DicomDictionary.Default[tag].ValueRepresentations[0]==DicomVR.UN)
                DicomDictionary.Default.Add(new DicomDictionaryEntry(tag,name,keyword,vm,false,vr));
        }
        public static void EnsureLoaded() { }
    }
}
