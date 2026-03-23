using TUnit.Core.Interfaces;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Assembly-level parallel limit for all integration tests.
/// CI runners have 4 processors, and TUnit defaults to processors * 4 = 16 parallel tests.
/// Each test creates 1-5 Kafka clients with connection pools, so 16 tests simultaneously
/// overwhelm the single Docker Kafka container with 50+ connections, causing delivery
/// timeouts, thread pool starvation, and flaky failures.
/// A limit of 4 (one per processor) keeps total concurrent Kafka connections manageable
/// while still exercising parallelism.
/// </summary>
public class IntegrationTestParallelLimit : IParallelLimit
{
    public int Limit => 4;
}
