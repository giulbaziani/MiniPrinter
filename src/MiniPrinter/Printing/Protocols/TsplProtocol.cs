using System.IO;
using System.Text;

namespace MiniPrinter.Printing.Protocols;

public sealed class TsplProtocol : IPrintProtocol
{
    public PrintProtocolKind Kind => PrintProtocolKind.Tspl;

    public byte[] BuildJob(LabelJob job)
    {
        var bmp = job.Bitmap;
        var wMm = bmp.WidthDots * 25.4 / job.Dpi;
        var hMm = bmp.HeightDots * 25.4 / job.Dpi;
        var sb = new StringBuilder();
        sb.Append($"SIZE {wMm:0.##} mm,{hMm:0.##} mm\r\n");
        sb.Append("GAP 2 mm,0 mm\r\n");
        sb.Append("DIRECTION 1\r\n");
        sb.Append("REFERENCE 0,0\r\n");
        sb.Append($"DENSITY {Math.Clamp(job.Density, 1, 15)}\r\n");
        sb.Append("CLS\r\n");
        sb.Append($"BITMAP 0,0,{bmp.WidthBytes},{bmp.HeightDots},0,");
        using var ms = new MemoryStream();
        var header = Encoding.ASCII.GetBytes(sb.ToString());
        ms.Write(header);
        var inverted = new byte[bmp.PackedMsbFirst.Length];
        for (var i = 0; i < inverted.Length; i++)
            inverted[i] = (byte)~bmp.PackedMsbFirst[i];
        ms.Write(inverted);
        ms.Write(Encoding.ASCII.GetBytes($"\r\nPRINT {Math.Max(1, job.Copies)},1\r\n"));
        return ms.ToArray();
    }
}
