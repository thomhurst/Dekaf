using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Infrastructure;

/// <summary>
/// Drives a <see cref="CachingStringDeserializer"/> through its evidence-first promotion
/// so benchmark setup can measure the cached steady state deterministically.
/// </summary>
internal static class CachingDeserializerWarmup
{
    /// <summary>
    /// Cycles the payload set until sampled reuse promotes the cache. The lookup bound is
    /// derived from the promotion constants so it survives retuning; exceeding it means
    /// the payload set genuinely cannot promote and the setup is wrong.
    /// </summary>
    public static void PromoteOrThrow(
        CachingStringDeserializer deserializer,
        SerializationContext context,
        ReadOnlyMemory<byte>[] payloads)
    {
        // One fill pass, then an all-hit stream needs stride * threshold samples;
        // 8x slack absorbs window resets and sampling phase effects.
        var maxLookups = payloads.Length
            + 8 * CachingStringDeserializer.ObserveSampleStride * CachingStringDeserializer.PromoteSampledHits;

        for (var i = 0; i < maxLookups && !deserializer.IsCacheEnabled; i++)
            deserializer.Deserialize(payloads[i % payloads.Length], context);

        if (!deserializer.IsCacheEnabled)
            throw new InvalidOperationException("Payload reuse did not promote the cache.");
    }
}
