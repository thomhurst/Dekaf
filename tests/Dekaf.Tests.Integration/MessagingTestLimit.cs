using TUnit.Core.Interfaces;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Limits parallelism for all Messaging-category integration tests.
/// Each test creates 1-5 Kafka clients (producers/consumers), each with its own connection pool.
/// Without limiting, dozens of tests running concurrently against a single Docker Kafka broker
/// overwhelm the container with concurrent connections, causing receive timeouts and test hangs.
/// A limit of 2 keeps total connections manageable. A limit of 3 still caused intermittent
/// receive timeouts on slow CI runners with thread pool starvation.
/// </summary>
public class MessagingTestLimit : IParallelLimit
{
    public int Limit => 2;
}
