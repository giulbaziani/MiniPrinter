using System.IO;
using System.Text.Json;

namespace MiniPrinter.Templates;

public static class TemplateStore
{
    private static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MiniPrinter");
    private static string FilePath => Path.Combine(Dir, "templates.json");

    public static List<SavedTemplate> Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<List<SavedTemplate>>(File.ReadAllText(FilePath)) ?? [];
        }
        catch { /* empty */ }
        return [];
    }

    public static void Save(IEnumerable<SavedTemplate> templates)
    {
        Directory.CreateDirectory(Dir);
        var user = templates.Where(t => !t.IsBuiltIn).ToList();
        File.WriteAllText(FilePath, JsonSerializer.Serialize(user, new JsonSerializerOptions { WriteIndented = true }));
    }
}
