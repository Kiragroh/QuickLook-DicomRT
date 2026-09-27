using System;
using System.Diagnostics;
using System.Threading;
namespace QuickLook.DicomRT {
 // One instance per preparation generation; cancelled jobs cannot alter the next run.
 internal sealed class PreparationProgress {
  int drrDone,outlineDone,drrTotal=-1,outlineTotal=-1;
  readonly Stopwatch drrTime=Stopwatch.StartNew(),outlineTime=Stopwatch.StartNew();
  public int Completed=>Volatile.Read(ref drrDone)+Volatile.Read(ref outlineDone);
  public int Total=>Math.Max(0,Volatile.Read(ref drrTotal))+Math.Max(0,Volatile.Read(ref outlineTotal));
  public void SetDrrTotal(int total){drrTime.Restart();Volatile.Write(ref drrTotal,total);}
  public void SetOutlineTotal(int total){outlineTime.Restart();Volatile.Write(ref outlineTotal,total);}
  public void DrrDone()=>Interlocked.Increment(ref drrDone);
  public void OutlineDone()=>Interlocked.Increment(ref outlineDone);
  internal static double Remaining(int done,int total,double seconds)=>total<=done?0:done<3||seconds<2?double.NaN:seconds/done*(total-done);
  internal static string Duration(double seconds)=>seconds<60?Math.Max(1,(int)Math.Ceiling(seconds))+" s":seconds<3600?Math.Ceiling(seconds/60)+" min":Math.Ceiling(seconds/3600)+" h";
  public string Text {get{
   int dt=Volatile.Read(ref drrTotal),ot=Volatile.Read(ref outlineTotal),dd=Volatile.Read(ref drrDone),od=Volatile.Read(ref outlineDone);
   if(dt<0||ot<0)return "DRR / outlines · planning views…";
   string text="DRRs "+dd+"/"+dt+" · ROI projections "+od+"/"+ot;
   double remaining=Math.Max(Remaining(dd,dt,drrTime.Elapsed.TotalSeconds),Remaining(od,ot,outlineTime.Elapsed.TotalSeconds));
   return text+(dt==dd&&ot==od?" · refining requested view":double.IsNaN(remaining)?" · estimating time…":" · ~"+Duration(remaining)+" left");
  }}
 }
}
