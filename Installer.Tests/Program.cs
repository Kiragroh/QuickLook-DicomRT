using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using QuickLook.DicomRT.Installer;

class Program
{
    static int checks;
    static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
    static byte[] Archive(string extra=null,bool wrongHash=false,bool omit=false,bool duplicate=false,string version="0.2.3")
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
            var good=Archive();using(var source=new MemoryStream(good))Check(Payload.Verify(source,Payload.Hash(good)).Files.Count==10,"complete allowlisted archive accepted");
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
            Console.WriteLine("PASS: "+checks+" installer archive/path/checksum checks; no installation or process changes performed.");return 0;
        }
        catch(Exception error){Console.Error.WriteLine("FAIL: "+error);return 1;}
    }
}
