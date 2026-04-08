using System.Buffers;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures the cost of copying a <see cref="ReadOnlySequence{T}"/> into a contiguous
/// pooled <c>byte[]</c>, isolating the operation that <c>KafkaConnection.TryReadResponse</c>
/// performs on every fetch response.
///
/// Hypothesis under test: at 95k msg/s with ~1KB records, the per-response memcpy
/// (<c>FetchMaxBytes + 1MB</c> ≈ 53MB sized buffer, but typically 1-4MB filled) is
/// the dominant cost of the consumer's ~10% throughput deficit vs Confluent.Kafka.
///
/// The reported <c>ThroughputMBps</c> column tells us how many MB/s a single core
/// can sustain on the copy alone — compare against the observed receive-loop bandwidth
/// at 95k msg/s × 1KB = ~95 MB/s. If single-core copy throughput is &lt;500 MB/s at 1MB,
/// the memcpy is plausibly the bottleneck. If it is &gt;5 GB/s, the deficit lives elsewhere.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(ThroughputConfig))]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class ReceiveLoopCopyBenchmark
{
    private ReadOnlySequence<byte> _sequence;
    private byte[] _destination = null!;

    [Params(65_536, 262_144, 1_048_576, 4_194_304, 16_777_216, 52_428_800)]
    public int ResponseSize { get; set; }

    [Params(1, 4, 16, 64)]
    public int SegmentCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _destination = ArrayPool<byte>.Shared.Rent(ResponseSize);
        _sequence = BuildSequence(ResponseSize, SegmentCount);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        ArrayPool<byte>.Shared.Return(_destination);
    }

    /// <summary>
    /// Exactly what <c>KafkaConnection.TryReadResponse</c> does today: bulk copy the entire
    /// response sequence into a pooled contiguous array.
    /// </summary>
    [Benchmark(Baseline = true)]
    public void BufferCopyTo()
    {
        _sequence.CopyTo(_destination.AsSpan(0, (int)_sequence.Length));
    }

    /// <summary>
    /// Allocating equivalent — included to show how much the pooling buys us versus a naive
    /// <c>ToArray</c>. Allocates per-call.
    /// </summary>
    [Benchmark]
    public byte[] BufferToArray() => _sequence.ToArray();

    /// <summary>
    /// Hand-rolled segment-by-segment copy. If this materially beats <see cref="BufferCopyTo"/>
    /// then <c>ReadOnlySequence.CopyTo</c> overhead matters; otherwise it is just memcpy at scale.
    /// </summary>
    [Benchmark]
    public void SegmentLoopCopy()
    {
        var dest = _destination.AsSpan(0, (int)_sequence.Length);
        foreach (var segment in _sequence)
        {
            segment.Span.CopyTo(dest);
            dest = dest.Slice(segment.Length);
        }
    }

    /// <summary>
    /// Zero-copy lower bound: when the sequence is already a single segment, avoid the copy
    /// entirely and just touch the span. For multi-segment inputs the segment loop still runs
    /// (counted but not copied) so we can see the dispatch cost of the check.
    /// </summary>
    [Benchmark]
    public int ZeroCopy_SingleSegmentFastPath()
    {
        if (_sequence.IsSingleSegment)
        {
            return _sequence.FirstSpan.Length;
        }

        var total = 0;
        foreach (var segment in _sequence)
        {
            total += segment.Length;
        }
        return total;
    }

    private static ReadOnlySequence<byte> BuildSequence(int totalSize, int segmentCount)
    {
        if (segmentCount <= 1)
        {
            var single = new byte[totalSize];
            FillDeterministic(single);
            return new ReadOnlySequence<byte>(single);
        }

        var baseSize = totalSize / segmentCount;
        var remainder = totalSize - (baseSize * segmentCount);

        BufferSegment? first = null;
        BufferSegment? last = null;
        var runningIndex = 0L;

        for (var i = 0; i < segmentCount; i++)
        {
            var size = baseSize + (i == segmentCount - 1 ? remainder : 0);
            var buffer = new byte[size];
            FillDeterministic(buffer);
            var segment = new BufferSegment(buffer, runningIndex);
            runningIndex += size;

            if (first is null)
            {
                first = segment;
                last = segment;
            }
            else
            {
                last!.SetNext(segment);
                last = segment;
            }
        }

        return new ReadOnlySequence<byte>(first!, 0, last!, last!.Memory.Length);
    }

    private static void FillDeterministic(Span<byte> span)
    {
        for (var i = 0; i < span.Length; i++)
        {
            span[i] = (byte)i;
        }
    }

    private sealed class BufferSegment : ReadOnlySequenceSegment<byte>
    {
        public BufferSegment(ReadOnlyMemory<byte> memory, long runningIndex)
        {
            Memory = memory;
            RunningIndex = runningIndex;
        }

        public void SetNext(BufferSegment next) => Next = next;
    }

    private sealed class ThroughputConfig : ManualConfig
    {
        public ThroughputConfig()
        {
            AddColumn(new ThroughputColumn());
        }
    }

    private sealed class ThroughputColumn : IColumn
    {
        public string Id => nameof(ThroughputColumn);
        public string ColumnName => "ThroughputMBps";
        public string Legend => "Effective single-core copy throughput in MB/s (ResponseSize / Mean).";
        public UnitType UnitType => UnitType.Dimensionless;
        public bool AlwaysShow => true;
        public ColumnCategory Category => ColumnCategory.Custom;
        public int PriorityInCategory => 0;
        public bool IsNumeric => true;
        public bool IsAvailable(Summary summary) => true;
        public bool IsDefault(Summary summary, BenchmarkDotNet.Running.BenchmarkCase benchmarkCase) => false;

        public string GetValue(Summary summary, BenchmarkDotNet.Running.BenchmarkCase benchmarkCase)
            => GetValue(summary, benchmarkCase, SummaryStyle.Default);

        public string GetValue(Summary summary, BenchmarkDotNet.Running.BenchmarkCase benchmarkCase, SummaryStyle style)
        {
            var report = summary[benchmarkCase];
            if (report?.ResultStatistics is null)
            {
                return "-";
            }

            var meanNs = report.ResultStatistics.Mean;
            if (meanNs <= 0)
            {
                return "-";
            }

            var sizeParam = benchmarkCase.Parameters["ResponseSize"];
            if (sizeParam is not int responseSize)
            {
                return "-";
            }

            // bytes / nanoseconds = GB/s; multiply by 1000 to get MB/s.
            var mbps = responseSize / meanNs * 1000.0;
            return mbps.ToString("N0");
        }
    }
}
