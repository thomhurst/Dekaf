using Dekaf;
using Dekaf.Producer;
using Dekaf.Serialization;

var headers = Headers.Create("compatibility", "legacy");
var message = new ProducerMessage<string, string>
{
    Topic = "compatibility",
    Key = "key",
    Value = "value",
    Headers = headers
};
var partition = new TopicPartition(message.Topic, 0);

if (typeof(Headers).Assembly.GetName().Name != "Dekaf.Abstractions"
    || typeof(ProducerMessage<,>).Assembly.GetName().Name != "Dekaf.Abstractions"
    || typeof(TopicPartition).Assembly.GetName().Name != "Dekaf.Abstractions")
{
    throw new InvalidOperationException("Legacy references did not resolve through Dekaf type forwarders.");
}

Console.WriteLine($"{partition.Topic}:{headers.Count}:{message.Value}");
