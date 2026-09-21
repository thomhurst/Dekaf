using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using Dekaf.Errors;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using NSubstitute;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Model-based check of the idempotent send loop against <see cref="SequenceContractBroker"/>.
/// A seeded fault plan rejects, loses or fails produce requests the way a broker and its
/// connection can, and the broker's append log is then checked for the guarantee idempotence
/// exists for: no record appended twice, every partition's records in order, and no more epoch
/// bumps than faults that can cause one. The seeds fix the fault plan; the interleaving of the
/// test thread and the send loop is free, because the guarantee holds for every interleaving.
/// </summary>
[Timeout(120_000)]
public sealed class IdempotentSequenceModelTests : ScriptedProduceResponseFixture
{
    private const string Topic = "model-topic";
    private const long ProducerId = 1234;
    private const int PartitionCount = 3;
    private const int BatchesPerPartition = 40;
    private const int FastResponses = 8;
    private const int SlowResponses = 2048;

    // Below the class timeout, so a stall is reported with the broker's request trace.
    private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(60);

    // Fast responses keep the pipeline shallow; slow ones fill it to the in-flight limit, so the
    // send loop processes responses while a coalesced wave waits for capacity (step 6).
    [Test]
    [Arguments(1, FastResponses)]
    [Arguments(7, FastResponses)]
    [Arguments(42, FastResponses)]
    [Arguments(1337, FastResponses)]
    [Arguments(20260920, FastResponses)]
    [Arguments(1, SlowResponses)]
    [Arguments(7, SlowResponses)]
    [Arguments(42, SlowResponses)]
    [Arguments(1337, SlowResponses)]
    [Arguments(20260920, SlowResponses)]
    public async Task RandomFaults_NeverDuplicateOrReorderAppendedRecords(
        int seed, int maxPumpYields, CancellationToken cancellationToken)
    {
        var options = CreateOptions();
        var broker = new FaultInjectingBroker(seed, maxPumpYields: maxPumpYields);
        var (pool, _) = broker.CreateConnection();
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var state = new ProducerState(new ProducerIdAndEpoch(ProducerId, 0));
        accumulator.PublishProducerState(state.Read());
        var bumps = 0;
        var sender = CreateSender(
            pool, options, accumulator, (_, _, _, _, _) => { },
            bumpEpoch: (expectedEpoch, _) =>
            {
                var current = state.Read();
                if (current.Epoch == expectedEpoch)
                {
                    current = new ProducerIdAndEpoch(ProducerId, (short)(expectedEpoch + 1));
                    accumulator.PublishProducerState(current);
                    state.Write(current);
                    Interlocked.Increment(ref bumps);
                }

                return new ValueTask<ProducerIdAndEpoch>(current);
            },
            getProducerState: state.Read);
        var pump = broker.RunResponsePumpAsync(cancellationToken);

        try
        {
            var planner = new Random(seed);
            var deliveries = new List<(TopicPartition Partition, long FirstOrdinal, int RecordCount, bool Expired, Task<RecordMetadata> Delivery)>();
            var nextOrdinal = new long[PartitionCount];
            for (var i = 0; i < BatchesPerPartition; i++)
            {
                for (var p = 0; p < PartitionCount; p++)
                {
                    var topicPartition = new TopicPartition(Topic, p);
                    var recordCount = planner.Next(1, 6);
                    // Already past its delivery deadline: fails at the first retry decision,
                    // whatever happened to the request that carried it.
                    var expired = planner.Next(20) == 0;
                    var current = state.Read();
                    var (batch, delivery) = CreateTrackedBatch(
                        valueTaskSourcePool, topicPartition, current.ProducerId, current.Epoch, recordCount,
                        expired ? Stopwatch.GetTimestamp() - options.DeliveryTimeoutTicks - 1 : 0);
                    broker.Register(batch.RecordBatch, topicPartition, nextOrdinal[p], recordCount);
                    deliveries.Add((topicPartition, nextOrdinal[p], recordCount, expired, delivery));
                    nextOrdinal[p] += recordCount;
                    sender.Enqueue(batch);
                }

                if (planner.Next(3) == 0)
                    await Task.Yield();
            }

            var failed = new HashSet<(TopicPartition, long)>();
            using var stallTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            stallTimeout.CancelAfter(StallTimeout);
            foreach (var (partition, firstOrdinal, _, expired, delivery) in deliveries)
            {
                try
                {
                    await delivery.WaitAsync(stallTimeout.Token);
                }
                catch (KafkaTimeoutException) when (expired)
                {
                    failed.Add((partition, firstOrdinal));
                }
                catch (OperationCanceledException) when (stallTimeout.IsCancellationRequested)
                {
                    // A batch parked for good (a hold that never releases, a retry nothing wakes)
                    // must fail with what the broker saw, not hang the run.
                    var undelivered = deliveries
                        .Where(entry => !entry.Delivery.IsCompleted)
                        .Select(entry => $"{entry.Partition.Partition}:{entry.FirstOrdinal}");
                    Assert.Fail(
                        $"The send loop stalled: no delivery for partition:ordinal {string.Join(", ", undelivered)} " +
                        $"after {StallTimeout.TotalSeconds:N0} s.{Environment.NewLine}{broker.DescribeRequests()}");
                }
            }

            for (var p = 0; p < PartitionCount; p++)
            {
                var topicPartition = new TopicPartition(Topic, p);
                var log = broker.Contract.GetLog(topicPartition);
                var appended = new HashSet<long>();
                var lastOrdinal = -1L;
                foreach (var entry in log)
                {
                    // Strictly increasing first ordinals: nothing appended twice, nothing reordered.
                    if (entry.FirstOrdinal <= lastOrdinal)
                    {
                        Assert.Fail(
                            $"{topicPartition}: records from ordinal {entry.FirstOrdinal} were appended after ordinal {lastOrdinal} " +
                            $"({entry}).{Environment.NewLine}{broker.DescribeRequests(topicPartition)}");
                    }

                    lastOrdinal = entry.FirstOrdinal + entry.RecordCount - 1;
                    appended.Add(entry.FirstOrdinal);
                }

                foreach (var (partition, firstOrdinal, _, _, _) in deliveries)
                {
                    if (partition == topicPartition && !failed.Contains((partition, firstOrdinal)))
                        await Assert.That(appended.Contains(firstOrdinal)).IsTrue();
                }
            }

            await Assert.That(Volatile.Read(ref bumps)).IsLessThanOrEqualTo(broker.BumpCapableFaults);
        }
        finally
        {
            await sender.DisposeAsync();
            broker.Stop();
            await pump;
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    [Test]
    public async Task BatchStraddlingTheSequenceWrap_IsAccepted_AndTheNextBatchContinuesAfterIt(CancellationToken cancellationToken)
    {
        // The sequence space is [0, int.MaxValue]: a long-lived partition reaches the end of it
        // and continues at 0 (Java's DefaultRecordBatch.incrementSequence). Without the wrap the
        // counter went negative, which the broker rejects and the send loop reads as "unassigned".
        var options = CreateOptions();
        var broker = new FaultInjectingBroker(seed: 0, faultsEnabled: false);
        var (pool, connection) = broker.CreateConnection();
        connection.CaptureProduceRequests = true;
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var state = new ProducerIdAndEpoch(ProducerId, 0);
        accumulator.PublishProducerState(state);
        var partition = new TopicPartition(Topic, 0);
        var bumps = 0;
        var sender = CreateSender(
            pool, options, accumulator, (_, _, _, _, _) => { },
            bumpEpoch: (_, _) =>
            {
                Interlocked.Increment(ref bumps);
                return new ValueTask<ProducerIdAndEpoch>(state);
            },
            getProducerState: () => state);
        var pump = broker.RunResponsePumpAsync(cancellationToken);

        try
        {
            // The partition has produced everything up to three sequences before the end.
            await Assert.That(accumulator.GetAndIncrementSequence(partition, 0, state, out _)).IsEqualTo(0);
            await Assert.That(accumulator.GetAndIncrementSequence(partition, int.MaxValue - 2)).IsEqualTo(0);
            broker.Contract.Seed(partition, ProducerId, epoch: 0, lastSequence: int.MaxValue - 3);

            var ordinal = 0L;
            var deliveries = new List<Task<RecordMetadata>>();
            foreach (var recordCount in new[] { 5, 4, 3 })
            {
                var (batch, delivery) = CreateTrackedBatch(valueTaskSourcePool, partition, ProducerId, 0, recordCount);
                broker.Register(batch.RecordBatch, partition, ordinal, recordCount);
                ordinal += recordCount;
                deliveries.Add(delivery);
                sender.Enqueue(batch);
            }

            using var stallTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            stallTimeout.CancelAfter(StallTimeout);
            try
            {
                foreach (var delivery in deliveries)
                    await delivery.WaitAsync(stallTimeout.Token);
            }
            catch (OperationCanceledException) when (stallTimeout.IsCancellationRequested)
            {
                Assert.Fail($"The send loop stalled.{Environment.NewLine}{broker.DescribeRequests()}");
            }

            int[] baseSequences;
            lock (connection.CapturedProduceRequests)
                baseSequences = connection.CapturedProduceRequests
                    .SelectMany(request => request.Batches)
                    .Select(batch => batch.BaseSequence)
                    .ToArray();

            await Assert.That(baseSequences).IsEquivalentTo([int.MaxValue - 2, 2, 6]);
            await Assert.That(broker.Contract.GetLog(partition).Select(entry => entry.FirstOrdinal).ToArray())
                .IsEquivalentTo([0L, 5L, 9L]);
            await Assert.That(Volatile.Read(ref bumps)).IsEqualTo(0);
        }
        finally
        {
            await sender.DisposeAsync();
            broker.Stop();
            await pump;
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

    private static ProducerOptions CreateOptions() => new()
    {
        BootstrapServers = ["localhost:9092"],
        MaxInFlightRequestsPerConnection = 5,
        ConnectionsPerBroker = 1,
        EnableAdaptiveConnections = false,
        EnableIdempotence = true,
        Acks = Acks.All,
        DeliveryTimeoutMs = 120_000,
        RequestTimeoutMs = 120_000,
        RetryBackoffMs = 1,
        RetryBackoffMaxMs = 1,
        LingerMs = 0
    };

    /// <summary>
    /// A batch of <paramref name="recordCount"/> records stamped with the given producer ID and
    /// epoch, as the accumulator would seal it, with its delivery exposed as a task.
    /// </summary>
    internal static (ReadyBatch Batch, Task<RecordMetadata> Delivery) CreateTrackedBatch(
        ValueTaskSourcePool<RecordMetadata> pool,
        TopicPartition topicPartition,
        long producerId,
        short producerEpoch,
        int recordCount = 1,
        long createdStopwatchTimestamp = 0)
    {
        var batch = new ReadyBatch();
        var source = pool.Rent();
        var delivery = source.Task.AsTask();
        var sources = ArrayPool<PooledValueTaskSource<RecordMetadata>>.Shared.Rent(1);
        sources[0] = source;
        batch.Initialize(
            topicPartition,
            new RecordBatch
            {
                // Sequences advance by the record count, so a batch without records would make
                // every stamp 0 and hide a wrong sequence.
                Records = new Record[recordCount],
                ProducerId = producerId,
                ProducerEpoch = producerEpoch
            },
            sources,
            completionSourcesCount: 1,
            recordCount: recordCount,
            dataSize: 100,
            createdStopwatchTimestamp: createdStopwatchTimestamp);
        // Carry-over only drains wire-ready batches; an unmarked batch would wait for
        // asynchronous compression that never runs in this harness.
        batch.MarkPreSerialized();
        batch.TrySetMemoryReleased();
        return (batch, delivery);
    }

    /// <summary>The producer's published state as the test and the send loop share it.</summary>
    private sealed class ProducerState(ProducerIdAndEpoch initial)
    {
        private ProducerIdAndEpoch _value = initial;

        public ProducerIdAndEpoch Read() => Volatile.Read(ref _value);

        public void Write(ProducerIdAndEpoch value) => Volatile.Write(ref _value, value);
    }

    private enum Fault
    {
        None,

        /// <summary>The broker rejects the request's first batch without appending it.</summary>
        OutOfOrderSequence,

        /// <summary>Everything is appended, then the response is lost.</summary>
        ResponseLostAfterAppend,

        /// <summary>The connection dies before the broker reads the request.</summary>
        DisconnectBeforeAppend,

        /// <summary>Nothing is appended; every partition answers NotLeaderOrFollower.</summary>
        NotLeader,

        /// <summary>Everything is appended, then every partition answers a retriable error.</summary>
        RetriableErrorAfterAppend
    }

    /// <summary>
    /// Connects a <see cref="TestKafkaConnection"/> to a <see cref="SequenceContractBroker"/> and
    /// injects faults from a seeded plan. Requests are applied when they are written; their
    /// responses are released in order by <see cref="RunResponsePumpAsync"/>, and a lost response
    /// takes every response still outstanding on the connection with it.
    /// </summary>
    private sealed class FaultInjectingBroker(int seed, bool faultsEnabled = true, int maxPumpYields = 8)
    {
        private readonly Random _faults = new(seed);
        private readonly Random _pacing = new(seed ^ 0x5bd1e995);
        private readonly ConcurrentDictionary<object, (TopicPartition Partition, long FirstOrdinal, int RecordCount)> _identities =
            new(ReferenceEqualityComparer.Instance);
        private readonly Channel<(TaskCompletionSource<ProduceResponse> Source, ProduceResponse? Response)> _outstanding =
            Channel.CreateUnbounded<(TaskCompletionSource<ProduceResponse>, ProduceResponse?)>();
        private readonly List<(TopicPartition Partition, string Line)> _requestTrace = [];
        private int _bumpCapableFaults;
        private int _requestNumber;

        public SequenceContractBroker Contract { get; } = new();

        /// <summary>
        /// Every batch the broker saw, in write order, for a failure message; of one partition
        /// when <paramref name="topicPartition"/> is given.
        /// </summary>
        public string DescribeRequests(TopicPartition? topicPartition = null)
        {
            lock (_requestTrace)
            {
                return string.Join(
                    Environment.NewLine,
                    _requestTrace
                        .Where(entry => topicPartition is null || entry.Partition == topicPartition.Value)
                        .Select(entry => topicPartition is null ? $"  p{entry.Partition.Partition}{entry.Line}" : entry.Line));
            }
        }

        /// <summary>Faults that leave a sequence gap or reject a batch, so that a bump may follow.</summary>
        public int BumpCapableFaults => Volatile.Read(ref _bumpCapableFaults);

        public void Register(RecordBatch recordBatch, TopicPartition partition, long firstOrdinal, int recordCount)
            => _identities[recordBatch] = (partition, firstOrdinal, recordCount);

        public (IConnectionPool Pool, TestKafkaConnection Connection) CreateConnection()
        {
            var connection = new TestKafkaConnection { SendProducePipelinedAfterWriteForRequest = OnWrite };
            var pool = Substitute.For<IConnectionPool>();
            pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(connection);
            pool.GetConnectionByIndexAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(connection);
            return (pool, connection);
        }

        public void Stop() => _outstanding.Writer.TryComplete();

        public async Task RunResponsePumpAsync(CancellationToken cancellationToken)
        {
            try
            {
                await foreach (var (source, response) in _outstanding.Reader.ReadAllAsync(cancellationToken))
                {
                    // Scheduling points, not time: they vary how many requests are in flight.
                    for (var yields = _pacing.Next(maxPumpYields); yields > 0; yields--)
                        await Task.Yield();

                    if (response is not null)
                    {
                        source.TrySetResult(response);
                        continue;
                    }

                    source.TrySetException(new IOException("Injected connection reset."));
                    while (_outstanding.Reader.TryRead(out var lost))
                        lost.Source.TrySetException(new IOException("Injected connection reset."));
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private ValueTask<Task<ProduceResponse>> OnWrite(CapturedProduceRequest request)
        {
            var fault = NextFault();
            var requestNumber = ++_requestNumber;
            if (fault is Fault.OutOfOrderSequence or Fault.DisconnectBeforeAppend or Fault.NotLeader)
                Interlocked.Increment(ref _bumpCapableFaults);

            var source = new TaskCompletionSource<ProduceResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            ProduceResponse? response = null;
            if (fault == Fault.DisconnectBeforeAppend)
            {
                foreach (var batch in request.Batches)
                    Trace(requestNumber, fault, batch, "not read by the broker");
            }
            else
            {
                var results = new List<(string Topic, int Partition, ErrorCode ErrorCode, long BaseOffset)>(request.Batches.Count);
                for (var i = 0; i < request.Batches.Count; i++)
                {
                    var batch = request.Batches[i];
                    var (errorCode, baseOffset) = fault switch
                    {
                        Fault.NotLeader => (ErrorCode.NotLeaderOrFollower, -1L),
                        // Legal only for a batch the broker does not hold: it finds duplicates
                        // before it checks the sequence.
                        Fault.OutOfOrderSequence when i == 0 && !IsHeld(batch) => (ErrorCode.OutOfOrderSequenceNumber, -1L),
                        _ => Apply(batch)
                    };
                    var brokerResult = errorCode;
                    if (fault == Fault.RetriableErrorAfterAppend)
                        (errorCode, baseOffset) = (ErrorCode.RequestTimedOut, -1L);
                    results.Add((batch.Topic, batch.Partition, errorCode, baseOffset));
                    Trace(requestNumber, fault, batch, $"broker={brokerResult} answered={errorCode}");
                }

                if (fault != Fault.ResponseLostAfterAppend)
                    response = CreateResponse(results);
            }

            _outstanding.Writer.TryWrite((source, response));
            return new ValueTask<Task<ProduceResponse>>(source.Task);
        }

        private void Trace(int requestNumber, Fault fault, CapturedProduceBatch batch, string outcome)
        {
            var (partition, firstOrdinal, recordCount) = _identities[batch.RecordBatch];
            lock (_requestTrace)
            {
                _requestTrace.Add((partition,
                    $"  request {requestNumber} [{fault}] ordinals {firstOrdinal}..{firstOrdinal + recordCount - 1} " +
                    $"stamp ({batch.ProducerId}, {batch.ProducerEpoch}, {batch.BaseSequence}): {outcome}"));
            }
        }

        private Fault NextFault()
        {
            if (!faultsEnabled)
                return Fault.None;

            return _faults.Next(100) switch
            {
                < 8 => Fault.OutOfOrderSequence,
                < 20 => Fault.ResponseLostAfterAppend,
                < 26 => Fault.DisconnectBeforeAppend,
                < 32 => Fault.NotLeader,
                < 40 => Fault.RetriableErrorAfterAppend,
                _ => Fault.None
            };
        }

        private bool IsHeld(CapturedProduceBatch batch)
        {
            var (partition, _, recordCount) = _identities[batch.RecordBatch];
            return Contract.Holds(partition, batch.ProducerId, batch.ProducerEpoch, batch.BaseSequence, recordCount);
        }

        private (ErrorCode ErrorCode, long BaseOffset) Apply(CapturedProduceBatch batch)
        {
            var (partition, firstOrdinal, recordCount) = _identities[batch.RecordBatch];
            return Contract.Append(
                partition, batch.ProducerId, batch.ProducerEpoch, batch.BaseSequence, recordCount, firstOrdinal);
        }

        private static ProduceResponse CreateResponse(
            List<(string Topic, int Partition, ErrorCode ErrorCode, long BaseOffset)> results)
        {
            var topics = new List<ProduceResponseTopicData>();
            foreach (var group in results.GroupBy(result => result.Topic))
            {
                var partitions = group
                    .Select(result => new ProduceResponsePartitionData
                    {
                        Index = result.Partition,
                        ErrorCode = result.ErrorCode,
                        BaseOffset = result.BaseOffset
                    })
                    .ToArray();
                topics.Add(new ProduceResponseTopicData
                {
                    Name = group.Key,
                    PartitionCount = partitions.Length,
                    PartitionResponses = partitions
                });
            }

            return new ProduceResponse { TopicCount = topics.Count, Responses = [.. topics] };
        }
    }
}
