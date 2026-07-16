namespace Dekaf.StressTests;

internal static class StressRunCompletionPolicy
{
    private const double MinimumDurationRatio = 0.9;

    internal static bool EndedEarly(
        double elapsedSeconds,
        int configuredDurationMinutes,
        bool isSelfBounded) =>
        // Self-bounded scenarios define their own completion window instead of running
        // for --duration: the steady round-trip produce phase ends at its configured
        // seconds or its log-byte budget, then a variable-length consume phase follows.
        // Their premature ends already fail the run through produce-timeout errors and
        // round-trip validation.
        !isSelfBounded &&
        elapsedSeconds < configuredDurationMinutes * 60 * MinimumDurationRatio;
}
