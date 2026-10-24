namespace MiniPrinter.Bluetooth;

public sealed class PrinterDeviceInfo
{
    public required string Id { get; init; }
    public required string Name { get; set; }
    public ulong BluetoothAddress { get; init; }
    public bool IsSerial { get; init; }
    public bool HasAdvertisedName { get; init; }
    public bool IsLikelyPrinter { get; set; }
    public string? ComPort { get; init; }
    public List<Guid> ServiceUuids { get; init; } = [];
    public override string ToString() => Name;
}
