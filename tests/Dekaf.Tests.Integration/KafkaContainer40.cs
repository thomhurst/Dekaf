namespace Dekaf.Tests.Integration;

public class KafkaContainer40 : KafkaTestContainer
{
    public override string ContainerName => "apache/kafka:4.0.1";
    public override int Version => 401; // major*100 + minor*10 + patch
}