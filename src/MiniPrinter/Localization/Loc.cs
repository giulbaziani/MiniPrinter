using System.IO;
using System.Reflection;
using System.Text.Json;
using MiniPrinter.Printing;

namespace MiniPrinter.Localization;

public static class Loc
{
    private static Dictionary<string, string> _en = [];
    private static Dictionary<string, string> _active = [];

    public static event Action? Changed;
    public static string Language { get; private set; } = "en";
    public static bool IsGeorgian => Language == "ka";

    static Loc()
    {
        _en = Load("en");
        _active = _en;
    }

    public static void Set(string language)
    {
        Language = language is "ka" or "en" ? language : "en";
        _active = Language == "en" ? _en : Load(Language);
        Changed?.Invoke();
    }

    public static string T(string key, params object[] args)
    {
        if (!_active.TryGetValue(key, out var value) && !_en.TryGetValue(key, out value))
            value = key;
        return args.Length == 0 ? value : string.Format(value, args);
    }

    public static string Error(ErrorCode code, params object[] args)
    {
        var id = AppException.FormatCode(code);
        var message = T($"error.{(int)code}", args);
        return T("error.format", id, message);
    }

    public static string FromException(Exception ex) => ex switch
    {
        AppException app => app.Display(),
        OperationCanceledException => Error(ErrorCode.OperationCancelled),
        UnauthorizedAccessException => Error(ErrorCode.SerialOpenFailed, ""),
        IOException => Error(ErrorCode.IoFailed),
        _ => Error(ErrorCode.Unknown)
    };

    public static string Gatt(object status)
    {
        var key = "gatt." + status;
        var text = T(key);
        return text == key ? status.ToString() ?? "" : text;
    }

    public static string Protocol(PrintProtocolKind kind) => T("proto.kind." + kind);

    private static Dictionary<string, string> Load(string language)
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = $"{asm.GetName().Name}.Localization.{language}.json";
        using var stream = asm.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Missing localization resource {name}");
        using var reader = new StreamReader(stream);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(reader.ReadToEnd()) ?? [];
    }
}
