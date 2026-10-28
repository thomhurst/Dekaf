using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Testing;
using NSubstitute;

namespace Dekaf.Tests.Unit.Admin;

public sealed class AdminClientControllerUnregistrationTests
{
    private const int ControllerId = 3;

    private static AdminClientOptions FastRetryOptions() => new()
    {
        BootstrapServers = ["localhost:9092"],
        RetryBackoffMs = 1,
        RetryBackoffMaxMs = 5
    };

    [Test]
    public async Task UnregisterControllerAsync_SendsV0RequestToController()
    {
        var (admin, connection) = CreateAdmin();
        UnregisterControllerRequest? sent = null;
        short sentVersion = -1;
        connection.SendAsync<UnregisterControllerRequest, UnregisterControllerResponse>(
                Arg.Any<UnregisterControllerRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                sent = callInfo.ArgAt<UnregisterControllerRequest>(0);
                sentVersion = callInfo.ArgAt<short>(1);
                return ValueTask.FromResult(new UnregisterControllerResponse { ErrorCode = ErrorCode.None });
            });

        await using (admin)
            await admin.UnregisterControllerAsync(ControllerId);

        await Assert.That(sent).IsNotNull();
        await Assert.That(sent!.ControllerId).IsEqualTo(ControllerId);
        await Assert.That(sentVersion).IsEqualTo((short)0);
    }

    [Test]
    public async Task UnregisterControllerAsync_ThroughIAdminClientExtension_DispatchesToCapability()
    {
        var (admin, connection) = CreateAdmin();
        ReturnsResponses(connection, ErrorCode.None);

        await using (admin)
            await ((IAdminClient)admin).UnregisterControllerAsync(ControllerId);

        await Assert.That(SendCount(connection)).IsEqualTo(1);
    }

    [Test]
    public async Task UnregisterControllerAsync_CustomClientWithoutCapability_ThrowsNotSupported()
    {
        var custom = Substitute.For<IAdminClient>();

        await Assert.That(async () => await custom.UnregisterControllerAsync(ControllerId))
            .Throws<NotSupportedException>();
    }

    [Test]
    public async Task UnregisterControllerAsync_UnknownController_ThrowsControllerIdNotRegistered()
    {
        var (admin, connection) = CreateAdmin();
        connection.SendAsync<UnregisterControllerRequest, UnregisterControllerResponse>(
                Arg.Any<UnregisterControllerRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new UnregisterControllerResponse
            {
                ErrorCode = ErrorCode.ControllerIdNotRegistered,
                ErrorMessage = "Controller ID 3 is not currently registered."
            }));

        await using (admin)
        {
            var exception = await Assert.That(async () => await admin.UnregisterControllerAsync(ControllerId))
                .Throws<KafkaException>();

            await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.ControllerIdNotRegistered);
            await Assert.That(exception.IsRetriable).IsFalse();
            await Assert.That(exception.Message).Contains("not currently registered");
        }

        // A definitive answer to the first send is not retried.
        await Assert.That(SendCount(connection)).IsEqualTo(1);
    }

    [Test]
    public async Task UnregisterControllerAsync_NullErrorMessage_UsesErrorCodeInMessage()
    {
        var (admin, connection) = CreateAdmin();
        ReturnsResponses(connection, ErrorCode.ClusterAuthorizationFailed);

        await using (admin)
        {
            var exception = await Assert.That(async () => await admin.UnregisterControllerAsync(ControllerId))
                .Throws<KafkaException>();

            await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.ClusterAuthorizationFailed);
            await Assert.That(exception.Message).Contains("UnregisterController failed");
        }
    }

    [Test]
    public async Task UnregisterControllerAsync_MetadataVersionTooOld_ThrowsUnsupportedVersionWithoutRetry()
    {
        var (admin, connection) = CreateAdmin();
        connection.SendAsync<UnregisterControllerRequest, UnregisterControllerResponse>(
                Arg.Any<UnregisterControllerRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new UnregisterControllerResponse
            {
                ErrorCode = ErrorCode.UnsupportedVersion,
                ErrorMessage = "The current MetadataVersion is too old to support controller unregistration."
            }));

        await using (admin)
        {
            var exception = await Assert.That(async () => await admin.UnregisterControllerAsync(ControllerId))
                .Throws<KafkaException>();

            await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.UnsupportedVersion);
        }

        await Assert.That(SendCount(connection)).IsEqualTo(1);
    }

    [Test]
    public async Task UnregisterControllerAsync_BrokerWithoutApi_ThrowsBrokerVersionWithoutSending()
    {
        // Kafka before 4.4 does not advertise API key 94.
        var (admin, connection) = AdminClientIdempotentRetryTests.CreateAdminWithMockConnection(FastRetryOptions());

        await using (admin)
        {
            await Assert.That(async () => await admin.UnregisterControllerAsync(ControllerId))
                .Throws<BrokerVersionException>();
        }

        await Assert.That(SendCount(connection)).IsEqualTo(0);
    }

    [Test]
    public async Task UnregisterControllerAsync_LostResponseThenNotRegistered_TreatedAsSuccess()
    {
        var (admin, connection) = CreateAdmin();
        var calls = 0;
        connection.SendAsync<UnregisterControllerRequest, UnregisterControllerResponse>(
                Arg.Any<UnregisterControllerRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns<ValueTask<UnregisterControllerResponse>>(_ =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                    throw new IOException("response lost");

                return ValueTask.FromResult(new UnregisterControllerResponse { ErrorCode = ErrorCode.ControllerIdNotRegistered });
            });

        await using (admin)
            await admin.UnregisterControllerAsync(ControllerId);

        await Assert.That(calls).IsEqualTo(2);
    }

    [Test]
    public async Task UnregisterControllerAsync_RequestTimedOutThenNotRegistered_TreatedAsSuccess()
    {
        var (admin, connection) = CreateAdmin();
        ReturnsResponses(connection, ErrorCode.RequestTimedOut, ErrorCode.ControllerIdNotRegistered);

        await using (admin)
            await admin.UnregisterControllerAsync(ControllerId);

        await Assert.That(SendCount(connection)).IsEqualTo(2);
    }

    [Test]
    public async Task UnregisterControllerAsync_RetriableErrorThenNotRegistered_Throws()
    {
        // NOT_CONTROLLER means the request was not applied, so a later
        // CONTROLLER_ID_NOT_REGISTERED is the controller's real answer.
        var (admin, connection) = CreateAdmin();
        ReturnsResponses(connection, ErrorCode.NotController, ErrorCode.ControllerIdNotRegistered);

        await using (admin)
        {
            var exception = await Assert.That(async () => await admin.UnregisterControllerAsync(ControllerId))
                .Throws<KafkaException>();

            await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.ControllerIdNotRegistered);
        }

        await Assert.That(SendCount(connection)).IsEqualTo(2);
    }

    [Test]
    public async Task UnregisterControllerAsync_CanceledBeforeCall_DoesNotSend()
    {
        var (admin, connection) = CreateAdmin();
        ReturnsResponses(connection, ErrorCode.None);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await using (admin)
        {
            await Assert.That(async () => await admin.UnregisterControllerAsync(ControllerId, cancellationToken: cts.Token))
                .Throws<OperationCanceledException>();
        }

        await Assert.That(SendCount(connection)).IsEqualTo(0);
    }

    [Test]
    public async Task UnregisterControllerAsync_CanceledWhileWaiting_StopsWaiting()
    {
        var (admin, connection) = CreateAdmin();
        var sendStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.SendAsync<UnregisterControllerRequest, UnregisterControllerResponse>(
                Arg.Any<UnregisterControllerRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => WaitForCancellationAsync(sendStarted, callInfo.ArgAt<CancellationToken>(2)));
        using var cts = new CancellationTokenSource();

        await using (admin)
        {
            var pending = admin.UnregisterControllerAsync(ControllerId, cancellationToken: cts.Token).AsTask();
            await sendStarted.Task;
            await cts.CancelAsync();

            await Assert.That(async () => await pending).Throws<OperationCanceledException>();
        }

        await Assert.That(SendCount(connection)).IsEqualTo(1);
    }

    [Test]
    public async Task UnregisterControllerAsync_UnansweredRequest_EndsAtTimeout()
    {
        var (admin, connection) = CreateAdmin();
        var sendStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.SendAsync<UnregisterControllerRequest, UnregisterControllerResponse>(
                Arg.Any<UnregisterControllerRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => WaitForCancellationAsync(sendStarted, callInfo.ArgAt<CancellationToken>(2)));

        await using (admin)
        {
            var exception = await Assert.That(async () => await admin.UnregisterControllerAsync(
                    ControllerId,
                    new UnregisterControllerOptions { TimeoutMs = 200 }))
                .Throws<KafkaTimeoutException>();

            await Assert.That(exception!.TimeoutKind).IsEqualTo(TimeoutKind.Api);
        }
    }

    [Test]
    public async Task UnregisterControllerAsync_ZeroTimeout_ThrowsWithoutSending()
    {
        var (admin, connection) = CreateAdmin();
        ReturnsResponses(connection, ErrorCode.None);

        await using (admin)
        {
            await Assert.That(async () => await admin.UnregisterControllerAsync(
                    ControllerId,
                    new UnregisterControllerOptions { TimeoutMs = 0 }))
                .Throws<KafkaTimeoutException>();
        }

        await Assert.That(SendCount(connection)).IsEqualTo(0);
    }

    [Test]
    public async Task UnregisterControllerAsync_InvalidArguments_Throw()
    {
        var (admin, _) = CreateAdmin();

        await using (admin)
        {
            await Assert.That(async () => await admin.UnregisterControllerAsync(-1))
                .Throws<ArgumentOutOfRangeException>();
            await Assert.That(async () => await admin.UnregisterControllerAsync(
                    ControllerId,
                    new UnregisterControllerOptions { TimeoutMs = -1 }))
                .Throws<ArgumentOutOfRangeException>();
        }
    }

    [Test]
    public async Task InMemoryAdminClient_UnregisterControllerAsync_ValidatesAndHonorsCancellation()
    {
        await using var admin = new InMemoryAdminClient(new InMemoryKafkaCluster());

        await admin.UnregisterControllerAsync(ControllerId);
        await ((IAdminClient)admin).UnregisterControllerAsync(ControllerId);
        await Assert.That(async () => await admin.UnregisterControllerAsync(-1))
            .Throws<ArgumentOutOfRangeException>();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.That(async () => await admin.UnregisterControllerAsync(ControllerId, cancellationToken: cts.Token))
            .Throws<OperationCanceledException>();
    }

    [Test]
    public async Task InMemoryAdminClient_UnregisterControllerAsync_HonorsTimeout()
    {
        await using var admin = new InMemoryAdminClient(new InMemoryKafkaCluster());

        await Assert.That(async () => await admin.UnregisterControllerAsync(
                ControllerId, new UnregisterControllerOptions { TimeoutMs = 0 }))
            .Throws<KafkaTimeoutException>();

        // The deadline also bounds the operation itself, including fault waits.
        admin.ConfigureTimeoutSourceTestHook = static source => source.Cancel();
        await Assert.That(async () => await admin.UnregisterControllerAsync(ControllerId))
            .Throws<KafkaTimeoutException>();
    }

    private static (AdminClient Admin, IKafkaConnection Connection) CreateAdmin() =>
        AdminClientIdempotentRetryTests.CreateAdminWithMockConnection(FastRetryOptions(), ApiKey.UnregisterController);

    private static void ReturnsResponses(IKafkaConnection connection, params ErrorCode[] errorCodes)
    {
        var calls = -1;
        connection.SendAsync<UnregisterControllerRequest, UnregisterControllerResponse>(
                Arg.Any<UnregisterControllerRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var index = Math.Min(Interlocked.Increment(ref calls), errorCodes.Length - 1);
                return ValueTask.FromResult(new UnregisterControllerResponse { ErrorCode = errorCodes[index] });
            });
    }

    private static int SendCount(IKafkaConnection connection) =>
        connection.ReceivedCalls().Count(call =>
            call.GetMethodInfo().Name == nameof(IKafkaConnection.SendAsync) &&
            call.GetArguments()[0] is UnregisterControllerRequest);

    private static async ValueTask<UnregisterControllerResponse> WaitForCancellationAsync(
        TaskCompletionSource sendStarted,
        CancellationToken cancellationToken)
    {
        sendStarted.TrySetResult();
        await Task.Delay(Timeout.Infinite, cancellationToken);
        throw new InvalidOperationException("Cancellation was not observed.");
    }
}
