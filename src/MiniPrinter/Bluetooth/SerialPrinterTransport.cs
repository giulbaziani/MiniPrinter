using System.IO.Ports;
using MiniPrinter.Localization;

namespace MiniPrinter.Bluetooth;

public sealed class SerialPrinterTransport : IPrinterTransport
{
    private readonly SerialPort _port;
    private readonly string _comPort;

    public SerialPrinterTransport(string comPort)
    {
        _comPort = comPort;
        _port = new SerialPort(comPort, 115200, Parity.None, 8, StopBits.One)
        {
            WriteTimeout = 8000,
            ReadTimeout = 2000
        };
        try
        {
            _port.Open();
        }
        catch (Exception ex)
        {
            throw new AppException(ErrorCode.SerialOpenFailed, ex, comPort);
        }
    }

    public IReadOnlyList<Guid> Services { get; } = [];

    public async Task WriteAsync(byte[] data, int chunkSize, int delayMs, CancellationToken ct)
    {
        try
        {
            for (var i = 0; i < data.Length; i += chunkSize)
            {
                ct.ThrowIfCancellationRequested();
                var n = Math.Min(chunkSize, data.Length - i);
                _port.Write(data, i, n);
                if (delayMs > 0)
                    await Task.Delay(delayMs, ct);
            }
        }
        catch (OperationCanceledException)
        {
            throw new AppException(ErrorCode.OperationCancelled);
        }
        catch (Exception ex) when (ex is not AppException)
        {
            throw new AppException(ErrorCode.SerialWriteFailed, ex, _comPort);
        }
    }

    public ValueTask DisposeAsync()
    {
        try { _port.Close(); } catch { /* ignore */ }
        _port.Dispose();
        return ValueTask.CompletedTask;
    }
}
