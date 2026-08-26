using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.ShareConsumer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures the callback result construction and dispatch stage shared by inline
/// ShareFetch acknowledgements and standalone ShareAcknowledge commits.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class AcknowledgementCommitCallbackBenchmarks
{
    private static long s_checksum;
    private Dictionary<TopicPartition, List<AcknowledgementBatchData>> _acknowledgements = null!;
    private List<AcknowledgementBatchData> _batches = null!;

    [Params(1, 1_000)]
    public int RecordCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var acknowledgementTypes = new byte[RecordCount];
        _batches =
        [
            new AcknowledgementBatchData(0, RecordCount - 1, acknowledgementTypes)
        ];
        _acknowledgements = new Dictionary<TopicPartition, List<AcknowledgementBatchData>>
        {
            [new TopicPartition("bench-topic", 0)] = _batches
        };

        AcknowledgementCommitCallbackInvoker.Invoke(
            ConsumeOffsetView,
            _acknowledgements,
            errors: null);
    }

    [Benchmark]
    public void PooledOffsetViewCallback()
    {
        AcknowledgementCommitCallbackInvoker.Invoke(
            ConsumeOffsetView,
            _acknowledgements,
            errors: null);
    }

    [Benchmark(Baseline = true)]
    public void PreviousMaterializedCallback()
    {
        var topicPartitions = new TopicPartition[_acknowledgements.Count];
        _acknowledgements.Keys.CopyTo(topicPartitions, 0);
        System.Array.Sort<TopicPartition>(topicPartitions, CompareTopicPartitions);

        var results = new PreviousResult[topicPartitions.Length];
        for (var index = 0; index < topicPartitions.Length; index++)
        {
            var topicPartition = topicPartitions[index];
            results[index] = new PreviousResult(
                topicPartition,
                MaterializeOffsets(_acknowledgements[topicPartition]));
        }

        ConsumePreviousResults(results);
    }

    private static void ConsumeOffsetView(ReadOnlySpan<ShareAcknowledgementCommitResult> results)
    {
        var checksum = 0L;
        for (var resultIndex = 0; resultIndex < results.Length; resultIndex++)
        {
            foreach (var offset in results[resultIndex].Offsets)
                checksum += offset;
        }

        Volatile.Write(ref s_checksum, checksum);
    }

    private static long[] MaterializeOffsets(List<AcknowledgementBatchData> batches)
    {
        var count = 0;
        for (var batchIndex = 0; batchIndex < batches.Count; batchIndex++)
            count = checked(count + batches[batchIndex].AcknowledgeTypes.Length);

        var offsets = new long[count];
        var index = 0;
        for (var batchIndex = 0; batchIndex < batches.Count; batchIndex++)
        {
            var batch = batches[batchIndex];
            for (var offsetIndex = 0; offsetIndex < batch.AcknowledgeTypes.Length; offsetIndex++)
                offsets[index++] = batch.FirstOffset + offsetIndex;
        }

        return offsets;
    }

    private static int CompareTopicPartitions(TopicPartition left, TopicPartition right)
    {
        var topicComparison = string.CompareOrdinal(left.Topic, right.Topic);
        return topicComparison != 0
            ? topicComparison
            : left.Partition.CompareTo(right.Partition);
    }

    private static void ConsumePreviousResults(ReadOnlySpan<PreviousResult> results)
    {
        var checksum = 0L;
        for (var resultIndex = 0; resultIndex < results.Length; resultIndex++)
        {
            var offsets = results[resultIndex].Offsets;
            for (var offsetIndex = 0; offsetIndex < offsets.Length; offsetIndex++)
                checksum += offsets[offsetIndex];
        }

        Volatile.Write(ref s_checksum, checksum);
    }

    private sealed record PreviousResult(TopicPartition TopicPartition, long[] Offsets);
}
