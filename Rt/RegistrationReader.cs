using System;
using System.Collections.Generic;
using System.Linq;
using Dicom;
namespace QuickLook.DicomRT
{
    public sealed class RegistrationLink
    {
        public string SourceFrame, TargetFrame, Label;
        public Matrix4 SourceToTarget;
        public HashSet<string> SourceSopUids = new HashSet<string>();
        public HashSet<string> TargetSopUids = new HashSet<string>();
    }
    public static class RegistrationReader
    {
        public static List<RegistrationLink> Read(DicomCatalog catalog)
        {
            var result=new List<RegistrationLink>();
            // Each REG defines its own registered RCS. Never join RCS nodes across REG objects.
            foreach(var entry in catalog.Files.Where(e=>e.Modality=="REG"))
            {
                var d=entry.Dataset;
                if(d==null || d.Contains(new DicomTag(0x0064,0x0002)) || RtDicom.Text(d,DicomTag.SOPClassUID)=="1.2.840.10008.5.1.4.1.1.66.3") continue;
                try
                {
                    var local=new List<RegistrationLink>();
                    string registeredFrame=RtDicom.Text(d,DicomTag.FrameOfReferenceUID);
                    if(!string.IsNullOrWhiteSpace(registeredFrame)) local.Add(new RegistrationLink {SourceFrame=registeredFrame,SourceToTarget=Matrix4.Identity});
                    foreach(var item in RtDicom.Items(d,DicomTag.RegistrationSequence))
                    {
                        string frame=RtDicom.Text(item,DicomTag.FrameOfReferenceUID);
                        var sops=new HashSet<string>(RtDicom.Items(item,DicomTag.ReferencedImageSequence).Select(i=>RtDicom.Text(i,DicomTag.ReferencedSOPInstanceUID)).Where(s=>s.Length>0));
                        var matchedFrames=catalog.Files.Where(e=>sops.Contains(e.SopUid??"") && !string.IsNullOrWhiteSpace(e.FrameUid)).Select(e=>e.FrameUid).Distinct().ToArray();
                        if(matchedFrames.Length>1 || (matchedFrames.Length==1 && frame.Length>0 && matchedFrames[0]!=frame)) throw new ArgumentException("Inconsistent registration references.");
                        if(frame.Length==0 && matchedFrames.Length==1) frame=matchedFrames[0];
                        var matrixRegs=RtDicom.Items(item,DicomTag.MatrixRegistrationSequence).ToArray();
                        if(matrixRegs.Length!=1) throw new ArgumentException("Invalid matrix registration sequence.");
                        var matrices=RtDicom.Items(matrixRegs[0],DicomTag.MatrixSequence).ToArray();
                        if(matrices.Length==0) throw new ArgumentException("Missing registration matrices.");
                        var combined=Matrix4.Identity;
                        foreach(var matrix in matrices)
                        {
                            string type=RtDicom.Text(matrix,DicomTag.FrameOfReferenceTransformationMatrixType);
                            if(type!="RIGID" && type!="RIGID_SCALE" && type!="AFFINE") throw new ArgumentException("Unsupported matrix type.");
                            var m=new Matrix4(RtDicom.Numbers(matrix,DicomTag.FrameOfReferenceTransformationMatrix));
                            m.Inverse(); // Reject singular transforms before they can be selected.
                            ValidateType(m,type);
                            // PS3.3 C.20.2 equation 2: M3 * (M2 * (M1 * p)).
                            combined=Matrix4.Multiply(m,combined);
                        }
                        if(frame.Length>0) local.Add(new RegistrationLink {SourceFrame=frame,SourceToTarget=combined,SourceSopUids=sops});
                    }
                    for(int i=0;i<local.Count;i++) for(int j=i+1;j<local.Count;j++)
                    {
                        // Both items map source coordinates into the SAME REG RCS.
                        result.Add(new RegistrationLink { SourceFrame=local[i].SourceFrame, TargetFrame=local[j].SourceFrame,
                            SourceToTarget=Matrix4.Multiply(local[j].SourceToTarget.Inverse(),local[i].SourceToTarget),
                            SourceSopUids=local[i].SourceSopUids,TargetSopUids=local[j].SourceSopUids,Label="Spatial REG" });
                    }
                }
                catch(ArgumentException) { /* Malformed REG is unusable, never a fallback identity. */ }
                catch(DicomDataException) { }
            }
            return result;
        }
        public static Matrix4 Resolve(List<RegistrationLink> links,string sourceFrame,string targetFrame)
        {
            if(string.IsNullOrWhiteSpace(sourceFrame) || string.IsNullOrWhiteSpace(targetFrame)) return null;
            if(sourceFrame==targetFrame) return Matrix4.Identity;
            Matrix4 selected=null;
            foreach(var link in links)
            {
                bool forward=link.SourceFrame==sourceFrame && link.TargetFrame==targetFrame;
                bool reverse=link.SourceFrame==targetFrame && link.TargetFrame==sourceFrame;
                if(!forward && !reverse) continue;
                if(link.SourceToTarget==null) return null;
                Matrix4 candidate;
                try { link.SourceToTarget.Inverse(); candidate=forward?link.SourceToTarget:link.SourceToTarget.Inverse(); }
                catch(ArgumentException) { return null; }
                if(selected==null) selected=candidate;
                else if(!Equivalent(selected,candidate)) return null;
            }
            return selected;
        }
        static bool Equivalent(Matrix4 a,Matrix4 b)
        {
            var av=a.Values; var bv=b.Values;
            for(int i=0;i<16;i++) if(Math.Abs(av[i]-bv[i])>1e-6) return false;
            return true;
        }
        static void ValidateType(Matrix4 matrix,string type)
        {
            if(type=="AFFINE")return;
            var m=matrix.Values;
            var x=new Vec3(m[0],m[4],m[8]);var y=new Vec3(m[1],m[5],m[9]);var z=new Vec3(m[2],m[6],m[10]);
            const double tolerance=1e-4;
            if(Math.Abs(x.Normalized().Dot(y.Normalized()))>tolerance || Math.Abs(x.Normalized().Dot(z.Normalized()))>tolerance || Math.Abs(y.Normalized().Dot(z.Normalized()))>tolerance)throw new ArgumentException("Nonorthogonal rigid transform.");
            if(type=="RIGID" && (Math.Abs(x.Length-1)>tolerance || Math.Abs(y.Length-1)>tolerance || Math.Abs(z.Length-1)>tolerance || x.Cross(y).Dot(z)<0))throw new ArgumentException("Invalid rigid transform.");
        }
    }
}
