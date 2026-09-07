using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Admin;

public sealed class AdminClientDetailedMutationTests
{
    [Test]
    [Arguments("create")]
    [Arguments("delete")]
    [Arguments("expand")]
    public async Task MixedOutcomes_KeepSuccessAndOriginalError(string operation)
    {
        var (admin, connection) = CreateAdmin();
        await using var client = admin;
        Setup(connection, operation, _ => [("good", ErrorCode.None, null), ("bad", ErrorCode.TopicAuthorizationFailed, "denied")]);
        var results = await Invoke(admin, operation);
        await Assert.That(results["good"].IsSuccess).IsTrue();
        await Assert.That(results["bad"].Outcome).IsEqualTo(AdminMutationOutcome.Failed);
        await Assert.That(results["bad"].ErrorCode).IsEqualTo(ErrorCode.TopicAuthorizationFailed);
        await Assert.That(results["bad"].ErrorMessage).IsEqualTo("denied");
    }

    [Test]
    [Arguments("create", ErrorCode.NotController)]
    [Arguments("delete", ErrorCode.NotController)]
    [Arguments("expand", ErrorCode.NotController)]
    [Arguments("create", ErrorCode.ThrottlingQuotaExceeded)]
    [Arguments("delete", ErrorCode.ThrottlingQuotaExceeded)]
    [Arguments("expand", ErrorCode.ThrottlingQuotaExceeded)]
    public async Task ConfirmedControllerRejection_RetriesOnlyRejectedEntity(string operation, ErrorCode rejection)
    {
        var (admin, connection) = CreateAdmin();
        await using var client = admin;
        var calls = 0;
        Setup(connection, operation, names =>
        {
            if (++calls == 1) return [("good", ErrorCode.None, null), ("bad", rejection, "rejected")];
            if (names.Length != 1 || names[0] != "bad") throw new InvalidOperationException("Confirmed mutation was replayed.");
            return [("bad", ErrorCode.None, null)];
        });
        var results = await Invoke(admin, operation);
        await Assert.That(calls).IsEqualTo(2);
        await Assert.That(results.Values.All(static result => result.IsSuccess)).IsTrue();
    }

