using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Unit.ShareConsumer;

public sealed class ShareAcknowledgedOffsetsTests
{
    [Test]
    public async Task OffsetView_IndexesCopiesAndEnumeratesAcrossBatches()
    {
        var offsets = new ShareAcknowledgedOffsets(
        [
            new AcknowledgementBatchData(10, 11, [(byte)AcknowledgeType.Accept, (byte)AcknowledgeType.Release]),
            new AcknowledgementBatchData(20, 22, [(byte)AcknowledgeType.Accept, (byte)AcknowledgeType.Accept, (byte)AcknowledgeType.Reject])
        ]);

        await Assert.That(offsets.Length).IsEqualTo(5);
        await Assert.That(offsets[0]).IsEqualTo(10);
        await Assert.That(offsets[2]).IsEqualTo(20);
        await Assert.That(offsets[4]).IsEqualTo(22);

        var copied = new long[offsets.Length];
        offsets.CopyTo(copied);
        await Assert.That(copied).IsEquivalentTo([10L, 11L, 20L, 21L, 22L]);

        var enumerated = new List<long>();
        foreach (var offset in offsets)
            enumerated.Add(offset);

        await Assert.That(enumerated).IsEquivalentTo(copied);
    }

    [Test]
    public async Task DefaultOffsetView_IsEmpty()
    {
        var offsets = default(ShareAcknowledgedOffsets);
        var enumerator = offsets.GetEnumerator();

        await Assert.That(offsets.Length).IsEqualTo(0);
        await Assert.That(enumerator.MoveNext()).IsFalse();
    }

    [Test]
    public async Task OffsetView_RejectsInvalidIndexAndShortDestination()
    {
        var offsets = new ShareAcknowledgedOffsets(
        [
            new AcknowledgementBatchData(10, 10, [(byte)AcknowledgeType.Accept])
        ]);

        await Assert.That(() => offsets[-1]).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => offsets[1]).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => offsets.CopyTo(Span<long>.Empty)).Throws<ArgumentException>();
    }
}
