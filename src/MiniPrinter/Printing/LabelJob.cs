namespace MiniPrinter.Printing;

public enum PaperKind
{
    Continuous,
    Gap
}

public enum PrintProtocolKind
{
    Auto,
    LuckJingleEscPos,
    EscPos,
    CatPrinter,
    Tspl
}

public sealed class LabelJob
{
    public required LabelBitmap Bitmap { get; init; }
    public int Dpi { get; init; } = 203;
    public PaperKind Paper { get; init; } = PaperKind.Continuous;
    public int Density { get; init; } = 3;
    public int Copies { get; init; } = 1;
    public int FeedDots { get; init; } = 80;
    public PrintProtocolKind Protocol { get; init; } = PrintProtocolKind.Auto;
}
