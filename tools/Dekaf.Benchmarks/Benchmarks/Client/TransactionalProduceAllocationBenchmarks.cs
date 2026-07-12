using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using Dekaf.Metadata;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Producer;
using System.Reflection;

namespace Dekaf.Benchmarks.Benchmarks.Client;

[MemoryDiagnoser]
[Config(typeof(AllocationJobConfig))]
public class TransactionalProduceAllocationBenchmarks
{
    private const int MessagesPerIteration = 1_000;

    private sealed class AllocationJobConfig : ManualConfig
    {
        public AllocationJobConfig()
        {
            AddJob(Job.Default
                .WithStrategy(RunStrategy.Throughput)
                .WithLaunchCount(1)
                .WithWarmupCount(3)
                .WithIterationCount(3)
                .WithInvocationCount(MessagesPerIteration)
                .WithUnrollFactor(1));
        }
    }

    private IKafkaProducer<string, string> _producer = null!;
    private ITransaction<string, string> _transaction = null!;
    private ProducerMessage<string, string> _message = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        _producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithTransactionalId("benchmark-transaction-allocation")
            .WithBufferMemory(ulong.MaxValue)
            .Build();
        var producer = (KafkaProducer<string, string>)_producer;
        var senderCts = GetField<CancellationTokenSource>(producer, "_senderCts");
        await senderCts.CancelAsync();
        await Task.WhenAll(
            GetField<Task>(producer, "_senderTask"),
            GetField<Task>(producer, "_lingerTask"));

        SeedMetadata(GetField<MetadataManager>(producer, "_metadataManager"));
        SetField(producer, "_initialized", true);
        SetField(producer, "_transactionState", TransactionState.Ready);
        _transaction = _producer.BeginTransaction();
        _message = new ProducerMessage<string, string>
        {
            Topic = "benchmark-transaction-allocation",
            Partition = 0,
            Key = "key",
            Value = "value"
        };
    }

    [Benchmark(Baseline = true)]
    public void ProducerProduceAsync() =>
        _ = _producer.ProduceAsync(_message);

    [Benchmark]
    public void TransactionProduceAsync() =>
        _ = _transaction.ProduceAsync(_message);

    private static void SeedMetadata(MetadataManager metadataManager) =>
        metadataManager.Metadata.Update(new MetadataResponse
        {
            Brokers =
            [
                new BrokerMetadata { NodeId = 0, Host = "localhost", Port = 9092 }
            ],
            ClusterId = "benchmark-cluster",
            ControllerId = 0,
            Topics =
            [
                new TopicMetadata
                {
                    ErrorCode = ErrorCode.None,
                    Name = "benchmark-transaction-allocation",
                    Partitions =
                    [
                        new PartitionMetadata
                        {
                            ErrorCode = ErrorCode.None,
                            PartitionIndex = 0,
                            LeaderId = 0,
                            ReplicaNodes = [0],
                            IsrNodes = [0]
                        }
                    ]
                }
            ]
        });

    private static T GetField<T>(object target, string name) =>
        (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(target)!;

    private static void SetField<T>(object target, string name, T value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(target, value);
}
