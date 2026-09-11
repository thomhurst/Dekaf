using Dekaf.Consumer;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Serialization;
using Dekaf.StressTests.Reporting;

namespace Dekaf.StressTests.Scenarios;

internal sealed class ConsumerFollowerRecoveryStressTest : IStressTestScenario
{
    public string Name => "consumer-follower-recovery";
    public string Client => "Dekaf";

    public async Task<StressTestResult> RunAsync(StressTestOptions options, CancellationToken cancellationToken)
    {
        if (options.BrokerCount != 3)
            throw new ArgumentException("Follower recovery requires the three-broker rack-aware environment.", nameof(options));
        var servers = options.BootstrapServers.Split(',');
        const int connections = 3;
        const string clientId = "stress-follower-recovery";
        var pool = new FollowerRecoveryPool(new ConnectionPool(clientId, new ConnectionOptions(),
            StressClientLogging.LoggerFactory, connections), options.Topic);
        var metadata = new MetadataManager(pool, servers);
        await using var consumer = new KafkaConsumer<string, string>(new ConsumerOptions
        {
            BootstrapServers = servers,
            ClientId = clientId,
            ClientRack = "rack-a",
            AutoOffsetReset = AutoOffsetReset.None,
            OffsetCommitMode = OffsetCommitMode.Manual,
            EnableAutoOffsetStore = false,
            EnableFetchSessions = false,
            ConnectionsPerBroker = connections,
            MaxConnectionsPerBroker = connections,
            EnableAdaptiveConnections = false,
            PrefetchPipelineDepth = 5,
            MaxPollRecords = 1000,
            FetchMinBytes = 1024,
            FetchMaxWaitMs = 200,
            MaxPartitionFetchBytes = 1024 * 1024,
            FetchMaxBytes = 16 * 1024 * 1024
        }, Serializers.String, Serializers.String, pool, metadata, StressClientLogging.LoggerFactory);
        await consumer.InitializeAsync(cancellationToken).ConfigureAwait(false);
        var endOffsets = await StressTestHelpers.QueryEndOffsetsAsync(consumer, options.Topic, options.Partitions, cancellationToken).ConfigureAwait(false);
        var oracle = new FollowerRecoveryOracle(endOffsets);
        pool.Oracle = oracle;
        var partitions = new TopicPartition[options.Partitions];
        var assignments = new TopicPartitionOffset[options.Partitions];
        for (var p = 0; p < partitions.Length; p++)
        {
            partitions[p] = new TopicPartition(options.Topic, p);
            assignments[p] = new TopicPartitionOffset(options.Topic, p, 0);
        }
        consumer.IncrementalAssign(assignments);
        FollowerRecoverySnapshot? measured = null;

        var result = await StressTestHelpers.RunConsumerAsync(options, this,
            async (throughput, token) =>
            {
                oracle.BeginPhase();
                var start = oracle.Snapshot();
                var progress = new PeriodicProgressReporter(throughput);
                try
                {
                    await foreach (var record in consumer.ConsumeAsync(token).ConfigureAwait(false))
                    {
                        var rewind = oracle.RecordConsumed(record.Partition, record.Offset);
                        throughput.RecordMessage(record.Value?.Length ?? 0);
                        progress.RecordMessage();
                        if (rewind)
                        {
                            consumer.Positions.SeekToBeginning(partitions);
                            oracle.CompleteRewind();
                        }
                    }
                }
                finally
                {
                    var snapshot = oracle.Snapshot();
                    measured = snapshot.Since(start);
                    Console.WriteLine($"  Follower recovery: faults={measured.InjectedFaults}, matching leader responses={measured.MatchingLeaderResponses}, leader responses after verified progress={measured.LeaderResponsesAfterVerifiedProgress}, complete passes={measured.CompletePasses}");
                    // A duration boundary can cancel the last pending retry. Require actual completed
                    // recovery in every phase rather than counting warmup-only faults as measured work.
                    if (snapshot.Violations != 0)
                        throughput.RecordError(new InvalidOperationException(oracle.LastViolation), "follower recovery correctness");
                    if (measured.InjectedFaults == 0 || measured.MatchingLeaderResponses + measured.LeaderResponsesAfterVerifiedProgress == 0
                        || measured.CompletePasses == 0
                        || (throughput.Warmup is not null && measured.MatchingLeaderResponses == 0))
                        throughput.RecordError(new InvalidOperationException("The phase did not exercise follower recovery and a complete replay."), "follower recovery coverage");
                }
            }, connections, captureConsumerDiagnostics: () => StressTestHelpers.CaptureConsumerDiagnostics(consumer),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        result.FollowerRecovery = measured;
        return result;
    }
}
