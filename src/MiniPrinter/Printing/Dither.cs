namespace MiniPrinter.Printing;

public static class Dither
{
    public static LabelBitmap To1Bpp(int width, int height, byte[] gray, bool floydSteinberg = true)
    {
        var buffer = new float[gray.Length];
        for (var i = 0; i < gray.Length; i++)
            buffer[i] = gray[i];

        var packed = new byte[((width + 7) / 8) * height];
        var stride = (width + 7) / 8;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = y * width + x;
                var old = buffer[i];
                var dark = old < 128;
                var neu = dark ? 0f : 255f;
                if (floydSteinberg)
                {
                    var err = old - neu;
                    Diffuse(buffer, width, height, x + 1, y, err * 7 / 16);
                    Diffuse(buffer, width, height, x - 1, y + 1, err * 3 / 16);
                    Diffuse(buffer, width, height, x, y + 1, err * 5 / 16);
                    Diffuse(buffer, width, height, x + 1, y + 1, err * 1 / 16);
                }

                if (dark)
                    packed[y * stride + x / 8] |= (byte)(0x80 >> (x % 8));
            }
        }

        return new LabelBitmap(width, height, packed);
    }

    private static void Diffuse(float[] buf, int w, int h, int x, int y, float amount)
    {
        if (x < 0 || y < 0 || x >= w || y >= h) return;
        buf[y * w + x] += amount;
    }
}
