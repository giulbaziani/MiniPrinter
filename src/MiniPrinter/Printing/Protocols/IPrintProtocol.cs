namespace MiniPrinter.Printing.Protocols;

public interface IPrintProtocol
{
    PrintProtocolKind Kind { get; }
    byte[] BuildJob(LabelJob job);
}
