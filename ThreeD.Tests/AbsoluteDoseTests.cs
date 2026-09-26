using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Media.Media3D;
using QuickLook.DicomRT;
internal static class AbsoluteDoseTests
{
 const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);}
 public static void Run()
 {
  var volume=new VolumeData{Width=21,Height=2,Depth=2,Origin=new Vec3(),AxisX=new Vec3(1,0,0),AxisY=new Vec3(0,1,0),AxisZ=new Vec3(0,0,1),SpacingX=1,SpacingY=1,SpacingZ=1,Max=20,Values=new float[84]};
  for(int i=0;i<84;i++)volume.Values[i]=i%21;
  var dose=(DoseGrid)typeof(DoseGrid).GetMethod("FromDerivedVolume",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{volume,"test","test"});
  var scene=new RenderScene{AbsoluteIsodoses=true,IsoColorMaximum=20,IsoLevels=new[]{2d,5.25,10,15},Doses=new List<DoseOverlay>{new DoseOverlay{Dose=dose}}};
  using(var control=new ThreeDControl())
  {
   var type=typeof(ThreeDControl);type.GetField("scene",Flags).SetValue(control,scene);type.GetMethod("ConfigureDoseChoices",Flags).Invoke(control,null);
   var choices=(ComboBox)type.GetField("doseLevel",Flags).GetValue(control);
   Check(choices.Items.Cast<object>().Select(x=>x.ToString()).SequenceEqual(new[]{"2 Gy","5.25 Gy","10 Gy","15 Gy"}),"3D selector retains absolute decimal levels");
   Check(choices.SelectedItem.ToString()=="10 Gy","3D initial choice nearest half maximum");
   var prepare=type.GetMethod("PrepareCore",BindingFlags.Static|BindingFlags.NonPublic);var cache=type.GetField("cache",Flags).GetValue(control);
   Func<double,object[]> parts=level=>{var result=prepare.Invoke(null,new object[]{scene,false,level,CancellationToken.None,cache,false,false,true,false});return ((IEnumerable)result.GetType().GetField("Parts").GetValue(result)).Cast<object>().ToArray();};
   var items=parts(5.25);Check(items.Length==1,"absolute decimal dose surface exists");var mesh=(MeshGeometry3D)items[0].GetType().GetField("Mesh").GetValue(items[0]);
   Check(mesh.Positions.Count>0&&mesh.Positions.All(p=>Math.Abs(p.X-5.25)<1e-5),"3D threshold is actual 5.25 Gy, not a fraction of maximum");
   Check(parts(25).Length==0,"above-maximum absolute surface omitted");dose.Units="RELATIVE";Check(parts(5.25).Length==0,"nonphysical grid never rendered as Gy");
  }
  Console.WriteLine("PASS absolute 3D dose thresholds, decimal selector, above-maximum and unit handling");
 }
}
