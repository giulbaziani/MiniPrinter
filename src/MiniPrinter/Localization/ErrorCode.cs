namespace MiniPrinter.Localization;

public enum ErrorCode
{
    None = 0,
    DeviceNotSelected = 1001,
    PrinterDisconnected = 1002,
    BleDeviceNotFound = 1003,
    GattServicesFailed = 1004,
    GattProfileNotFound = 1005,
    WriteCharacteristicMissing = 1006,
    BleWriteFailed = 1007,
    SerialOpenFailed = 1008,
    SerialWriteFailed = 1009,
    OperationCancelled = 1010,
    BitmapSizeMismatch = 1011,
    TemplateNameEmpty = 1012,
    PreviewFailed = 1013,
    ConnectFailed = 1014,
    PrintFailed = 1015,
    Unknown = 1016,
    BluetoothUnavailable = 1017,
    IoFailed = 1018
}

public sealed class AppException : Exception
{
    public ErrorCode Code { get; }
    public object[] Args { get; }

    public AppException(ErrorCode code, params object[] args)
        : base(FormatCode(code))
    {
        Code = code;
        Args = args;
    }

    public AppException(ErrorCode code, Exception inner, params object[] args)
        : base(FormatCode(code), inner)
    {
        Code = code;
        Args = args;
    }

    public string Display() => Loc.Error(Code, Args);

    public static string FormatCode(ErrorCode code) => $"MP-{(int)code:D4}";
}
