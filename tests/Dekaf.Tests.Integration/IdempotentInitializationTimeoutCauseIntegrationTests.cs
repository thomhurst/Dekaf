using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Dekaf.Errors;
using Dekaf.Producer;
using Dekaf.Protocol;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Idempotent initialization against a real socket transport whose cluster metadata names no
/// broker (every broker restarting). A real broker always lists itself, so a minimal
/// Kafka-protocol listener plays the cluster: it answers ApiVersions and the bootstrap Metadata
/// request with an empty broker list, then leaves every later Metadata request unanswered. The
/// refresh InitProducerId starts for the empty list is therefore still waiting on the socket
/// when max.block.ms expires.
/// </summary>
[Category("Producer")]
[Category("Resilience")]
public sealed class IdempotentInitializationTimeoutCauseIntegrationTests
{
    [Test]
    [Timeout(30_000)]
    public async Task InitializeAsync_MaxBlockExpiresDuringNoBrokerRefresh_TimeoutNamesMissingBrokers(
        CancellationToken cancellationToken)
    {
        await using var cluster = new NoBrokerCluster();
        var stopwatch = Stopwatch.StartNew();

        var exception = await Assert.That(async () =>
            {
                await using var producer = await Kafka.CreateProducer<string, string>()
                    .WithBootstrapServers(cluster.BootstrapServers)
                    .WithIdempotence(true)
                    .WithAcks(Acks.All)
                    .WithMaxBlock(TimeSpan.FromMilliseconds(1500))
                    .BuildAsync(cancellationToken);
            })
            .Throws<KafkaTimeoutException>();

        await Assert.That(stopwatch.Elapsed).IsLessThan(TimeSpan.FromSeconds(15));
        // The bootstrap refresh succeeded and the stalled one came from InitProducerId, so the
        // timeout is the idempotent initialization's, not metadata initialization's.
        await Assert.That(cluster.MetadataRequests).IsGreaterThanOrEqualTo(2);
        await Assert.That(exception!.Message).Contains("InitProducerId");
        await Assert.That(exception.TimeoutKind).IsEqualTo(TimeoutKind.Api);
        await Assert.That(exception.InnerException is OperationCanceledException).IsFalse();
        await Assert.That(exception.InnerException).IsTypeOf<KafkaException>();
        await Assert.That(((KafkaException)exception.InnerException!).ErrorCode).IsEqualTo(ErrorCode.BrokerNotAvailable);
    }

    private sealed class NoBrokerCluster : IAsyncDisposable
    {
        private const short ApiVersionsKey = (short)ApiKey.ApiVersions;
        private const short MetadataKey = (short)ApiKey.Metadata;
        private const short MetadataVersion = 12;

        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        private readonly List<Socket> _sockets = [];
        private readonly Task _acceptLoop;
        private int _metadataRequests;

        public NoBrokerCluster()
        {
            _listener.Start();
            BootstrapServers = $"127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";
            _acceptLoop = AcceptLoopAsync();
        }

        public string BootstrapServers { get; }

        public int MetadataRequests => Volatile.Read(ref _metadataRequests);

        private async Task AcceptLoopAsync()
        {
            var connections = new List<Task>();
            try
            {
                while (true)
                {
                    var socket = await _listener.AcceptSocketAsync(_stop.Token);
                    lock (_sockets)
                        _sockets.Add(socket);
                    connections.Add(ServeAsync(socket));
                }
            }
            catch (Exception) when (_stop.IsCancellationRequested)
            {
            }

            await Task.WhenAll(connections);
        }

        private async Task ServeAsync(Socket socket)
        {
            try
            {
                await using var stream = new NetworkStream(socket, ownsSocket: false);
                var length = new byte[sizeof(int)];
                while (true)
                {
                    await stream.ReadExactlyAsync(length, _stop.Token);
                    var request = new byte[BinaryPrimitives.ReadInt32BigEndian(length)];
                    await stream.ReadExactlyAsync(request, _stop.Token);
                    var apiKey = BinaryPrimitives.ReadInt16BigEndian(request);
                    var correlationId = BinaryPrimitives.ReadInt32BigEndian(request.AsSpan(4));

                    if (apiKey == ApiVersionsKey)
                    {
                        await stream.WriteAsync(Frame(correlationId, flexibleHeader: false, WriteApiVersions), _stop.Token);
                    }
                    else if (apiKey == MetadataKey && Interlocked.Increment(ref _metadataRequests) == 1)
                    {
                        await stream.WriteAsync(Frame(correlationId, flexibleHeader: true, WriteNoBrokerMetadata), _stop.Token);
                    }

                    // Every later request stays unanswered: the brokers are still restarting.
                }
            }
            catch (Exception) when (_stop.IsCancellationRequested)
            {
            }
            catch (IOException)
            {
                // The client closed the connection (EndOfStreamException included).
            }
        }

        private static void WriteApiVersions(ref KafkaProtocolWriter writer)
        {
            writer.WriteInt16(0); // ErrorCode
            writer.WriteUnsignedVarInt(4); // three API keys, compact array
            WriteApi(ref writer, ApiKey.ApiVersions, 0, 3);
            WriteApi(ref writer, ApiKey.Metadata, MetadataVersion, MetadataVersion);
            WriteApi(ref writer, ApiKey.InitProducerId, 0, 4);
            writer.WriteInt32(0); // ThrottleTimeMs
            writer.WriteEmptyTaggedFields();

            static void WriteApi(ref KafkaProtocolWriter writer, ApiKey key, short min, short max)
            {
                writer.WriteInt16((short)key);
                writer.WriteInt16(min);
                writer.WriteInt16(max);
                writer.WriteEmptyTaggedFields();
            }
        }

        private static void WriteNoBrokerMetadata(ref KafkaProtocolWriter writer)
        {
            writer.WriteInt32(0); // ThrottleTimeMs
            writer.WriteUnsignedVarInt(1); // Brokers: empty compact array
            writer.WriteCompactString("no-broker-cluster");
            writer.WriteInt32(-1); // ControllerId
            writer.WriteUnsignedVarInt(1); // Topics: empty compact array
            writer.WriteEmptyTaggedFields();
        }

        private delegate void BodyWriter(ref KafkaProtocolWriter writer);

        private static byte[] Frame(int correlationId, bool flexibleHeader, BodyWriter writeBody)
        {
            var body = new ArrayBufferWriter<byte>();
            var writer = new KafkaProtocolWriter(body);
            writer.WriteInt32(correlationId);
            if (flexibleHeader)
                writer.WriteEmptyTaggedFields();
            writeBody(ref writer);

            var frame = new byte[sizeof(int) + body.WrittenCount];
            BinaryPrimitives.WriteInt32BigEndian(frame, body.WrittenCount);
            body.WrittenSpan.CopyTo(frame.AsSpan(sizeof(int)));
            return frame;
        }

        public async ValueTask DisposeAsync()
        {
            _stop.Cancel();
            _listener.Stop();
            lock (_sockets)
            {
                foreach (var socket in _sockets)
                    socket.Dispose();
            }

            await _acceptLoop;
            _stop.Dispose();
        }
    }
}
