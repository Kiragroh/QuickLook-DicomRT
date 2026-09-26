using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Controls;
using QuickLook.DicomRT;
internal static class IsodosePreferenceScenarios
{
 const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
 internal sealed class TestScope:IDisposable
 {
  internal readonly string DirectoryPath=Path.Combine(Path.GetTempPath(),"dicomrt-isodose-tests-"+Guid.NewGuid().ToString("N"));
  readonly FieldInfo current=typeof(ViewerControl).Assembly.GetType("QuickLook.DicomRT.IsodosePreferences").GetField("Current",BindingFlags.Static|BindingFlags.NonPublic);
  readonly object previous;
  internal TestScope(){previous=current.GetValue(null);current.SetValue(null,Activator.CreateInstance(current.FieldType,Flags,null,new object[]{DirectoryPath},null));}
  public void Dispose(){current.SetValue(null,previous);if(Directory.Exists(DirectoryPath)){foreach(string file in Directory.GetFiles(DirectoryPath))File.Delete(file);Directory.Delete(DirectoryPath);}}
 }
 static T Get<T>(object o,string n)=>(T)o.GetType().GetField(n,Flags).GetValue(o);
 static void Call(object o,string n)=>o.GetType().GetMethod(n,Flags).Invoke(o,null);
 static void Context(ViewerControl viewer,bool absolute,double max)
 {var doses=Get<List<DoseGrid>>(viewer,"doses");doses.Clear();doses.Add(new DoseGrid{Units=absolute?"GY":"RELATIVE",DoseType="PHYSICAL",Maximum=(float)max});Call(viewer,"UpdateIsodoseContext");}
 public static void Run(Action<bool,string> check,string directory)
 {
  using(var first=new ViewerControl())using(var second=new ViewerControl())
  {
   Context(first,true,27.7);Context(second,true,68);
   Get<TextBox>(first,"isoLevels").Text="4; 12.25; 20";Call(first,"ApplyIsodoseLevels");
   var desired=new[]{4d,12.25,20};
   check(Get<double[]>(first,"displayedIsoLevels").SequenceEqual(desired)&&Get<double[]>(second,"displayedIsoLevels").SequenceEqual(desired),"Apply globally updates separate open viewers");
   Context(second,true,100);check(Get<double[]>(second,"displayedIsoLevels").SequenceEqual(desired),"Changing plan/dose retains reduced global Gy levels");
   Context(second,false,100);check(Get<double[]>(second,"displayedIsoLevels").Length==10,"Relative defaults remain independent of saved Gy levels");
   Get<TextBox>(second,"isoLevels").Text="25; 50; 75";Call(second,"ApplyIsodoseLevels");check(Get<double[]>(first,"displayedIsoLevels").SequenceEqual(desired),"Relative apply never overwrites an absolute viewer");
   Context(second,true,50);check(Get<double[]>(second,"displayedIsoLevels").SequenceEqual(desired),"Switching back restores saved Gy choices");
   using(var reopened=new ViewerControl()){Context(reopened,true,30);check(Get<double[]>(reopened,"displayedIsoLevels").SequenceEqual(desired),"Newly opened viewer restores global Gy settings");}
   var type=typeof(ViewerControl).Assembly.GetType("QuickLook.DicomRT.IsodosePreferences");var newProcessStore=Activator.CreateInstance(type,Flags,null,new object[]{directory},null);
   check(((double[])type.GetMethod("Read",Flags).Invoke(newProcessStore,new object[]{true})).SequenceEqual(desired),"Fresh preference store reads persisted decimal Gy values");
   check(((double[])type.GetMethod("Read",Flags).Invoke(newProcessStore,new object[]{false})).SequenceEqual(new[]{25d,50,75}),"Fresh store retains separate relative preferences");
   Get<TextBox>(first,"isoLevels").Text="1.234";Call(first,"ApplyIsodoseLevels");check(Get<double[]>(first,"displayedIsoLevels").SequenceEqual(desired),"Invalid precision does not overwrite global values");
   File.WriteAllText(Path.Combine(directory,"isodoses-gy.txt"),"invalid");check(type.GetMethod("Read",Flags).Invoke(newProcessStore,new object[]{true})==null,"Malformed preferences fall back to automatic levels");
  }
 }
}
