using System;

namespace QuickLook.DicomRT
{
    public static class DicomPatientScope
    {
        public static bool Matches(DicomEntry a, DicomEntry b)
        {
            if(a==null || b==null)return false;
            var left=(a.PatientKey??"|").Split('|');
            var right=(b.PatientKey??"|").Split('|');
            string idA=left[0],idB=right[0];
            bool sameStudy=!string.IsNullOrEmpty(a.StudyUid)&&a.StudyUid==b.StudyUid;
            string issuerA=left.Length>1?left[1]:"",issuerB=right.Length>1?right[1]:"";
            if(!string.IsNullOrWhiteSpace(issuerA)&&!string.IsNullOrWhiteSpace(issuerB)&&issuerA!=issuerB)return false;
            if(string.IsNullOrEmpty(idA)||string.IsNullOrEmpty(idB))return sameStudy;
            if(!string.Equals(idA,idB,StringComparison.Ordinal))return false;
            if(issuerA==issuerB)return true;
            // A missing issuer is incomplete identity metadata, not a conflicting issuer.
            // Limit this fallback to the same study; never merge two declared issuers.
            return sameStudy&&(string.IsNullOrWhiteSpace(issuerA)||string.IsNullOrWhiteSpace(issuerB));
        }
    }
}
