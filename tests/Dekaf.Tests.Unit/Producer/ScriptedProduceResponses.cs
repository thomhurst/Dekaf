using System.Buffers;
using Dekaf.Compression;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Scripts a fixed set of produce responses for a <see cref="TestKafkaConnection"/> and converts
/// any unscripted (extra) send into an immediate, diagnosable failure. Previously an exhausted
/// script threw <c>InvalidOperationException("Queue empty.")</c> from <c>Queue.Dequeue</c>, which
/// <c>BrokerSender.SendCoalescedAsync</c> treats as a retriable connection failure — with the
/// zero retry backoff these tests configure, the sender retried against the exhausted script
/// forever and the test died as an opaque 30s timeout (#2187). An unscripted send now records
/// a diagnostic (rethrow it from an <c>[After(Test)]</c> hook), cancels the token issued by
/// <see cref="Guard"/> so in-test waits abort instantly, and parks the sender on a response
/// that never completes so no retry loop can start.
/// </summary>
internal sealed class ScriptedProduceResponses : IDisposable
{
    private readonly Queue<TaskCompletionSource<ProduceResponse>> _responses;
    private readonly Action? _onSend;
    private readonly TaskCompletionSource<ProduceResponse> _unscriptedSendResponse = new();
    // object, not System.Threading.Lock: this project also targets net8.0.
    private readonly object _lock = new();
    private CancellationTokenSource? _guardCts;
    private int _dequeuedCount;
    private volatile InvalidOperationException? _unscriptedSendFailure;

    public ScriptedProduceResponses(
        Queue<TaskCompletionSource<ProduceResponse>> responses,
        Action? onSend)
    {
        _responses = responses;
        _onSend = onSend;
    }

    public InvalidOperationException? UnscriptedSendFailure => _unscriptedSendFailure;

    /// <summary>
    /// Links the test's cancellation token to this script so an unscripted send aborts every
    /// in-test wait immediately instead of hanging until the test timeout.
    /// </summary>
    public CancellationToken Guard(CancellationToken testToken)
    {
        CancellationTokenSource guardCts;
        bool alreadyFailed;
        lock (_lock)
        {
            guardCts = CancellationTokenSource.CreateLinkedTokenSource(testToken);
            _guardCts = guardCts;
            alreadyFailed = _unscriptedSendFailure is not null;
        }

        if (alreadyFailed)
            guardCts.Cancel();
        return guardCts.Token;
    }

    public Task<ProduceResponse> Dequeue()
    {
        TaskCompletionSource<ProduceResponse>? scripted = null;
        CancellationTokenSource? guardCts = null;
        lock (_lock)
        {
            if (_responses.Count > 0)
            {
                scripted = _responses.Dequeue();
                _dequeuedCount++;
            }
            else
            {
                // Capture the sender-side stack: it names the code path that produced the
                // extra send, which is the input the underlying-race investigation needs.
                _unscriptedSendFailure ??= new InvalidOperationException(
                    $"Unscripted send: all {_dequeuedCount} scripted produce responses were " +
                    "already consumed when the sender issued another send. Sender stack: " +
                    Environment.StackTrace);
                guardCts = _guardCts;
            }
        }

        if (scripted is not null)
        {
            _onSend?.Invoke();
            return scripted.Task;
        }

        // Cancel asynchronously: a synchronous Cancel would resume guarded test waits inline
        // on this sender-loop thread, and a resumed continuation may join this same thread
        // (sender.DisposeAsync in the test's finally). If Dispose raced ahead and the source
        // is already disposed, CancelAsync surfaces ObjectDisposedException through its
        // returned task rather than synchronously — observe the fault so it cannot become
        // an unobserved-task exception; the recorded failure still fails the test.
        guardCts?.CancelAsync().ContinueWith(
            static t => _ = t.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        return _unscriptedSendResponse.Task;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _guardCts?.Dispose();
            _guardCts = null;
        }
    }
}

/// <summary>
/// Base fixture for test classes that script produce responses through
/// <see cref="ScriptedProduceResponses"/>: registers every script the test creates, links
/// them into the test's cancellation token via <see cref="GuardUnscriptedSends"/>, and
/// fails the test after it finishes if any unscripted send was recorded.
/// </summary>
public abstract class ScriptedProduceResponseFixture
{
    private readonly List<ScriptedProduceResponses> _scriptedResponses = [];

    private protected ScriptedProduceResponses RegisterScript(
        Queue<TaskCompletionSource<ProduceResponse>> responses,
        Action? onSend = null)
    {
        var scripted = new ScriptedProduceResponses(responses, onSend);
        _scriptedResponses.Add(scripted);
        return scripted;
    }

