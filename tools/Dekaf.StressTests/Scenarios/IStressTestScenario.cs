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
    public int RoundTripMessages { get; init; } = 250_000;
    public bool EnableProducerDeliveryDiagnostics { get; init; }
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
