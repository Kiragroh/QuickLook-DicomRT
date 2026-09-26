using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace QuickLook.DicomRT
{
    public sealed class DoseSumResult
    {
        public DoseGrid Dose {get;internal set;}
        public string Message {get;internal set;}
        public int IncludedCount {get;internal set;}
        public int ExcludedCount {get;internal set;}
        public int AmbiguousPlanCount {get;internal set;}
        public double CoverageFraction {get;internal set;}
        public string ReferencePlanUid {get;internal set;}
        public string[] IncludedPlanUids {get;internal set;}=new string[0];
    }
    public static class DoseSum
    {
        sealed class Input {public DoseGrid Dose;public Matrix4 ReferenceToDose;}
        /// <summary>In-memory physical dose preview on the first eligible regular dose grid.
        /// Every output node requires finite values in every included source; other nodes remain NaN.
        /// No fraction scaling, biological conversion, extrapolation, or DICOM output is performed.</summary>
        public static DoseSumResult Calculate(IReadOnlyList<DoseGrid> doses,IReadOnlyList<RegistrationLink> registrations,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();var result=new DoseSumResult();
            if(doses==null||doses.Count==0){result.Message="No RTDOSE is available for a plan sum.";return result;}
            var seen=new HashSet<string>(StringComparer.Ordinal);var candidates=new List<DoseGrid>();
            foreach(var d in doses)
            {
                token.ThrowIfCancellationRequested();
                if(d==null||d.Units!="GY"||d.DoseType!="PHYSICAL"||d.SummationType!="PLAN"||d.ReferencedPlanCount!=1||string.IsNullOrEmpty(d.PlanUid)||string.IsNullOrEmpty(d.FrameUid)||string.IsNullOrEmpty(d.Entry?.SopUid)||!Finite(d.Maximum)||d.Maximum<0||!seen.Add(d.Entry.SopUid))continue;
                candidates.Add(d);
            }
            var groups=candidates.GroupBy(d=>d.PlanUid,StringComparer.Ordinal).ToArray();
            result.AmbiguousPlanCount=groups.Count(g=>g.Count()>1);
            var unique=groups.Where(g=>g.Count()==1).Select(g=>g.First()).ToArray();
            var reference=unique.FirstOrDefault(d=>d.Volume!=null);
            if(reference==null){result.ExcludedCount=doses.Count;result.Message="No unambiguous physical PLAN dose with a regular reference grid is available.";return result;}
            result.ReferencePlanUid=reference.PlanUid;
            var links=registrations==null?new List<RegistrationLink>():registrations.ToList();
            var included=new List<Input>();
            foreach(var d in unique)
            {
                if(!string.IsNullOrEmpty(reference.Entry?.PatientKey)&&!string.IsNullOrEmpty(d.Entry?.PatientKey)&&reference.Entry.PatientKey!=d.Entry.PatientKey)continue;
                var mapping=RegistrationReader.Resolve(links,reference.FrameUid,d.FrameUid);
                if(mapping!=null&&Rigid(mapping))included.Add(new Input {Dose=d,ReferenceToDose=mapping});
            }
            result.IncludedCount=included.Count;result.ExcludedCount=doses.Count-included.Count;
            result.IncludedPlanUids=included.Select(i=>i.Dose.PlanUid).ToArray();
            if(included.Count==0){result.Message="No unambiguously registered plan doses are available.";return result;}
            var v=reference.Volume;long count=(long)v.Width*v.Height*v.Depth;
            if(count<=0||count>128L*1024*1024||count*included.Count>500000000L)
            {result.Message="Plan sum exceeds the memory/computation budget; the reference resolution was not reduced.";return result;}
            var output=new VolumeData {Width=v.Width,Height=v.Height,Depth=v.Depth,Origin=v.Origin,AxisX=v.AxisX,AxisY=v.AxisY,AxisZ=v.AxisZ,SpacingX=v.SpacingX,SpacingY=v.SpacingY,SpacingZ=v.SpacingZ,Values=new float[(int)count],Min=float.PositiveInfinity,Max=float.NegativeInfinity};
            long covered=0;int index=0;
            for(int z=0;z<v.Depth;z++)for(int y=0;y<v.Height;y++)for(int x=0;x<v.Width;x++,index++)
            {
                if((index&2047)==0)token.ThrowIfCancellationRequested();var point=v.WorldAt(x,y,z);double sum=0;bool known=true;
                foreach(var input in included)
                {float sample=input.Dose.Sample(input.ReferenceToDose.Transform(point));if(!Finite(sample)||sample<0){known=false;break;}sum+=sample;}
                if(!known||!Finite(sum)||sum>float.MaxValue){output.Values[index]=float.NaN;continue;}
                float value=(float)sum;output.Values[index]=value;output.Min=Math.Min(output.Min,value);output.Max=Math.Max(output.Max,value);covered++;
            }
            result.CoverageFraction=(double)covered/count;
            if(covered==0){result.Message="No shared dose coverage on the reference grid; no plan sum was generated.";return result;}
            result.Dose=DoseGrid.FromDerivedVolume(output,reference.FrameUid,"Plan sum · "+included.Count+" plans");
            result.Message=$"{included.Count} plans added · {result.ExcludedCount} dose objects excluded · {result.AmbiguousPlanCount} plans with ambiguous dose. Reference grid unchanged; {result.CoverageFraction:P1} of grid points have shared coverage. Dose remains unknown elsewhere.";
            return result;
        }
        static bool Finite(double n)=>!double.IsNaN(n)&&!double.IsInfinity(n);
        static bool Rigid(Matrix4 m)
        {
            var v=m.Values;var a=new Vec3(v[0],v[4],v[8]);var b=new Vec3(v[1],v[5],v[9]);var c=new Vec3(v[2],v[6],v[10]);
            return Math.Abs(a.Length-1)<1e-4&&Math.Abs(b.Length-1)<1e-4&&Math.Abs(c.Length-1)<1e-4&&Math.Abs(a.Dot(b))+Math.Abs(a.Dot(c))+Math.Abs(b.Dot(c))<1e-4&&Math.Abs(a.Cross(b).Dot(c)-1)<1e-4;
        }
    }
}
