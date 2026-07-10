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

        if (args is [var inputPath])
        {
            KafkaProtocolFuzzTarget.Run(File.ReadAllBytes(inputPath));
            return;
        }

        Fuzzer.LibFuzzer.Run(KafkaProtocolFuzzTarget.Run);
    }
}
