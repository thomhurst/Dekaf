using System.Runtime.CompilerServices;

namespace Dekaf.Retry;

/// <summary>
/// Calculates the overflow-safe exponential backoff shared by Kafka client requests.
/// </summary>
internal static class ExponentialRetryBackoff
{
    private const double MinimumJitterFactor = 0.8;
    private const double JitterFactorRange = 0.4;
    private const int MaximumExponent = 62;

    public static void Validate(int initialDelayMs, int maximumDelayMs)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(initialDelayMs);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumDelayMs);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CalculateDelayMilliseconds(int initialDelayMs, int maximumDelayMs, int failureCount)
        => (int)Math.Min(
            int.MaxValue,
            CalculateDelayMilliseconds(
                initialDelayMs,
                maximumDelayMs,
                failureCount,
                Random.Shared.NextDouble()));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static double CalculateDelayMilliseconds(
        double initialDelayMs,
        double maximumDelayMs,
        int failureCount,
        double randomValue)
    {
        if (initialDelayMs <= 0 || maximumDelayMs <= 0)
            return 0;

        var effectiveMaximumDelayMs = Math.Max(initialDelayMs, maximumDelayMs);
        var exponent = Math.Min(Math.Max(failureCount - 1, 0), MaximumExponent);
        var exponentialDelayMs = initialDelayMs * Math.Pow(2, exponent);
        var cappedDelayMs = Math.Min(effectiveMaximumDelayMs, exponentialDelayMs);
        var unitRandom = double.IsNaN(randomValue) ? 0.5 : Math.Clamp(randomValue, 0, 1);
        return cappedDelayMs * (MinimumJitterFactor + (unitRandom * JitterFactorRange));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static double CalculateHardCappedDelayMilliseconds(
        double initialDelayMs,
        double maximumDelayMs,
        int exponent,
        double randomValue)
    {
        if (initialDelayMs <= 0 || maximumDelayMs <= 0)
            return 0;

        var effectiveMaximumDelayMs = Math.Max(initialDelayMs, maximumDelayMs);
        var maximumExponent = Math.Min(
            Math.Log(effectiveMaximumDelayMs / initialDelayMs, 2),
            MaximumExponent);
        var boundedExponent = Math.Min(Math.Max(exponent, 0), maximumExponent);
        var exponentialDelayMs = initialDelayMs * Math.Pow(2, boundedExponent);
        var unitRandom = double.IsNaN(randomValue) ? 0.5 : Math.Clamp(randomValue, 0, 1);
        var jitteredDelayMs = exponentialDelayMs
            * (MinimumJitterFactor + (unitRandom * JitterFactorRange));
        return Math.Min(effectiveMaximumDelayMs, jitteredDelayMs);
    }
}
