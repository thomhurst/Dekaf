using Dekaf.Telemetry;

namespace Dekaf.Networking;

/// <summary>Holds a shared control request's collector and write callback in one pooled reference.</summary>
internal sealed class TelemetryWriteObservationState
{
    private static readonly Reservoir.ObjectPool<TelemetryWriteObservationState, PoolPolicy> Pool = new(256);
    internal ClientTelemetryMetricCollector? Collector { get; private set; }
    internal Action? Callback { get; private set; }

    internal static TelemetryWriteObservationState Rent(ClientTelemetryMetricCollector collector, Action callback)
    {
        var state = Pool.Rent();
        state.Collector = collector;
        state.Callback = callback;
        return state;
    }

    internal void Return()
    {
        Collector = null;
        Callback = null;
        Pool.Return(this);
    }

    private readonly struct PoolPolicy : Reservoir.IPooledObjectPolicy<TelemetryWriteObservationState>, Reservoir.INonThrowingResetPolicy
    {
        public TelemetryWriteObservationState Create() => new();
        public void Destroy(TelemetryWriteObservationState state) { }
        public bool TryReset(TelemetryWriteObservationState state) => true;
    }
}
