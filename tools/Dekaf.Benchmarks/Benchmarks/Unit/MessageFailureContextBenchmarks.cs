using BenchmarkDotNet.Attributes;
using Dekaf.Consumer;
using Dekaf.Extensions.Hosting;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Guards allocation-free hosted-consumer failure context creation.</summary>
[MemoryDiagnoser(displayGenColumns: false)]
[ShortRunJob]
public class MessageFailureContextBenchmarks
{
    private readonly ConsumeResult<string, string> _result = new(
        topic: "orders",
        partition: 1,
        offset: 42,
        keyData: default,
        isKeyNull: true,
        valueData: default,
        isValueNull: true,
        headers: null,
        timestampMs: 0,
        timestampType: TimestampType.NotAvailable,
        leaderEpoch: null,
        keyDeserializer: null,
        valueDeserializer: null);
    private readonly Exception _processingException = new InvalidOperationException("Processing failed");

    [Benchmark]
    public MessageFailureContext<string, string> Create() =>
        new(
            _result,
            _processingException,
            attemptNumber: 1,
            failureCount: 1,
            MessageFailureStage.Processing);
}
