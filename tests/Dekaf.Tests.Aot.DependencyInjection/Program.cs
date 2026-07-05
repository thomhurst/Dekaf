using Dekaf;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Extensions.DependencyInjection;
using Dekaf.Producer;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddDekaf(dekaf =>
{
    dekaf.AddGlobalProducerInterceptor<string, string>(static _ => new AotProducerInterceptor());
    dekaf.AddGlobalConsumerInterceptor<string, string>(static _ => new AotConsumerInterceptor());

    dekaf.AddProducer<string, string>(
        new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "aot-producer"
        });

    dekaf.AddConsumer<string, string>(
        new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "aot-consumer",
            GroupId = "aot-group"
        },
        consumer => consumer.SubscribeTo("aot-topic"));

    dekaf.AddAdminClient(
        new AdminClientOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "aot-admin"
        });
});

await using var provider = services.BuildServiceProvider();
_ = provider.GetRequiredService<IKafkaProducer<string, string>>();
_ = provider.GetRequiredService<IKafkaConsumer<string, string>>();
_ = provider.GetRequiredService<IAdminClient>();

file sealed class AotProducerInterceptor : IProducerInterceptor<string, string>
{
    public ProducerMessage<string, string> OnSend(ProducerMessage<string, string> message) => message;

    public void OnAcknowledgement(RecordMetadata metadata, Exception? exception)
    {
    }
}

file sealed class AotConsumerInterceptor : IConsumerInterceptor<string, string>
{
    public ConsumeResult<string, string> OnConsume(ConsumeResult<string, string> result) => result;

    public void OnCommit(IReadOnlyList<TopicPartitionOffset> offsets)
    {
    }
}
