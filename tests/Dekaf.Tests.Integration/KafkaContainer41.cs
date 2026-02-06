namespace Dekaf.Tests.Integration;

public class KafkaContainer41 : KafkaTestContainer
{
    public override string ContainerName => "apache/kafka:4.1.1";
    public override int Version => 411;
}