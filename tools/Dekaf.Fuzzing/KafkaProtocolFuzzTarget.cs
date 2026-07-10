namespace Dekaf.Fuzzing;

public static class KafkaProtocolFuzzTarget
{
    public static void Run(ReadOnlySpan<byte> input)
    {
        if (ResponseParserFuzzTarget.IsResponseInput(input))
        {
            ResponseParserFuzzTarget.Run(input);
            return;
        }

        ProtocolReaderFuzzTarget.Run(input);
    }
}
