using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace QuickLook.DicomRT.Installer
{
    public sealed class PayloadManifest { public string version {get;set;} public PayloadFile[] files {get;set;} }
    public sealed class PayloadFile { public string name {get;set;} public string sha256 {get;set;} public long bytes {get;set;} }
    public sealed class VerifiedPayload
    {
        public string ArchiveHash {get;internal set;}
        public Dictionary<string,byte[]> Files {get;internal set;}
    }
    public static class Payload
    {
        public const string Version="0.2.7";
        const long MaxFileBytes=32L*1024*1024, MaxPackageBytes=64L*1024*1024;
        public static readonly string[] RequiredNames={"Dicom.Core.dll","QuickLook.DicomRT.Core.dll","QuickLook.DicomRT.Rt.dll","QuickLook.DicomRT.Viewer.dll","QuickLook.Plugin.DicomRT.dll","QuickLook.Plugin.Metadata.config","README.md","THIRD_PARTY.md","fo-dicom-MS-PL.html","Cyotek.Drawing.BitmapFont.dll","HelixToolkit.dll","HelixToolkit.Wpf.SharpDX.dll","Microsoft.Extensions.Logging.Abstractions.dll","SharpDX.dll","SharpDX.D3DCompiler.dll","SharpDX.Direct2D1.dll","SharpDX.Direct3D11.dll","SharpDX.Direct3D9.dll","SharpDX.DXGI.dll","SharpDX.Mathematics.dll","System.Buffers.dll","System.Memory.dll","System.Numerics.Vectors.dll","System.Runtime.CompilerServices.Unsafe.dll","HelixToolkit-LICENSE.txt","SharpDX-LICENSE.txt","Cyotek-LICENSE.txt","Microsoft-Logging-LICENSE.txt","Microsoft-Runtime-LICENSE.txt","Microsoft-Runtime-NOTICES.txt","Microsoft-Logging-NOTICES.txt"};
        static readonly HashSet<string> Allowed=new HashSet<string>(RequiredNames.Concat(new[]{"Dicom.Native.dll","Dicom.Native64.dll","manifest.json"}),StringComparer.OrdinalIgnoreCase);
        public static string Hash(byte[] data){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(data)).Replace("-","");}
        public static bool IsSafeName(string name)
        {
            return !string.IsNullOrEmpty(name)&&name==name.Trim()&&name.IndexOfAny(new[]{'/','\\',':','\0'})<0&&
                name==Path.GetFileName(name)&&!name.EndsWith(".",StringComparison.Ordinal)&&Allowed.Contains(name);
        }
        static byte[] ReadLimited(Stream stream,long limit)
        {
            using(var output=new MemoryStream())
            {
                var buffer=new byte[81920];int count;
                while((count=stream.Read(buffer,0,buffer.Length))>0){if(output.Length+count>limit)throw new InvalidDataException("Payload exceeds its size limit.");output.Write(buffer,0,count);}
                return output.ToArray();
            }
        }
        static bool IsHash(string value)=>value!=null&&value.Length==64&&value.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f'||c>='A'&&c<='F');
        public static VerifiedPayload Verify(Stream source,string expectedArchiveHash)
        {
            if(source==null||!IsHash(expectedArchiveHash))throw new InvalidDataException("Missing payload or embedded checksum.");
            var archiveBytes=ReadLimited(source,MaxPackageBytes);string actualHash=Hash(archiveBytes);
            if(!string.Equals(actualHash,expectedArchiveHash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Embedded archive checksum mismatch.");
            var files=new Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase);long total=0;
            using(var bytes=new MemoryStream(archiveBytes,false))using(var zip=new ZipArchive(bytes,ZipArchiveMode.Read,false))
            {
                foreach(var entry in zip.Entries)
                {
                    if(!IsSafeName(entry.FullName)||files.ContainsKey(entry.FullName))throw new InvalidDataException("Unsafe, duplicate, or unexpected archive filename.");
                    if(entry.Length<0||entry.Length>MaxFileBytes||(total+=entry.Length)>MaxPackageBytes)throw new InvalidDataException("Payload exceeds its size limit.");
                    using(var content=entry.Open()){var data=ReadLimited(content,MaxFileBytes);if(data.LongLength!=entry.Length)throw new InvalidDataException("Archive entry length mismatch.");files.Add(entry.FullName,data);}
                }
            }
            byte[] manifestBytes;
            if(!files.TryGetValue("manifest.json",out manifestBytes)||manifestBytes.Length>1024*1024)throw new InvalidDataException("Missing or oversized payload manifest.");
            var manifest=new JavaScriptSerializer().Deserialize<PayloadManifest>(Encoding.UTF8.GetString(manifestBytes).TrimStart('\uFEFF'));
            if(manifest==null||manifest.version!=Version||manifest.files==null)throw new InvalidDataException("Unexpected payload version or manifest.");
            var listed=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var item in manifest.files)
            {
                byte[] data;
                if(item==null||!IsSafeName(item.name)||item.name.Equals("manifest.json",StringComparison.OrdinalIgnoreCase)||!listed.Add(item.name)||!IsHash(item.sha256)||!files.TryGetValue(item.name,out data))throw new InvalidDataException("Invalid payload manifest entry.");
                if(data.LongLength!=item.bytes||!string.Equals(Hash(data),item.sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Payload file checksum or length mismatch.");
            }
            if(listed.Count!=files.Count-1||RequiredNames.Any(name=>!listed.Contains(name)))throw new InvalidDataException("Incomplete or unlisted payload files.");
            return new VerifiedPayload{ArchiveHash=actualHash,Files=files};
        }
    }
}
