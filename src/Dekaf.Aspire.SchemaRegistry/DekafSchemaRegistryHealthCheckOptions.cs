namespace Dekaf.Aspire;

/// <summary>Options for the Schema Registry health check.</summary>
public sealed class DekafSchemaRegistryHealthCheckOptions
{
    /// <summary>Gets or sets the timeout for listing subjects. The default is 30 seconds.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
