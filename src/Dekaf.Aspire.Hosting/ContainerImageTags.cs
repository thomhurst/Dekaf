namespace Aspire.Hosting;

internal static class ContainerImageTags
{
    /// <remarks>docker.io</remarks>
    public const string Registry = "docker.io";

    /// <remarks>apache/kafka</remarks>
    public const string KafkaImage = "apache/kafka";

    /// <remarks>4.3.1</remarks>
    public const string KafkaTag = "4.3.1";

    /// <remarks>confluentinc/cp-schema-registry</remarks>
    public const string SchemaRegistryImage = "confluentinc/cp-schema-registry";

    /// <remarks>8.2.0</remarks>
    public const string SchemaRegistryTag = "8.2.0";

    /// <remarks>kafbat/kafka-ui</remarks>
    public const string KafkaUiImage = "kafbat/kafka-ui";

    /// <remarks>v1.5.0</remarks>
    public const string KafkaUiTag = "v1.5.0";
}
