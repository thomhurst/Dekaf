namespace Dekaf.Protocol;

internal static class ProtocolDataErrorClassifier
{
    public static bool IsProtocolDataError(Exception exception) => exception is
        InsufficientDataException or
        MalformedProtocolDataException or
        ArgumentOutOfRangeException;
}
