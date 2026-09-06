using System.Buffers;
using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Admin;

public sealed class AdminClientPartitionExpansionTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task AmbiguousPartialSuccess_RetriesOnlyUnconfirmedTopics(bool typed)
    {
        var (admin, connection) = AdminClientIdempotentRetryTests.CreateAdminWithMockConnection(ApiKey.CreatePartitions);
        await using var client = admin;
        var calls = 0;
        connection.SendAsync<CreatePartitionsRequest, CreatePartitionsResponse>(Arg.Any<CreatePartitionsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<CreatePartitionsRequest>();
                if (++calls == 1)
                    throw new KafkaException(ErrorCode.RequestTimedOut, "ambiguous timeout");
                if (calls == 2)
                    return ValueTask.FromResult(new CreatePartitionsResponse
                    {
                        Results =
                        [
                            new() { Name = "retry-topic", ErrorCode = ErrorCode.InvalidPartitions },
                            new() { Name = "still-pending", ErrorCode = ErrorCode.NotController }
                        ]
                    });
                if (request.Topics.Count != 1 || request.Topics[0].Name != "still-pending")
                    throw new InvalidOperationException("A metadata-confirmed topic was sent again.");
                return ValueTask.FromResult(new CreatePartitionsResponse { Results = [new() { Name = "still-pending" }] });
            });

        if (typed)
            await admin.CreatePartitionsAsync(new Dictionary<string, NewPartitions>
            {
                ["retry-topic"] = new() { TotalCount = 3 }, ["still-pending"] = new() { TotalCount = 3 }
            });
        else
            await admin.CreatePartitionsAsync(new Dictionary<string, int> { ["retry-topic"] = 3, ["still-pending"] = 3 });

        await Assert.That(calls).IsEqualTo(3);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task PartialSuccess_RetriesOnlyUnconfirmedTopics(bool successFirst, bool typed)
    {
        var (admin, connection) = AdminClientIdempotentRetryTests.CreateAdminWithMockConnection(ApiKey.CreatePartitions);
        await using var client = admin;
        var calls = 0;
        connection.SendAsync<CreatePartitionsRequest, CreatePartitionsResponse>(Arg.Any<CreatePartitionsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<CreatePartitionsRequest>();
                if (++calls == 1)
                {
                    var success = new CreatePartitionsResponseResult { Name = "applied", ErrorCode = ErrorCode.None };
                    var retry = new CreatePartitionsResponseResult { Name = "retry-topic", ErrorCode = ErrorCode.NotController };
                    return ValueTask.FromResult(new CreatePartitionsResponse { Results = successFirst ? [success, retry] : [retry, success] });
                }
                if (request.Topics.Count != 1 || request.Topics[0].Name != "retry-topic")
                    throw new InvalidOperationException("A confirmed topic was sent again.");
                return ValueTask.FromResult(new CreatePartitionsResponse { Results = [new() { Name = "retry-topic" }] });
            });

        if (typed)
            await admin.CreatePartitionsAsync(new Dictionary<string, NewPartitions>
            {
                ["applied"] = new() { TotalCount = 3 }, ["retry-topic"] = new() { TotalCount = 3 }
            });
        else
            await admin.CreatePartitionsAsync(new Dictionary<string, int> { ["applied"] = 3, ["retry-topic"] = 3 });

        await Assert.That(calls).IsEqualTo(2);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CreatePartitions_PropagatesOptionsAndOrderedAssignments(bool explicitAssignments)
    {
        var (admin, connection) = AdminClientIdempotentRetryTests.CreateAdminWithMockConnection(ApiKey.CreatePartitions);
        await using var client = admin;
        CreatePartitionsRequest? captured = null;
        connection.SendAsync<CreatePartitionsRequest, CreatePartitionsResponse>(Arg.Any<CreatePartitionsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                captured = call.Arg<CreatePartitionsRequest>();
                return ValueTask.FromResult(new CreatePartitionsResponse { Results = [] });
            });

        IAdminClient capability = admin;
        await capability.CreatePartitionsAsync(new Dictionary<string, NewPartitions>
        {
            ["retry-topic"] = new() { TotalCount = 5, ReplicaAssignments = explicitAssignments ? [[3, 1], [1, 3]] : null }
        }, new CreatePartitionsOptions { ValidateOnly = true, TimeoutMs = 1234 });

        await Assert.That(captured!.ValidateOnly).IsTrue();
        await Assert.That(captured.TimeoutMs).IsEqualTo(1234);
        await Assert.That(captured.Topics[0].Count).IsEqualTo(5);
        if (explicitAssignments)
        {
            await Assert.That(captured.Topics[0].Assignments![0].BrokerIds[1]).IsEqualTo(1);
            await Assert.That(captured.Topics[0].Assignments![0].BrokerIds[0]).IsEqualTo(3);
        }
        else
            await Assert.That(captured.Topics[0].Assignments).IsNull();

        VerifyWire(captured, explicitAssignments);
    }

    private static void VerifyWire(CreatePartitionsRequest request, bool explicitAssignments)
    {
        foreach (short version in new short[] { 2, 3 })
        {
            var buffer = new ArrayBufferWriter<byte>();
            var writer = new KafkaProtocolWriter(buffer);
            request.Write(ref writer, version);
            var reader = new KafkaProtocolReader(buffer.WrittenMemory);
            if (reader.ReadUnsignedVarInt() != 2 || reader.ReadCompactString() != "retry-topic" || reader.ReadInt32() != 5)
                throw new InvalidOperationException("Incorrect topic encoding");
            if (reader.ReadUnsignedVarInt() != (explicitAssignments ? 3u : 0u))
                throw new InvalidOperationException("Incorrect assignment count");
            if (explicitAssignments)
            {
                VerifyReplicaAssignment(ref reader, 3, 1);
                VerifyReplicaAssignment(ref reader, 1, 3);
            }
            if (reader.ReadUnsignedVarInt() != 0 || reader.ReadInt32() != 1234 || !reader.ReadBoolean() || reader.ReadUnsignedVarInt() != 0 || reader.Remaining != 0)
                throw new InvalidOperationException("Incorrect options encoding");
        }
    }

    private static void VerifyReplicaAssignment(ref KafkaProtocolReader reader, int firstBroker, int secondBroker)
    {
        if (reader.ReadUnsignedVarInt() != 3 || reader.ReadInt32() != firstBroker || reader.ReadInt32() != secondBroker || reader.ReadUnsignedVarInt() != 0)
            throw new InvalidOperationException("Incorrect replica order");
    }

    [Test]
    public async Task ValidateOnly_AmbiguousRetry_DoesNotSuppressInvalidPartitions()
    {
        var (admin, connection) = AdminClientIdempotentRetryTests.CreateAdminWithMockConnection(ApiKey.CreatePartitions);
        await using var client = admin;
        var calls = 0;
        connection.SendAsync<CreatePartitionsRequest, CreatePartitionsResponse>(Arg.Any<CreatePartitionsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (++calls == 1)
                    throw new KafkaException(ErrorCode.RequestTimedOut, "ambiguous timeout");
                return ValueTask.FromResult(new CreatePartitionsResponse
                {
                    Results = [new() { Name = "retry-topic", ErrorCode = ErrorCode.InvalidPartitions }]
                });
            });
        var error = await Assert.ThrowsAsync<KafkaException>(async () => await admin.CreatePartitionsAsync(
            new Dictionary<string, NewPartitions> { ["retry-topic"] = new() { TotalCount = 3 } },
            new CreatePartitionsOptions { ValidateOnly = true }));
        await Assert.That(error!.ErrorCode).IsEqualTo(ErrorCode.InvalidPartitions);
        await Assert.That(calls).IsEqualTo(2);
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    [Arguments(4)]
    [Arguments(5)]
    [Arguments(6)]
    public async Task InvalidShape_IsRejectedBeforeSending(int shape)
    {
        var (admin, connection) = AdminClientIdempotentRetryTests.CreateAdminWithMockConnection(ApiKey.CreatePartitions);
        await using var client = admin;
        IReadOnlyList<IReadOnlyList<int>> assignments = shape switch
        {
            0 => [], 1 => [[]], 2 => [[1, 1]], 3 => [[-1]],
            4 => [[1], [1, 2]], 5 => [null!], _ => [[1], [1], [1], [1], [1]]
        };
        var error = await Assert.ThrowsAsync<ArgumentException>(async () => await admin.CreatePartitionsAsync(
            new Dictionary<string, NewPartitions> { ["retry-topic"] = new() { TotalCount = 5, ReplicaAssignments = assignments } }));
        await Assert.That(error!.ParamName).IsEqualTo("newPartitions");
        await connection.DidNotReceive().SendAsync<CreatePartitionsRequest, CreatePartitionsResponse>(Arg.Any<CreatePartitionsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>());
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ExplicitAssignments_AmbiguousRetry_RequiresMatchingReplicas(bool matches)
    {
        var (admin, connection) = AdminClientIdempotentRetryTests.CreateAdminWithMockConnection(ApiKey.CreatePartitions);
        await using var client = admin;
        var calls = 0;
        var replicas = new[] { matches ? 1 : 2 };
        connection.SendAsync<CreatePartitionsRequest, CreatePartitionsResponse>(Arg.Any<CreatePartitionsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<CreatePartitionsRequest>();
                if (request.Topics[0].Assignments![0].BrokerIds[0] != (matches ? 1 : 2))
                    throw new InvalidOperationException("Retry lost original replica assignment");
                replicas[0] = 99;
                if (++calls == 1)
                    throw new KafkaException(ErrorCode.RequestTimedOut, "ambiguous timeout");
                return ValueTask.FromResult(new CreatePartitionsResponse
                {
                    Results = [new() { Name = "retry-topic", ErrorCode = ErrorCode.InvalidPartitions }]
                });
            });
        var expansion = new Dictionary<string, NewPartitions>
        {
            ["retry-topic"] = new() { TotalCount = 3, ReplicaAssignments = [replicas] }
        };
        if (matches)
            await admin.CreatePartitionsAsync(expansion);
        else
        {
            var error = await Assert.ThrowsAsync<KafkaException>(async () => await admin.CreatePartitionsAsync(expansion));
            await Assert.That(error!.ErrorCode).IsEqualTo(ErrorCode.InvalidPartitions);
        }
        await Assert.That(calls).IsEqualTo(2);
    }

    [Test]
    public async Task ValidateOnly_RetriableResponse_PreservesOptions()
    {
        var (admin, connection) = AdminClientIdempotentRetryTests.CreateAdminWithMockConnection(ApiKey.CreatePartitions);
        await using var client = admin;
        var calls = 0;
        connection.SendAsync<CreatePartitionsRequest, CreatePartitionsResponse>(Arg.Any<CreatePartitionsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<CreatePartitionsRequest>();
                if (!request.ValidateOnly || request.TimeoutMs != 2000)
                    throw new InvalidOperationException("Retry changed validation options");
                return ValueTask.FromResult(new CreatePartitionsResponse
                {
                    Results = [new() { Name = "retry-topic", ErrorCode = ++calls == 1 ? ErrorCode.NotController : ErrorCode.None }]
                });
            });
        await admin.CreatePartitionsAsync(new Dictionary<string, NewPartitions> { ["retry-topic"] = new() { TotalCount = 5 } },
            new CreatePartitionsOptions { ValidateOnly = true, TimeoutMs = 2000 });
        await Assert.That(calls).IsEqualTo(2);
    }

    [Test]
    public async Task CancelledOperation_DoesNotSend()
    {
        var (admin, connection) = AdminClientIdempotentRetryTests.CreateAdminWithMockConnection(ApiKey.CreatePartitions);
        await using var client = admin;
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await admin.CreatePartitionsAsync(
            new Dictionary<string, NewPartitions> { ["retry-topic"] = new() { TotalCount = 5 } },
            cancellationToken: new CancellationToken(true)));
        await connection.DidNotReceive().SendAsync<CreatePartitionsRequest, CreatePartitionsResponse>(Arg.Any<CreatePartitionsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>());
    }

    [Test]
    [Arguments(0, 30000)]
    [Arguments(-1, 30000)]
    [Arguments(5, -1)]
    public async Task InvalidCountOrTimeout_IsRejectedBeforeSending(int count, int timeoutMs)
    {
        var (admin, connection) = AdminClientIdempotentRetryTests.CreateAdminWithMockConnection(ApiKey.CreatePartitions);
        await using var client = admin;
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await admin.CreatePartitionsAsync(
            new Dictionary<string, NewPartitions> { ["retry-topic"] = new() { TotalCount = count } },
            new CreatePartitionsOptions { TimeoutMs = timeoutMs }));
        await connection.DidNotReceive().SendAsync<CreatePartitionsRequest, CreatePartitionsResponse>(Arg.Any<CreatePartitionsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UnsupportedCustomClient_ThrowsClearly()
    {
        var custom = Substitute.For<IAdminClient>();
        await Assert.ThrowsAsync<NotSupportedException>(async () => await custom.CreatePartitionsAsync(new Dictionary<string, NewPartitions>()));
    }
}
