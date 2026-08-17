namespace Dekaf.Consumer;

/// <summary>
/// Specifies how consumer offsets are committed.
/// </summary>
public enum OffsetCommitMode
{
    /// <summary>Offsets are committed periodically in the background.</summary>
    Auto,

    /// <summary>Offsets must be committed explicitly.</summary>
    Manual
}
