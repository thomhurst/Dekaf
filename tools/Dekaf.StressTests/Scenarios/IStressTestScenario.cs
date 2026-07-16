using Dekaf.StressTests.Diagnostics;
using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Reporting;

namespace Dekaf.StressTests.Scenarios;

internal interface IStressTestScenario
{
    string Name { get; }
    string Client { get; }
    Task<StressTestResult> RunAsync(StressTestOptions options, CancellationToken cancellationToken);
}

internal sealed class StressTestOptions
{
    public const int HighThroughputConsumerConnectionsPerBroker = 3;

    public required string BootstrapServers { get; init; }
    public required string Topic { get; init; }
    public required int DurationMinutes { get; init; }
    public required int MessageSizeBytes { get; init; }
    public int Partitions { get; init; } = 6;
    public int LingerMs { get; init; } = 5;
    public int BatchSize { get; init; } = 16384;
    public int DeliveryLatencyTargetMs { get; init; } = 10;
    public int? ConsumerSeedBatchSizeBytes { get; init; }
    public string Compression { get; init; } = "none";
    public int BrokerCount { get; init; } = 1;
    public int ConnectionsPerBroker { get; init; } = 1;
    public int RoundTripSteadySeconds { get; init; } = 60;

    /// <summary>
    /// Fallback round-trip log budget for runs that don't declare a broker log-dir size
    /// via STRESS_BROKER_TMPFS (Program derives the budget from that when present).
    /// </summary>
    public const long DefaultRoundTripSteadyMaxLogBytes = 4_000_000_000;

    /// <summary>
    /// Estimated broker log bytes at which the round-trip produce phase stops even if
    /// <see cref="RoundTripSteadySeconds"/> has not elapsed. The round-trip topic disables
    /// retention so validation can consume everything, so the produce phase must fit the
    /// broker's log dir (a tmpfs in CI stress jobs; see stress-tests.yml).
    /// </summary>
    public long RoundTripSteadyMaxLogBytes { get; init; } = DefaultRoundTripSteadyMaxLogBytes;
    public bool EnableProducerDeliveryDiagnostics { get; init; }

    /// <summary>
    /// Enables the Dekaf-only per-fetch diagnostics listener and 1-minute snapshot sampler
    /// in the consumer scenarios. Off by default so the measured window carries no
    /// diagnostics overhead; pass <c>--consumer-fetch-diagnostics</c> on debug runs.
    /// </summary>
    public bool EnableConsumerFetchDiagnostics { get; init; }
    public required ProgressWatchdog ProgressWatchdog { get; init; }
    public int SoakMessagesPerSecond { get; init; } = 5_000;
    public double ResourceSampleIntervalSeconds { get; init; } = 60;
    public ResourceTrendThresholds ResourceTrendThresholds { get; init; } = new()
    {
        WarmupMinutes = 60,
        MinimumSampleCount = 30,
        MaxWorkingSetSlopeMibPerHour = 8,
        MaxGcHeapSlopeMibPerHour = 4,
        MaxLohSlopeMibPerHour = 4,
        MaxThroughputDecayPercentPerHour = 5
    };
}
