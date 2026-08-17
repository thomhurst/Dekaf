using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using Dekaf.Producer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
public class TransactionRetryClockBenchmarks
{
    private KafkaProducer<string, string> _producer = null!;

    [GlobalSetup]
    public void Setup() =>
        _producer = (KafkaProducer<string, string>)RuntimeHelpers.GetUninitializedObject(
            typeof(KafkaProducer<string, string>));

    [Benchmark(Description = "Read default transaction retry clock")]
    public long ReadDefaultTransactionRetryClock() => _producer.GetTransactionTimestampMilliseconds();
}
