namespace MiniPrinter.Printing;

public sealed class LabelBitmap
{
    public LabelBitmap(int widthDots, int heightDots, byte[] packedMsbFirst)
    {
        WidthDots = widthDots;
        HeightDots = heightDots;
        WidthBytes = (widthDots + 7) / 8;
        PackedMsbFirst = packedMsbFirst;
        if (packedMsbFirst.Length != WidthBytes * heightDots)
            throw new MiniPrinter.Localization.AppException(MiniPrinter.Localization.ErrorCode.BitmapSizeMismatch);
    }

    public int WidthDots { get; }
    public int HeightDots { get; }
    public int WidthBytes { get; }
    public byte[] PackedMsbFirst { get; }

    public byte[] PackedLsbFirst()
    {
        var output = new byte[PackedMsbFirst.Length];
        for (var i = 0; i < PackedMsbFirst.Length; i++)
            output[i] = ReverseBits(PackedMsbFirst[i]);
        return output;
    }

    public bool Pixel(int x, int y)
    {
        if (x < 0 || y < 0 || x >= WidthDots || y >= HeightDots) return false;
        var b = PackedMsbFirst[y * WidthBytes + x / 8];
        return (b & (0x80 >> (x % 8))) != 0;
    }

    private static byte ReverseBits(byte value)
    {
        value = (byte)((value & 0xF0) >> 4 | (value & 0x0F) << 4);
        value = (byte)((value & 0xCC) >> 2 | (value & 0x33) << 2);
        value = (byte)((value & 0xAA) >> 1 | (value & 0x55) << 1);
        return value;
    }
}
