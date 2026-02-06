namespace Dekaf.Tests.Integration;

/// <summary>
/// Base class for integration tests that require a Kafka container.
/// The <see cref="KafkaTestContainer"/> instance is shared across all tests in the session via TUnit's ClassDataSource.
/// Derived classes receive the container through their own primary constructor parameter.
/// </summary>
[ClassDataSource<KafkaContainer39>(Shared = SharedType.PerTestSession)]
[ClassDataSource<KafkaContainer40>(Shared = SharedType.PerTestSession)]
[ClassDataSource<KafkaContainer41>(Shared = SharedType.PerTestSession)]
public abstract class KafkaIntegrationTest;