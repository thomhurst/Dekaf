namespace Dekaf.StressTests.FaultInjection;

internal sealed class ToxiproxyControlPlaneException(
    string operation,
    int attemptCount,
    Exception innerException)
    : Exception(
        $"Toxiproxy control-plane operation '{operation}' failed after {attemptCount} attempts due to a harness infrastructure error.",
        innerException);
