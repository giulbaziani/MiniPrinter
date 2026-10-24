using System.IO;

namespace MiniPrinter.Printing.Protocols;

/// <summary>
/// ORICO S1 / Luck Jingle pocket printers: ESC/POS raster inside 10 FF session frames.
/// Head width is typically 384 dots (48 bytes/row). Gap-feed (GS FF) is avoided
/// unless the user selected gap paper — otherwise the motor waits on a mark and prints nothing.
/// </summary>
public sealed class LuckJingleProtocol : IPrintProtocol
{
    public PrintProtocolKind Kind => PrintProtocolKind.LuckJingleEscPos;

    public byte[] BuildJob(LabelJob job)
    {
        using var ms = new MemoryStream();
        for (var i = 0; i < 12; i++) ms.WriteByte(0x00);
        ms.Write([0x10, 0xFF, 0x40]);
        ms.Write([0x10, 0xFF, 0xF1, 0x03]);
        ms.Write([0x10, 0xFF, 0x12, 0x00, 0x00]);
        var density = (byte)Math.Clamp(job.Density, 1, 5);
        ms.Write([0x10, 0xFF, 0x10, 0x00, density, 0x00]);
        EscPosProtocol.WriteRaster(ms, job.Bitmap);
        ms.Write([0x1B, 0x4A, (byte)Math.Clamp(Math.Max(job.FeedDots, 24), 0, 255)]);
        if (job.Paper == PaperKind.Gap)
            ms.Write([0x1D, 0x0C]);
        ms.Write([0x10, 0xFF, 0xF1, 0x45]);
        return ms.ToArray();
    }
}
