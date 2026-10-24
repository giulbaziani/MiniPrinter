namespace MiniPrinter.Bluetooth;

public interface IPrinterTransport : IAsyncDisposable
{
    IReadOnlyList<Guid> Services { get; }
    Task WriteAsync(byte[] data, int chunkSize, int delayMs, CancellationToken ct);
}
