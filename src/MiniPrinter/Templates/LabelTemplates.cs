using MiniPrinter.Localization;
using MiniPrinter.Printing;

namespace MiniPrinter.Templates;

public enum LayoutKind
{
    Plain, TitleBody, Price, Name, Address, Qr, Barcode, Date, Fragile, Inventory, Custom
}

public sealed class SavedTemplate
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public bool IsBuiltIn { get; set; }
    public LayoutKind Layout { get; set; } = LayoutKind.Plain;
    public double WidthMm { get; set; } = 57;
    public double HeightMm { get; set; } = 30;
    public string Body { get; set; } = "";
    public int PaperIndex { get; set; }
    public int Density { get; set; } = 3;
    public string Icon { get; set; } = "\uE8A5";
    public string? ImageFileName { get; set; }
    public int FontSize { get; set; } = 20;
    public string? BodyXaml { get; set; }
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public bool Underline { get; set; }
}

public static class LabelTemplates
{
    public static SavedTemplate[] BuiltIn { get; } =
    [
        Built("plain", LayoutKind.Plain, 57, 30, "\uE8A5"),
        Built("title", LayoutKind.TitleBody, 57, 30, "\uE8D2"),
        Built("price", LayoutKind.Price, 57, 40, "\uE8C7"),
        Built("name", LayoutKind.Name, 57, 40, "\uE77B"),
        Built("address", LayoutKind.Address, 57, 50, "\uE707"),
        Built("qr", LayoutKind.Qr, 57, 57, "\uED14"),
        Built("barcode", LayoutKind.Barcode, 57, 35, "\uE71A"),
        Built("date", LayoutKind.Date, 57, 30, "\uE787"),
        Built("fragile", LayoutKind.Fragile, 57, 40, "\uE7BA"),
        Built("inventory", LayoutKind.Inventory, 57, 40, "\uE14C")
    ];

    public static string TitleKey(LayoutKind k) => k switch
    {
        LayoutKind.TitleBody => "t.title",
        LayoutKind.Price => "t.price",
        LayoutKind.Name => "t.name",
        LayoutKind.Address => "t.address",
        LayoutKind.Qr => "t.qr",
        LayoutKind.Barcode => "t.barcode",
        LayoutKind.Date => "t.date",
        LayoutKind.Fragile => "t.fragile",
        LayoutKind.Inventory => "t.inventory",
        LayoutKind.Custom => "t.custom",
        _ => "t.plain"
    };

    public static string HintKey(LayoutKind k) => TitleKey(k) + ".h";

    public static LabelContent Build(LayoutKind kind, string text)
    {
        var parts = Split(text);
        var pipe = Split(text, '|');
        return kind switch
        {
            LayoutKind.TitleBody => new LabelContent { Title = parts[0], Body = parts[1], TitleSize = 26, BodySize = 16 },
            LayoutKind.Price => new LabelContent { Title = pipe[0], Body = pipe[1], TitleSize = 20, BodySize = 36, BoldTitle = true },
            LayoutKind.Name => new LabelContent { Title = parts[0], Body = parts[1], Footer = DateTime.Now.ToString("dd.MM.yyyy") },
            LayoutKind.Address => new LabelContent { Title = Loc.T("address.title"), Body = text, Align = System.Windows.TextAlignment.Left, BodySize = 16 },
            LayoutKind.Qr => new LabelContent
            {
                Title = parts[0].Length > 40 ? "QR" : parts[0],
                QrPayload = string.IsNullOrWhiteSpace(parts[1]) ? parts[0] : parts[1],
                BodySize = 14
            },
            LayoutKind.Barcode => new LabelContent
            {
                Title = parts[0],
                BarcodePayload = string.IsNullOrWhiteSpace(parts[1]) ? parts[0] : parts[1]
            },
            LayoutKind.Date => new LabelContent
            {
                Title = string.IsNullOrWhiteSpace(text) ? Loc.T("date.title") : text,
                Body = DateTime.Now.ToString("dd.MM.yyyy HH:mm"),
                Footer = Loc.T("app.name")
            },
            LayoutKind.Fragile => new LabelContent
            {
                Title = Loc.T("fragile.title"),
                Body = string.IsNullOrWhiteSpace(text) ? Loc.T("fragile.body") : text,
                TitleSize = 28
            },
            LayoutKind.Inventory => new LabelContent { Title = pipe[1], BarcodePayload = pipe[0], BodySize = 16 },
            _ => new LabelContent { Body = text, BodySize = 22, Align = System.Windows.TextAlignment.Left }
        };
    }

    private static SavedTemplate Built(string id, LayoutKind layout, double w, double h, string icon) => new()
    {
        Id = id,
        IsBuiltIn = true,
        Layout = layout,
        WidthMm = w,
        HeightMm = h,
        Icon = icon
    };

    private static string[] Split(string text, char sep = '\n')
    {
        var i = text.IndexOf(sep);
        if (i < 0) return [text.Trim(), ""];
        return [text[..i].Trim(), text[(i + 1)..].Trim()];
    }
}
