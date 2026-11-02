using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using QuickLook.DicomRT.Installer;
using Microsoft.Win32;

class Program
{
    static int checks;
    static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
    static byte[] Archive(string extra=null,bool wrongHash=false,bool omit=false,bool duplicate=false,string version=Payload.Version)
    {
        var names=Payload.RequiredNames.Where(n=>!omit||n!="QuickLook.Plugin.DicomRT.dll").ToArray();
        var files=names.ToDictionary(n=>n,n=>Encoding.UTF8.GetBytes("synthetic installer test: "+n));
        var manifest=new PayloadManifest{version=version,files=files.Select(f=>new PayloadFile{name=f.Key,bytes=f.Value.Length,sha256=Payload.Hash(f.Value)}).ToArray()};
        if(wrongHash)manifest.files[0].sha256=new string('0',64);
        using(var output=new MemoryStream())
        {
            using(var zip=new ZipArchive(output,ZipArchiveMode.Create,true))
            {
                Action<string,byte[]> add=(name,bytes)=>{using(var stream=zip.CreateEntry(name).Open())stream.Write(bytes,0,bytes.Length);};
                foreach(var file in files)add(file.Key,file.Value);
                add("manifest.json",Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(manifest)));
                if(extra!=null)add(extra,new byte[]{1,2,3});
                if(duplicate)add("MANIFEST.JSON",new byte[]{1});
            }
            return output.ToArray();
        }
    }
    static void Reject(byte[] bytes,string label,string hash=null)
    {
        bool rejected=false;try{using(var source=new MemoryStream(bytes))Payload.Verify(source,hash??Payload.Hash(bytes));}catch(InvalidDataException){rejected=true;}Check(rejected,label);
    }
    static int Main(string[] args)
    {
        try
        {
            ExplorerMenuChecks();
            var good=Archive();using(var source=new MemoryStream(good))Check(Payload.Verify(source,Payload.Hash(good)).Files.Count==Payload.RequiredNames.Length+1,"complete allowlisted archive accepted");
            Reject(good,"whole archive checksum rejects tampering",new string('0',64));
            Reject(Archive(wrongHash:true),"per-file hash mismatch rejected");Reject(Archive(omit:true),"missing required plugin rejected");
            Reject(Archive(duplicate:true),"case-insensitive duplicate rejected");Reject(Archive(version:"9.9.9"),"unexpected package version rejected");
            foreach(var name in new[]{"../QuickLook.Plugin.DicomRT.dll","sub/QuickLook.Plugin.DicomRT.dll","sub\\QuickLook.Plugin.DicomRT.dll","C:\\QuickLook.Plugin.DicomRT.dll","QuickLook.Plugin.DicomRT.dll:stream","evil.exe","README.md ","README.md.","QuickLook.Plugin.MIQ.dll"})
            {Check(!Payload.IsSafeName(name),"unsafe name rejected");Reject(Archive(extra:name),"unsafe archive member rejected");}
            Check(InstallEngine.Within(@"C:\safe\own\file",@"C:\safe\own")==@"C:\safe\own\file","owned directory accepted");
            foreach(var candidate in new[]{@"C:\safe\other",@"C:\safe\own-other",@"C:\safe\own\..\other"})
            {bool rejected=false;try{InstallEngine.Within(candidate,@"C:\safe\own");}catch(IOException){rejected=true;}Check(rejected,"directory escape rejected");}
            Check(InstallEngine.UnsupportedHostReason(@"C:\Program Files\WindowsApps\QuickLook\QuickLook.exe")!=null,"Store host rejected before mutation");
            Check(InstallEngine.UnsupportedHostReason(@"C:\Program Files\QuickLook\QuickLook.exe")==null,"standard desktop host accepted");
            var fixture=Path.Combine(Path.GetTempPath(),"DicomRT-installer-test-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(fixture);
            try{File.WriteAllText(Path.Combine(fixture,"portable.lock"),"");Check(InstallEngine.UnsupportedHostReason(Path.Combine(fixture,"QuickLook.exe"))!=null,"portable host rejected before mutation");}
            finally{File.Delete(Path.Combine(fixture,"portable.lock"));Directory.Delete(fixture,false);}
            if(args.Length==1)
            {
                var bytes=File.ReadAllBytes(args[0]);using(var source=new MemoryStream(bytes)){var package=Payload.Verify(source,Payload.Hash(bytes));Check(package.Files.ContainsKey("QuickLook.Plugin.DicomRT.dll"),"current release archive verified");}
            }
            Console.WriteLine("PASS: "+checks+" installer archive/path/checksum/menu checks; registry tests use a disposable non-shell subtree; no installation or process changes performed.");return 0;
        }
        catch(Exception error){Console.Error.WriteLine("FAIL: "+error);return 1;}
    }

    static void ExplorerMenuChecks()
    {
        string folder=Path.Combine(Path.GetTempPath(),"QuickLook menu test & spaces "+Guid.NewGuid().ToString("N"));
        string host=Path.Combine(folder,"QuickLook.exe"), keyPath=@"Software\QuickLookDicomRT.Tests\"+Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(folder);File.WriteAllText(host,"synthetic fixture, never executed");
        try
        {
            Check(ExplorerIntegration.Command(host)=="\""+host+"\" \"%1\"","host path and selected filename quoted without a command shell");
            Check(ExplorerIntegration.Command(host,true)=="\""+host+"\" \"%V\"","folder background uses current directory");
            foreach(var invalid in new[]{"QuickLook.exe",@"C:QuickLook.exe",@"\QuickLook.exe",@"C:\Tools\cmd.exe","C:\\Tools\\\"bad\\QuickLook.exe","C:\\Tools\\\nQuickLook.exe"})
            {bool rejected=false;try{ExplorerIntegration.Command(invalid);}catch(ArgumentException){rejected=true;}Check(rejected,"invalid host command rejected");}
            using(var root=Registry.CurrentUser.CreateSubKey(keyPath))
            {
                using(var other=root.CreateSubKey(@"*\shell\OtherViewer\command"))other.SetValue("","keep me");
                using(var association=root.CreateSubKey(".dcm"))association.SetValue("","ExistingViewer");
                using(var previous=root.CreateSubKey(@"*\shell\QuickLookRT\command"))previous.SetValue("","old host");
                ExplorerIntegration.RegisterAt(root,host);ExplorerIntegration.RegisterAt(root,host);
                foreach(var path in new[]{@"*\shell\QuickLookRT",@"Directory\shell\QuickLookRT",@"Directory\Background\shell\QuickLookRT"})
                {
                    using(var verb=root.OpenSubKey(path))
                    {Check((string)verb.GetValue("")==ExplorerIntegration.Label,"menu title registered");Check((string)verb.GetValue("Icon")=="\""+host+"\",0","host icon registered");Check((string)verb.GetValue("MultiSelectModel")=="Single","one preview per invocation");}
                    using(var command=root.OpenSubKey(path+@"\command"))Check((string)command.GetValue("")==ExplorerIntegration.Command(host,path.Contains(@"\Background\")),"menu command survives repeat install");
                }
                using(var other=root.OpenSubKey(@"*\shell\OtherViewer\command"))Check((string)other.GetValue("")=="keep me","other menu untouched");
                using(var association=root.OpenSubKey(".dcm"))Check((string)association.GetValue("")=="ExistingViewer","default association untouched");
                bool missing=false;try{ExplorerIntegration.RegisterAt(root,Path.Combine(folder,"missing","QuickLook.exe"));}catch(FileNotFoundException){missing=true;}Check(missing,"missing host rejected before registration");
            }
        }
        finally{Registry.CurrentUser.DeleteSubKeyTree(keyPath,false);File.Delete(host);Directory.Delete(folder,false);}
    }
}
