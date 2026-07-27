namespace Dekaf.Protocol;

/// <summary>
/// Finite semantic limits for broker-controlled response arrays. Minimum wire-size
/// validation remains the proportional defense for every frame size.
/// </summary>
internal static class ResponseArrayLimits
{
    internal const int MaxGroupCount = 100_000;
    internal const int MaxMemberCount = 100_000;
    internal const int MaxCoordinatorCount = 100_000;
    internal const int MaxTopicCount = 1_000_000;
    internal const int MaxPartitionCount = 1_000_000;
}
