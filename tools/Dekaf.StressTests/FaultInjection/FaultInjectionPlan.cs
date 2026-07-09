namespace Dekaf.StressTests.FaultInjection;

internal enum FaultWindowKind
{
    ConnectionReset,
    HalfOpen,
    SlowClose,
    LatencyAndBandwidth,
    BrokerKillAndRestart,
    LeaderElection,
    RollingRestart
}

internal sealed record FaultWindowDefinition(string Name, FaultWindowKind Kind);

internal static class FaultInjectionPlan
{
    private static readonly FaultWindowDefinition[] NetworkFaults =
    [
        new("connection-reset", FaultWindowKind.ConnectionReset),
        new("half-open", FaultWindowKind.HalfOpen),
        new("slow-close", FaultWindowKind.SlowClose),
        new("latency-bandwidth", FaultWindowKind.LatencyAndBandwidth)
    ];

    internal static IReadOnlyList<FaultWindowDefinition> Build(string profile, int brokerCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profile);
        if (brokerCount is not 1 and not 3)
        {
            throw new ArgumentOutOfRangeException(nameof(brokerCount), brokerCount,
                "Fault injection supports one or three brokers.");
        }

        var includeNetwork = profile.Equals("network", StringComparison.OrdinalIgnoreCase)
            || profile.Equals("all", StringComparison.OrdinalIgnoreCase);
        var includeBroker = profile.Equals("broker", StringComparison.OrdinalIgnoreCase)
            || profile.Equals("all", StringComparison.OrdinalIgnoreCase);
        if (!includeNetwork && !includeBroker)
        {
            throw new ArgumentException("Fault profile must be network, broker, or all.", nameof(profile));
        }

        var plan = new List<FaultWindowDefinition>(7);
        if (includeNetwork)
        {
            plan.AddRange(NetworkFaults);
        }

        if (includeBroker)
        {
            plan.Add(new FaultWindowDefinition("broker-kill-restart", FaultWindowKind.BrokerKillAndRestart));
            if (brokerCount == 3)
            {
                plan.Add(new FaultWindowDefinition("leader-election", FaultWindowKind.LeaderElection));
                plan.Add(new FaultWindowDefinition("rolling-restart", FaultWindowKind.RollingRestart));
            }
        }

        return plan;
    }
}
