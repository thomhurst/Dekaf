using System.Reflection;
using Dekaf.Internal;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;
using NSubstitute;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// A producer over a real <see cref="ConnectionPool"/> and <see cref="MetadataManager"/> whose
/// two brokers are substitutes. Connection setup and InitProducerId responses are scripted
/// through <see cref="Connect"/> and <see cref="Send"/>; metadata bootstrap is skipped so the
/// producer's own initialization and metadata-wait paths run against the scripted transport.
/// </summary>
internal sealed class ProducerInitializationHarness : IAsyncDisposable
{
    private readonly ConnectionPool _pool;
    private readonly MetadataManager _metadata;
    public MetadataManager Metadata => _metadata;
    public KafkaProducer<string, string> Producer { get; }
    public Dictionary<int, IKafkaConnection> Connections { get; } = [];
    public int[] BrokerIds { get; }
    public List<int> ConnectionAttempts { get; } = [];
    public List<(int BrokerId, InitProducerIdRequest Request)> Requests { get; } = [];
    public Func<int, CancellationToken, ValueTask<IKafkaConnection>> Connect { get; set; }
    public Func<int, CancellationToken, ValueTask<InitProducerIdResponse>> Send { get; set; } = (_, _) => ValueTask.FromResult(Success());

    public ProducerInitializationHarness(
        int maxBlockMs = 5000,
        int retryBackoffMs = 1,
        bool idempotent = true,
        string? transactionalId = null,
        MetadataOptions? metadataOptions = null)
    {
        Connect = (id, _) => ValueTask.FromResult(Connections[id]);
        _pool = new ConnectionPool("idempotent-init-test", new ConnectionOptions { ReconnectBackoff = TimeSpan.Zero }, 1,
            (id, _, _, _, token) =>
            {
                lock (ConnectionAttempts)
                    ConnectionAttempts.Add(id);
                return Connect(id, token);
            });
        _metadata = metadataOptions is null
            ? new MetadataManager(_pool, ["localhost:9092"])
            : new MetadataManager(_pool, ["localhost:9092"], metadataOptions);
        _metadata.Metadata.Update(new MetadataResponse
        {
            Brokers =
            [
                new BrokerMetadata { NodeId = 1, Host = "localhost", Port = 9092 },
                new BrokerMetadata { NodeId = 2, Host = "localhost", Port = 9093 }
            ],
            Topics = []
        });
        // Skip only metadata bootstrap; exercise the public producer initialization path.
        typeof(MetadataManager).GetField("_initialized", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(_metadata, true);
        _metadata.SetApiVersion(ApiKey.InitProducerId, 2, 5);
        BrokerIds = _metadata.Metadata.GetBrokers().Select(broker => broker.NodeId).ToArray();
        foreach (var broker in _metadata.Metadata.GetBrokers())
        {
            _pool.RegisterBroker(broker.NodeId, broker.Host, broker.Port);
            var connection = Substitute.For<IKafkaConnection>();
            connection.BrokerId.Returns(broker.NodeId);
            connection.Host.Returns(broker.Host);
            connection.Port.Returns(broker.Port);
            connection.IsConnected.Returns(true);
            connection.SendAsync<InitProducerIdRequest, InitProducerIdResponse>(
                    Arg.Any<InitProducerIdRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    Requests.Add((broker.NodeId, call.ArgAt<InitProducerIdRequest>(0)));
                    return Send(broker.NodeId, call.ArgAt<CancellationToken>(2));
                });
            Connections.Add(broker.NodeId, connection);
        }
        Producer = new KafkaProducer<string, string>(new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            EnableIdempotence = idempotent,
            TransactionalId = transactionalId,
            MaxBlockMs = maxBlockMs,
            RetryBackoffMs = retryBackoffMs,
            RetryBackoffMaxMs = retryBackoffMs,
            CloseTimeoutMs = 100
        }, Serializers.String, Serializers.String, _pool, _metadata, DekafMemoryBudget.Global);
    }

    public static InitProducerIdResponse Success() => new()
    {
        ErrorCode = ErrorCode.None,
        ProducerId = 1234,
        ProducerEpoch = 7
    };

    public async ValueTask DisposeAsync()
    {
        await Producer.DisposeAsync();
        await _metadata.DisposeAsync();
        await _pool.DisposeAsync();
    }
}
