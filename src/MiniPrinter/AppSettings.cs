using System.IO;
using System.Text.Json;

namespace MiniPrinter;

public sealed class AppSettings
{
    public string Language { get; set; } = "en";
    public string LastTemplateId { get; set; } = "plain";
    public double WidthMm { get; set; } = 57;
    public double HeightMm { get; set; } = 30;

    private static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MiniPrinter");
    private static string PathFile => Path.Combine(Dir, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(PathFile))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(PathFile)) ?? new AppSettings();
        }
        catch { /* first run */ }
        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(PathFile, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
