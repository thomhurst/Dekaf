namespace Dekaf.Tests.Integration;

public sealed class ReassignmentSequenceOracleTests
{
    [Test]
    public async Task ExactlyOnce_OrderedRecords_Completes()
    {
        var oracle = new ReassignmentSequenceOracle(1, 2, allowDuplicates: false);

        oracle.Observe("topic", 0, 0, "0:0", "0:0");
        oracle.Observe("topic", 0, 1, "0:1", "0:1");
        oracle.EnsureComplete();

        await Assert.That(oracle.IsComplete).IsTrue();
    }

    [Test]
    public async Task ExactlyOnce_DuplicateRecord_Throws()
    {
        var oracle = new ReassignmentSequenceOracle(1, 2, allowDuplicates: false);
        oracle.Observe("topic", 0, 0, "0:0", "0:0");

        await Assert.That(() => oracle.Observe("topic", 0, 1, "0:0", "0:0"))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task AtLeastOnce_DuplicateAndReorderedRecords_Completes()
    {
        var oracle = new ReassignmentSequenceOracle(1, 3, allowDuplicates: true);

        oracle.Observe("topic", 0, 0, "0:0", "0:0");
        oracle.Observe("topic", 0, 1, "0:0", "0:0");
        oracle.Observe("topic", 0, 2, "0:2", "0:2");
        oracle.Observe("topic", 0, 3, "0:1", "0:1");
        oracle.EnsureComplete();

        await Assert.That(oracle.IsComplete).IsTrue();
    }

    [Test]
    public async Task AtLeastOnce_MissingRecord_Throws()
    {
        var oracle = new ReassignmentSequenceOracle(1, 2, allowDuplicates: true);
        oracle.Observe("topic", 0, 0, "0:0", "0:0");

        await Assert.That(oracle.EnsureComplete).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task AtLeastOnce_CorruptRecord_Throws()
    {
        var oracle = new ReassignmentSequenceOracle(1, 2, allowDuplicates: true);

        await Assert.That(() => oracle.Observe("topic", 0, 0, "0:0", "0:1"))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task AtLeastOnce_OutOfRangeRecord_Throws()
    {
        var oracle = new ReassignmentSequenceOracle(1, 2, allowDuplicates: true);

        await Assert.That(() => oracle.Observe("topic", 0, 0, "0:2", "0:2"))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task AtLeastOnce_BrokerOffsetGap_Throws()
    {
        var oracle = new ReassignmentSequenceOracle(1, 2, allowDuplicates: true);

        await Assert.That(() => oracle.Observe("topic", 0, 1, "0:0", "0:0"))
            .Throws<InvalidOperationException>();
    }
}
