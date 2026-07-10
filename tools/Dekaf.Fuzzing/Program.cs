using SharpFuzz;

namespace Dekaf.Fuzzing;

public static class Program
{
    public static void Main()
    {
        Fuzzer.LibFuzzer.Run(ProtocolReaderFuzzTarget.Run);
    }
}
