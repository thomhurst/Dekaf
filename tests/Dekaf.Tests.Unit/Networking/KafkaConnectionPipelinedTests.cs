using System.Buffers;
using System.IO.Pipelines;
using System.Reflection;
using Dekaf.Errors;
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
    public async Task PendingRequests_DisposeCancelsBlockedSlotWaiters()
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

        var waiters = new Task[capacity + 2];
        for (var i = 0; i < waiters.Length; i++)
        {
            waiters[i] = ReservePendingRequestSlotAsync(connection).AsTask();
        }

        await Assert.That(waiters.Any(static waiter => !waiter.IsCompleted)).IsTrue();

        await connection.DisposeAsync();

        foreach (var waiter in waiters)
        {
            await Assert.That(async () => await waiter.WaitAsync(TimeSpan.FromSeconds(1)))
                .Throws<ObjectDisposedException>();
        }
    }

    [Test]
    public async Task PendingRequests_ZeroOneTransitions_DoNotCancelPipeReader()
    {
        var connection = new KafkaConnection("localhost", 9092);
        var reader = new CountingPipeReader();
        SetPrivateField(connection, "_reader", reader);

        await ReservePendingRequestSlotAsync(connection);
        var request = new PooledPendingRequest();
        request.Initialize(responseHeaderVersion: 0, CancellationToken.None, registerCancellation: false);

        AddPendingRequest(connection, correlationId: 1, request);
        await Assert.That(reader.CancelPendingReadCount).IsEqualTo(0);

        await Assert.That(TryRemovePendingRequest(connection, correlationId: 1)).IsTrue();
        await Assert.That(reader.CancelPendingReadCount).IsEqualTo(0);

        await connection.DisposeAsync();
    }

    [Test]
    public async Task PendingRequests_OutstandingPastRequestTimeout_CancelsPipeReader()
    {
        var connection = new KafkaConnection(
            "localhost",
            9092,
            options: new ConnectionOptions { RequestTimeout = TimeSpan.FromHours(1) });
        var reader = new CountingPipeReader();
        SetPrivateField(connection, "_reader", reader);

        await ReservePendingRequestSlotAsync(connection);
        var request = new PooledPendingRequest();
        request.Initialize(responseHeaderVersion: 0, CancellationToken.None, registerCancellation: false);

        AddPendingRequest(connection, correlationId: 1, request);
        SetPrivateField(connection, "_receiveTimeoutDeadlineTimestamp", 1L);
        InvokeReceiveTimeout(connection);

        await Assert.That(reader.CancelPendingReadCount).IsEqualTo(1);

        await Assert.That(TryRemovePendingRequest(connection, correlationId: 1)).IsTrue();
        await connection.DisposeAsync();
    }

    [Test]
    public async Task ReceiveTimeoutRefresh_StaleZeroRefresh_DoesNotDisarmLivePendingRequest()
    {
        var connection = new KafkaConnection(
            "localhost",
            9092,
            options: new ConnectionOptions { RequestTimeout = TimeSpan.FromHours(1) });

        await ReservePendingRequestSlotAsync(connection);
        var first = new PooledPendingRequest();
        first.Initialize(responseHeaderVersion: 0, CancellationToken.None, registerCancellation: false);
        AddPendingRequest(connection, correlationId: 1, first);

        await Assert.That(TryRemovePendingRequest(connection, correlationId: 1)).IsTrue();

        await ReservePendingRequestSlotAsync(connection);
        var second = new PooledPendingRequest();
        second.Initialize(responseHeaderVersion: 0, CancellationToken.None, registerCancellation: false);
        AddPendingRequest(connection, correlationId: 2, second);
        await Assert.That(GetPrivateField<int>(connection, "_pendingRequestCount")).IsEqualTo(1);

        InvokeStaleZeroOrFreshReceiveTimeoutRefresh(connection);

        await Assert.That(GetPrivateField<long>(connection, "_receiveTimeoutDeadlineTimestamp")).IsNotEqualTo(0);
        await Assert.That(TryRemovePendingRequest(connection, correlationId: 2)).IsTrue();
        await connection.DisposeAsync();
    }

    [Test]
    public async Task ReceiveLoop_TimeoutCanceledRead_FailsPendingRequestWithReceiveTimeout()
    {
        var connection = new KafkaConnection(
            "localhost",
            9092,
            options: new ConnectionOptions { RequestTimeout = TimeSpan.FromMilliseconds(50) });
        var reader = new CancelablePipeReader();
        using var receiveCts = new CancellationTokenSource();
        SetPrivateField(connection, "_reader", reader);

        await ReservePendingRequestSlotAsync(connection);
        var request = new PooledPendingRequest();
        request.Initialize(responseHeaderVersion: 0, CancellationToken.None, registerCancellation: false);
        var requestTask = request.AsValueTask().AsTask();
        AddPendingRequest(connection, correlationId: 1, request);

        var receiveTask = StartReceiveLoop(connection, receiveCts.Token);
        await reader.ReadStarted.WaitAsync(TimeSpan.FromSeconds(1));
        SetPrivateField(connection, "_receiveTimeoutDeadlineTimestamp", 1L);
        InvokeReceiveTimeout(connection);

        await Assert.That(async () => await requestTask.WaitAsync(TimeSpan.FromSeconds(1)))
            .Throws<KafkaException>()
            .WithMessageContaining("Receive timeout");
        await receiveTask.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task ReceiveLoop_DisposalCanceledRead_IgnoresStaleReceiveTimeout()
    {
        var connection = new KafkaConnection("localhost", 9092);
        var reader = new CancelablePipeReader();
        using var receiveCts = new CancellationTokenSource();
        SetPrivateField(connection, "_reader", reader);

        await ReservePendingRequestSlotAsync(connection);
        var request = new PooledPendingRequest();
        request.Initialize(responseHeaderVersion: 0, CancellationToken.None, registerCancellation: false);
        var requestTask = request.AsValueTask().AsTask();
        AddPendingRequest(connection, correlationId: 1, request);
        SetPrivateField(connection, "_receiveTimeoutExpired", 1);

        var receiveTask = StartReceiveLoop(connection, receiveCts.Token);
        await reader.ReadStarted.WaitAsync(TimeSpan.FromSeconds(1));
        await receiveCts.CancelAsync();
        reader.CancelPendingRead();

        await Assert.That(async () => await requestTask.WaitAsync(TimeSpan.FromSeconds(1)))
            .Throws<OperationCanceledException>()
            .WithMessageContaining("Connection closing");
        await receiveTask.WaitAsync(TimeSpan.FromSeconds(1));
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

    private static Task StartReceiveLoop(KafkaConnection connection, CancellationToken cancellationToken)
    {
        var method = typeof(KafkaConnection).GetMethod(
            "ReceiveLoopAsync",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (Task)method.Invoke(connection, [cancellationToken])!;
    }

    private static void InvokeReceiveTimeout(KafkaConnection connection)
    {
        var method = typeof(KafkaConnection).GetMethod(
            "OnReceiveTimeout",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        method.Invoke(connection, []);
    }

    private static void InvokeStaleZeroOrFreshReceiveTimeoutRefresh(KafkaConnection connection)
    {
        var staleOverload = typeof(KafkaConnection).GetMethod(
            "RefreshReceiveTimeout",
            BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            types: [typeof(int)],
            modifiers: null);
        if (staleOverload is not null)
        {
            staleOverload.Invoke(connection, [0]);
            return;
        }

        var method = typeof(KafkaConnection).GetMethod(
            "RefreshReceiveTimeout",
            BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            types: [],
            modifiers: null)!;
        method.Invoke(connection, []);
    }

    private static void SetPrivateField<T>(KafkaConnection connection, string name, T value)
    {
        var field = typeof(KafkaConnection).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(connection, value);
    }

    private static T GetPrivateField<T>(KafkaConnection connection, string name)
    {
        var field = typeof(KafkaConnection).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (T)field.GetValue(connection)!;
    }

    private sealed class CountingPipeReader : PipeReader
    {
        private int _cancelPendingReadCount;

        public int CancelPendingReadCount => Volatile.Read(ref _cancelPendingReadCount);

        public override void AdvanceTo(SequencePosition consumed)
        {
        }

        public override void AdvanceTo(SequencePosition consumed, SequencePosition examined)
        {
        }

        public override void CancelPendingRead()
            => Interlocked.Increment(ref _cancelPendingReadCount);

        public override void Complete(Exception? exception = null)
        {
        }

        public override ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new ReadResult(ReadOnlySequence<byte>.Empty, isCanceled: false, isCompleted: false));

        public override bool TryRead(out ReadResult result)
        {
            result = default;
            return false;
        }
    }

    private sealed class CancelablePipeReader : PipeReader
    {
        private readonly TaskCompletionSource<ReadResult> _readResult =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _readStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task ReadStarted => _readStarted.Task;

        public override void AdvanceTo(SequencePosition consumed)
        {
        }

        public override void AdvanceTo(SequencePosition consumed, SequencePosition examined)
        {
        }

        public override void CancelPendingRead()
            => _readResult.TrySetResult(
                new ReadResult(ReadOnlySequence<byte>.Empty, isCanceled: true, isCompleted: false));

        public override void Complete(Exception? exception = null)
        {
            if (exception is not null)
            {
                _readResult.TrySetException(exception);
                return;
            }

            _readResult.TrySetResult(
                new ReadResult(ReadOnlySequence<byte>.Empty, isCanceled: false, isCompleted: true));
        }

        public override ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
        {
            _readStarted.TrySetResult();
            return new ValueTask<ReadResult>(_readResult.Task);
        }

        public override bool TryRead(out ReadResult result)
        {
            result = default;
            return false;
        }
    }
}
