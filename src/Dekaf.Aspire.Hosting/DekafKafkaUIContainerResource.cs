using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>A Kafka UI container that manages the Kafka brokers in the application.</summary>
/// <param name="name">The resource name.</param>
[AspireExport(ExposeProperties = true)]
public sealed class DekafKafkaUIContainerResource(string name) : ContainerResource(name)
{
    internal const string PrimaryEndpointName = "http";

    private EndpointReference? _primaryEndpoint;

    /// <summary>Gets the endpoint that serves the Kafka UI.</summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new(this, PrimaryEndpointName);
}
