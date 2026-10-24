using System.IO.Ports;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Enumeration;
using MiniPrinter.Localization;

namespace MiniPrinter.Bluetooth;

public sealed class DeviceScanner : IDisposable
{
    private BluetoothLEAdvertisementWatcher? _watcher;
    private readonly Dictionary<ulong, PrinterDeviceInfo> _found = [];

    public event Action<PrinterDeviceInfo>? DeviceFound;
    public bool IncludeSerialPorts { get; set; }

    public void Start()
    {
        Stop();
        _found.Clear();

        if (IncludeSerialPorts)
        {
            foreach (var port in SerialPort.GetPortNames().OrderBy(p => p))
            {
                DeviceFound?.Invoke(new PrinterDeviceInfo
                {
                    Id = "com:" + port,
                    Name = Loc.T("comPort", port),
                    IsSerial = true,
                    ComPort = port,
                    IsLikelyPrinter = false
                });
            }
        }

        _ = EnumeratePairedAsync();

        _watcher = new BluetoothLEAdvertisementWatcher
        {
            ScanningMode = BluetoothLEScanningMode.Active
        };
        _watcher.Received += OnAdvertisement;
        _watcher.Start();
    }

    public void Stop()
    {
        if (_watcher is null) return;
        _watcher.Received -= OnAdvertisement;
        try { _watcher.Stop(); } catch { /* ignore */ }
        _watcher = null;
    }

    private async Task EnumeratePairedAsync()
    {
        try
        {
            var selector = BluetoothLEDevice.GetDeviceSelector();
            var paired = await DeviceInformation.FindAllAsync(selector);
            foreach (var di in paired)
            {
                if (string.IsNullOrWhiteSpace(di.Name)) continue;
                var device = await BluetoothLEDevice.FromIdAsync(di.Id);
                if (device is null) continue;
                Add(new PrinterDeviceInfo
                {
                    Id = di.Id,
                    Name = di.Name,
                    BluetoothAddress = device.BluetoothAddress,
                    HasAdvertisedName = true,
                    IsLikelyPrinter = PrinterFilter.IsLikelyPrinter(di.Name, null, false)
                });
                device.Dispose();
            }
        }
        catch
        {
            // Bluetooth adapter missing
        }
    }

    private void OnAdvertisement(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
    {
        var local = args.Advertisement.LocalName;
        var services = args.Advertisement.ServiceUuids.ToList();
        var named = !string.IsNullOrWhiteSpace(local);
        if (!named && services.Count == 0 && args.Advertisement.DataSections.Count == 0)
            return;

        var name = named ? local : Loc.T("device.ble", args.BluetoothAddress.ToString("X"));
        Add(new PrinterDeviceInfo
        {
            Id = args.BluetoothAddress.ToString("X"),
            Name = name,
            BluetoothAddress = args.BluetoothAddress,
            HasAdvertisedName = named,
            ServiceUuids = services,
            IsLikelyPrinter = PrinterFilter.IsLikelyPrinter(name, services, false)
        });
    }

    private void Add(PrinterDeviceInfo info)
    {
        lock (_found)
        {
            if (!info.IsSerial && _found.TryGetValue(info.BluetoothAddress, out var existing))
            {
                if (info.IsLikelyPrinter) existing.IsLikelyPrinter = true;
                if (existing.HasAdvertisedName || !info.HasAdvertisedName)
                    return;
            }
            if (!info.IsSerial)
                _found[info.BluetoothAddress] = info;
        }
        DeviceFound?.Invoke(info);
    }

    public void Dispose() => Stop();
}
