using MiniPrinter.Printing.Protocols;

namespace MiniPrinter.Printing;

public static class ProtocolFactory
{
    public static IPrintProtocol Resolve(PrintProtocolKind kind, IReadOnlyList<Guid> services)
    {
        if (kind != PrintProtocolKind.Auto)
            return Create(kind);

        if (services.Any(s => s == BleUuids.CatService || s == BleUuids.CatServiceAlt))
            return new CatPrinterProtocol();
        if (services.Any(s => s == BleUuids.Ff00 || s == BleUuids.Fd00))
            return new LuckJingleProtocol();
        if (services.Any(s => s == BleUuids.IsscService || s == BleUuids.GenericPrinter))
            return new EscPosProtocol();
        return new LuckJingleProtocol();
    }

    public static IPrintProtocol Create(PrintProtocolKind kind) => kind switch
    {
        PrintProtocolKind.CatPrinter => new CatPrinterProtocol(),
        PrintProtocolKind.Tspl => new TsplProtocol(),
        PrintProtocolKind.EscPos => new EscPosProtocol(),
        _ => new LuckJingleProtocol()
    };
}

public static class BleUuids
{
    public static readonly Guid Ff00 = Guid.Parse("0000ff00-0000-1000-8000-00805f9b34fb");
    public static readonly Guid Ff01 = Guid.Parse("0000ff01-0000-1000-8000-00805f9b34fb");
    public static readonly Guid Ff02 = Guid.Parse("0000ff02-0000-1000-8000-00805f9b34fb");
    public static readonly Guid Ff03 = Guid.Parse("0000ff03-0000-1000-8000-00805f9b34fb");
    public static readonly Guid Fd00 = Guid.Parse("0000fd00-0000-1000-8000-00805f9b34fb");
    public static readonly Guid Fd01 = Guid.Parse("0000fd01-0000-1000-8000-00805f9b34fb");
    public static readonly Guid Fd02 = Guid.Parse("0000fd02-0000-1000-8000-00805f9b34fb");
    public static readonly Guid IsscService = Guid.Parse("49535343-fe7d-4ae5-8fa9-9fafd205e455");
    public static readonly Guid IsscWrite = Guid.Parse("49535343-8841-43f4-a8d4-ecbe34729bb3");
    public static readonly Guid IsscNotify = Guid.Parse("49535343-1e4d-4bd9-ba61-23c647249616");
    public static readonly Guid GenericPrinter = Guid.Parse("000018f0-0000-1000-8000-00805f9b34fb");
    public static readonly Guid GenericWrite = Guid.Parse("00002af1-0000-1000-8000-00805f9b34fb");
    public static readonly Guid GenericNotify = Guid.Parse("00002af0-0000-1000-8000-00805f9b34fb");
    public static readonly Guid CatService = Guid.Parse("0000ae30-0000-1000-8000-00805f9b34fb");
    public static readonly Guid CatServiceAlt = Guid.Parse("0000ae3a-0000-1000-8000-00805f9b34fb");
    public static readonly Guid CatWrite = Guid.Parse("0000ae01-0000-1000-8000-00805f9b34fb");
    public static readonly Guid CatNotify = Guid.Parse("0000ae02-0000-1000-8000-00805f9b34fb");
}
