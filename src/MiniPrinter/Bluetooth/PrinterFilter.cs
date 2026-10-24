using MiniPrinter.Printing;

namespace MiniPrinter.Bluetooth;

public static class PrinterFilter
{
    private static readonly string[] NameHints =
    [
        "print", "printer", "orico", "s1", "s1_", "mini", "luck", "jingle", "luckp",
        "marklife", "phomemo", "peri", "page", "paperang", "niim", "cat", "yhk",
        "gt01", "gt02", "gb01", "gb02", "gb03", "mxw", "mx0", "m02", "m110", "m220",
        "d11", "d110", "p12", "p15", "p50", "p80", "thermal", "label", "sticker",
        "tsp", "walkprint", "tiny", "peripage", "barcode", "lp15", "lp90", "l13",
        "dps1", "itp", "mint", "poooli", "printmaster"
    ];

    private static readonly Guid[] PrinterServices =
    [
        BleUuids.Ff00, BleUuids.Fd00, BleUuids.IsscService, BleUuids.GenericPrinter,
        BleUuids.CatService, BleUuids.CatServiceAlt,
        Guid.Parse("e7810a71-73ae-499d-8c15-faa9aef0c3f2")
    ];

    public static bool IsLikelyPrinter(string name, IEnumerable<Guid>? services, bool isSerial)
    {
        if (isSerial) return false;
        if (services is not null && services.Any(s => PrinterServices.Contains(s)))
            return true;
        if (string.IsNullOrWhiteSpace(name)) return false;
        var n = name.Trim().ToLowerInvariant();
        if (n.StartsWith("s1") || n.Contains("s1_")) return true;
        return NameHints.Any(h => n.Contains(h));
    }
}
