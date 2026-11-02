using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
namespace QuickLook.DicomRT
{
 // A scheduling/display filter only: never changes clinical data or DVH exports.
 internal sealed class MlcDoseRelevance:IDisposable
 {
  sealed class Entry {public StructureRoi Roi;public DoseGrid Dose;public string Map;public DvhResult Result;}
  readonly List<Entry> saved=new List<Entry>();
  readonly Action changed;CancellationTokenSource source;string signature;int generation,done,total;
  double highest;DoseGrid currentDose;StructureRoi[] currentRois=new StructureRoi[0];
  Dictionary<StructureRoi,double> values=new Dictionary<StructureRoi,double>();
  public bool Pending {get;private set;}
  public Task Completion {get;private set;}=Task.CompletedTask;
  public string Status=>Pending?$"Dose relevance · D2% {Volatile.Read(ref done)}/{total}":"";
  public MlcDoseRelevance(Action changed){this.changed=changed;}
  static string MapKey(Matrix4 map)=>string.Join(",",map.Values.Select(v=>v.ToString("R",CultureInfo.InvariantCulture)));
  internal static bool Eligible(double d2,double highest)=>!BeamProjection.Finite(d2)||!BeamProjection.Finite(highest)||highest<=0||d2>highest*.1;
  public bool Allows(StructureRoi roi){double value;var snapshot=values;return !snapshot.TryGetValue(roi,out value)||Eligible(value,highest);}
  public void Configure(RenderScene scene,bool enabled)
  {
   var dose=scene?.Doses?.Count==1?scene.Doses[0]:null;
   var rois=scene?.Structures?.ToArray()??new RoiOverlay[0];
   var jobs=rois.Select(r=>new {Roi=r.Roi,Map=dose==null?null:Matrix4.Multiply(dose.ImageToDose,r.RoiToImage)}).ToArray();
   string key=enabled&&dose!=null?System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(dose.Dose)+":"+string.Join(";",jobs.Select(j=>System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(j.Roi)+":"+MapKey(j.Map))):"off";
   if(key==signature&&ReferenceEquals(currentDose,dose?.Dose)&&currentRois.SequenceEqual(rois.Select(r=>r.Roi)))return;signature=key;currentDose=dose?.Dose;currentRois=rois.Select(r=>r.Roi).ToArray();highest=0;source?.Cancel();source?.Dispose();source=new CancellationTokenSource();var token=source.Token;int mine=++generation;
   values=new Dictionary<StructureRoi,double>();Pending=enabled&&dose!=null&&jobs.Length>0;done=0;total=jobs.Length;if(!Pending)return;
   Completion=Calculate();
   async Task Calculate()
   {
    try{
     var result=await Task.Run(()=>{var scores=new Dictionary<StructureRoi,double>();foreach(var job in jobs){token.ThrowIfCancellationRequested();string map=MapKey(job.Map);Entry previous;lock(saved)previous=saved.FirstOrDefault(x=>x.Roi==job.Roi&&x.Dose==dose.Dose&&x.Map==map);
       var dvh=previous?.Result??DvhCalculator.Calculate(job.Roi,dose.Dose,job.Map,token);token.ThrowIfCancellationRequested();
       if(previous==null)lock(saved)saved.Add(new Entry{Roi=job.Roi,Dose=dose.Dose,Map=map,Result=dvh});
       scores[job.Roi]=dvh.Status==DvhStatus.Complete?dvh.D2:double.NaN;Interlocked.Increment(ref done);
      }return scores;},token);
     if(mine!=generation)return;highest=result.Values.Where(BeamProjection.Finite).DefaultIfEmpty(0).Max();values=result;Pending=false;changed?.Invoke();
    }catch(OperationCanceledException){}catch(Exception){if(mine==generation){Pending=false;changed?.Invoke();}}
   }
  }
  public void Dispose(){generation++;source?.Cancel();source?.Dispose();source=null;}
 }
}
