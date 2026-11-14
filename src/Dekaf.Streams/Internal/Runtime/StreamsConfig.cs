using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace Dekaf.Streams.Internal.Runtime;

internal sealed class StreamsConfig
{
    public required string BootstrapServers { get; init; }

    public Action<KafkaClientBuilder>? ConfigureClient { get; init; }

    public ProcessingGuarantee Guarantee { get; init; } = ProcessingGuarantee.ExactlyOnce;

    public TimeSpan CommitInterval { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>How long the loop waits for input before it checks the assignment and the commit timer again.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMilliseconds(100);

    public TimeSpan TransactionTimeout { get; init; } = TimeSpan.FromSeconds(10);

    public TimeSpan RebalanceTimeout { get; init; } = TimeSpan.FromSeconds(30);

    public int ThreadCount { get; init; } = 1;

    public string? InstanceId { get; init; }

    public string? StateDirectory { get; init; }

    public IStateStoreEngine? StoreEngine { get; init; }

    public ILoggerFactory? LoggerFactory { get; init; }

    /// <summary>Identifies this process to the coordinator, so tasks are spread across processes.</summary>
    public string ProcessId { get; } = Guid.NewGuid().ToString();
}

internal readonly record struct TaskId(int Subtopology, int Partition)
{
    public override string ToString() => $"{Subtopology}_{Partition}";
}

/// <summary>Instruments of the <c>Dekaf.Streams</c> meter. Updated once per batch or commit, not per record.</summary>
internal static class StreamsMetrics
{
    public const string MeterName = "Dekaf.Streams";

    private static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> RecordsProcessed =
        Meter.CreateCounter<long>("dekaf.streams.records.processed", "{record}", "Records processed by tasks.");

    public static readonly Counter<long> RecordsRestored =
        Meter.CreateCounter<long>("dekaf.streams.records.restored", "{record}", "Changelog records applied while restoring state.");

    public static readonly Counter<long> Commits =
        Meter.CreateCounter<long>("dekaf.streams.commits", "{commit}", "Completed commits.");

    public static readonly Counter<long> AbortedCommits =
        Meter.CreateCounter<long>("dekaf.streams.commits.aborted", "{commit}", "Commits that were aborted; their tasks were restored and their input reprocessed.");

    public static readonly Histogram<double> CommitDuration =
        Meter.CreateHistogram<double>("dekaf.streams.commit.duration", "s", "Time taken by a commit.");

    public static readonly UpDownCounter<long> ActiveTasks =
        Meter.CreateUpDownCounter<long>("dekaf.streams.tasks.active", "{task}", "Tasks currently owned and processing.");
}