    [Test]
    [Arguments("create", false)]
    [Arguments("delete", false)]
    [Arguments("expand", false)]
    [Arguments("create", true)]
    [Arguments("delete", true)]
    [Arguments("expand", true)]
    public async Task AmbiguousFailure_NeverReplaysOrInfersSuccess(string operation, bool brokerTimeout)
    {
        var (admin, connection) = CreateAdmin();
        await using var client = admin;
        var calls = 0;
        Setup(connection, operation, _ =>
        {
            calls++;
            if (!brokerTimeout) throw new IOException("response lost");
            return [("good", ErrorCode.RequestTimedOut, "unknown"), ("bad", ErrorCode.RequestTimedOut, "unknown")];
        });
        var results = await Invoke(admin, operation);
        await Assert.That(calls).IsEqualTo(1);
        foreach (var result in results.Values)
        {
            await Assert.That(result.Outcome).IsEqualTo(AdminMutationOutcome.Unknown);
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.ErrorCode).IsEqualTo(brokerTimeout ? ErrorCode.RequestTimedOut : (ErrorCode?)null);
        }
    }

    [Test]
    [Arguments("create")]
    [Arguments("delete")]
    [Arguments("expand")]
    public async Task MissingOrDuplicateResponse_IsUnknown(string operation)
    {
        var (admin, connection) = CreateAdmin();
        await using var client = admin;
        Setup(connection, operation, _ => [("good", ErrorCode.None, null), ("good", ErrorCode.None, null), ("unrequested", ErrorCode.None, null)]);
        var results = await Invoke(admin, operation);
        await Assert.That(results.Count).IsEqualTo(2);
        await Assert.That(results["good"].Outcome).IsEqualTo(AdminMutationOutcome.Unknown);
        await Assert.That(results["bad"].Outcome).IsEqualTo(AdminMutationOutcome.Unknown);
    }

    [Test]
    public async Task LaterTransportFailure_PreservesEarlierSuccess()
    {
        var (admin, connection) = CreateAdmin();
        await using var client = admin;
        var calls = 0;
        Setup(connection, "create", _ => ++calls == 1
            ? [("good", ErrorCode.None, null), ("bad", ErrorCode.NotController, "moved")]
            : throw new IOException("response lost"));
        var results = await Invoke(admin, "create");
        await Assert.That(results["good"].IsSuccess).IsTrue();
        await Assert.That(results["bad"].Outcome).IsEqualTo(AdminMutationOutcome.Unknown);
        await Assert.That(results["bad"].Exception).IsTypeOf<IOException>();
    }

    [Test]
    public async Task ExhaustedRetry_PreservesBrokerRejectionAndSiblingSuccess()
    {
        var (admin, connection) = CreateAdmin();
        await using var client = admin;
        var calls = 0;
        Setup(connection, "delete", names =>
        {
            calls++;
            return names.Select(name => (name, name == "good" ? ErrorCode.None : ErrorCode.NotController, (string?)"original")).ToArray();
        });
        var results = await Invoke(admin, "delete");
        await Assert.That(calls).IsEqualTo(4);
        await Assert.That(results["good"].IsSuccess).IsTrue();
        await Assert.That(results["bad"].ErrorCode).IsEqualTo(ErrorCode.NotController);
        await Assert.That(results["bad"].ErrorMessage).IsEqualTo("original");
    }

    [Test]
    public async Task RetryDiscoveryOutage_PreservesConfirmedResults()
    {
        var (admin, connection) = CreateAdmin();
        await using var client = admin;
        var calls = 0;
        Setup(connection, "create", _ =>
        {
            calls++;
            connection.SendAsync<MetadataRequest, MetadataResponse>(Arg.Any<MetadataRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
                .Returns<ValueTask<MetadataResponse>>(_ => throw new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.HostNotFound));
            return [("good", ErrorCode.None, null), ("bad", ErrorCode.NotController, "moved")];
        });
        var results = await Invoke(admin, "create");
        await Assert.That(calls).IsEqualTo(4);
        await Assert.That(results["good"].IsSuccess).IsTrue();
        await Assert.That(results["bad"].Outcome).IsEqualTo(AdminMutationOutcome.Failed);
        await Assert.That(results["bad"].ErrorCode).IsEqualTo(ErrorCode.NotController);
        await Assert.That(results["bad"].ErrorMessage).IsEqualTo("moved");
    }

    [Test]
    public async Task WrappedSendFailure_PreservesSuccessWithoutReplayingUnknownMutation()
    {
        var (admin, connection) = CreateAdmin();
        await using var client = admin;
        var failure = new InvalidOperationException("Transport unavailable", new IOException("response lost"));
        var calls = 0;
        Setup(connection, "create", _ => ++calls == 1
            ? [("good", ErrorCode.None, null), ("bad", ErrorCode.NotController, "moved")]
            : throw failure);
        var results = await Invoke(admin, "create");
        await Assert.That(calls).IsEqualTo(2);
        await Assert.That(results["good"].IsSuccess).IsTrue();
        await Assert.That(results["bad"].Outcome).IsEqualTo(AdminMutationOutcome.Unknown);
        await Assert.That(results["bad"].Exception).IsSameReferenceAs(failure);
    }

    [Test]
    public async Task UnrelatedInvariantFailure_StillPropagates()
    {
        var (admin, connection) = CreateAdmin();
        await using var client = admin;
        var failure = new InvalidOperationException("Invalid fixture invariant");
        IEnumerable<NewTopic> InvalidInputSource()
        {
            yield return new() { Name = "good" };
            throw failure;
        }
        var caught = await Assert.ThrowsAsync<InvalidOperationException>(() => admin.CreateTopicsDetailedAsync(InvalidInputSource()).AsTask());
        await Assert.That(caught).IsSameReferenceAs(failure);
        await Assert.That(connection.ReceivedCalls()).IsEmpty();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RealConnectionLifecycleFailure_PreservesEarlierSuccess(bool disposed)
    {
        var (admin, connection) = CreateAdmin();
        await using var client = admin;
        await using var unavailable = new KafkaConnection("localhost", 9092);
        if (disposed) await unavailable.DisposeAsync();
        var calls = 0;
        connection.SendAsync<CreateTopicsRequest, CreateTopicsResponse>(Arg.Any<CreateTopicsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call => ++calls == 1
                ? ValueTask.FromResult(new CreateTopicsResponse { Topics =
                    [new() { Name = "good" }, new() { Name = "bad", ErrorCode = ErrorCode.NotController }] })
                : unavailable.SendAsync<CreateTopicsRequest, CreateTopicsResponse>(call.Arg<CreateTopicsRequest>(), call.Arg<short>(), call.Arg<CancellationToken>()));
        var results = await Invoke(admin, "create");
        await Assert.That(calls).IsEqualTo(2);
        await Assert.That(results["good"].IsSuccess).IsTrue();
        await Assert.That(results["bad"].Outcome).IsEqualTo(AdminMutationOutcome.Unknown);
        await Assert.That(results["bad"].ErrorCode).IsNull();
        if (disposed) await Assert.That(results["bad"].Exception).IsTypeOf<ObjectDisposedException>();
        else await Assert.That(results["bad"].Exception).IsTypeOf<InvalidOperationException>();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ControllerRegistrationGap_PreservesUnsentOrConfirmedOutcomes(bool afterResponse)
    {
        var (admin, connection) = CreateAdmin();
        await using var client = admin;
        await using var unregisteredPool = new ConnectionPool();
        Setup(connection, "create", names => names.Select(name => (name, ErrorCode.None, (string?)null)).ToArray());
        await Invoke(admin, "create");
        var pool = (IConnectionPool)typeof(AdminClient)
            .GetField("_connectionPool", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(admin)!;
        var unavailable = !afterResponse;
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => unavailable
                ? unregisteredPool.GetConnectionAsync(call.Arg<int>(), call.Arg<CancellationToken>())
                : ValueTask.FromResult(connection));
        var sends = 0;
        Setup(connection, "create", _ =>
        {
            sends++;
            unavailable = true;
            return [("good", ErrorCode.None, null), ("bad", ErrorCode.NotController, "moved")];
        });

        var results = await Invoke(admin, "create");
        await Assert.That(sends).IsEqualTo(afterResponse ? 1 : 0);
        if (afterResponse)
        {
            await Assert.That(results["good"].IsSuccess).IsTrue();
            await Assert.That(results["bad"].Outcome).IsEqualTo(AdminMutationOutcome.Failed);
            await Assert.That(results["bad"].ErrorCode).IsEqualTo(ErrorCode.NotController);
        }
        else
        {
            foreach (var result in results.Values)
            {
                await Assert.That(result.Outcome).IsEqualTo(AdminMutationOutcome.NotAttempted);
                await Assert.That(result.Exception).IsTypeOf<InvalidOperationException>();
                await Assert.That(result.Exception!.Message).Contains("Unknown broker ID:");
            }
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task LeaseLifecycleFailure_ReturnsNotAttemptedAndRetainsOriginalException(bool disposed)
    {
        var (admin, connection) = CreateAdmin();
        await using var client = admin;
        Setup(connection, "create", names => names.Select(name => (name, ErrorCode.None, (string?)null)).ToArray());
        await Invoke(admin, "create");
        connection.ClearReceivedCalls();
        var pool = (IConnectionPool)typeof(AdminClient)
            .GetField("_connectionPool", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(admin)!;
        Exception failure = disposed ? new ObjectDisposedException(nameof(ConnectionPool))
            : new InvalidOperationException("Unavailable lease fixture");
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns<ValueTask<IKafkaConnection>>(_ => throw failure);
        var results = await Invoke(admin, "create");
        await Assert.That(results.Count).IsEqualTo(2);
        foreach (var result in results.Values)
        {
            await Assert.That(result.Outcome).IsEqualTo(AdminMutationOutcome.NotAttempted);
            await Assert.That(result.Exception).IsSameReferenceAs(failure);
        }
        await Assert.That(connection.ReceivedCalls()).IsEmpty();
    }

    [Test]
    public async Task ResponseMappingInvariant_StillPropagates()
    {
        var (admin, connection) = CreateAdmin();
        await using var client = admin;
        var failure = new InvalidOperationException("Invalid response fixture invariant");
        connection.SendAsync<CreateTopicsRequest, CreateTopicsResponse>(Arg.Any<CreateTopicsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new CreateTopicsResponse { Topics = new InvalidTopicResults(failure) }));
        var caught = await Assert.ThrowsAsync<InvalidOperationException>(() => Invoke(admin, "create").AsTask());
        await Assert.That(caught).IsSameReferenceAs(failure);
    }

    private sealed class InvalidTopicResults(InvalidOperationException failure) : IReadOnlyList<CreateTopicsResponseTopic>
    {
        public int Count => 1;
        public CreateTopicsResponseTopic this[int index] => throw failure;
        public IEnumerator<CreateTopicsResponseTopic> GetEnumerator() => throw failure;
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task DeleteValidation_UsesPublicParameterNames(bool inMemory)
    {
        await using IAdminClient admin = inMemory
            ? new Dekaf.Testing.InMemoryAdminClient(new Dekaf.Testing.InMemoryKafkaCluster())
            : CreateAdmin().Item1;
        var id = Guid.NewGuid();
        var names = await Assert.ThrowsAsync<ArgumentException>(() => admin.DeleteTopicsDetailedAsync(["same", "same"]).AsTask());
        var ids = await Assert.ThrowsAsync<ArgumentException>(() => admin.DeleteTopicsDetailedAsync([id, id]).AsTask());
        var nullNames = await Assert.ThrowsAsync<ArgumentNullException>(() => admin.DeleteTopicsDetailedAsync((IEnumerable<string>)null!).AsTask());
        var nullIds = await Assert.ThrowsAsync<ArgumentNullException>(() => admin.DeleteTopicsDetailedAsync((IEnumerable<Guid>)null!).AsTask());
        await Assert.That(names!.ParamName).IsEqualTo("topicNames");
        await Assert.That(ids!.ParamName).IsEqualTo("topicIds");
        await Assert.That(nullNames!.ParamName).IsEqualTo("topicNames");
        await Assert.That(nullIds!.ParamName).IsEqualTo("topicIds");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CancellationDuringSend_ReturnsUnknownWithCorrectCause(bool timeout)
    {
        var (admin, connection) = CreateAdmin();
        await using var client = admin;
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.SendAsync<CreateTopicsRequest, CreateTopicsResponse>(Arg.Any<CreateTopicsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call => WaitForCancellation(entered, call.Arg<CancellationToken>()));
        var pending = admin.CreateTopicsDetailedAsync([new() { Name = "good" }],
            new() { TimeoutMs = timeout ? 500 : 30000 }, cancellation.Token).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        if (!timeout) cancellation.Cancel();
        var results = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(results["good"].Outcome).IsEqualTo(AdminMutationOutcome.Unknown);
        if (timeout) await Assert.That(results["good"].Exception).IsTypeOf<KafkaTimeoutException>();
        else await Assert.That(results["good"].Exception is OperationCanceledException).IsTrue();
    }

    private static async ValueTask<CreateTopicsResponse> WaitForCancellation(TaskCompletionSource entered, CancellationToken token)
    {
        entered.SetResult();
        await Task.Delay(Timeout.Infinite, token);
        throw new InvalidOperationException("Unreachable after cancellation.");
    }

    [Test]
    public async Task CancellationAfterResponse_KeepsConfirmedSuccessAndRejection()
    {
        var (admin, connection) = CreateAdmin();
        await using var client = admin;
        using var cancellation = new CancellationTokenSource();
        Setup(connection, "create", _ =>
        {
            cancellation.Cancel();
            return [("good", ErrorCode.None, null), ("bad", ErrorCode.NotController, "moved")];
        });
        var results = await admin.CreateTopicsDetailedAsync([new() { Name = "good" }, new() { Name = "bad" }],
            cancellationToken: cancellation.Token);
        await Assert.That(results["good"].IsSuccess).IsTrue();
        await Assert.That(results["bad"].Outcome).IsEqualTo(AdminMutationOutcome.Failed);
        await Assert.That(results["bad"].ErrorCode).IsEqualTo(ErrorCode.NotController);
    }

    [Test]
    public async Task TypedExpansion_CopiesReplicasAndSendsValidationOptions()
    {
        var (admin, connection) = CreateAdmin();
        await using var client = admin;
        var replicas = new[] { 1 };
        CreatePartitionsRequest? request = null;
        connection.SendAsync<CreatePartitionsRequest, CreatePartitionsResponse>(Arg.Any<CreatePartitionsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                replicas[0] = 999;
                request = call.Arg<CreatePartitionsRequest>();
                return ValueTask.FromResult(new CreatePartitionsResponse { Results = [new() { Name = "orders" }] });
            });
        var result = await admin.CreatePartitionsDetailedAsync(new Dictionary<string, NewPartitions>
        {
            ["orders"] = new() { TotalCount = 2, ReplicaAssignments = [replicas] }
        }, new() { ValidateOnly = true, TimeoutMs = 12345 });
        await Assert.That(result["orders"].IsSuccess).IsTrue();
        await Assert.That(request!.ValidateOnly).IsTrue();
        await Assert.That(request.TimeoutMs).IsEqualTo(12345);
        await Assert.That(request.Topics[0].Assignments![0].BrokerIds[0]).IsEqualTo(1);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ReassignmentRetry_PreservesTopLevelOrPartialErrors(bool topLevel)
    {
        var (admin, connection) = CreateAdmin();
        await using var client = admin;
        var good = new TopicPartition("orders", 0);
        var retry = new TopicPartition("orders", 1);
        var calls = 0;
        connection.SendAsync<AlterPartitionReassignmentsRequest, AlterPartitionReassignmentsResponse>(Arg.Any<AlterPartitionReassignmentsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                if (++calls == 1)
                    return ValueTask.FromResult(topLevel
                        ? new AlterPartitionReassignmentsResponse { ErrorCode = ErrorCode.NotController, ErrorMessage = "moved", Responses = [] }
                        : new AlterPartitionReassignmentsResponse
                        {
                            Responses = [new() { Name = "orders", Partitions =
                            [new() { PartitionIndex = 0 }, new() { PartitionIndex = 1, ErrorCode = ErrorCode.NotController }] }]
                        });
                var partitions = call.Arg<AlterPartitionReassignmentsRequest>().Topics[0].Partitions;
                if (partitions.Count != (topLevel ? 2 : 1) || (!topLevel && partitions[0].PartitionIndex != 1))
                    throw new InvalidOperationException("Incorrect unresolved reassignment batch.");
                return ValueTask.FromResult(new AlterPartitionReassignmentsResponse
                {
                    Responses = [new() { Name = "orders",
                    Partitions = partitions.Select(static item => new AlterPartitionReassignmentsResponsePartition { PartitionIndex = item.PartitionIndex }).ToArray() }]
                });
            });
        var results = await admin.AlterPartitionReassignmentsDetailedAsync(new Dictionary<TopicPartition, Optional<NewPartitionReassignment>>
        {
            [good] = NewPartitionReassignment.ToReplicas(1),
            [retry] = NewPartitionReassignment.ToReplicas(1)
        });
        await Assert.That(calls).IsEqualTo(2);
        await Assert.That(results.Values.All(static result => result.IsSuccess)).IsTrue();
    }

    [Test]
    public async Task ZeroDeadline_IsNotAttemptedAndDoesNotSend()
    {
        var (admin, connection) = CreateAdmin();
        await using var client = admin;
        var results = await admin.CreateTopicsDetailedAsync([new() { Name = "good" }], new() { TimeoutMs = 0 });
        await Assert.That(results["good"].Outcome).IsEqualTo(AdminMutationOutcome.NotAttempted);
        await Assert.That(results["good"].Exception).IsTypeOf<KafkaTimeoutException>();
        await Assert.That(connection.ReceivedCalls()).IsEmpty();
    }

    [Test]
    public async Task TopicIdDeletion_UsesResponseIdentityWithoutPositionalGuessing()
    {
        var (admin, connection) = CreateAdmin();
        await using var client = admin;
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        connection.SendAsync<DeleteTopicsRequest, DeleteTopicsResponse>(Arg.Any<DeleteTopicsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                if (call.Arg<short>() < 6) throw new InvalidOperationException("Topic IDs need v6.");
                return ValueTask.FromResult(new DeleteTopicsResponse
                {
                    Responses = [new() { Name = string.Empty, TopicId = second }, new() { Name = string.Empty, TopicId = Guid.Empty, ErrorCode = ErrorCode.None }]
                });
            });
        var results = await admin.DeleteTopicsDetailedAsync([first, second]);
        await Assert.That(results[second].IsSuccess).IsTrue();
        await Assert.That(results[first].Outcome).IsEqualTo(AdminMutationOutcome.Unknown);
    }

    [Test]
    public async Task Reassignment_PreservesPartitionErrorsAndCancellationRequest()
    {
        var (admin, connection) = CreateAdmin();
        await using var client = admin;
        var good = new TopicPartition("orders", 0);
        var bad = new TopicPartition("orders", 1);
        AlterPartitionReassignmentsRequest? captured = null;
        connection.SendAsync<AlterPartitionReassignmentsRequest, AlterPartitionReassignmentsResponse>(Arg.Any<AlterPartitionReassignmentsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                captured = call.Arg<AlterPartitionReassignmentsRequest>();
                return ValueTask.FromResult(new AlterPartitionReassignmentsResponse
                {
                    Responses = [new() { Name = "orders", Partitions = [new() { PartitionIndex = 0 },
                        new() { PartitionIndex = 1, ErrorCode = ErrorCode.NoReassignmentInProgress, ErrorMessage = "nothing to cancel" }] }]
                });
            });
        var results = await admin.AlterPartitionReassignmentsDetailedAsync(new Dictionary<TopicPartition, Optional<NewPartitionReassignment>>
        {
            [good] = NewPartitionReassignment.ToReplicas(1),
            [bad] = Optional.None<NewPartitionReassignment>()
        }, new() { AllowReplicationFactorChange = false, TimeoutMs = 10000 });
        await Assert.That(results[good].IsSuccess).IsTrue();
        await Assert.That(results[bad].ErrorCode).IsEqualTo(ErrorCode.NoReassignmentInProgress);
        await Assert.That(captured!.AllowReplicationFactorChange).IsFalse();
        await Assert.That(captured.Topics[0].Partitions[1].Replicas).IsNull();
    }

    [Test]
    public async Task EmptyInputs_DoNotInitializeOrSend()
    {
        var (admin, connection) = CreateAdmin();
        await using var client = admin;
        await Assert.That((await admin.CreateTopicsDetailedAsync([])).Count).IsEqualTo(0);
        await Assert.That((await admin.DeleteTopicsDetailedAsync(Array.Empty<string>())).Count).IsEqualTo(0);
        await Assert.That((await admin.DeleteTopicsDetailedAsync(Array.Empty<Guid>())).Count).IsEqualTo(0);
        await Assert.That((await admin.CreatePartitionsDetailedAsync(new Dictionary<string, int>())).Count).IsEqualTo(0);
        await Assert.That((await admin.CreatePartitionsDetailedAsync(new Dictionary<string, NewPartitions>())).Count).IsEqualTo(0);
        await Assert.That((await admin.AlterPartitionReassignmentsDetailedAsync(new Dictionary<TopicPartition, Optional<NewPartitionReassignment>>())).Count).IsEqualTo(0);
        await Assert.That(connection.ReceivedCalls()).IsEmpty();
    }

    [Test]
    public async Task InvalidInputsAndPreCancellation_DoNotSend()
    {
        var (admin, connection) = CreateAdmin();
        await using var client = admin;
        await Assert.ThrowsAsync<ArgumentNullException>(() => admin.CreateTopicsDetailedAsync(null!).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => admin.CreateTopicsDetailedAsync([new() { Name = "same" }, new() { Name = "same" }]).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => admin.DeleteTopicsDetailedAsync(["same", "same"]).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => admin.DeleteTopicsDetailedAsync([Guid.Empty]).AsTask());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => admin.CreatePartitionsDetailedAsync(
            new Dictionary<string, NewPartitions> { ["topic"] = new() { TotalCount = 2 } }, new() { TimeoutMs = -1 }).AsTask());
        await Assert.ThrowsAsync<OperationCanceledException>(() => admin.CreateTopicsDetailedAsync([new() { Name = "topic" }],
            cancellationToken: new CancellationToken(true)).AsTask());
        await Assert.That(connection.ReceivedCalls()).IsEmpty();
        IAdminClient unsupported = Substitute.For<IAdminClient>();
        await Assert.ThrowsAsync<NotSupportedException>(() => unsupported.CreateTopicsDetailedAsync([]).AsTask());
    }

    private static (AdminClient, IKafkaConnection) CreateAdmin() =>
        AdminClientIdempotentRetryTests.CreateAdminWithMockConnection(ApiKey.CreateTopics, ApiKey.DeleteTopics,
            ApiKey.CreatePartitions, ApiKey.AlterPartitionReassignments);

    private static ValueTask<IReadOnlyDictionary<string, AdminMutationResult>> Invoke(AdminClient admin, string operation) => operation switch
    {
        "create" => admin.CreateTopicsDetailedAsync([new() { Name = "good" }, new() { Name = "bad" }]),
        "delete" => admin.DeleteTopicsDetailedAsync(["good", "bad"]),
        "expand" => admin.CreatePartitionsDetailedAsync(new Dictionary<string, int> { ["good"] = 3, ["bad"] = 3 }),
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    private static void Setup(IKafkaConnection connection, string operation,
        Func<string[], (string Name, ErrorCode Code, string? Message)[]> respond)
    {
        switch (operation)
        {
            case "create":
                connection.SendAsync<CreateTopicsRequest, CreateTopicsResponse>(Arg.Any<CreateTopicsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
                    .Returns(call => ValueTask.FromResult(new CreateTopicsResponse
                    {
                        Topics = respond(call.Arg<CreateTopicsRequest>().Topics.Select(static item => item.Name).ToArray())
                            .Select(static item => new CreateTopicsResponseTopic { Name = item.Name, ErrorCode = item.Code, ErrorMessage = item.Message }).ToArray()
                    }));
                break;
            case "delete":
                connection.SendAsync<DeleteTopicsRequest, DeleteTopicsResponse>(Arg.Any<DeleteTopicsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
                    .Returns(call => ValueTask.FromResult(new DeleteTopicsResponse
                    {
                        Responses = respond(call.Arg<DeleteTopicsRequest>().Topics!.Select(static item => item.Name!).ToArray())
                            .Select(static item => new DeleteTopicsResponseTopic { Name = item.Name, ErrorCode = item.Code, ErrorMessage = item.Message }).ToArray()
                    }));
                break;
            case "expand":
                connection.SendAsync<CreatePartitionsRequest, CreatePartitionsResponse>(Arg.Any<CreatePartitionsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
                    .Returns(call => ValueTask.FromResult(new CreatePartitionsResponse
                    {
                        Results = respond(call.Arg<CreatePartitionsRequest>().Topics.Select(static item => item.Name).ToArray())
                            .Select(static item => new CreatePartitionsResponseResult { Name = item.Name, ErrorCode = item.Code, ErrorMessage = item.Message }).ToArray()
                    }));
                break;
            default: throw new ArgumentOutOfRangeException(nameof(operation));
        }
    }
}
