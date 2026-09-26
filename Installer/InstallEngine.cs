using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace QuickLook.DicomRT.Installer
{
    public sealed class InstallResult { public int Updated,Unchanged;public string BackupPath,RestartWarning; }
    public static class InstallEngine
    {
        public static string TargetDirectory=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"pooi.moe","QuickLook","QuickLook.Plugin","QuickLook.Plugin.DicomRT");
        static string WorkRoot=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DicomRT");
        static int Session=>Process.GetCurrentProcess().SessionId;
        static string ProcessPath(Process process)
        {
            try{return process.SessionId==Session?Path.GetFullPath(process.MainModule.FileName):null;}catch(Exception){return null;}
        }
        public static string FindQuickLook()
        {
            foreach(var process in Process.GetProcessesByName("QuickLook"))using(process)
            {var file=ProcessPath(process);if(IsQuickLookExecutable(file))return file;}
            foreach(var root in new[]{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs"),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)})
            {var file=Path.Combine(root,"QuickLook","QuickLook.exe");if(IsQuickLookExecutable(file))return file;}
            return null;
        }
        static bool IsQuickLookExecutable(string file)=>!string.IsNullOrEmpty(file)&&string.Equals(Path.GetFileName(file),"QuickLook.exe",StringComparison.OrdinalIgnoreCase)&&File.Exists(file);
        public static string UnsupportedHostReason(string executable)
        {
            if(string.IsNullOrEmpty(executable))return null;
            var folder=Path.GetDirectoryName(Path.GetFullPath(executable));
            bool store=folder.Split(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar).Any(part=>part.Equals("WindowsApps",StringComparison.OrdinalIgnoreCase));
            if(store||File.Exists(Path.Combine(folder,"portable.lock")))
                return "Portable and Microsoft Store QuickLook installations use a different plugin location. Install the .qlplugin package through QuickLook instead, or use the standard desktop version.";
            return null;
        }
        public static string Within(string directory,string boundary)
        {
            var full=Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);var parent=Path.GetFullPath(boundary).TrimEnd(Path.DirectorySeparatorChar);
            if(!full.Equals(parent,StringComparison.OrdinalIgnoreCase)&&!full.StartsWith(parent+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("Path is outside the installer directory.");
            return full;
        }
        static void PlainDirectory(string directory,string boundary,bool create)
        {
            string full=Within(directory,boundary),parent=Within(boundary,boundary);var current=parent;
            foreach(var part in new[]{""}.Concat(full.Substring(parent.Length).Split(new[]{Path.DirectorySeparatorChar},StringSplitOptions.RemoveEmptyEntries)))
            {
                if(part.Length>0)current=Path.Combine(current,part);
                if(Directory.Exists(current)&&(File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new IOException("Linked installation directories are not supported.");
                if(create&&!Directory.Exists(current))Directory.CreateDirectory(current);
            }
        }
        static void PlainFile(string file,string boundary)
        {
            Within(file,boundary);if(File.Exists(file)&&(File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)throw new IOException("Linked plugin files are not supported.");
        }
        static bool SameFile(string file,byte[] expected)
        {return File.Exists(file)&&new FileInfo(file).Length==expected.LongLength&&Payload.Hash(File.ReadAllBytes(file))==Payload.Hash(expected);}
        static bool Matching(Process process,string executable)=>string.Equals(ProcessPath(process),executable,StringComparison.OrdinalIgnoreCase);
        static bool Running(string executable)
        {foreach(var p in Process.GetProcessesByName("QuickLook"))using(p){if(Matching(p,executable))return true;}return false;}
        static void StopQuickLook(string executable)
        {
            foreach(var p in Process.GetProcessesByName("QuickLook"))using(p)
            {
                if(!Matching(p,executable))continue;
                try
                {
                    DateTime started=p.StartTime;p.CloseMainWindow();if(p.WaitForExit(1500))continue;
                    p.Refresh();if(!Matching(p,executable)||p.StartTime!=started)throw new IOException("QuickLook process identity changed; close QuickLook and try again.");
                    p.Kill();if(!p.WaitForExit(5000))throw new IOException("QuickLook is still running. Close it before updating.");
                }
                catch(InvalidOperationException){if(!p.HasExited)throw;}
            }
        }
        static void StartQuickLook(string executable)
        {
            if(!Running(executable))Process.Start(new ProcessStartInfo(executable){UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(executable),CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});
        }
        static void BackupTree(string source,string target,string sourceBoundary,string backupBoundary)
        {
            PlainDirectory(source,sourceBoundary,false);PlainDirectory(target,backupBoundary,true);
            foreach(var file in Directory.GetFiles(source))
            {PlainFile(file,sourceBoundary);File.Copy(file,Path.Combine(target,Path.GetFileName(file)),false);}
            foreach(var folder in Directory.GetDirectories(source))BackupTree(folder,Path.Combine(target,Path.GetFileName(folder)),sourceBoundary,backupBoundary);
        }
        public static InstallResult Install(VerifiedPayload payload,string executable,Action<string> progress)
        {
            if(payload==null||!IsQuickLookExecutable(executable))throw new InvalidOperationException("Install QuickLook first, then reopen this installer.");
            var unsupported=UnsupportedHostReason(executable);if(unsupported!=null)throw new InvalidOperationException(unsupported);
            // This method is called only by the explicit Install / Update button.
            executable=Path.GetFullPath(executable);var appData=Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);var localData=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string target=TargetDirectory;PlainDirectory(target,appData,false);PlainDirectory(WorkRoot,localData,true);
            var stage=Path.Combine(WorkRoot,"Staging",Guid.NewGuid().ToString("N"));PlainDirectory(stage,WorkRoot,true);
            var changed=new List<string>();var touched=new List<string>();var result=new InstallResult();bool restart=false,installed=false;
            try
            {
                progress?.Invoke("Checking the installed files …");
                foreach(var pair in payload.Files)
                {
                    if(!Payload.IsSafeName(pair.Key))throw new InvalidDataException("Unexpected payload filename.");
                    string destination=Path.Combine(target,pair.Key);PlainFile(destination,target);
                    if(SameFile(destination,pair.Value)){result.Unchanged++;continue;}
                    File.WriteAllBytes(Path.Combine(stage,pair.Key),pair.Value);changed.Add(pair.Key);
                }
                if(changed.Count>0)
                {
                    if(Directory.Exists(target))
                    {
                        progress?.Invoke("Backing up the existing DICOM RT plugin …");
                        result.BackupPath=Path.Combine(WorkRoot,"Backups",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8));
                        BackupTree(target,result.BackupPath,target,WorkRoot);
                    }
                    progress?.Invoke("Restarting QuickLook and updating DICOM RT …");
                    restart=Running(executable);StopQuickLook(executable);PlainDirectory(target,appData,true);
                    foreach(var name in changed)
                    {
                        var destination=Path.Combine(target,name);PlainFile(destination,target);touched.Add(name);
                        File.Copy(Path.Combine(stage,name),destination,true);
                        if(!SameFile(destination,payload.Files[name]))throw new IOException("Installed file verification failed.");
                        result.Updated++;
                    }
                }
                installed=true;restart=true;return result;
            }
            catch(Exception failure)
            {
                // Roll back only filenames this install touched, never another plugin's directory.
                var restoreFailures=new List<Exception>();
                foreach(var name in touched.AsEnumerable().Reverse())
                {
                    try
                    {
                        string destination=Path.Combine(target,name);PlainFile(destination,target);
                        var backup=result.BackupPath==null?null:Path.Combine(result.BackupPath,name);
                        if(backup!=null&&File.Exists(backup))File.Copy(backup,destination,true);else if(File.Exists(destination))File.Delete(destination);
                    }
                    catch(Exception error){restoreFailures.Add(error);}
                }
                if(restoreFailures.Count>0)
                {
                    restart=false;
                    throw new IOException("Some files could not be restored. QuickLook has been left stopped. Restore the backup before restarting it. The backup remains at "+result.BackupPath,new AggregateException(new[]{failure}.Concat(restoreFailures)));
                }
                throw;
            }
            finally
            {
                try{PlainDirectory(stage,WorkRoot,false);foreach(var name in changed){var file=Path.Combine(stage,name);if(File.Exists(file))File.Delete(file);}if(Directory.Exists(stage))Directory.Delete(stage,false);}catch(IOException){}catch(UnauthorizedAccessException){}
                if(restart)
                {
                    try{StartQuickLook(executable);}catch(Exception){if(installed)result.RestartWarning="Installation verified. Start QuickLook manually to load the plugin.";}
                }
            }
        }
    }
}
