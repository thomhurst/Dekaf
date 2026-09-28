using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// A Confluent Schema Registry container that stores its schemas in a Kafka broker.
/// </summary>
/// <param name="name">The resource name, also used as the connection string name.</param>
[AspireExport(ExposeProperties = true)]
public class DekafSchemaRegistryResource(string name) : ContainerResource(name), IResourceWithConnectionString
{
    internal const string PrimaryEndpointName = "http";

    private EndpointReference? _primaryEndpoint;

    /// <summary>Gets the endpoint that serves the Schema Registry REST API.</summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new(this, PrimaryEndpointName);

    /// <summary>Gets the registry URL, resolved for the network of the resource that references it.</summary>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create($"{PrimaryEndpoint.Property(EndpointProperty.Url)}");

    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties()
    {
        yield return new("Host", ReferenceExpression.Create($"{PrimaryEndpoint.Property(EndpointProperty.Host)}"));
        yield return new("Port", ReferenceExpression.Create($"{PrimaryEndpoint.Property(EndpointProperty.Port)}"));
        yield return new("Uri", ConnectionStringExpression);
    }
}
