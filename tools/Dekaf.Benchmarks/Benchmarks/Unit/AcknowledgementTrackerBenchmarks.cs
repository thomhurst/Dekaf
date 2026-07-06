using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.ShareConsumer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Compares the share-consumer acknowledgement range tracker against a
/// per-offset dictionary baseline for a contiguous poll result.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 1, iterationCount: 3)]
public class AcknowledgementTrackerBenchmarks
{
    private TopicPartition _topicPartition;

    [Params(100, 1_000, 10_000)]
    public int RecordCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _topicPartition = new TopicPartition("bench-topic", 0);
    }

    [Benchmark]
    public int RangeTracker_TrackAndFlush()
    {
        var tracker = new AcknowledgementTracker();

        tracker.TrackDeliveredRecords(_topicPartition, 0, RecordCount - 1);
        var result = tracker.Flush();

        return result[_topicPartition][0].AcknowledgeTypes.Length;
    }

    [Benchmark(Baseline = true)]
    public int DictionaryPerOffset_TrackAndFlush()
    {
        var tracker = new DictionaryAcknowledgementTracker();

        tracker.TrackDeliveredRecords(_topicPartition, 0, RecordCount - 1);
        var result = tracker.Flush();

        return result[_topicPartition][0].AcknowledgeTypes.Length;
    }

    private sealed class DictionaryAcknowledgementTracker
    {
        private readonly Dictionary<TopicPartition, Dictionary<long, AcknowledgeType>> _pendingAcks = [];

        internal void TrackDeliveredRecords(TopicPartition topicPartition, long firstOffset, long lastOffset)
        {
            var capacity = checked((int)(lastOffset - firstOffset + 1));
            var partitionAcks = GetOrAddPartition(topicPartition, capacity);
            for (var offset = firstOffset; offset <= lastOffset; offset++)
                partitionAcks[offset] = AcknowledgeType.Accept;
        }

        internal Dictionary<TopicPartition, List<AcknowledgementBatchData>> Flush()
        {
            var result = new Dictionary<TopicPartition, List<AcknowledgementBatchData>>(_pendingAcks.Count);

            foreach (var (topicPartition, partitionAcks) in _pendingAcks)
                result[topicPartition] = BuildBatches(partitionAcks);

            _pendingAcks.Clear();
            return result;
        }

        private Dictionary<long, AcknowledgeType> GetOrAddPartition(TopicPartition topicPartition, int capacity)
        {
            if (_pendingAcks.TryGetValue(topicPartition, out var partitionAcks))
                return partitionAcks;

            partitionAcks = new Dictionary<long, AcknowledgeType>(capacity);
            _pendingAcks[topicPartition] = partitionAcks;
            return partitionAcks;
        }

        private static List<AcknowledgementBatchData> BuildBatches(
            Dictionary<long, AcknowledgeType> partitionAcks)
        {
            if (partitionAcks.Count == 0)
                return [];

            var offsets = partitionAcks.Keys.ToArray();
            Array.Sort(offsets);

            List<AcknowledgementBatchData> batches = [];
            var runStart = offsets[0];
            var previousOffset = runStart;
            List<byte> runTypes = [(byte)partitionAcks[runStart]];

            for (var i = 1; i < offsets.Length; i++)
            {
                var offset = offsets[i];
                if (offset == previousOffset + 1)
                {
                    runTypes.Add((byte)partitionAcks[offset]);
                }
                else
                {
                    batches.Add(new AcknowledgementBatchData(runStart, previousOffset, runTypes.ToArray()));
                    runStart = offset;
                    runTypes = [(byte)partitionAcks[offset]];
                }

                previousOffset = offset;
            }

            batches.Add(new AcknowledgementBatchData(runStart, previousOffset, runTypes.ToArray()));
            return batches;
        }
    }
}
