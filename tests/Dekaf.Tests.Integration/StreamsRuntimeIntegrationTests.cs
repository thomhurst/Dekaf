using Dekaf.Consumer;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Producer;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;
using Dekaf.Streams;
using Dekaf.Streams.Storage.ZoneTree;
using static Dekaf.Admin.AdminClientStreamsGroupDescriptionExtensions;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Runs Dekaf.Streams topologies against a real broker. The streams group protocol needs Kafka 4.4 for
/// the features the host uses.
/// </summary>
[Category("Streams")]
[SupportsKafka(440)]
public sealed class StreamsRuntimeIntegrationTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(90);

    [Test]
    [Arguments(ProcessingGuarantee.ExactlyOnce)]
    [Arguments(ProcessingGuarantee.AtLeastOnce)]
    public async Task WordCount_ProducesCountsThroughARepartitionTopic(ProcessingGuarantee guarantee)
    {
        var input = await KafkaContainer.CreateTestTopicAsync(partitions: 3);
        var output = await KafkaContainer.CreateTestTopicAsync(partitions: 3);
        var applicationId = $"streams-wordcount-{Guid.NewGuid():N}";

        await using var app = Start(WordCount(applicationId, input, output), b => b.WithProcessingGuarantee(guarantee));
        await ProduceAsync(input, ("a", "hello streams"), ("b", "hello dekaf"), ("c", "streams streams"));

        var counts = await ReadLatestAsync(output, latest =>
            latest.GetValueOrDefault("hello") == 2
            && latest.GetValueOrDefault("streams") == 3
            && latest.GetValueOrDefault("dekaf") == 1);
        await Assert.That(counts.Count).IsEqualTo(3);

        await app.StopAsync();
        await Assert.That(app.Streams.State).IsEqualTo(KafkaStreamsState.Stopped);
    }

    [Test]
    public async Task Restart_RestoresStateFromTheChangelogAndResumesAtTheCommittedOffsets()
    {
        var input = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        var output = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        var applicationId = $"streams-restart-{Guid.NewGuid():N}";

        await using (var first = Start(WordCount(applicationId, input, output)))
        {
            await ProduceAsync(input, ("a", "one two"), ("b", "two three"));
            await ReadLatestAsync(output, latest => latest.GetValueOrDefault("two") == 2 && latest.ContainsKey("three"));
            await first.StopAsync();
        }

        // A new instance has empty stores. Counting on from 2 proves the state came back from the changelog;
        // not recounting the first records proves it resumed at the committed offsets.
        await using var second = Start(WordCount(applicationId, input, output));
        await ProduceAsync(input, ("c", "two"));

        var counts = await ReadLatestAsync(output, latest => latest.GetValueOrDefault("two") == 3);
        await Assert.That(counts["one"]).IsEqualTo(1);
        await Assert.That(counts["three"]).IsEqualTo(1);
        await second.StopAsync();
    }

    [Test]
    public async Task PersistentState_SurvivesARestart()
    {
        var input = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        var output = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        var applicationId = $"streams-persistent-{Guid.NewGuid():N}";
        var stateDirectory = Path.Combine(Path.GetTempPath(), applicationId);
        try
        {
            await using (var first = Start(
                             WordCount(applicationId, input, output),
                             b => b.UseZoneTreeState(stateDirectory)))
            {
                await ProduceAsync(input, ("a", "one two"), ("b", "two three"));
                await ReadLatestAsync(output, latest => latest.GetValueOrDefault("two") == 2 && latest.ContainsKey("three"));
                await first.StopAsync();
            }

            // A clean stop leaves a checkpoint beside each task's stores.
            var checkpoints = Directory.GetFiles(stateDirectory, ".checkpoint", SearchOption.AllDirectories);
            await Assert.That(checkpoints.Length).IsGreaterThan(0);

            await using var second = Start(
                WordCount(applicationId, input, output),
                b => b.UseZoneTreeState(stateDirectory));
            await ProduceAsync(input, ("c", "two three"));
            var counts = await ReadLatestAsync(output, latest =>
                latest.GetValueOrDefault("two") == 3 && latest.GetValueOrDefault("three") == 2);
            await Assert.That(counts["one"]).IsEqualTo(1);
            await second.StopAsync();
        }
        finally
        {
            if (Directory.Exists(stateDirectory))
            {
                Directory.Delete(stateDirectory, recursive: true);
            }
        }
    }

    [Test]
    public async Task TwoInstances_ShareTasksAndTheSurvivorTakesOver()
    {
        var input = await KafkaContainer.CreateTestTopicAsync(partitions: 4);
        var output = await KafkaContainer.CreateTestTopicAsync(partitions: 4);
        var applicationId = $"streams-rebalance-{Guid.NewGuid():N}";

        await using var first = Start(WordCount(applicationId, input, output));
        await ProduceAsync(input, Sentences(0, 40));
        await ReadLatestAsync(output, latest => latest.GetValueOrDefault("word") == 40);

        await using var second = Start(WordCount(applicationId, input, output));
        await using var client = Kafka.Connect(KafkaContainer.BootstrapServers);
        await using var admin = client.CreateAdminClient().Build();

        // Both members own tasks once the first has handed some over.
        await TestWait.WaitForConditionAsync(
            async () =>
            {
                var group = (await admin.DescribeStreamsGroupsAsync([applicationId]))[applicationId];
                return group.Members.Select(member => member.Assignment.ActiveTasks.Sum(tasks => tasks.Partitions.Count)).ToArray();
            },
            owned => owned.Length == 2 && owned.All(count => count > 0) && owned.Sum() == 8,
            maxRetries: 60,
            description: "both streams members own tasks",
            formatObserved: static owned => $"[{string.Join(", ", owned)}]");

        await ProduceAsync(input, Sentences(40, 40));
        await ReadLatestAsync(output, latest => latest.GetValueOrDefault("word") == 80);

        // The first instance leaves; its tasks move with their state, and nothing is counted twice or lost.
        await first.StopAsync();
        await ProduceAsync(input, Sentences(80, 40));
        // Exact totals for every key: a duplicate or a lost record would leave one of them off.
        var counts = await ReadLatestAsync(output, latest =>
            latest.GetValueOrDefault("word") == 120
            && Enumerable.Range(0, 8).All(key => latest.GetValueOrDefault($"k{key}") == 15));
        await Assert.That(counts.Count).IsEqualTo(9);

        await second.StopAsync();
    }

    [Test]
    public async Task StreamTableJoinAndWindowedCount_RunOnTheBroker()
    {
        var orders = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        var customers = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        var joined = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        var windowed = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        var applicationId = $"streams-join-{Guid.NewGuid():N}";

        var builder = new StreamsBuilder(applicationId);
        var customerTable = builder.Table(customers, "customers", Serializers.String, Serializers.String);
        var orderStream = builder.Stream(orders, Serializers.String, Serializers.String);
        orderStream
            .Join(customerTable, static (order, customer) => $"{customer}:{order}")
            .ToTopic(joined, Serializers.String, Serializers.String);
        var windows = TimeWindows.Tumbling(TimeSpan.FromSeconds(10)).EmitOnClose();
        orderStream
            .GroupByKey()
            .WindowedBy(windows)
            .Count("orders-per-window")
            .ToStream()
            .Select(static (key, count) => count)
            .SelectKey(static (key, _) => $"{key.Key}@{key.Start}")
            .ToTopic(windowed, Serializers.String, Serializers.Int64);

        await using var app = Start(builder.Build());
        await ProduceAsync(customers, ("c1", "Ada"));
        // The table must hold the customer before the orders are joined against it.
        await Task.Delay(TimeSpan.FromSeconds(2));
        await using (var producer = await Kafka.CreateProducer<string, string>()
                         .WithBootstrapServers(KafkaContainer.BootstrapServers)
                         .BuildAsync())
        {
            foreach (var (value, timestamp) in new[] { ("o1", 1_000L), ("o2", 2_000L), ("o3", 11_000L) })
            {
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = orders,
                    Key = "c1",
                    Value = value,
                    Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(timestamp)
                });
            }
        }

        var joinedValues = await ReadAsync<string>(joined, Serializers.String, values => values.Count >= 3);
        await Assert.That(joinedValues.Select(static r => r.Value)).IsEquivalentTo(["Ada:o1", "Ada:o2", "Ada:o3"]);

        // The record at 11s closes the window [0s, 10s), which then emits its final count once.
        var closed = await ReadAsync<long>(windowed, Serializers.Int64, values => values.Count >= 1);
        await Assert.That((closed[0].Key, closed[0].Value)).IsEqualTo(("c1@0", 2L));
        await app.StopAsync();
    }

    [Test]
    public async Task Deletes_AreWrittenAsTombstonesAndStayDeletedAfterARestart()
    {
        var events = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var customers = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var output = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var applicationId = $"streams-deletes-{Guid.NewGuid():N}";

        Topology Build()
        {
            var builder = new StreamsBuilder(applicationId);
            var customerTable = builder.Table(customers, "customers", Serializers.String, Serializers.String);
            builder.Stream(events, Serializers.String, Serializers.String)
                // Counts events per key in a changelog-backed state value; "clear" deletes it.
                .Process<long, string, string>(
                    "event-count",
                    Serializers.Int64,
                    static (in Record<string, string> record, IValueState<long> count, IForwarder<string, string> forward) =>
                    {
                        if (record.Value == "clear")
                        {
                            count.Clear();
                            return;
                        }

                        count.TryGet(out var seen);
                        count.Set(seen + 1);
                        forward.Forward(record.WithValue($"{seen + 1}"));
                    })
                .Repartition("by-key", Serializers.String, Serializers.String)
                .LeftJoin(customerTable, static (count, customer, found) => found ? $"{customer}:{count}" : $"none:{count}")
                .ToTopic(output, Serializers.String, Serializers.String);
            return builder.Build();
        }

        await using (var first = Start(Build()))
        {
            await ProduceAsync(customers, ("k", "Ada"));
            await Task.Delay(TimeSpan.FromSeconds(2));
            await ProduceAsync(events, ("k", "e"), ("k", "e"), ("k", "clear"));
            var before = await ReadAsync<string>(output, Serializers.String, records => records.Count >= 2);
            await Assert.That(before.Select(static r => r.Value)).IsEquivalentTo(["Ada:1", "Ada:2"]);

            // Delete the customer; give the table time to apply it before the instance stops.
            await ProduceAsync(customers, ("k", null!));
            await Task.Delay(TimeSpan.FromSeconds(2));
            await first.StopAsync();
        }

        // Both stores are rebuilt from Kafka. A delete restored as an empty value would count on from a
        // broken state value and would still find a customer.
        await using var second = Start(Build());
        await ProduceAsync(events, ("k", "e"));
        var after = await ReadAsync<string>(output, Serializers.String, records => records.Count >= 3);
        await Assert.That(after[2].Value).IsEqualTo("none:1");
        await second.StopAsync();
    }

    [Test]
    public async Task ChangedTopology_UnderTheSameApplicationId_FailsWithAnExplanation()
    {
        var input = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var output = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var applicationId = $"streams-changed-{Guid.NewGuid():N}";

        await using (var original = Start(WordCount(applicationId, input, output)))
        {
            await ProduceAsync(input, ("a", "one"));
            await ReadLatestAsync(output, latest => latest.ContainsKey("one"));
            await original.StopAsync();
        }

        var changed = new StreamsBuilder(applicationId);
        changed.Stream(input, Serializers.String, Serializers.String)
            .GroupByKey()
            .Count("other-counts")
            .ToStream()
            .ToTopic(output, Serializers.String, Serializers.Int64);

        await using var app = Start(changed.Build());
        var failure = await Assert.That(async () => await app.Run.WaitAsync(Timeout)).Throws<StreamsException>();
        await Assert.That(failure!.Message).Contains("topology");
        await Assert.That(app.Streams.State).IsEqualTo(KafkaStreamsState.Failed);
    }

    [Test]
    public async Task FencedMember_DiscardsItsUncommittedWorkAndRecoversExactlyOnce()
    {
        var input = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        var output = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        var applicationId = $"streams-fenced-{Guid.NewGuid():N}";

        await using var app = Start(WordCount(applicationId, input, output));
        await ProduceAsync(input, Sentences(0, 40));
        await ReadLatestAsync(output, latest => latest.GetValueOrDefault("word") == 40);

        // Remove the member behind its back, as a session expiry would. It keeps processing until a
        // heartbeat or a commit tells it; that work must be discarded and redone from committed state.
        await using var client = Kafka.Connect(KafkaContainer.BootstrapServers);
        await using var admin = client.CreateAdminClient().Build();
        var memberId = (await admin.DescribeStreamsGroupsAsync([applicationId]))[applicationId].Members.Single().MemberId;
        await ForceLeaveAsync(applicationId, memberId);
        await ProduceAsync(input, Sentences(40, 40));

        var counts = await ReadLatestAsync(output, latest =>
            latest.GetValueOrDefault("word") == 80
            && Enumerable.Range(0, 8).All(key => latest.GetValueOrDefault($"k{key}") == 10));
        await Assert.That(counts.Count).IsEqualTo(9);

        var rejoined = (await admin.DescribeStreamsGroupsAsync([applicationId]))[applicationId].Members.Single();
        await Assert.That(rejoined.Assignment.ActiveTasks.Sum(static tasks => tasks.Partitions.Count)).IsEqualTo(4);
        await app.StopAsync();
    }

    [Test]
    public async Task ProcessingFailure_StopsTheInstanceAndARestartCountsEveryRecordOnce()
    {
        var input = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var output = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var applicationId = $"streams-poison-{Guid.NewGuid():N}";
        var skipPoison = false;

        Topology Build()
        {
            var builder = new StreamsBuilder(applicationId);
            builder.OnError(_ => Volatile.Read(ref skipPoison) ? ErrorResponse.Continue : ErrorResponse.Fail);
            builder.Stream(input, Serializers.String, Serializers.String)
                .Select(static (_, value) => value == "poison" ? throw new InvalidOperationException("poison") : value)
                .GroupByKey()
                .Count("counts")
                .ToStream()
                .ToTopic(output, Serializers.String, Serializers.Int64);
            return builder.Build();
        }

        // The good records and the poison record are fetched together, so the counts they produced are
        // still uncommitted when the failure aborts the transaction.
        await ProduceAsync(input, [.. Enumerable.Range(0, 20).Select(static i => ("k", $"v{i}")), ("k", "poison")]);
        await using (var failing = Start(Build()))
        {
            var failure = await Assert.That(async () => await failing.Run.WaitAsync(Timeout)).Throws<StreamsException>();
            await Assert.That(failure!.InnerException).IsTypeOf<InvalidOperationException>();
            await Assert.That(failing.Streams.State).IsEqualTo(KafkaStreamsState.Failed);
        }

        Volatile.Write(ref skipPoison, true);
        await using var recovered = Start(Build());
        await ProduceAsync(input, ("k", "after"));

        // 20 good records before the poison record and one after it, each counted once.
        var counts = await ReadLatestAsync(output, latest => latest.GetValueOrDefault("k") >= 21);
        await Assert.That(counts["k"]).IsEqualTo(21);
        await recovered.StopAsync();
    }

    [Test]
    public async Task TwoThreads_RunAsTwoMembersOfOneInstance()
    {
        var input = await KafkaContainer.CreateTestTopicAsync(partitions: 4);
        var output = await KafkaContainer.CreateTestTopicAsync(partitions: 4);
        var applicationId = $"streams-threads-{Guid.NewGuid():N}";

        await using var app = Start(WordCount(applicationId, input, output), static b => b.WithThreads(2));
        await ProduceAsync(input, Sentences(0, 80));
        var counts = await ReadLatestAsync(output, latest =>
            latest.GetValueOrDefault("word") == 80
            && Enumerable.Range(0, 8).All(key => latest.GetValueOrDefault($"k{key}") == 10));
        await Assert.That(counts.Count).IsEqualTo(9);

        await using var client = Kafka.Connect(KafkaContainer.BootstrapServers);
        await using var admin = client.CreateAdminClient().Build();
        var owned = await TestWait.WaitForConditionAsync(
            async () =>
            {
                var group = (await admin.DescribeStreamsGroupsAsync([applicationId]))[applicationId];
                return group.Members.Select(member => member.Assignment.ActiveTasks.Sum(tasks => tasks.Partitions.Count)).ToArray();
            },
            static tasks => tasks.Length == 2 && tasks.All(static count => count > 0) && tasks.Sum() == 8,
            maxRetries: 60,
            description: "both threads own tasks",
            formatObserved: static tasks => $"[{string.Join(", ", tasks)}]");
        await Assert.That(owned.Length).IsEqualTo(2);
        await app.StopAsync();
    }

    /// <summary>Sends the leave heartbeat for a member from outside it, which removes it from the group.</summary>
    private async Task ForceLeaveAsync(string groupId, string memberId)
    {
        await using var pool = new ConnectionPool(
            "streams-runtime-fencing-test",
            new ConnectionOptions { RequestTimeout = TimeSpan.FromSeconds(10) },
            loggerFactory: null);
        var endpoint = BootstrapServerList.Parse(KafkaContainer.BootstrapServers);
        var bootstrap = await pool.GetConnectionAsync(endpoint.Host, endpoint.Port, CancellationToken.None);
        var findVersion = ((IKafkaCapabilityProvider)bootstrap).Capabilities.NegotiateVersion(
            ApiKey.FindCoordinator,
            FindCoordinatorRequest.LowestSupportedVersion,
            FindCoordinatorRequest.HighestSupportedVersion);
        var coordinator = (await bootstrap.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
            new FindCoordinatorRequest { Key = groupId, KeyType = CoordinatorType.Group },
            findVersion,
            CancellationToken.None)).Coordinators.Single();
        pool.RegisterBroker(coordinator.NodeId, coordinator.Host, coordinator.Port);
        var connection = await pool.GetConnectionAsync(coordinator.NodeId, CancellationToken.None);
        var heartbeatVersion = ((IKafkaCapabilityProvider)connection).Capabilities.NegotiateVersion(
            ApiKey.StreamsGroupHeartbeat,
            StreamsGroupHeartbeatRequest.LowestSupportedVersion,
            StreamsGroupHeartbeatRequest.HighestSupportedVersion);
        var leave = await connection.SendAsync<StreamsGroupHeartbeatRequest, StreamsGroupHeartbeatResponse>(
            new StreamsGroupHeartbeatRequest { GroupId = groupId, MemberId = memberId, MemberEpoch = -1 },
            heartbeatVersion,
            CancellationToken.None);
        await Assert.That(leave.ErrorCode).IsEqualTo(ErrorCode.None);
    }

    private static Topology WordCount(string applicationId, string input, string output)
    {
        var builder = new StreamsBuilder(applicationId);
        builder.Stream(input, Serializers.String, Serializers.String)
            .SelectMany<string>(static (_, sentence, sink) =>
            {
                foreach (var word in sentence.Split(' '))
                {
                    sink.Add(word);
                }
            })
            .GroupBy(static (_, word) => word, Serializers.String, Serializers.String)
            .Count("counts")
            .ToStream()
            .ToTopic(output, Serializers.String, Serializers.Int64);
        return builder.Build();
    }

    // Every sentence adds one to "word" and one to its key's own word, spread over eight keys.
    private static (string Key, string Value)[] Sentences(int from, int count) =>
        Enumerable.Range(from, count).Select(i => ($"k{i % 8}", $"word k{i % 8}")).ToArray();

    private RunningApp Start(Topology topology, Action<KafkaStreamsBuilder>? configure = null)
    {
        var builder = KafkaStreams.Create(topology)
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory());
        configure?.Invoke(builder);
        var streams = builder.Build();
        var stop = new CancellationTokenSource();
        return new RunningApp(streams, streams.RunAsync(stop.Token), stop);
    }

    private async Task ProduceAsync(string topic, params (string Key, string Value)[] records)
    {
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();
        foreach (var (key, value) in records)
        {
            await producer.ProduceAsync(topic, key, value);
        }
    }

    /// <summary>Reads committed output until the latest value per key satisfies <paramref name="done"/>.</summary>
    private async Task<Dictionary<string, long>> ReadLatestAsync(string topic, Func<Dictionary<string, long>, bool> done)
    {
        var latest = new Dictionary<string, long>();
        await ReadAsync<long>(topic, Serializers.Int64, records =>
        {
            var record = records[^1];
            latest[record.Key] = record.Value;
            return done(latest);
        });
        return latest;
    }

    private async Task<List<(string Key, TValue Value)>> ReadAsync<TValue>(
        string topic,
        IDeserializer<TValue> valueDeserializer,
        Func<List<(string Key, TValue Value)>, bool> done)
    {
        await using var consumer = await Kafka.CreateConsumer<string, TValue>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"streams-reader-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .WithValueDeserializer(valueDeserializer)
            .BuildAsync();
        consumer.Subscribe(topic);

        var records = new List<(string Key, TValue Value)>();
        using var timeout = new CancellationTokenSource(Timeout);
        try
        {
            await foreach (var record in consumer.ConsumeAsync(timeout.Token))
            {
                records.Add((record.Key!, record.Value));
                if (done(records))
                {
                    return records;
                }
            }
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
        }

        throw new TimeoutException(
            $"Topic '{topic}' did not reach the expected output within {Timeout}. Read {records.Count} records: " +
            string.Join(", ", records.TakeLast(20).Select(static r => $"{r.Key}={r.Value}")));
    }

    private sealed class RunningApp(KafkaStreams streams, Task run, CancellationTokenSource stop) : IAsyncDisposable
    {
        public KafkaStreams Streams => streams;

        public Task Run => run;

        /// <summary>Stops the instance and rethrows anything it failed with.</summary>
        public async Task StopAsync()
        {
            await stop.CancelAsync();
            await run;
        }

        public async ValueTask DisposeAsync()
        {
            await stop.CancelAsync();
            try
            {
                await run;
            }
            catch (Exception)
            {
                // A failure was already reported by StopAsync or by the assertion that ended the test.
            }

            stop.Dispose();
        }
    }
}
