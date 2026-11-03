using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace QuickLookRT.Mac;
public static class NativeBridge
{
    static readonly ConcurrentDictionary<string, PreviewSession> sessions = new();
    [UnmanagedCallersOnly]
    public static nint Invoke(nint request)
    {
        int id=0;string response;
        try {
            using var doc=JsonDocument.Parse(Marshal.PtrToStringUTF8(request));
            var r=doc.RootElement;
            id=r.GetProperty("id").GetInt32();
            string key=r.GetProperty("session").GetString(),op=r.GetProperty("op").GetString();
            if(op=="close") {if(sessions.TryRemove(key,out var removed))removed.Cancel();response=JsonSerializer.Serialize(new{id,result=true});}
            else {
                var session=sessions.GetOrAdd(key,_=>new PreviewSession());object value;
                if(op=="progress")value=session.Progress();
                else if(op=="cancelWork")value=session.CancelWork(r);
                else if(op is "mesh" or "beam" or "dvh" or "volume" or "doseReference" or "render" or "fields") {PreviewSession snapshot;lock(session.Gate)snapshot=session.WorkSnapshot(r);value=snapshot.Execute(op,r);}
                else lock(session.Gate)value=session.Execute(op,r);
                response=JsonSerializer.Serialize(new{id,result=value});
            }
        } catch(Exception ex) {
            string message=ex is OperationCanceledException?"Vorschau beendet."
                :ex is NotSupportedException?"DICOM-Daten oder Geometrie nicht unterstützt."
                :ex is UnauthorizedAccessException||ex is IOException?"Dateizugriff fehlgeschlagen. Bitte den DICOM-Ordner freigeben."
                :"Vorschau nicht verfügbar. Datei, Zuordnung oder Geometrie prüfen.";
            response=JsonSerializer.Serialize(new{id,error=message,cancelled=ex is OperationCanceledException});
        }
        return Marshal.StringToCoTaskMemUTF8(response);
    }
    [UnmanagedCallersOnly]
    public static void Release(nint response)=>Marshal.FreeCoTaskMem(response);
}
