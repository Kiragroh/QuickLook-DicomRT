using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace QuickLook.DicomRT
{
 internal sealed class IsodosePreferences
 {
  internal static IsodosePreferences Current = new IsodosePreferences(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DicomRT","Preferences"));
  readonly string directory;
  internal event Action<bool,double[]> Changed;
  internal IsodosePreferences(string directory){this.directory=directory;}
  string FileName=>Path.Combine(directory,"isodoses-percent.txt");
  internal double[] Read(bool absolute)
  {
   // Legacy Gy preferences must never become defaults for another dose context.
   if(absolute)return null;
   try{var path=FileName;if(!File.Exists(path)||new FileInfo(path).Length>4096)return null;double[] values;string error;return IsodoseConfiguration.TryParse(File.ReadAllText(path),false,out values,out error)?values:null;}
   catch(IOException){return null;}catch(UnauthorizedAccessException){return null;}
  }
  internal bool Save(bool absolute,double[] levels,out string error)
  {
   if(absolute){error="Absolute Gy levels apply only to the current dose selection.";return false;}
   string value=string.Join("; ",levels.Select(x=>x.ToString("0.##",CultureInfo.InvariantCulture)));double[] validated;
   if(!IsodoseConfiguration.TryParse(value,absolute,out validated,out error))return false;
   string temporary=null;
   try
   {
    Directory.CreateDirectory(directory);string path=FileName;temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
    File.WriteAllText(temporary,value);if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
   }
   catch(Exception ex) when(ex is IOException||ex is UnauthorizedAccessException)
   {error="Could not save global isodose settings. Check access to your local preferences folder.";return false;}
   finally{if(temporary!=null)try{if(File.Exists(temporary))File.Delete(temporary);}catch(IOException){}catch(UnauthorizedAccessException){}}
   Changed?.Invoke(false,validated);error=null;return true;
  }
 }
}
