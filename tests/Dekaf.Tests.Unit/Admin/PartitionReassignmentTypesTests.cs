using Dekaf.Admin;

namespace Dekaf.Tests.Unit.Admin;

public sealed class PartitionReassignmentTypesTests
{
    [Test]
    public async Task Optional_None_HasNoValue()
    {
        var optional = Optional.None<NewPartitionReassignment>();

        await Assert.That(optional.HasValue).IsFalse();
    }

    [Test]
    public async Task Optional_Some_HasValue()
    {
        var reassignment = NewPartitionReassignment.ToReplicas(1, 2, 3);
        var optional = Optional.Some(reassignment);

        await Assert.That(optional.HasValue).IsTrue();
        await Assert.That(optional.Value).IsEqualTo(reassignment);
    }

    [Test]
    public async Task NewPartitionReassignment_ToReplicas_SetsTargetReplicas()
    {
        var reassignment = NewPartitionReassignment.ToReplicas(1, 2, 3);

        await Assert.That(reassignment.TargetReplicas).IsEquivalentTo([1, 2, 3]);
    }

    [Test]
    public async Task Options_DefaultValues_AreCorrect()
    {
        var alterOptions = new AlterPartitionReassignmentsOptions();
        var listOptions = new ListPartitionReassignmentsOptions();

        await Assert.That(alterOptions.TimeoutMs).IsEqualTo(60000);
        await Assert.That(alterOptions.AllowReplicationFactorChange).IsTrue();
        await Assert.That(listOptions.TimeoutMs).IsEqualTo(60000);
    }

    [Test]
    public async Task PartitionReassignment_CanBeCreated()
    {
        var reassignment = new PartitionReassignment
        {
            Replicas = [1, 2],
            AddingReplicas = [3],
            RemovingReplicas = [1]
        };

        await Assert.That(reassignment.Replicas).IsEquivalentTo([1, 2]);
        await Assert.That(reassignment.AddingReplicas).IsEquivalentTo([3]);
        await Assert.That(reassignment.RemovingReplicas).IsEquivalentTo([1]);
    }
}
