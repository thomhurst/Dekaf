namespace Dekaf.Consumer;

/// <summary>
/// The leader epoch of the batch that ended at one prefetch position, kept with the whole
/// 64-bit position it belongs to. One instance per assigned partition, allocated when the
/// partition's first response is published and updated in place after that, so recording an
/// epoch allocates nothing per fetch.
/// </summary>
/// <remarks>
/// Readers take no lock: <see cref="_version"/> is odd while a record is being replaced, and a
/// reader that saw it change read nothing it can use. <see cref="Record"/> calls must not
/// overlap each other. <see cref="Clear"/> may overlap anything: a record that outlives it
/// still pairs an epoch with its own position, and a request only uses the epoch recorded for
/// the position it fetches from.
/// </remarks>
internal sealed class FetchedLeaderEpoch
{
    // Not a fetch position: those are offsets, or -1 and -2 for the log end and start.
    private const long NotRecorded = long.MinValue;

    private int _version;
    private long _fetchOffset = NotRecorded;
    private int _leaderEpoch;

    public FetchedLeaderEpoch()
    {
    }

    public FetchedLeaderEpoch(long fetchOffset, int leaderEpoch) => Record(fetchOffset, leaderEpoch);

    public void Record(long fetchOffset, int leaderEpoch)
    {
        var version = _version;
        Volatile.Write(ref _version, version + 1);
        Volatile.Write(ref _fetchOffset, fetchOffset);
        Volatile.Write(ref _leaderEpoch, leaderEpoch);
        Volatile.Write(ref _version, version + 2);
    }

    public void Clear() => Volatile.Write(ref _fetchOffset, NotRecorded);

    /// <returns>
    /// False when nothing is recorded. Otherwise true, with the epoch recorded for
    /// <paramref name="fetchOffset"/>, or -1 when the record belongs to another position or is
    /// being replaced.
    /// </returns>
    public bool TryResolve(long fetchOffset, out int leaderEpoch)
    {
        leaderEpoch = -1;
        var version = Volatile.Read(ref _version);
        var recordedOffset = Volatile.Read(ref _fetchOffset);
        var recordedEpoch = Volatile.Read(ref _leaderEpoch);
        if ((version & 1) != 0 || Volatile.Read(ref _version) != version)
            return true;

        if (recordedOffset == NotRecorded)
            return false;

        if (recordedOffset == fetchOffset)
            leaderEpoch = recordedEpoch;
        return true;
    }
}
