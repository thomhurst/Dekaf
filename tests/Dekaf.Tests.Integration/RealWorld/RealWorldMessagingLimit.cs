using TUnit.Core.Interfaces;

namespace Dekaf.Tests.Integration.RealWorld;

/// <summary>
/// Limits parallelism for RealWorld messaging tests (MessageOrdering, EventPipeline, FanOut).
/// These tests create multiple concurrent producers and consumers against shared Kafka containers,
/// which can overwhelm the broker under high parallelism on CI runners.
/// </summary>
public class RealWorldMessagingLimit : IParallelLimit
{
    public int Limit => 3;
}
