using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using QuickLook.DicomRT;
internal static class SumAndBlockScenarios
{
 internal static DoseGrid Dose(string id,string frame,float value)
 {
  var v=new VolumeData{Width=2,Height=2,Depth=2,Origin=new Vec3(10,20,30),AxisX=new Vec3(1,0,0),AxisY=new Vec3(0,1,0),AxisZ=new Vec3(0,0,1),SpacingX=2,SpacingY=3,SpacingZ=4,Values=Enumerable.Repeat(value,8).ToArray(),Min=value,Max=value+1};v.Values[7]=value+1;
  var d=(DoseGrid)typeof(DoseGrid).GetMethod("FromDerivedVolume",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{v,frame,id});d.Entry.SopUid="dose"+id;d.Entry.PatientKey="test|";d.PlanUid=id;d.ReferencedPlanCount=1;d.SummationType="PLAN";return d;
 }
 public static void Run(Action<bool,string> check)
 {
  var a=Dose("A","frame1",10);var b=Dose("B","frame2",2);var c=Dose("C","frame2",3);var d=Dose("D","frame2",4);var input=new[]{a,b,c,d};var links=new List<RegistrationLink>();
  var groups=DoseSum.CompatibleGroups(input,links);check(groups.Count==1&&groups[0].Count==3&&!groups[0].Contains(a),"sum groups exclude unrelated first frame and retain all compatible plans");
  var sum=DoseSum.Calculate(groups[0],links,CancellationToken.None);check(sum.IncludedCount==3&&sum.IncludedPlanUids.Length==3&&Math.Abs(sum.Dose.Maximum-12)<1e-6,"all three selected plans contribute to labelled sum");
  check((sum.Dose.MaximumPosition.Value-new Vec3(12,23,34)).Length<1e-8,"derived Dmax maps to physical maximum voxel");
  var reduced=DoseSum.Calculate(new[]{b,c},links,CancellationToken.None);check(reduced.IncludedCount==2&&reduced.Dose.Maximum==7,"deselected plan does not contribute");
  var duplicate=Dose("B","frame2",8);duplicate.Entry.SopUid="second-dose-B";check(DoseSum.CompatibleGroups(new[]{b,duplicate,c},links).Count==0,"ambiguous duplicate plan doses do not silently double-count");
  var block=BeamBlock.Create("APERTURE",new[]{-20d,-20,20,-20,20,0,0,0,0,20,-20,20},6);
  double area=block.Triangles.Sum(t=>Math.Abs((t[1]-t[0]).Cross(t[2]-t[0]).Z)/2);check(Math.Abs(area-1200)<1e-8,"concave block triangulation preserves area and notch");
  var cp=new ControlPoint{XJaws=new[]{-100d,100},YJaws=new[]{-100d,100},Blocks={block}};
  var shapeType=typeof(ViewerControl).Assembly.GetType("QuickLook.DicomRT.BlockApertureGeometry");var method=shapeType.GetMethod("Create",BindingFlags.Static|BindingFlags.NonPublic);var shape=(Geometry)method.Invoke(null,new[]{cp});
  check(shape.FillContains(new Point(-10,10))&&!shape.FillContains(new Point(10,10))&&!shape.FillContains(new Point(50,0)),"custom aperture excludes jaw-only area and preserves concavity");
  var withoutJaws=(Geometry)method.Invoke(null,new[]{new ControlPoint{Blocks=cp.Blocks}});check(withoutJaws.FillContains(new Point(-10,10))&&!withoutJaws.FillContains(new Point(10,10)),"recorded cutout remains usable without jaw tags");
  cp.Blocks.Add(BeamBlock.Create("SHIELDING",new[]{-15d,-15,-5,-15,-5,-5,-15,-5},4));
  // Blocks are immutable after parsing: a fresh list represents a changed beam.
  cp.Blocks=cp.Blocks.ToList();shape=(Geometry)method.Invoke(null,new[]{cp});check(!shape.FillContains(new Point(-10,-10))&&shape.FillContains(new Point(10,-10)),"shielding block is subtracted from aperture");
  check(ReferenceEquals(method.Invoke(null,new[]{cp}),shape),"static custom apertures reuse prepared geometry");
  bool invalid=false;try{BeamBlock.Create("APERTURE",new[]{0d,0,1,1},2);}catch(ArgumentException){invalid=true;}check(invalid,"missing block geometry never becomes a fake jaw aperture");
 }
}
