using System.Text.Json;
using QuickLook.DicomRT;

// Private pipes only. No web server, persistent patient cache or telemetry.
var session = new PreviewSession();
while (Console.ReadLine() is string line)
{
    int id = 0;
    try
    {
        using var doc = JsonDocument.Parse(line);
        var request = doc.RootElement;
        id = request.GetProperty("id").GetInt32();
        object result = session.Execute(request.GetProperty("op").GetString(), request);
        Console.WriteLine(JsonSerializer.Serialize(new { id, result }));
    }
    catch (Exception ex)
    {
        // Exceptions can contain private paths/UIDs. Only expose a fixed message.
        string error = ex is NotSupportedException ? "Diese DICOM-Daten oder Geometrie werden nicht unterstützt."
            : ex is UnauthorizedAccessException || ex is IOException ? "Dateizugriff fehlgeschlagen. Bitte den DICOM-Ordner freigeben."
            : "Vorschau nicht verfügbar. Datei, Zuordnung oder Geometrie prüfen.";
        Console.WriteLine(JsonSerializer.Serialize(new { id, error }));
    }
}
