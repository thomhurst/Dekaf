using SharpFuzz;

namespace Dekaf.Fuzzing;

public static class Program
{
    public static void Main(string[] args)
    {
        if (args is ["--build-response-corpus", var fixturesDirectory, var outputDirectory])
        {
            ResponseParserCorpusBuilder.Build(fixturesDirectory, outputDirectory);
            return;
        }

        var target = Environment.GetEnvironmentVariable("DEKAF_FUZZ_TARGET");
        var useRecordBatchTarget = target switch
        {
            null or "kafka-protocol" => false,
            "record-batch" => true,
            _ => throw new ArgumentException($"Unknown DEKAF_FUZZ_TARGET value '{target}'")
        };

        if (args is [var inputPath])
        {
            var input = File.ReadAllBytes(inputPath);
            if (useRecordBatchTarget)
            {
                RecordBatchFuzzTarget.Run(input);
            }
            else
            {
                KafkaProtocolFuzzTarget.Run(input);
            }

            return;
        }

        if (useRecordBatchTarget)
        {
            Fuzzer.LibFuzzer.Run(RecordBatchFuzzTarget.Run);
        }
        else
        {
            Fuzzer.LibFuzzer.Run(KafkaProtocolFuzzTarget.Run);
        }
    }
}
