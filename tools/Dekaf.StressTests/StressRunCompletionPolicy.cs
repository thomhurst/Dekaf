namespace Dekaf.StressTests;

internal static class StressRunCompletionPolicy
{
    private const double MinimumDurationRatio = 0.9;

    internal static bool EndedEarly(
        double elapsedSeconds,
        int configuredDurationMinutes,
        bool isMessageBounded) =>
        !isMessageBounded &&
        elapsedSeconds < configuredDurationMinutes * 60 * MinimumDurationRatio;
}
