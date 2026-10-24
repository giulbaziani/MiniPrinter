using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QRCoder;

namespace MiniPrinter.Printing;

public sealed class LabelContent
{
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string Footer { get; set; } = "";
    public string? QrPayload { get; set; }
    public string? BarcodePayload { get; set; }
    public bool BoldTitle { get; set; } = true;
    public int TitleSize { get; set; } = 28;
    public int BodySize { get; set; } = 18;
    public bool BoldBody { get; set; }
    public bool Italic { get; set; }
    public bool Underline { get; set; }
    public TextAlignment Align { get; set; } = TextAlignment.Center;
    public List<TextSpan> BodySpans { get; set; } = [];
}

public sealed class TextSpan
{
    public string Text { get; set; } = "";
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public bool Underline { get; set; }
}

public static class LabelRenderer
{
    public static (LabelBitmap Bitmap, BitmapSource Preview) Render(
        double widthMm,
        double heightMm,
        int dpi,
        LabelContent content,
        int maxWidthDots = 384)
    {
        var width = Math.Max(8, MmToDots(widthMm, dpi));
        var height = Math.Max(8, MmToDots(heightMm, dpi));
        if (maxWidthDots > 0)
            width = Math.Min(width, maxWidthDots);
        width = (width + 7) & ~7;

        var dv = new DrawingVisual();
        RenderOptions.SetEdgeMode(dv, EdgeMode.Aliased);
        TextOptions.SetTextFormattingMode(dv, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(dv, TextRenderingMode.Aliased);
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
            var pad = Math.Max(4, width / 32.0);
            var y = pad;
            var maxW = width - pad * 2;

            if (!string.IsNullOrWhiteSpace(content.Title))
            {
                y += DrawText(dc, content.Title, pad, y, maxW, content.TitleSize, content.BoldTitle, content.Italic, content.Underline, content.Align);
                y += 4;
            }

            if (!string.IsNullOrWhiteSpace(content.QrPayload))
            {
                var qrSize = Math.Min(maxW, height * 0.45);
                var qr = BuildQr(content.QrPayload, (int)qrSize);
                var x = content.Align == TextAlignment.Left ? pad : (width - qrSize) / 2;
                dc.DrawImage(qr, new Rect(x, y, qrSize, qrSize));
                y += qrSize + 6;
            }

            if (!string.IsNullOrWhiteSpace(content.Body) || content.BodySpans.Count > 0)
            {
                y += DrawText(dc, content.Body, pad, y, maxW, content.BodySize, content.BoldBody, content.Italic, content.Underline, content.Align, content.BodySpans);
                y += 4;
            }

            if (!string.IsNullOrWhiteSpace(content.BarcodePayload))
            {
                var barH = Math.Min(48, height - y - pad - 18);
                if (barH > 16)
                {
                    DrawCode128(dc, content.BarcodePayload, pad, y, maxW, barH);
                    y += barH + 2;
                    DrawText(dc, content.BarcodePayload, pad, y, maxW, 12, true, false, false, TextAlignment.Center);
                }
            }

            if (!string.IsNullOrWhiteSpace(content.Footer))
            {
                DrawText(dc, content.Footer, pad, height - pad - content.BodySize, maxW, Math.Max(12, content.BodySize - 4), content.BoldBody, content.Italic, false, content.Align);
            }
        }

        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        rtb.Freeze();

        var pixels = new byte[width * height * 4];
        rtb.CopyPixels(pixels, width * 4, 0);
        var gray = new byte[width * height];
        for (var i = 0; i < gray.Length; i++)
        {
            var o = i * 4;
            gray[i] = (byte)((pixels[o + 2] * 30 + pixels[o + 1] * 59 + pixels[o] * 11) / 100);
        }

        var bmp = Dither.To1Bpp(width, height, gray, floydSteinberg: false, threshold: 248);
        return (bmp, bmp.ToBitmapSource());
    }

    public static (LabelBitmap Bitmap, BitmapSource Preview) RenderImage(
        string path,
        double widthMm,
        double heightMm,
        int dpi,
        int maxWidthDots = 384)
    {
        var width = Math.Max(8, MmToDots(widthMm, dpi));
        var height = Math.Max(8, MmToDots(heightMm, dpi));
        if (maxWidthDots > 0)
            width = Math.Min(width, maxWidthDots);
        width = (width + 7) & ~7;

        var img = new BitmapImage();
        img.BeginInit();
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.UriSource = new Uri(path);
        img.EndInit();
        img.Freeze();

        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
            var scale = Math.Min(width / (double)img.PixelWidth, height / (double)img.PixelHeight);
            var dw = img.PixelWidth * scale;
            var dh = img.PixelHeight * scale;
            dc.DrawImage(img, new Rect((width - dw) / 2, (height - dh) / 2, dw, dh));
        }

        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        rtb.Freeze();

        var pixels = new byte[width * height * 4];
        rtb.CopyPixels(pixels, width * 4, 0);
        var gray = new byte[width * height];
        for (var i = 0; i < gray.Length; i++)
        {
            var o = i * 4;
            gray[i] = (byte)((pixels[o + 2] * 30 + pixels[o + 1] * 59 + pixels[o] * 11) / 100);
        }