    /// <summary>
    /// Links the test's cancellation token to every script registered so far, so an
    /// unscripted (extra) send aborts in-test waits immediately instead of hanging
    /// until the test timeout (#2187).
    /// </summary>
    protected CancellationToken GuardUnscriptedSends(CancellationToken testToken)
    {
        var guarded = testToken;
        foreach (var scripted in _scriptedResponses)
            guarded = scripted.Guard(guarded);
        return guarded;
    }

    [After(Test)]
    public void FailTestOnUnscriptedSend()
    {
        InvalidOperationException? failure = null;
        foreach (var scripted in _scriptedResponses)
        {
            scripted.Dispose();
            failure ??= scripted.UnscriptedSendFailure;
        }

        _scriptedResponses.Clear();
        if (failure is not null)
            throw failure;
    }

    /// <summary>
    /// Creates a minimal ReadyBatch suitable for send loop testing.
    /// Sets MemoryReleased=true by default to skip accumulator memory tracking; pass
    /// markMemoryReleased: false for tests that exercise real buffer-memory accounting
    /// (the caller must then reserve dataSize bytes on the accumulator first).
    /// </summary>
    private protected static ReadyBatch CreateTestBatch(
        ValueTaskSourcePool<RecordMetadata> pool,
        string topic, int partition, int messageCount = 1,
        bool markMemoryReleased = true, int dataSize = 100)
    {
        var batch = new ReadyBatch();
        var sources = ArrayPool<PooledValueTaskSource<RecordMetadata>>.Shared.Rent(messageCount);
        for (var i = 0; i < messageCount; i++)
            sources[i] = pool.Rent();

        batch.Initialize(
            new TopicPartition(topic, partition),
            new RecordBatch { Records = Array.Empty<Record>() },
            sources,
            messageCount,
            dataSize: dataSize);

        if (markMemoryReleased)
        {
            // Skip accumulator memory tracking in tests
            batch.TrySetMemoryReleased();
        }

        return batch;
    }

    /// <summary>
    /// Creates a ProduceResponse with success for the given topic/partition.
    /// </summary>
    private protected static ProduceResponse CreateSuccessResponse(
        string topic, int partition, long baseOffset, int throttleTimeMs = 0) =>
        new()
        {
            ThrottleTimeMs = throttleTimeMs,
            TopicCount = 1,
            Responses =
            [
                new ProduceResponseTopicData
                {
                    Name = topic,
                    PartitionCount = 1,
                    PartitionResponses =
                    [
                        new ProduceResponsePartitionData
                        {
                            Index = partition,
                            ErrorCode = ErrorCode.None,
                            BaseOffset = baseOffset
                        }
                    ]
                }
            ]
        };

    /// <summary>
    /// Creates a BrokerSender with standard test defaults, reducing constructor boilerplate.
    /// </summary>
    private protected static BrokerSender CreateSender(
        IConnectionPool pool,
        ProducerOptions options,
        RecordAccumulator accumulator,
        Action<TopicPartition, long, DateTimeOffset, int, Exception?> onAcknowledgement,
        MetadataManager? metadataManager = null,
        Action<ReadyBatch, int>? rerouteBatch = null,
        Action<int>? onBrokerThrottle = null,
        Func<long>? getTimestamp = null,
        Func<int, CancellationToken, ValueTask>? delayForThrottle = null,
        Action? onBlockedBucketRequeued = null,
        Action? onPipelinedResponseAcquired = null,
        Action? onWaveCoalesceStarted = null,
        Action? onIdleWaitStarted = null,
        BrokerUnackedByteBudget? unackedBudget = null,
        int produceApiVersion = 9,
        bool isTransactional = false,
        bool usesTransactionV2 = false,
        Func<ReadyBatch[], int, Action<Exception?>, HashSet<TopicPartition>, HashSet<TopicPartition>,
            TransactionPartitionEnrollmentResult>?
            tryEnsurePartitionsInTransaction = null) =>
        new(
            brokerId: 1, pool,
            metadataManager ?? new MetadataManager(pool, options.BootstrapServers),
            accumulator, options,
            new CompressionCodecRegistry(),
            inflightTracker: new PartitionInflightTracker(),
            getProduceApiVersion: () => produceApiVersion,
            setProduceApiVersion: _ => { },
            isTransactional: () => isTransactional,
            tryEnsurePartitionsInTransaction: tryEnsurePartitionsInTransaction,
            bumpEpoch: null,
            getCurrentEpoch: null,
            rerouteBatch,
            onAcknowledgement: onAcknowledgement,
            logger: null,
            onBrokerThrottle: onBrokerThrottle,
            getTimestamp: getTimestamp,
            delayForThrottle: delayForThrottle,
            onBlockedBucketRequeued: onBlockedBucketRequeued,
            unackedBudget: unackedBudget,
            usesTransactionV2: () => usesTransactionV2,
            onPipelinedResponseAcquired: onPipelinedResponseAcquired,
            onWaveCoalesceStarted: onWaveCoalesceStarted,
            onIdleWaitStarted: onIdleWaitStarted);
}
