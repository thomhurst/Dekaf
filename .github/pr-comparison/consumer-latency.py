"""Apply identical consumer measurement changes, without changing library code."""
import sys
from pathlib import Path


def replace_once(text, old, new):
    if text.count(old) != 1:
        raise ValueError(f"Expected one patch anchor: {old[:100]}")
    return text.replace(old, new, 1)


def patch(directory):
    path = directory / 'tools/Dekaf.StressTests/Scenarios/ConsumerRawStressTest.cs'
    text = path.read_text(encoding='utf-8-sig')
    text = 'using System.Diagnostics;\n' + text
    text = replace_once(text, '        // GC baseline before consumer measurement', '''        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        await using var records = consumer.ConsumeAsync(cts.Token).GetAsyncEnumerator(cts.Token);
        var expectedOffsets = new long[options.Partitions];
        var replayCount = 0L;
        void CheckAndReplay(ConsumeResult<Ignore, ReadOnlyMemory<byte>> record)
        {
            if (record.Offset != expectedOffsets[record.Partition])
                throw new InvalidOperationException($"Offset discontinuity: partition {record.Partition}, expected {expectedOffsets[record.Partition]}, got {record.Offset}");
            expectedOffsets[record.Partition]++;
            if (replay.RecordConsumed(record.Partition, record.Offset))
            {
                consumer.Positions.SeekToBeginning(partitions);
                Array.Clear(expectedOffsets);
                replayCount++;
            }
        }

        // Keep the same enumerator and fetch pipeline across warmup and measurement.
        // This excludes group join/JIT/adaptation from the measured latency distribution.
        Console.WriteLine("  Warming consumer for 30 seconds...");
        var warmupStarted = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(warmupStarted) < TimeSpan.FromSeconds(30))
        {
            if (!await records.MoveNextAsync().ConfigureAwait(false))
                throw new InvalidOperationException("Consumer ended during warmup");
            CheckAndReplay(records.Current);
        }
        var warmupReplays = replayCount;
        var latency = new LatencyTracker(maxValueMs: 100, bucketWidthUs: 0.1);

        // GC baseline before consumer measurement''')
    text = replace_once(text, '''        using var gcStats = new GcStats();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);''',
        '        using var gcStats = new GcStats();')
    text = replace_once(text, '''            await foreach (var record in consumer.ConsumeAsync(cts.Token).ConfigureAwait(false))
            {
                throughput.RecordMessage(record.Value.Length);
                progress.RecordMessage();

                if (replay.RecordConsumed(record.Partition, record.Offset))
                {
                    consumer.Positions.SeekToBeginning(partitions);
                }
            }''', '''            while (!cts.IsCancellationRequested)
            {
                var readStarted = Stopwatch.GetTimestamp();
                if (!await records.MoveNextAsync().ConfigureAwait(false))
                    throw new InvalidOperationException("Consumer ended during measurement");
                latency.RecordTicks(Stopwatch.GetTimestamp() - readStarted);
                var record = records.Current;
                CheckAndReplay(record);
                throughput.RecordMessage(record.Value.Length);
                progress.RecordMessage();
            }''')
    text = replace_once(text, '            Latency = null,', '            Latency = latency.GetSnapshot(),')
    text = replace_once(text, '        throughput.Stop();', '''        throughput.Stop();
        Console.WriteLine($"  Validated replays: warmup={warmupReplays}, measured={replayCount - warmupReplays}");''')
    path.write_text(text, encoding='utf-8', newline='\n')


if __name__ == '__main__':
    for arg in sys.argv[1:]:
        patch(Path(arg))
