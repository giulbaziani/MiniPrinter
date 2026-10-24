using System.IO;

namespace MiniPrinter.Printing.Protocols;

/// <summary>
/// Generic "cat printer" 0x5178 protocol (GB01, GT01, MX*, many 58 mm BLE toys).
/// </summary>
public sealed class CatPrinterProtocol : IPrintProtocol
{
    public PrintProtocolKind Kind => PrintProtocolKind.CatPrinter;

    public byte[] BuildJob(LabelJob job)
    {
        using var ms = new MemoryStream();
        var energy = (ushort)(0x2E00 + job.Density * 0x1800);
        Write(ms, 0xA3, [0x00]);
        Write(ms, 0xAF, [(byte)(energy & 0xFF), (byte)(energy >> 8)]);
        Write(ms, 0xBE, [0x01]);
        Write(ms, 0xA4, [0x31]);

        var rows = job.Bitmap.PackedLsbFirst();
        var w = job.Bitmap.WidthBytes;
        for (var y = 0; y < job.Bitmap.HeightDots; y++)
        {
            var row = new byte[w];
            Buffer.BlockCopy(rows, y * w, row, 0, w);
            Write(ms, 0xA2, row);
        }

        Write(ms, 0xBD, [(byte)Math.Clamp(job.FeedDots, 1, 255), 0x00]);
        return ms.ToArray();
    }

    private static void Write(Stream ms, byte cmd, byte[] payload)
    {
        ms.WriteByte(0x51);
        ms.WriteByte(0x78);
        ms.WriteByte(cmd);
        ms.WriteByte(0x00);
        ms.WriteByte((byte)(payload.Length & 0xFF));
        ms.WriteByte((byte)(payload.Length >> 8));
        ms.Write(payload);
        ms.WriteByte(Crc8(payload));
        ms.WriteByte(0xFF);
    }

    private static byte Crc8(byte[] data)
    {
        byte crc = 0;
        foreach (var b in data)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
                crc = (crc & 0x80) != 0 ? (byte)((crc << 1) ^ 0x07) : (byte)(crc << 1);
        }
        return crc;
    }
}
