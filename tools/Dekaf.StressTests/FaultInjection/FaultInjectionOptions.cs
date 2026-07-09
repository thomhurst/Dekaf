namespace Dekaf.StressTests.FaultInjection;

internal sealed class FaultInjectionOptions
{
    internal string Profile { get; init; } = "all";
    internal int BrokerCount { get; init; } = 1;
    internal int PartitionCount { get; init; } = 6;
    internal int MessageSizeBytes { get; init; } = 1_000;
    internal TimeSpan FaultDuration { get; init; } = TimeSpan.FromSeconds(5);
    internal int MessagesBeforeFault { get; init; } = 2_000;
    internal int MaxMessagesDuringFault { get; init; } = 20_000;
    internal int MessagesAfterFault { get; init; } = 2_000;
    internal string OutputPath { get; init; } = "./results";

    internal void Validate()
    {
        _ = FaultInjectionPlan.Build(Profile, BrokerCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(PartitionCount);
        ArgumentOutOfRangeException.ThrowIfLessThan(MessageSizeBytes, 32);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(FaultDuration, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MessagesBeforeFault);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxMessagesDuringFault);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MessagesAfterFault);
        ArgumentException.ThrowIfNullOrWhiteSpace(OutputPath);
    }
}
