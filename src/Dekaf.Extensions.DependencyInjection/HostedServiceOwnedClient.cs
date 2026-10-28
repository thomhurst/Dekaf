namespace Dekaf.Extensions.DependencyInjection;

/// <summary>
/// Marks a registered client that a hosted service initializes itself, so
/// <see cref="DekafInitializationService"/> leaves it out of eager startup initialization.
/// </summary>
/// <remarks>
/// Hosted consumer services initialize their consumer inside their restart loop, which retries a
/// transient failure such as an unreachable broker. Eager initialization would instead throw out of
/// <c>IHostedService.StartAsync</c> and stop the host before that loop runs.
/// </remarks>
internal sealed class HostedServiceOwnedClient(object client)
{
    public object Client { get; } = client;
}
