using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
namespace QuickLook.DicomRT {
 internal sealed class OrientationAvatarPreferences:INotifyPropertyChanged {
  internal static readonly string[] Ids={"human","frieza","obelisk","elsa","saitama"};
  internal static readonly string[] Labels={"Human","Freeza · Dragon Ball","Obelisk · Codex pet","Elsa · Frozen","Saitama · One-Punch Man"};
  internal static OrientationAvatarPreferences Current=new OrientationAvatarPreferences(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DicomRT","Preferences"));
  readonly string directory;internal string Selected {get;private set;}="human";
  public event PropertyChangedEventHandler PropertyChanged;
  internal OrientationAvatarPreferences(string directory){this.directory=directory;try{var path=Path.Combine(directory,"orientation-avatar.txt");if(File.Exists(path)&&new FileInfo(path).Length<100){var value=File.ReadAllText(path).Trim();if(Ids.Contains(value))Selected=value;}}catch(IOException){}catch(UnauthorizedAccessException){}}
  internal bool Select(string id){if(!Ids.Contains(id))return false;if(Selected==id)return true;
   try{Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"orientation-avatar.txt"),id);}catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}
   Selected=id;PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(Selected)));return true;
  }
 }
}