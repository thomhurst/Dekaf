using BenchmarkDotNet.Attributes;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Guards the allocation and CPU cost of stamping fetch-request write order.</summary>
[MemoryDiagnoser(displayGenColumns: false)]
[ShortRunJob]
public class ConsumerWatermarkWriteSequenceBenchmarks
{
    private readonly SequenceSource _source = new();
    private IRequestWriteSequenceTarget _request = null!;

    [GlobalSetup]
    public void Setup()
    {
        _request = new FetchRequest();
        _request.WriteSequenceSource = _source;
    }

    [Benchmark]
    public long CaptureWriteSequence()
    {
        _request.RequestWriteStarted();
        return _request.WriteSequence;
    }

    private sealed class SequenceSource : IRequestWriteSequenceSource
    {
        private long _sequence;

        public long NextRequestWriteSequence() => Interlocked.Increment(ref _sequence);
    }
}
