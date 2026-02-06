namespace Dekaf.Tests.Integration;

public class KafkaContainer39 : KafkaTestContainer
{
    public override string ContainerName => "apache/kafka:3.9.1";
    public override int Version => 391;
}