using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
namespace QuickLook.DicomRT
{
    public enum RoiCategory { Other, Target, Organ, External, Support }
    // Display groups only. Never rewrite RTROIInterpretedType or infer a clinical prescription.
    public static class RoiClassification
    {
        static readonly HashSet<string> organs=new HashSet<string>(StringComparer.Ordinal){
            "brain","brainstem","hirn","hirnstamm","cerebrum","cerebellum","kleinhirn",
            "spinalcord","spinalcanal","spinalchannel","cord","myelon","rueckenmark","caudaequina",
            "heart","herz","lung","lungs","lunge","lungen","liver","leber","kidney","kidneys","niere","nieren",
            "bladder","urinarybladder","harnblase","blase","rectum","rektum","sigmoid","sigma","colon","darm",
            "bowel","bowelloop","bowelloops","bowelbag","smallbowel","largebowel","bowelsmall","bowellarge","duenndarm","dickdarm",
            "stomach","magen","duodenum","esophagus","oesophagus","oesophag","speiseroehre",
            "parotid","parotis","parotids","submandibular","submandibulargland","glandsubmand","glndsubmand",
            "larynx","kehlkopf","pharynx","oralcavity","cavityoral","mundhoehle","tongue","zunge",
            "eye","eyes","auge","augen","eyeball","lens","linse","lenses","retina","cochlea",
            "opticnerve","opticnrv","nrvoptic","nopticus","nervusopticus","chiasm","opticchiasm","chiasma","chiasmopt",
            "pituitary","hypophysis","hypophyse","hippocampus","hippocampi","hippocamp",
            "thyroid","schilddruese","trachea","bronchus","bronchi","aorta","carotid","carotis",
            "breast","mamma","mammarygland","brust","femur","femoralhead","femurhead","humerus","humeralhead",
            "penilebulb","bulbpenile","penisbulb","testis","testes","ovary","ovaries","uterus","vagina",
            "plexusbrachial","brachialplexus","plexusbrachialis","chestwall","thoraxwand","spleen","milz","pancreas","pankreas",
            "prostate","prostata","urethra","ureter","sacralplexus","plexussacral","mandible","mandibula","unterkiefer"};
        static string Compact(string s)=>Regex.Replace((s??"").ToLowerInvariant().Replace("ä","ae").Replace("ö","oe").Replace("ü","ue").Replace("ß","ss"),"[^a-z0-9]","");
        static bool IsOrganName(string name)
        {
            var n=Compact(name);
            // Limit suffix removal to conventional laterality/PRV markers; avoid ring/optimization/helper names.
            for(int i=0;i<3;i++){
                if(organs.Contains(n))return true;
                var trimmed=Regex.Replace(n,@"(?:left|right|links|rechts|lt|rt|li|re|l|r|prv(?:\d+(?:mm|cm)?)?)$","");
                if(trimmed==n)break;n=trimmed;
            }
            return organs.Contains(n);
        }
        static bool MatchesPlan(StructureRoi roi,PlanData plan)=>plan!=null&&!string.IsNullOrEmpty(roi.StructureSopUid)&&roi.StructureSopUid==plan.StructureSopUid;
        public static RoiCategory Category(StructureRoi roi,PlanData plan=null)
        {
            if(roi==null)return RoiCategory.Other;
            string type=(roi.InterpretedType??"").Trim().ToUpperInvariant(),name=Compact(roi.Name);
            if(type=="EXTERNAL"||name=="body"||name=="external"||name=="skin"||name=="patient"||name=="koerper")return RoiCategory.External;
            if(type=="SUPPORT"||new[]{"couch","table","tisch","support","immobilization","fixation"}.Any(p=>name.StartsWith(p,StringComparison.Ordinal)))return RoiCategory.Support;
            if(MatchesPlan(roi,plan)&&plan.TargetRoiNumbers.Contains(roi.Number)||type=="PTV"||type=="CTV"||type=="GTV"||type=="ITV"||type=="TREATED_VOLUME"||(roi.Name??"").IndexOf("PTV",StringComparison.OrdinalIgnoreCase)>=0)return RoiCategory.Target;
            if(type=="ORGAN"||MatchesPlan(roi,plan)&&plan.OrganRoiNumbers.Contains(roi.Number))return RoiCategory.Organ;
            if((type.Length==0||type=="NONE"||type=="UNDEFINED"||type=="AVOIDANCE")&&IsOrganName(roi.Name))return RoiCategory.Organ;
            return RoiCategory.Other;
        }
        public static string Explain(StructureRoi roi,PlanData plan=null)
        {
            var group=Category(roi,plan);
            string reason=MatchesPlan(roi,plan)&&((group==RoiCategory.Target&&plan.TargetRoiNumbers.Contains(roi.Number))||(group==RoiCategory.Organ&&plan.OrganRoiNumbers.Contains(roi.Number)))?"referenced ROI in selected RTPLAN":
                group==RoiCategory.Target&&(roi.Name??"").IndexOf("PTV",StringComparison.OrdinalIgnoreCase)>=0?"PTV in ROI name":
                group==RoiCategory.Organ&&(roi.InterpretedType??"").Trim().ToUpperInvariant()!="ORGAN"?"recognized organ name":
                string.IsNullOrWhiteSpace(roi.InterpretedType)?"name rule / unspecified DICOM type":"DICOM type / display name rule";
            return "Display group: "+group+" · "+reason+"\nDICOM type: "+(string.IsNullOrWhiteSpace(roi.InterpretedType)?"not specified":roi.InterpretedType);
        }
    }
}
