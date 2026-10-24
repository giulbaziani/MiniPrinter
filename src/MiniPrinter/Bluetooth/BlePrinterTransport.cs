using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;
using MiniPrinter.Localization;
using MiniPrinter.Printing;

namespace MiniPrinter.Bluetooth;

public sealed class BlePrinterTransport : IPrinterTransport
{
    private readonly BluetoothLEDevice _device;
    private readonly GattCharacteristic _write;
    private readonly GattCharacteristic? _credit;
    private int _credits = 4;
    private readonly SemaphoreSlim _creditGate = new(1, 1);

    private BlePrinterTransport(
        BluetoothLEDevice device,
        GattCharacteristic write,
        GattCharacteristic? credit,
        IReadOnlyList<Guid> services)
    {
        _device = device;
        _write = write;
        _credit = credit;
        Services = services;
    }

    public IReadOnlyList<Guid> Services { get; }

    public static async Task<BlePrinterTransport> ConnectAsync(ulong address, CancellationToken ct)
    {
        var device = await BluetoothLEDevice.FromBluetoothAddressAsync(address);
        if (device is null)
            throw new AppException(ErrorCode.BleDeviceNotFound);

        var result = await device.GetGattServicesAsync(BluetoothCacheMode.Uncached);
        if (result.Status != GattCommunicationStatus.Success)
            throw new AppException(ErrorCode.GattServicesFailed, Loc.Gatt(result.Status));

        var serviceIds = result.Services.Select(s => s.Uuid).ToList();
        var profile = PickProfile(result.Services);
        if (profile is null)
            throw new AppException(ErrorCode.GattProfileNotFound);

        var write = await GetChar(profile.Value.Service, profile.Value.Write);
        if (write is null)
            throw new AppException(ErrorCode.WriteCharacteristicMissing);

        GattCharacteristic? credit = null;
        if (profile.Value.Credit is { } creditUuid)
            credit = await GetChar(profile.Value.Service, creditUuid);

        if (profile.Value.Notify is { } notifyUuid)
        {
            var notify = await GetChar(profile.Value.Service, notifyUuid);
            if (notify is not null)
                try
                {
                    await notify.WriteClientCharacteristicConfigurationDescriptorAsync(
                        GattClientCharacteristicConfigurationDescriptorValue.Notify);
                }
                catch { /* some firmware rejects CCCD */ }
        }

        var transport = new BlePrinterTransport(device, write, credit, serviceIds);
        if (credit is not null)
        {
            try
            {
                await credit.WriteClientCharacteristicConfigurationDescriptorAsync(
                    GattClientCharacteristicConfigurationDescriptorValue.Notify);
            }
            catch { /* optional flow control */ }
            credit.ValueChanged += transport.OnCredit;
        }
        return transport;
    }

    private static async Task<GattCharacteristic?> GetChar(GattDeviceService service, Guid uuid)
    {
        var chars = await service.GetCharacteristicsForUuidAsync(uuid, BluetoothCacheMode.Uncached);
        return chars.Status == GattCommunicationStatus.Success ? chars.Characteristics.FirstOrDefault() : null;
    }

    private static (GattDeviceService Service, Guid Write, Guid? Notify, Guid? Credit)? PickProfile(
        IReadOnlyList<GattDeviceService> services)
    {
        foreach (var s in services)
        {
            if (s.Uuid == BleUuids.Ff00)
                return (s, BleUuids.Ff02, BleUuids.Ff01, BleUuids.Ff03);
            if (s.Uuid == BleUuids.CatService || s.Uuid == BleUuids.CatServiceAlt)
                return (s, BleUuids.CatWrite, BleUuids.CatNotify, null);
            if (s.Uuid == BleUuids.IsscService)
                return (s, BleUuids.IsscWrite, BleUuids.IsscNotify, null);
            if (s.Uuid == BleUuids.Fd00)
                return (s, BleUuids.Fd01, BleUuids.Fd02, null);
            if (s.Uuid == BleUuids.GenericPrinter)
                return (s, BleUuids.GenericWrite, BleUuids.GenericNotify, null);
        }

        foreach (var s in services)
        {
            var chars = s.GetCharacteristicsAsync(BluetoothCacheMode.Uncached).GetAwaiter().GetResult();
            var writable = chars.Characteristics.FirstOrDefault(c =>
                c.CharacteristicProperties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse) ||
                c.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Write));
            if (writable is not null)
                return (s, writable.Uuid, null, null);
        }

        return null;
    }

    private void OnCredit(GattCharacteristic sender, GattValueChangedEventArgs args)
    {
        var reader = DataReader.FromBuffer(args.CharacteristicValue);
        var bytes = new byte[reader.UnconsumedBufferLength];
        reader.ReadBytes(bytes);
        if (bytes.Length >= 2 && bytes[0] == 0x01)
            Interlocked.Exchange(ref _credits, bytes[1]);
        else if (bytes.Length >= 3 && bytes[0] == 0x02)
        {
            // MTU announcement — ignore, we already chunk conservatively
        }
        try { _creditGate.Release(); } catch (SemaphoreFullException) { /* already open */ }
    }

    public async Task WriteAsync(byte[] data, int chunkSize, int delayMs, CancellationToken ct)
    {
        try
        {
        var withoutResponse = _write.CharacteristicProperties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse);
        for (var i = 0; i < data.Length; i += chunkSize)
        {
            ct.ThrowIfCancellationRequested();
            if (_credit is not null)
            {
                while (Volatile.Read(ref _credits) <= 0)
                {
                    await _creditGate.WaitAsync(ct);
                }
                Interlocked.Decrement(ref _credits);
            }

            var n = Math.Min(chunkSize, data.Length - i);
            var writer = new DataWriter();
            writer.WriteBytes(data.AsSpan(i, n).ToArray());
            var buf = writer.DetachBuffer();
            var opt = withoutResponse ? GattWriteOption.WriteWithoutResponse : GattWriteOption.WriteWithResponse;
            var status = await _write.WriteValueAsync(buf, opt);
            if (status != GattCommunicationStatus.Success)
                throw new AppException(ErrorCode.BleWriteFailed, Loc.Gatt(status));
            if (delayMs > 0)
                await Task.Delay(delayMs, ct);
        }
        }
        catch (OperationCanceledException)
        {
            throw new AppException(ErrorCode.OperationCancelled);
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_credit is not null)
            _credit.ValueChanged -= OnCredit;
        _device.Dispose();
        return ValueTask.CompletedTask;
    }
}
