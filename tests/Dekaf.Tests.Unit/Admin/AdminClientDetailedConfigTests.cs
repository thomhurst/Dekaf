using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Admin;

public sealed class AdminClientDetailedConfigTests
{
    [Test]
    [Arguments(ErrorCode.TopicAuthorizationFailed)]
    [Arguments(ErrorCode.ClusterAuthorizationFailed)]
    public async Task AlterConfigs_AuthorizationFailureThrowsTypedException(ErrorCode error)
    {
        var (admin, connections) = CreateAdmin();
        await using var client = admin;
        var resource = ConfigResource.Topic("denied");
        Setup(connections[1], false, _ => [(resource, error, "denied")]);

        var exception = await Assert.That(async () => await admin.AlterConfigsAsync(
            new Dictionary<ConfigResource, IReadOnlyList<ConfigEntry>>
            {
                [resource] = [new ConfigEntry { Name = "retention.ms", Value = "1000" }]
            })).Throws<AuthorizationException>();

        await Assert.That(exception!.ErrorCode).IsEqualTo(error);
        await connections[1].Received(1).SendAsync<AlterConfigsRequest, AlterConfigsResponse>(
            Arg.Any<AlterConfigsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>());
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task MixedResults_PreserveResourceTypeAndOriginalBrokerError(bool incremental)
    {
        var (admin, connections) = CreateAdmin();
        await using var client = admin;
        var topic = ConfigResource.Topic("same");
        var group = new ConfigResource { Type = ConfigResourceType.Group, Name = "same" };
        Setup(connections[1], incremental, _ => [(group, ErrorCode.GroupAuthorizationFailed, "original denial"), (topic, ErrorCode.None, null)]);
        var result = await Invoke(admin, incremental, topic, group);
        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result[topic].IsSuccess).IsTrue();
        await Assert.That(result[group].Outcome).IsEqualTo(AdminMutationOutcome.Failed);
        await Assert.That(result[group].ErrorCode).IsEqualTo(ErrorCode.GroupAuthorizationFailed);
        await Assert.That(result[group].ErrorMessage).IsEqualTo("original denial");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task EndpointGroups_BatchOnlyResourcesForTheSameBroker(bool incremental)
    {
        var (admin, connections) = CreateAdmin();
        await using var client = admin;
        var observed = new Dictionary<int, ConfigResource[]>();
        foreach (var pair in connections)
            Setup(pair.Value, incremental, resources => { observed.Add(pair.Key, resources); return Success(resources); });
        var topic = ConfigResource.Topic("orders");
        var cluster = ConfigResource.ClusterBroker();
        var broker = ConfigResource.Broker(2);
        var logger = ConfigResource.BrokerLogger(2);
        var other = ConfigResource.BrokerLogger(3);
        var result = await Invoke(admin, incremental, topic, cluster, broker, logger, other);
        await Assert.That(result.Values.All(static value => value.IsSuccess)).IsTrue();
        await Assert.That(observed[1]).IsEquivalentTo([topic, cluster]);
        await Assert.That(observed[2]).IsEquivalentTo([broker, logger]);
        await Assert.That(observed[3]).IsEquivalentTo([other]);
    }

    [Test]
    [Arguments(false, ErrorCode.NotController)]
    [Arguments(true, ErrorCode.NotController)]
    [Arguments(false, ErrorCode.ThrottlingQuotaExceeded)]
    [Arguments(true, ErrorCode.ThrottlingQuotaExceeded)]
    public async Task ExplicitRejection_RetriesOnlyRejectedResources(bool incremental, ErrorCode error)
    {
        var (admin, connections) = CreateAdmin();
        await using var client = admin;
        var good = ConfigResource.Topic("done");
        var retry = ConfigResource.Topic("retry");
        var calls = 0;
        Setup(connections[1], incremental, resources =>
        {
            if (++calls == 1) return [(good, ErrorCode.None, null), (retry, error, "rejected")];
            if (resources.Length != 1 || !resources[0].Equals(retry)) throw new InvalidOperationException("Successful resource replayed.");
            return Success(resources);
        });
        var result = await Invoke(admin, incremental, good, retry);
        await Assert.That(calls).IsEqualTo(2);
        await Assert.That(result.Values.All(static value => value.IsSuccess)).IsTrue();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task AmbiguousEndpointFailure_PreservesEarlierSuccessAndStillAttemptsOtherEndpoints(bool incremental)
    {
        var (admin, connections) = CreateAdmin();
        await using var client = admin;
        var calls = 0;
        Setup(connections[1], incremental, Success);
        Setup(connections[2], incremental, _ => { calls++; throw new IOException("lost response"); });
        Setup(connections[3], incremental, Success);
        var first = ConfigResource.Topic("done");
        var uncertain = ConfigResource.BrokerLogger(2);
        var last = ConfigResource.BrokerLogger(3);
        var result = await Invoke(admin, incremental, first, uncertain, last);
        await Assert.That(result[first].IsSuccess).IsTrue();
        await Assert.That(result[last].IsSuccess).IsTrue();
        await Assert.That(result[uncertain].Outcome).IsEqualTo(AdminMutationOutcome.Unknown);
        await Assert.That(result[uncertain].Exception).IsTypeOf<IOException>();
        await Assert.That(calls).IsEqualTo(1);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task MissingDuplicateAndTimedOutResponses_AreNotSuccess(bool incremental)
    {
        var (admin, connections) = CreateAdmin();
        await using var client = admin;
        var duplicate = ConfigResource.Topic("duplicate");
        var missing = ConfigResource.Topic("missing");
        var timeout = ConfigResource.Topic("timeout");
        Setup(connections[1], incremental, _ => [(duplicate, ErrorCode.None, null), (duplicate, ErrorCode.None, null),
            (timeout, ErrorCode.RequestTimedOut, "ambiguous"), (ConfigResource.Topic("extra"), ErrorCode.None, null)]);
        var result = await Invoke(admin, incremental, duplicate, missing, timeout);
        await Assert.That(result.Count).IsEqualTo(3);
        await Assert.That(result.Values.All(static value => value.Outcome == AdminMutationOutcome.Unknown)).IsTrue();
        await Assert.That(result[timeout].ErrorCode).IsEqualTo(ErrorCode.RequestTimedOut);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CancellationBetweenEndpoints_PreservesConfirmedAndMarksRemainingNotAttempted(bool incremental)
    {
        var (admin, connections) = CreateAdmin();
        await using var client = admin;
        using var cancellation = new CancellationTokenSource();
        Setup(connections[1], incremental, resources => { cancellation.Cancel(); return Success(resources); });
        var done = ConfigResource.Topic("done");
        var later = ConfigResource.BrokerLogger(2);
        var input = Input(done, later);
        var result = incremental
            ? await admin.IncrementalAlterConfigsDetailedAsync(input, cancellationToken: cancellation.Token)
            : await admin.AlterConfigsDetailedAsync(Replacement(input), cancellationToken: cancellation.Token);
        await Assert.That(result[done].IsSuccess).IsTrue();
        await Assert.That(result[later].Outcome).IsEqualTo(AdminMutationOutcome.NotAttempted);
        await Assert.That(connections[2].ReceivedCalls()).IsEmpty();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task EmptyInvalidAndPreCancelledInput_DoNotSend(bool incremental)
    {
        var (admin, connections) = CreateAdmin();
        await using var client = admin;
        await Assert.That((await Invoke(admin, incremental)).Count).IsEqualTo(0);
        await Assert.ThrowsAsync<ArgumentException>(() => Invoke(admin, incremental, ConfigResource.Broker(-1)).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => Invoke(admin, incremental, ConfigResource.Topic(" ")).AsTask());
        IAdminClient unsupported = Substitute.For<IAdminClient>();
        await Assert.ThrowsAsync<NotSupportedException>(() => unsupported.AlterConfigsDetailedAsync(new Dictionary<ConfigResource, IReadOnlyList<ConfigEntry>>()).AsTask());
        await Assert.ThrowsAsync<OperationCanceledException>(() => admin.IncrementalAlterConfigsDetailedAsync(Input(ConfigResource.Topic("t")), cancellationToken: new(true)).AsTask());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => admin.AlterConfigsDetailedAsync(Replacement(Input()), new() { TimeoutMs = -1 }).AsTask());
        foreach (var connection in connections.Values) await Assert.That(connection.ReceivedCalls()).IsEmpty();
    }

    [Test]
    public async Task IncrementalOperationsAndValidateOnly_AreSnapshottedBeforeRetry()
    {
        var (admin, connections) = CreateAdmin();
        await using var client = admin;
        var resource = ConfigResource.Topic("orders");
        ConfigAlter[] changes = [ConfigAlter.Set("a", "1"), ConfigAlter.Delete("b"), ConfigAlter.Append("c", "x"), ConfigAlter.Subtract("d", "y")];
        var requests = new List<IncrementalAlterConfigsRequest>();
        connections[1].SendAsync<IncrementalAlterConfigsRequest, IncrementalAlterConfigsResponse>(Arg.Any<IncrementalAlterConfigsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<IncrementalAlterConfigsRequest>();
                requests.Add(request);
                changes[0] = ConfigAlter.Set("changed", "2");
                return ValueTask.FromResult(new IncrementalAlterConfigsResponse { Responses = [new()
                {
                    ResourceType = (sbyte)resource.Type, ResourceName = resource.Name,
                    ErrorCode = requests.Count == 1 ? ErrorCode.NotController : ErrorCode.None
                }] });
            });
        var result = await admin.IncrementalAlterConfigsDetailedAsync(new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>> { [resource] = changes }, new() { ValidateOnly = true });
        await Assert.That(result[resource].IsSuccess).IsTrue();
        await Assert.That(requests.Count).IsEqualTo(2);
        foreach (var request in requests)
        {
            await Assert.That(request.ValidateOnly).IsTrue();
            await Assert.That(request.Resources[0].Configs.Select(static change => change.Name)).IsEquivalentTo(["a", "b", "c", "d"]);
            await Assert.That(request.Resources[0].Configs.Select(static change => change.ConfigOperation)).IsEquivalentTo(new sbyte[] { 0, 1, 2, 3 });
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Deadline_IsSharedAcrossAllEndpoints(bool incremental)
    {
        var (admin, connections) = CreateAdmin();
        await using var client = admin;
        var first = ConfigResource.Topic("blocked");
        var later = ConfigResource.BrokerLogger(2);
        if (incremental)
            connections[1].SendAsync<IncrementalAlterConfigsRequest, IncrementalAlterConfigsResponse>(Arg.Any<IncrementalAlterConfigsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
                .Returns(call => new ValueTask<IncrementalAlterConfigsResponse>(WaitForCancellation<IncrementalAlterConfigsResponse>(call.Arg<CancellationToken>())));
        else
            connections[1].SendAsync<AlterConfigsRequest, AlterConfigsResponse>(Arg.Any<AlterConfigsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
                .Returns(call => new ValueTask<AlterConfigsResponse>(WaitForCancellation<AlterConfigsResponse>(call.Arg<CancellationToken>())));
        var input = Input(first, later);
        var pending = incremental
            ? admin.IncrementalAlterConfigsDetailedAsync(input, new() { TimeoutMs = 1000 })
            : admin.AlterConfigsDetailedAsync(Replacement(input), new() { TimeoutMs = 1000 });
        var result = await pending.AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(result[first].Outcome).IsEqualTo(AdminMutationOutcome.Unknown);
        await Assert.That(result[later].Outcome).IsEqualTo(AdminMutationOutcome.NotAttempted);
        await Assert.That(result[first].Exception).IsTypeOf<KafkaTimeoutException>();
        await Assert.That(result[later].Exception).IsTypeOf<KafkaTimeoutException>();
        await Assert.That(connections[2].ReceivedCalls()).IsEmpty();
    }

    [Test]
    public async Task ReplacementValidateOnly_SnapshotsValuesAndAllowsEmptyReplacement()
    {
        var (admin, connections) = CreateAdmin();
        await using var client = admin;
        var first = ConfigResource.Topic("orders");
        var reset = ConfigResource.Topic("reset");
        ConfigEntry[] entries = [new() { Name = "retention.ms", Value = "1000" }];
        var requests = new List<AlterConfigsRequest>();
        connections[1].SendAsync<AlterConfigsRequest, AlterConfigsResponse>(Arg.Any<AlterConfigsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<AlterConfigsRequest>();
                requests.Add(request);
                entries[0] = new() { Name = "changed", Value = "bad" };
                return ValueTask.FromResult(new AlterConfigsResponse { Responses = request.Resources.Select(resource => new AlterConfigsResourceResponse
                {
                    ResourceType = resource.ResourceType, ResourceName = resource.ResourceName,
                    ErrorCode = requests.Count == 1 ? ErrorCode.NotController : ErrorCode.None
                }).ToArray() });
            });
        var result = await admin.AlterConfigsDetailedAsync(new Dictionary<ConfigResource, IReadOnlyList<ConfigEntry>>
            { [first] = entries, [reset] = [] }, new() { ValidateOnly = true });
        await Assert.That(result.Values.All(static outcome => outcome.IsSuccess)).IsTrue();
        await Assert.That(requests.Count).IsEqualTo(2);
        foreach (var request in requests)
        {
            await Assert.That(request.ValidateOnly).IsTrue();
            await Assert.That(request.Resources[0].Configs[0].Name).IsEqualTo("retention.ms");
            await Assert.That(request.Resources[0].Configs[0].Value).IsEqualTo("1000");
            await Assert.That(request.Resources[1].Configs.Count).IsEqualTo(0);
        }
    }

    [Test]
    public async Task DuplicateKeysAndUnknownOperations_FailBeforeDispatch()
    {
        var (admin, connections) = CreateAdmin();
        await using var client = admin;
        var resource = ConfigResource.Topic("orders");
        await Assert.ThrowsAsync<ArgumentException>(() => admin.IncrementalAlterConfigsDetailedAsync(
            new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>> { [resource] = [ConfigAlter.Set("same", "1"), ConfigAlter.Delete("same")] }).AsTask());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => admin.IncrementalAlterConfigsDetailedAsync(
            new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>> { [resource] = [new() { Name = "key", Operation = (ConfigAlterOperation)(-1) }] }).AsTask());
        await Assert.ThrowsAsync<ArgumentNullException>(() => admin.AlterConfigsDetailedAsync(
            new Dictionary<ConfigResource, IReadOnlyList<ConfigEntry>> { [resource] = null! }).AsTask());
        foreach (var connection in connections.Values) await Assert.That(connection.ReceivedCalls()).IsEmpty();
    }

    private static async Task<T> WaitForCancellation<T>(CancellationToken token)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, token);
        throw new InvalidOperationException("The request did not observe cancellation.");
    }

    internal static ValueTask<IReadOnlyDictionary<ConfigResource, AdminMutationResult>> Invoke(AdminClient admin, bool incremental, params ConfigResource[] resources) =>
        incremental ? admin.IncrementalAlterConfigsDetailedAsync(Input(resources)) : admin.AlterConfigsDetailedAsync(Replacement(Input(resources)));

    private static Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>> Input(params ConfigResource[] resources) =>
        resources.ToDictionary(static resource => resource, static _ => (IReadOnlyList<ConfigAlter>)[ConfigAlter.Set("retention.ms", "1000")]);

    private static Dictionary<ConfigResource, IReadOnlyList<ConfigEntry>> Replacement(IReadOnlyDictionary<ConfigResource, IReadOnlyList<ConfigAlter>> input) =>
        input.ToDictionary(static pair => pair.Key, static pair => (IReadOnlyList<ConfigEntry>)pair.Value.Select(static change => new ConfigEntry { Name = change.Name, Value = change.Value }).ToArray());

    internal static (ConfigResource Resource, ErrorCode Code, string? Message)[] Success(ConfigResource[] resources) =>
        resources.Select(static resource => (resource, ErrorCode.None, (string?)null)).ToArray();

    internal static void Setup(IKafkaConnection connection, bool incremental,
        Func<ConfigResource[], (ConfigResource Resource, ErrorCode Code, string? Message)[]> response)
    {
        if (incremental)
            connection.SendAsync<IncrementalAlterConfigsRequest, IncrementalAlterConfigsResponse>(Arg.Any<IncrementalAlterConfigsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
                .Returns(call => ValueTask.FromResult(new IncrementalAlterConfigsResponse
                {
                    Responses = response(call.Arg<IncrementalAlterConfigsRequest>().Resources.Select(static item => new ConfigResource
                        { Type = (ConfigResourceType)item.ResourceType, Name = item.ResourceName }).ToArray()).Select(static item => new IncrementalAlterConfigsResourceResponse
                        { ResourceType = (sbyte)item.Resource.Type, ResourceName = item.Resource.Name, ErrorCode = item.Code, ErrorMessage = item.Message }).ToArray()
                }));
        else
            connection.SendAsync<AlterConfigsRequest, AlterConfigsResponse>(Arg.Any<AlterConfigsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
                .Returns(call => ValueTask.FromResult(new AlterConfigsResponse
                {
                    Responses = response(call.Arg<AlterConfigsRequest>().Resources.Select(static item => new ConfigResource
                        { Type = (ConfigResourceType)item.ResourceType, Name = item.ResourceName }).ToArray()).Select(static item => new AlterConfigsResourceResponse
                        { ResourceType = (sbyte)item.Resource.Type, ResourceName = item.Resource.Name, ErrorCode = item.Code, ErrorMessage = item.Message }).ToArray()
                }));
    }

    private static (AdminClient, Dictionary<int, IKafkaConnection>) CreateAdmin()
    {
        var connections = new Dictionary<int, IKafkaConnection>();
        foreach (var id in new[] { 1, 2, 3 })
        {
            var connection = Substitute.For<IKafkaConnection>();
            connection.BrokerId.Returns(id);
            connection.Host.Returns($"broker-{id}");
            connection.Port.Returns(9092);
            connection.IsConnected.Returns(true);
            connections.Add(id, connection);
        }
        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(call => ValueTask.FromResult(connections[call.Arg<int>()]));
        pool.GetConnectionAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(connections[1]));
        var metadata = new MetadataResponse { Brokers = connections.Keys.Select(static id => new BrokerMetadata
            { NodeId = id, Host = $"broker-{id}", Port = 9092 }).ToArray(), ControllerId = 1, Topics = [] };
        var manager = new MetadataManager(pool, ["broker-1:9092"]);
        manager.Metadata.Update(metadata);
        manager.SetApiVersion(ApiKey.Metadata, 9, 13);
        manager.SetApiVersion(ApiKey.AlterConfigs, 0, 2);
        manager.SetApiVersion(ApiKey.IncrementalAlterConfigs, 0, 1);
        foreach (var connection in connections.Values)
            connection.SendAsync<MetadataRequest, MetadataResponse>(Arg.Any<MetadataRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(metadata));
        return (new AdminClient(new AdminClientOptions { BootstrapServers = ["broker-1:9092"], RetryBackoffMs = 1, RetryBackoffMaxMs = 1 }, pool, manager, ownsResources: true), connections);
    }
}
