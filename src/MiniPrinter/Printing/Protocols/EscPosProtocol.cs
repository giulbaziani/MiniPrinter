using System.IO;

namespace MiniPrinter.Printing.Protocols;

public sealed class EscPosProtocol : IPrintProtocol
{
    public PrintProtocolKind Kind => PrintProtocolKind.EscPos;

    public byte[] BuildJob(LabelJob job)
    {
        var bmp = job.Bitmap;
        using var ms = new MemoryStream();
        ms.Write([0x1B, 0x40]);
        WriteRaster(ms, bmp);
        ms.Write([0x1B, 0x4A, (byte)Math.Clamp(job.FeedDots, 0, 255)]);
        if (job.Paper == PaperKind.Gap)
            ms.Write([0x1D, 0x0C]);
        return ms.ToArray();
    }

    internal static void WriteRaster(Stream ms, LabelBitmap bmp)
    {
        var wL = (byte)(bmp.WidthBytes & 0xFF);
        var wH = (byte)(bmp.WidthBytes >> 8);
        var hL = (byte)(bmp.HeightDots & 0xFF);
        var hH = (byte)(bmp.HeightDots >> 8);
        ms.Write([0x1D, 0x76, 0x30, 0x00, wL, wH, hL, hH]);
        ms.Write(bmp.PackedMsbFirst);
    }
}
