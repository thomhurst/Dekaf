using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Telemetry;

namespace Dekaf.Tests.Unit.Networking;

public sealed partial class KafkaConnectionTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    [Timeout(10_000)]
    public async Task SharedControlObservation_KeepsCollectorsUntilTheirResponsesComplete(
        bool pipelined, CancellationToken cancellationToken)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var accept = AcceptAndCompleteHandshakeAsync(listener, cancellationToken);
        await using var connection = new KafkaConnection(1, IPAddress.Loopback.ToString(), port);
        await connection.ConnectAsync(cancellationToken);
        using var client = await accept;
        var firstCollector = new ClientTelemetryMetricCollector(ClientTelemetryClientRole.Producer);
        var secondCollector = new ClientTelemetryMetricCollector(ClientTelemetryClientRole.Producer);
        var writes = 0;
        Action writeStarted = () => Interlocked.Increment(ref writes);
        Task<ApiVersionsResponse> first;
        Task<ApiVersionsResponse> second;
        if (pipelined)
        {
            var pending = await connection.SendPipelinedWithTelemetryAfterWriteAsync<ApiVersionsRequest, ApiVersionsResponse>(
                TelemetryControlRequest(), 3, firstCollector, writeStarted, cancellationToken);
            first = pending.AsValueTask().AsTask();
            // The first observation's pooled state is now reusable, while its response is pending.
            pending = await connection.SendPipelinedWithTelemetryAfterWriteAsync<ApiVersionsRequest, ApiVersionsResponse>(
                TelemetryControlRequest(), 3, secondCollector, writeStarted, cancellationToken);
            second = pending.AsValueTask().AsTask();
        }
        else
        {
            first = connection.SendWithTelemetryAsync<ApiVersionsRequest, ApiVersionsResponse>(
                TelemetryControlRequest(), 3, firstCollector, writeStarted, cancellationToken).AsTask();
            second = connection.SendWithTelemetryAsync<ApiVersionsRequest, ApiVersionsResponse>(
                TelemetryControlRequest(), 3, secondCollector, writeStarted, cancellationToken).AsTask();
        }

        for (var i = 0; i < 2; i++)
        {
            var frame = await ReadRequestFrameAsync(client.GetStream(), cancellationToken);
            var correlationId = BinaryPrimitives.ReadInt32BigEndian(frame.AsSpan(4, 4));
            await client.GetStream().WriteAsync(BuildApiVersionsV3ResponseFrame(correlationId), cancellationToken);
        }
        await Assert.That((await first).ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That((await second).ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(writes).IsEqualTo(2);

        var subscription = new ClientTelemetrySubscription(Guid.NewGuid(), 1, 0, 1000, 1024,
            true, [ClientTelemetryMetricNames.ProducerNodeRequestLatencyAvg]);
        foreach (var collector in new[] { firstCollector, secondCollector })
        {
            var metrics = collector.Collect(subscription).Metrics;
            await Assert.That(metrics.Count).IsEqualTo(1);
            await Assert.That(metrics[0].Attributes.Single(attribute => attribute.Name == "node_id").Value).IsEqualTo("1");
            await Assert.That(collector.Collect(subscription).Metrics.Count).IsEqualTo(0);
        }
    }

    [Test]
    [Arguments("write lock", false)]
    [Arguments("pending slot", false)]
    [Arguments("broker throttle", false)]
    [Arguments("write lock", true)]
    [Arguments("pending slot", true)]
    [Arguments("broker throttle", true)]
    [Timeout(10_000)]
    public async Task SharedControlObservation_PreWriteCancellationPreservesCallbacksAndPendingSlots(
        string wait, bool pipelined, CancellationToken cancellationToken)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var accept = AcceptAndCompleteHandshakeAsync(listener, cancellationToken);
        await using var connection = new KafkaConnection(1, IPAddress.Loopback.ToString(), port);
        await connection.ConnectAsync(cancellationToken);
        using var client = await accept;
        using var caller = new CancellationTokenSource();
        var collector = new ClientTelemetryMetricCollector(ClientTelemetryClientRole.Producer);
        var writes = 0;
        Action writeStarted = () => Interlocked.Increment(ref writes);
        SemaphoreSlim? held = null;
        var heldCount = 0;
        try
        {
            if (wait == "broker throttle")
                GetPrivateField<BrokerThrottleState>(connection, "_brokerThrottleState").Observe(60_000);
            else
            {
                held = GetPrivateField<SemaphoreSlim>(connection,
                    wait == "write lock" ? "_writeLock" : "_pendingRequestSlots");
                while (held.Wait(0, cancellationToken)) heldCount++;
            }

            Task send = pipelined
                ? connection.SendPipelinedWithTelemetryAfterWriteAsync<ApiVersionsRequest, ApiVersionsResponse>(
                    TelemetryControlRequest(), 3, collector, writeStarted, caller.Token).AsTask()
                : connection.SendWithTelemetryAsync<ApiVersionsRequest, ApiVersionsResponse>(
                    TelemetryControlRequest(), 3, collector, writeStarted, caller.Token).AsTask();
            await Assert.That(send.IsCompleted).IsFalse();
            await caller.CancelAsync();
            var exception = await Assert.ThrowsAsync<OperationCanceledException>(async () => await send);
            await Assert.That(exception!.CancellationToken.IsCancellationRequested).IsTrue();
            await Assert.That(writes).IsEqualTo(0);
            await Assert.That(client.GetStream().DataAvailable).IsFalse();
            await Assert.That(GetPrivateField<int>(connection, "_pendingRequestCount")).IsEqualTo(0);
        }
        finally
        {
            if (heldCount != 0) held!.Release(heldCount);
        }
    }

    private static ApiVersionsRequest TelemetryControlRequest() =>
        new() { ClientSoftwareName = "test", ClientSoftwareVersion = "1.0" };
}
