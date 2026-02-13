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
    public required string BootstrapServers { get; init; }
    public required string Topic { get; init; }
    public required int DurationMinutes { get; init; }
    public required int MessageSizeBytes { get; init; }
    public int Partitions { get; init; } = 6;
    public int LingerMs { get; init; } = 5;
    public int BatchSize { get; init; } = 16384;
    public string Compression { get; init; } = "none";
}
