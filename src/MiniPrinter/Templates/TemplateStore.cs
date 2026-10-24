using System.IO;
using System.Text.Json;

namespace MiniPrinter.Templates;

public static class TemplateStore
{
    public static string DirectoryPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MiniPrinter");

    public static string ImagesPath => Path.Combine(DirectoryPath, "images");
    private static string FilePath => Path.Combine(DirectoryPath, "templates.json");

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
        Directory.CreateDirectory(DirectoryPath);
        var user = templates.Where(t => !t.IsBuiltIn).ToList();
        File.WriteAllText(FilePath, JsonSerializer.Serialize(user, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static string? CopyImage(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath)) return null;
        Directory.CreateDirectory(ImagesPath);
        var name = Guid.NewGuid().ToString("N") + Path.GetExtension(sourcePath);
        var dest = Path.Combine(ImagesPath, name);
        File.Copy(sourcePath, dest, true);
        return name;
    }

    public static string? ResolveImage(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;
        var path = Path.Combine(ImagesPath, fileName);
        return File.Exists(path) ? path : null;
    }
}
