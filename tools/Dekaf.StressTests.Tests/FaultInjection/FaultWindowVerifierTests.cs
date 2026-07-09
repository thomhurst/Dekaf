using Dekaf.StressTests.FaultInjection;

namespace Dekaf.StressTests.Tests.FaultInjection;

public class FaultWindowVerifierTests
{
    [Test]
    public async Task Verify_ExactIdempotentDelivery_Passes()
    {
        var result = FaultWindowVerifier.Verify(
            acceptedMessageCount: 4,
            brokerDeliveredCount: 4,
            deliveryErrorIds: [],
            consumedIds: [0, 1, 2, 3],
            requireZeroDuplicates: true);

        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.UnexplainedLossCount).IsEqualTo(0);
        await Assert.That(result.DuplicateCount).IsEqualTo(0);
        await Assert.That(result.MissingIds).IsEmpty();
        await Assert.That(result.UnexpectedIds).IsEmpty();
    }

    [Test]
    public async Task Verify_RecordedDeliveryError_DoesNotCountAsUnexplainedLoss()
    {
        var result = FaultWindowVerifier.Verify(
            acceptedMessageCount: 4,
            brokerDeliveredCount: 3,
            deliveryErrorIds: [2],
            consumedIds: [0, 1, 3],
            requireZeroDuplicates: true);

        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.UnexplainedLossCount).IsEqualTo(0);
        await Assert.That(result.MissingIds).IsEmpty();
    }

    [Test]
    public async Task Verify_UnexplainedMissingMessage_Fails()
    {
        var result = FaultWindowVerifier.Verify(
            acceptedMessageCount: 4,
            brokerDeliveredCount: 3,
            deliveryErrorIds: [],
            consumedIds: [0, 1, 3],
            requireZeroDuplicates: true);

        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.UnexplainedLossCount).IsEqualTo(1);
        await Assert.That(result.MissingIds).IsEquivalentTo([2L]);
    }

    [Test]
    public async Task Verify_DuplicateIdempotentRecord_Fails()
    {
        var result = FaultWindowVerifier.Verify(
            acceptedMessageCount: 4,
            brokerDeliveredCount: 5,
            deliveryErrorIds: [],
            consumedIds: [0, 1, 2, 2, 3],
            requireZeroDuplicates: true);

        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.DuplicateCount).IsEqualTo(1);
        await Assert.That(result.DuplicateIds).IsEquivalentTo([2L]);
    }

    [Test]
    public async Task Verify_UnexpectedMessageId_Fails()
    {
        var result = FaultWindowVerifier.Verify(
            acceptedMessageCount: 4,
            brokerDeliveredCount: 5,
            deliveryErrorIds: [],
            consumedIds: [0, 1, 2, 3, 99],
            requireZeroDuplicates: true);

        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.UnexpectedIds).IsEquivalentTo([99L]);
    }

    [Test]
    [Arguments("network", 1, 4)]
    [Arguments("broker", 1, 1)]
    [Arguments("broker", 3, 3)]
    [Arguments("all", 3, 7)]
    public async Task BuildPlan_SelectsRequiredFaultWindows(string profile, int brokerCount, int expectedCount)
    {
        var plan = FaultInjectionPlan.Build(profile, brokerCount);

        await Assert.That(plan).Count().IsEqualTo(expectedCount);
    }

    [Test]
    public async Task DetermineExitCode_AllowsNamedLiveConsumerRecoveryFailure()
    {
        FaultWindowRunResult[] results =
        [
            new()
            {
                Name = "leader-election",
                StartedAtUtc = DateTime.UnixEpoch,
                Succeeded = false,
                LiveConsumerRecoveryFailed = true
            }
        ];

        var allowedFailures = new HashSet<string>(["leader-election"], StringComparer.OrdinalIgnoreCase);

        var exitCode = FaultInjectionRunner.DetermineExitCode(results, allowedFailures);

        await Assert.That(exitCode).IsEqualTo(0);
    }

    [Test]
    public async Task DetermineExitCode_KeepsOtherFailuresHardInAllowedWindow()
    {
        FaultWindowRunResult[] results =
        [
            new()
            {
                Name = "leader-election",
                StartedAtUtc = DateTime.UnixEpoch,
                Succeeded = false
            }
        ];

        var allowedFailures = new HashSet<string>(["leader-election"], StringComparer.OrdinalIgnoreCase);

        var exitCode = FaultInjectionRunner.DetermineExitCode(results, allowedFailures);

        await Assert.That(exitCode).IsEqualTo(1);
    }

    [Test]
    public async Task DetermineExitCode_KeepsLiveConsumerRecoveryFailureHardOutsideAllowedWindow()
    {
        FaultWindowRunResult[] results =
        [
            new()
            {
                Name = "broker-kill-restart",
                StartedAtUtc = DateTime.UnixEpoch,
                Succeeded = false,
                LiveConsumerRecoveryFailed = true
            }
        ];

        var allowedFailures = new HashSet<string>(["leader-election"], StringComparer.OrdinalIgnoreCase);

        var exitCode = FaultInjectionRunner.DetermineExitCode(results, allowedFailures);

        await Assert.That(exitCode).IsEqualTo(1);
    }

    [Test]
    public async Task ClassifyLiveConsumerFailure_ShutdownAfterRecovery_RemainsHardFailure()
    {
        var failure = FaultInjectionRunner.ClassifyLiveConsumerFailure(
            recoveryFailure: null,
            shutdownFailure: new TimeoutException("consumer did not stop"),
            consumerExitedBeforeCancellation: false);

        await Assert.That(failure).IsEqualTo(LiveConsumerFailureKind.Shutdown);
    }

    [Test]
    public async Task ClassifyLiveConsumerFailure_RecoveryTimeoutAndShutdownHang_RemainsHardFailure()
    {
        var failure = FaultInjectionRunner.ClassifyLiveConsumerFailure(
            recoveryFailure: new TimeoutException("consumer did not recover"),
            shutdownFailure: new TimeoutException("consumer did not stop"),
            consumerExitedBeforeCancellation: false);

        await Assert.That(failure).IsEqualTo(LiveConsumerFailureKind.Shutdown);
    }

    [Test]
    public async Task ClassifyLiveConsumerFailure_ConsumerFaultDuringRecovery_IsRecoveryFailure()
    {
        var consumerFailure = new InvalidOperationException("consumer failed");
        var failure = FaultInjectionRunner.ClassifyLiveConsumerFailure(
            recoveryFailure: consumerFailure,
            shutdownFailure: consumerFailure,
            consumerExitedBeforeCancellation: true);

        await Assert.That(failure).IsEqualTo(LiveConsumerFailureKind.Recovery);
    }

    [Test]
    public async Task DetermineExitCode_KeepsShutdownFailureHardInAllowedWindow()
    {
        FaultWindowRunResult[] results =
        [
            new()
            {
                Name = "leader-election",
                StartedAtUtc = DateTime.UnixEpoch,
                Succeeded = false,
                LiveConsumerShutdownFailed = true
            }
        ];

        var allowedFailures = new HashSet<string>(["leader-election"], StringComparer.OrdinalIgnoreCase);

        var exitCode = FaultInjectionRunner.DetermineExitCode(results, allowedFailures);

        await Assert.That(exitCode).IsEqualTo(1);
    }

    [Test]
    public async Task GetExpectedBrokerDeliveryCount_SubtractsDeliveryErrors()
    {
        var expected = FaultInjectionRunner.GetExpectedBrokerDeliveryCount(
            acceptedMessages: 10,
            deliveryErrorCount: 2);

        await Assert.That(expected).IsEqualTo(8);
    }

    [Test]
    public async Task Validate_RejectsAllowedFailureOutsideSelectedPlan()
    {
        var options = new FaultInjectionOptions
        {
            Profile = "broker",
            BrokerCount = 1,
            AllowedFailureWindows = new HashSet<string>(["leader-election"], StringComparer.OrdinalIgnoreCase)
        };

        await Assert.That(options.Validate)
            .Throws<ArgumentException>()
            .WithMessageContaining("leader-election");
    }
}
