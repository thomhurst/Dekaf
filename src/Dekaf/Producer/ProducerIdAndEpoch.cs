namespace Dekaf.Producer;

/// <summary>
/// Immutable producer ID and epoch pair, published as a single reference so that readers on other
/// threads never observe the ID of one producer session combined with the epoch of another
/// (the equivalent of Java's <c>ProducerIdAndEpoch</c>). The producer ID only changes when the
/// epoch space of the previous ID is exhausted, so a torn read of two separate fields would stamp
/// a batch with a brand-new producer ID and the stale <see cref="short.MaxValue"/> epoch, which the
/// broker would then record as that ID's epoch and reject every later batch with
/// <c>InvalidProducerEpoch</c>.
/// </summary>
internal sealed class ProducerIdAndEpoch
{
    /// <summary>No producer ID assigned yet.</summary>
    public static readonly ProducerIdAndEpoch None = new(-1, -1);

    public ProducerIdAndEpoch(long producerId, short epoch)
    {
        ProducerId = producerId;
        Epoch = epoch;
    }

    public long ProducerId { get; }

    public short Epoch { get; }
}
