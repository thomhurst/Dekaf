namespace Dekaf.Tests.Integration;

/// <summary>
/// Rack-aware cluster that forms with brokers 1 and 2 while broker 3 remains unstarted.
/// </summary>
public sealed class BrokerScaleOutKafkaContainer : RackAwareKafkaContainer
{
    private static readonly int[] InitialBrokerNodeIds = [1, 2];

    protected override IReadOnlyList<int> InitiallyStartedBrokerIds => InitialBrokerNodeIds;
}
