using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
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
 static DoseGrid Dose(bool physical,float max)=>new DoseGrid{Units=physical?"GY":"RELATIVE",DoseType="PHYSICAL",Maximum=max};
 static void Context(ViewerControl viewer,params DoseGrid[] context)
 {var doses=Get<List<DoseGrid>>(viewer,"doses");doses.Clear();doses.AddRange(context);Call(viewer,"UpdateIsodoseContext");}
 static void Apply(ViewerControl viewer,string text){Get<TextBox>(viewer,"isoLevels").Text=text;Get<Button>(viewer,"applyIsodosesButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
 static void Default(ViewerControl viewer)=>Get<Button>(viewer,"defaultIsodosesButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
 static void Mode(ViewerControl viewer,bool absolute)=>Get<ComboBox>(viewer,"isodoseMode").SelectedIndex=absolute?0:1;
 static bool Levels(ViewerControl viewer,params double[] levels)=>Get<double[]>(viewer,"displayedIsoLevels").SequenceEqual(levels);
 static bool Automatic(ViewerControl viewer,double max)=>Levels(viewer,IsodoseConfiguration.AutomaticLevels(max,true));
 public static void Run(Action<bool,string> check,string directory)
 {
  Directory.CreateDirectory(directory);var legacyPath=Path.Combine(directory,"isodoses-gy.txt");File.WriteAllText(legacyPath,"3; 17.25");
  var defaults=Enumerable.Range(1,10).Select(i=>i*10d).ToArray();
  var desired=new[]{4d,12.25,20};var relative=new[]{25d,50,75};
  var firstDose=Dose(true,27.7f);var otherDose=Dose(true,68);
  using(var first=new ViewerControl())using(var second=new ViewerControl())
  {
   Context(first,firstDose);Context(second,otherDose);
   check(Automatic(first,firstDose.Maximum)&&Automatic(second,otherDose.Maximum),"Physical doses ignore legacy global Gy settings and start automatic");
   Apply(first,"4; 12.25; 20");
   check(Levels(first,desired)&&Automatic(second,68),"Gy Apply updates only the active viewer dose context");
   check(Get<RenderScene>(first,"latestScene").AbsoluteIsodoses&&Get<RenderScene>(first,"latestScene").IsoLevels.SequenceEqual(desired),"All rendered views receive the local Gy levels");
   Context(first,otherDose);check(Automatic(first,68),"Another dose receives its own automatic Gy defaults");
   Context(first,firstDose);check(Levels(first,desired),"Revisiting the same dose object restores its local Gy edit");
   Context(first,Dose(true,27.7f));check(Automatic(first,firstDose.Maximum),"A different dose object with identical maximum never inherits local Gy edits");
   Context(first,firstDose);Apply(first,"1.234");check(Levels(first,desired),"Invalid decimal precision preserves applied local levels");
   Default(first);check(Automatic(first,firstDose.Maximum)&&Automatic(second,68),"Gy Default restores automatic whole-Gy levels locally");
   Context(first,otherDose);Context(first,firstDose);check(Automatic(first,firstDose.Maximum),"Gy Default removes the cached custom selection");
   var low=Dose(true,.8f);Context(first,low);Apply(first,"0.25; 0.5");Default(first);
   check(Levels(first)&&Get<TextBox>(first,"isoLevels").Text=="","Sub-Gy Default restores an empty whole-Gy level set");
   Context(first,firstDose,otherDose);Apply(first,"7; 14");Context(first,firstDose);Context(first,otherDose,firstDose);
   check(Levels(first,7,14),"Local Gy settings follow the selected dose object set regardless of ordering");
   Context(first,firstDose);Apply(first,"4; 12.25; 20");Mode(first,false);
   check(!Get<bool>(first,"absoluteIsodoses")&&Levels(first,defaults),"Physical dose explicitly supports percentage mode with default levels");
   Apply(first,"25; 50; 75");check(Levels(first,relative)&&Automatic(second,68),"Global percentages never replace another viewer's active Gy levels");
   Mode(second,false);check(Levels(second,relative),"A physical-dose viewer entering percentage mode uses globally saved levels");
   Apply(second,"20; 40; 80");check(Levels(first,20,40,80),"Percentage Apply broadcasts to other open percentage viewers");
   check(!Get<RenderScene>(first,"latestScene").AbsoluteIsodoses&&Get<RenderScene>(first,"latestScene").IsoLevels.SequenceEqual(new[]{20d,40,80}),"Relative mode reaches the shared render scene for physical doses");
   Context(first,otherDose);check(!Get<bool>(first,"absoluteIsodoses")&&Levels(first,20,40,80),"Explicit relative mode survives physical dose selection changes");
   var nonphysical=Dose(false,100);Context(first,nonphysical);Mode(first,true);
   check(!Get<bool>(first,"absoluteIsodoses")&&Get<ComboBox>(first,"isodoseMode").SelectedIndex==1&&!((ComboBoxItem)Get<ComboBox>(first,"isodoseMode").Items[0]).IsEnabled,"Nonphysical dose cannot enter Gy mode");
   Context(first,firstDose);check(!Get<bool>(first,"absoluteIsodoses"),"Forced percentage mode preserves the previous physical-dose relative choice");
   Mode(first,true);check(Levels(first,desired)&&Get<bool>(first,"absoluteIsodoses"),"Returning to Gy restores local absolute edits without converting percentages");
   Context(first,nonphysical);Context(first,firstDose);check(Get<bool>(first,"absoluteIsodoses")&&Levels(first,desired),"Forced percentage mode also preserves the previous physical-dose Gy choice");
   var effective=Dose(true,100);effective.DoseType="EFFECTIVE";Context(first,effective);
   check(!Get<bool>(first,"absoluteIsodoses"),"Effective dose is restricted to percentage mode even with GY units");
   Context(first,firstDose);Mode(first,false);Apply(first,"101");check(Levels(first,20,40,80)&&Levels(second,20,40,80),"Invalid percentage input cannot replace global settings");
   Default(first);check(Levels(first,defaults)&&Levels(second,defaults),"Percentage Default restores ten standard levels in both open viewers");
   Apply(first,"25; 50; 75");
   using(var reopened=new ViewerControl())
   {
    Context(reopened,firstDose);check(Get<bool>(reopened,"absoluteIsodoses")&&Automatic(reopened,firstDose.Maximum),"New viewers start physical doses in automatic Gy, without another viewer's edits");
    Mode(reopened,false);check(Levels(reopened,relative),"New physical-dose viewer can use persisted global percentages");
    Context(reopened,nonphysical);check(Levels(reopened,relative),"New relative-dose context reads global percentage settings");
   }
   var type=typeof(ViewerControl).Assembly.GetType("QuickLook.DicomRT.IsodosePreferences");var newProcessStore=Activator.CreateInstance(type,Flags,null,new object[]{directory},null);
   check(type.GetMethod("Read",Flags).Invoke(newProcessStore,new object[]{true})==null,"Fresh preference store refuses to read legacy Gy values");
   check(((double[])type.GetMethod("Read",Flags).Invoke(newProcessStore,new object[]{false})).SequenceEqual(relative),"Fresh preference store restores saved global percentages");
   var saveArgs=new object[]{true,new[]{9d},null};check(!(bool)type.GetMethod("Save",Flags).Invoke(newProcessStore,saveArgs),"Preference API refuses global Gy saves");
   check(File.ReadAllText(legacyPath)=="3; 17.25","Legacy Gy file remains untouched by local Apply and Default");
   Default(first);check(((double[])type.GetMethod("Read",Flags).Invoke(newProcessStore,new object[]{false})).SequenceEqual(defaults),"Global percentage Default persists across fresh store reads");
   File.WriteAllText(Path.Combine(directory,"isodoses-percent.txt"),"invalid");check(type.GetMethod("Read",Flags).Invoke(newProcessStore,new object[]{false})==null,"Malformed percentage preferences fall back to defaults");
  }
 }
}
