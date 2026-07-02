using System.Reflection;
using Dekaf.Internal;
using Dekaf.Networking;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Networking;

/// <summary>
/// Tests for KafkaConnection.SendPipelinedAsync to verify the pipelined send
/// refactoring preserves behavior while enabling pipelining.
/// </summary>
public sealed class KafkaConnectionPipelinedTests
{
    [Test]
    public async Task SendPipelinedAsync_DisposedConnection_ThrowsObjectDisposedException()
    {
        var connection = new KafkaConnection("localhost", 9092);
        await connection.DisposeAsync();

        var act = async () => await connection.SendPipelinedAsync<MetadataRequest, MetadataResponse>(
            new MetadataRequest { Topics = null },
            12);

        await Assert.That(act).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task SendPipelinedAsync_NotConnected_ThrowsInvalidOperationException()
    {
        var connection = new KafkaConnection("localhost", 9092);

        var act = async () => await connection.SendPipelinedAsync<MetadataRequest, MetadataResponse>(
            new MetadataRequest { Topics = null },
            12);

        await Assert.That(act).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task SendAsync_AfterRefactor_NotConnected_ThrowsInvalidOperationException()
    {
        // Verify existing SendAsync still works after the refactor to use AwaitAndParseResponseAsync
        var connection = new KafkaConnection("localhost", 9092);

        var act = async () => await connection.SendAsync<MetadataRequest, MetadataResponse>(
            new MetadataRequest { Topics = null },
            12);

        await Assert.That(act).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task SendAsync_AfterRefactor_Disposed_ThrowsObjectDisposedException()
    {
        var connection = new KafkaConnection("localhost", 9092);
        await connection.DisposeAsync();

        var act = async () => await connection.SendAsync<MetadataRequest, MetadataResponse>(
            new MetadataRequest { Topics = null },
            12);

        await Assert.That(act).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task PendingRequests_RemoveReleasesReservedSlot()
    {
        var connection = new KafkaConnection("localhost", 9092);
        var capacity = PoolSizing.ForConnection(maxInFlightRequestsPerConnection: 5).PendingRequests;

        for (var i = 0; i < capacity; i++)
        {
            await ReservePendingRequestSlotAsync(connection);
            var request = new PooledPendingRequest();
            request.Initialize(responseHeaderVersion: 0, CancellationToken.None, registerCancellation: false);
            AddPendingRequest(connection, i + 1, request);
        }

        var waitingForSlot = ReservePendingRequestSlotAsync(connection).AsTask();
        await Task.Delay(50);
        await Assert.That(waitingForSlot.IsCompleted).IsFalse();

        await Assert.That(TryRemovePendingRequest(connection, correlationId: 1)).IsTrue();
        await waitingForSlot.WaitAsync(TimeSpan.FromSeconds(1));
        ReleasePendingRequestSlot(connection);

        await connection.DisposeAsync();
    }

    [Test]
    public async Task SendPipelinedAsync_Cancellation_Throws()
    {
        var connection = new KafkaConnection("localhost", 9092);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Should throw either OperationCanceledException or InvalidOperationException
        // depending on which check runs first.
        var act = async () => await connection.SendPipelinedAsync<MetadataRequest, MetadataResponse>(
            new MetadataRequest { Topics = null },
            12,
            cts.Token);

        await Assert.That(act).ThrowsException();
    }

    [Test]
    public async Task SendFireAndForgetAsync_StillWorks_AfterRefactor()
    {
        var connection = new KafkaConnection("localhost", 9092);

        var act = async () => await connection.SendFireAndForgetAsync<MetadataRequest, MetadataResponse>(
            new MetadataRequest { Topics = null },
            12);

        await Assert.That(act).Throws<InvalidOperationException>();
    }

    private static async ValueTask ReservePendingRequestSlotAsync(KafkaConnection connection)
    {
        var method = typeof(KafkaConnection).GetMethod(
            "ReservePendingRequestSlotAsync",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var valueTask = (ValueTask)method.Invoke(connection, [CancellationToken.None])!;
        await valueTask;
    }

    private static void ReleasePendingRequestSlot(KafkaConnection connection)
    {
        var method = typeof(KafkaConnection).GetMethod(
            "ReleasePendingRequestSlot",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        method.Invoke(connection, []);
    }

    private static void AddPendingRequest(KafkaConnection connection, int correlationId, PooledPendingRequest request)
    {
        var method = typeof(KafkaConnection).GetMethod(
            "AddPendingRequest",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        method.Invoke(connection, [correlationId, request]);
    }

    private static bool TryRemovePendingRequest(KafkaConnection connection, int correlationId)
    {
        var method = typeof(KafkaConnection).GetMethod(
            "TryRemovePendingRequest",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        object?[] args = [correlationId, null];
        return (bool)method.Invoke(connection, args)!;
    }
}
