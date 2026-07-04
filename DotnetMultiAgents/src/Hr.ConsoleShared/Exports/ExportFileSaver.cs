using System.Text.Json;

namespace Hr.ConsoleShared.Exports;

public static class ExportFileSaver
{
    public static string? TrySaveExportFile(string json, string outputFolder)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("fileName", out var fileNameEl) ||
                !root.TryGetProperty("content", out var contentEl))
                return null;

            var fileName = fileNameEl.GetString();
            var base64 = contentEl.GetString();
            if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(base64))
                return null;

            var bytes = Convert.FromBase64String(base64);
            Directory.CreateDirectory(outputFolder);
            var fullPath = Path.GetFullPath(Path.Combine(outputFolder, fileName));
            File.WriteAllBytes(fullPath, bytes);
            return $"Saved to: {fullPath}";
        }
        catch (Exception ex)
        {
            return $"Export save failed: {ex.Message}";
        }
    }
}