        var bmp = Dither.To1Bpp(width, height, gray, floydSteinberg: true, threshold: 128);
        return (bmp, rtb);
    }

    public static int MmToDots(double mm, int dpi) => (int)Math.Round(mm / 25.4 * dpi);

    private static double DrawText(
        DrawingContext dc,
        string text,
        double x,
        double y,
        double maxWidth,
        int size,
        bool bold,
        bool italic,
        bool underline,
        TextAlignment align,
        IReadOnlyList<TextSpan>? spans = null)
    {
        if (spans is { Count: > 0 })
            text = string.Concat(spans.Select(s => s.Text));
        if (string.IsNullOrEmpty(text)) return 0;

        var ft = new FormattedText(
            text,
            CultureInfo.GetCultureInfo("ka-GE"),
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"),
                italic ? FontStyles.Italic : FontStyles.Normal,
                bold ? FontWeights.Bold : FontWeights.Normal,
                FontStretches.Normal),
            size,
            Brushes.Black,
            1.0)
        {
            MaxTextWidth = maxWidth,
            TextAlignment = align
        };
        if (spans is { Count: > 0 })
        {
            var i = 0;
            foreach (var s in spans)
            {
                var n = s.Text.Length;
                if (n <= 0) continue;
                if (s.Bold) ft.SetFontWeight(FontWeights.Bold, i, n);
                else ft.SetFontWeight(FontWeights.Normal, i, n);
                if (s.Italic) ft.SetFontStyle(FontStyles.Italic, i, n);
                if (s.Underline) ft.SetTextDecorations(TextDecorations.Underline, i, n);
                i += n;
            }
        }
        var origin = new Point(Math.Floor(x), Math.Floor(y));
        var geo = ft.BuildGeometry(origin);
        geo.Freeze();
        dc.DrawGeometry(Brushes.Black, null, geo);
        if (underline && spans is not { Count: > 0 } && ft.Width > 0)
        {
            var left = align switch
            {
                TextAlignment.Center => x + (maxWidth - ft.Width) / 2,
                TextAlignment.Right => x + maxWidth - ft.Width,
                _ => x
            };
            var uy = Math.Floor(y + ft.Baseline + 2);
            dc.DrawRectangle(Brushes.Black, null, new Rect(Math.Floor(left), uy, Math.Ceiling(ft.Width), 2));
        }
        return ft.Height;
    }

    private static BitmapSource BuildQr(string payload, int size)
    {
        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        var qr = new PngByteQRCode(data);
        var png = qr.GetGraphic(6);
        using var ms = new System.IO.MemoryStream(png);
        var img = new BitmapImage();
        img.BeginInit();
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.StreamSource = ms;
        img.DecodePixelWidth = size;
        img.EndInit();
        img.Freeze();
        return img;
    }

    private static void DrawCode128(DrawingContext dc, string payload, double x, double y, double w, double h)
    {
        var pattern = Code128.Encode(payload);
        if (pattern.Length == 0) return;
        var barW = w / pattern.Length;
        for (var i = 0; i < pattern.Length; i++)
        {
            if (pattern[i] == '1')
                dc.DrawRectangle(Brushes.Black, null, new Rect(x + i * barW, y, Math.Ceiling(barW), h));
        }
    }
}

internal static class Code128
{
    private static readonly string[] Patterns =
    [
        "11011001100","11001101100","11001100110","10010011000","10010001100","10001001100","10011001000",
        "10011000100","10001100100","11001001000","11001000100","11000100100","10110011100","10011011100",
        "10011001110","10111001100","10011101100","10011100110","11001110010","11001011100","11001001110",
        "11011100100","11001110100","11101101110","11101001100","11100101100","11100100110","11101100100",
        "11100110100","11100110010","11011011000","11011000110","11000110110","10100011000","10001011000",
        "10001000110","10110001000","10001101000","10001100010","11010001000","11000101000","11000100010",
        "10110111000","10110001110","10001101110","10111011000","10111000110","10001110110","11101110110",
        "11010001110","11000101110","11011101000","11011100010","11011101110","11101011000","11101000110",
        "11100010110","11101101000","11101100010","11100011010","11101111010","11001000010","11110001010",
        "10100110000","10100001100","10010110000","10010000110","10000101100","10000100110","10110010000",
        "10110000100","10011010000","10011000010","10000110100","10000110010","11000010010","11001010000",
        "11110111010","11000010100","10001111010","10100111100","10010111100","10010011110","10111100100",
        "10011110100","10011110010","11110100100","11110010100","11110010010","11011011110","11011110110",
        "11110110110","10101111000","10100011110","10001011110","10111101000","10111100010","11110101000",
        "11110100010","10111011110","10111101110","11101011110","11110101110","11010000100","11010010000",
        "11010011100","11000111010"
    ];

    public static string Encode(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var codes = new List<int> { 104 };
        var checksum = 104;
        var pos = 1;
        foreach (var ch in text)
        {
            var v = ch - 32;
            if (v is < 0 or > 94) v = '?' - 32;
            codes.Add(v);
            checksum += v * pos;
            pos++;
        }
        codes.Add(checksum % 103);
        codes.Add(106);
        return string.Concat(codes.Select(c => Patterns[Math.Clamp(c, 0, Patterns.Length - 1)])) + "11";
    }
}
