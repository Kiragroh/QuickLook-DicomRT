using System;
using System.IO;
namespace QuickLook.DicomRT
{
 internal sealed class ViewerPreferences
 {
  internal static ViewerPreferences Current=new ViewerPreferences(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DicomRT","Preferences"));
  readonly string directory;
  public bool HideX,RelativeIsodoses,DrrOnDemand=true,ProgressiveDrr=true,OutlinesFirst=true,DoseRelevantOutlines=true;
  public int LabelMode;
  internal ViewerPreferences(string directory){this.directory=directory;try{string file=Path.Combine(directory,"viewer.txt");if(!File.Exists(file)||new FileInfo(file).Length>4096)return;foreach(string line in File.ReadAllLines(file)){var pair=line.Split('=');if(pair.Length!=2)continue;bool b;int n;if(pair[0]=="LabelMode"&&int.TryParse(pair[1],out n)&&n>=0&&n<=3)LabelMode=n;if(!bool.TryParse(pair[1],out b))continue;switch(pair[0]){case "HideX":HideX=b;break;case "RelativeIsodoses":RelativeIsodoses=b;break;case "DrrOnDemand":DrrOnDemand=b;break;case "ProgressiveDrr":ProgressiveDrr=b;break;case "OutlinesFirst":OutlinesFirst=b;break;case "DoseRelevantOutlines":DoseRelevantOutlines=b;break;}}}catch(IOException){}catch(UnauthorizedAccessException){}}
  internal bool Save(out string error){string temp=null;try{Directory.CreateDirectory(directory);string file=Path.Combine(directory,"viewer.txt");temp=file+"."+Guid.NewGuid().ToString("N")+".tmp";File.WriteAllLines(temp,new[]{"LabelMode="+LabelMode,"HideX="+HideX,"RelativeIsodoses="+RelativeIsodoses,"DrrOnDemand="+DrrOnDemand,"ProgressiveDrr="+ProgressiveDrr,"OutlinesFirst="+OutlinesFirst,"DoseRelevantOutlines="+DoseRelevantOutlines});if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);error=null;return true;}catch(Exception e)when(e is IOException||e is UnauthorizedAccessException){error="Could not save viewer settings.";return false;}finally{if(temp!=null)try{if(File.Exists(temp))File.Delete(temp);}catch(IOException){}catch(UnauthorizedAccessException){}}}
 }
}
