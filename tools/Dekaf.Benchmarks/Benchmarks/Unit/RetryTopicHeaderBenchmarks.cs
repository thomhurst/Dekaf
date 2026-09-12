using BenchmarkDotNet.Attributes;
using Dekaf.Consumer.DeadLetter;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
public class RetryTopicHeaderBenchmarks
{
    private IReadOnlyList<Header> _headers = null!;

    [Params(-62135596800000L, 1700000000000L, 253402300799999L)]
    public long Timestamp { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var headers = new Headers();
        headers.Add(RetryTopicHeaders.DueTimestampMsKey, Timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture));
        _headers = headers.ToList();
    }

    [Benchmark]
    public DateTimeOffset ReadDueTimestamp()
    {
        if (!RetryTopicHeaders.TryGetDueAt(_headers, out var dueAt))
            throw new InvalidOperationException("Valid retry timestamp was rejected.");
        return dueAt;
    }
}
