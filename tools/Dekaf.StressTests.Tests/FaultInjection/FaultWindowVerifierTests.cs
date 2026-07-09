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
}
