using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// An Apache Kafka broker, run as a single-node KRaft container for local development.
/// </summary>
/// <param name="name">The resource name, also used as the connection string name.</param>
[AspireExport(ExposeProperties = true)]
public class DekafKafkaServerResource(string name) : ContainerResource(name), IResourceWithConnectionString
{
    // Host processes reach the broker through this endpoint.
    internal const string PrimaryEndpointName = "tcp";

    // Containers reach the broker through this endpoint on the Aspire container network.
    internal const string InternalEndpointName = "internal";

    private EndpointReference? _primaryEndpoint;
    private EndpointReference? _internalEndpoint;

    /// <summary>
    /// Gets the endpoint that host processes use to reach the broker.
    /// Containers use <see cref="InternalEndpoint"/>.
    /// </summary>
    public EndpointReference PrimaryEndpoint =>
        _primaryEndpoint ??= new(this, PrimaryEndpointName, KnownNetworkIdentifiers.LocalhostNetwork);

    /// <summary>
    /// Gets the endpoint that other containers use to reach the broker.
    /// Host processes use <see cref="PrimaryEndpoint"/>.
    /// </summary>
    public EndpointReference InternalEndpoint =>
        _internalEndpoint ??= new(this, InternalEndpointName, KnownNetworkIdentifiers.DefaultAspireContainerNetwork);

    /// <summary>Gets the host of the primary endpoint.</summary>
    public EndpointReferenceExpression Host => PrimaryEndpoint.Property(EndpointProperty.Host);

    /// <summary>Gets the port of the primary endpoint.</summary>
    public EndpointReferenceExpression Port => PrimaryEndpoint.Property(EndpointProperty.Port);

    /// <summary>Gets the bootstrap servers, in <c>host:port</c> form.</summary>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create($"{PrimaryEndpoint.Property(EndpointProperty.HostAndPort)}");

    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties()
    {
        yield return new("Host", ReferenceExpression.Create($"{Host}"));
        yield return new("Port", ReferenceExpression.Create($"{Port}"));
    }
}
