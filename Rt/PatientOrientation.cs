using System;
namespace QuickLook.DicomRT
{
    public static class PatientOrientation
    {
        // Nominal DICOM patient LPS to IEC FIXED at couch zero: +Y towards gantry, +Z up.
        // This only orients a schematic; it does not register anatomy or encode pitch/roll.
        public static Matrix4 ToIec(string position)
        {
            Vec3 left,posterior,superior;
            switch((position??"").Trim().ToUpperInvariant())
            {
                case "HFS":left=new Vec3(1,0,0);posterior=new Vec3(0,0,-1);superior=new Vec3(0,1,0);break;
                case "HFP":left=new Vec3(-1,0,0);posterior=new Vec3(0,0,1);superior=new Vec3(0,1,0);break;
                case "FFS":left=new Vec3(-1,0,0);posterior=new Vec3(0,0,-1);superior=new Vec3(0,-1,0);break;
                case "FFP":left=new Vec3(1,0,0);posterior=new Vec3(0,0,1);superior=new Vec3(0,-1,0);break;
                case "HFDR":left=new Vec3(0,0,1);posterior=new Vec3(1,0,0);superior=new Vec3(0,1,0);break;
                case "HFDL":left=new Vec3(0,0,-1);posterior=new Vec3(-1,0,0);superior=new Vec3(0,1,0);break;
                case "FFDR":left=new Vec3(0,0,1);posterior=new Vec3(-1,0,0);superior=new Vec3(0,-1,0);break;
                case "FFDL":left=new Vec3(0,0,-1);posterior=new Vec3(1,0,0);superior=new Vec3(0,-1,0);break;
                default:return null;
            }
            return new Matrix4(new[]{left.X,posterior.X,superior.X,0,left.Y,posterior.Y,superior.Y,0,left.Z,posterior.Z,superior.Z,0,0,0,0,1d});
        }
        public static double SchematicAnchor(string region,bool noncoplanar,out string description)
        {
            switch((region??"").Trim().ToUpperInvariant())
            {
                case "HEAD":case "BRAIN":case "SKULL":case "HEADNECK":description="head cue from metadata";return .55;
                case "NECK":description="neck cue from metadata";return .35;
                case "CHEST":case "THORAX":case "BREAST":case "LUNG":description="chest cue from metadata";return 0;
                case "ABDOMEN":description="abdomen cue from metadata";return -.15;
                case "PELVIS":description="pelvis cue from metadata";return -.30;
                default:description=noncoplanar?"head cue assumed · noncoplanar":"chest cue assumed";return noncoplanar?.55:0;
            }
        }
    }
}
