using System.Collections.Concurrent;
using Dekaf.Protocol.Records;

namespace Dekaf.Producer;

/// <summary>
/// KIP-126 compression ratio estimates, isolated per producer, topic, and codec.
/// </summary>
internal sealed class CompressionRatioEstimator
{
    internal const double EstimationFactor = 1.05;
    internal const double ImprovingStep = 0.005;
    internal const double DeterioratingStep = 0.05;

    private readonly ConcurrentDictionary<(string Topic, CompressionType Type), Estimate> _estimates = new();

    internal double Get(string topic, CompressionType type)
        => type == CompressionType.None
            ? 1.0
            : _estimates.GetOrAdd((topic, type), static _ => new Estimate()).Value;

    internal double Update(string topic, CompressionType type, double observedRatio)
    {
        if (type == CompressionType.None
            || double.IsNaN(observedRatio)
            || double.IsInfinity(observedRatio)
            || observedRatio <= 0)
            return 1.0;

        return _estimates.GetOrAdd((topic, type), static _ => new Estimate())
            .Update(observedRatio);
    }

    internal void ResetAfterSplit(string topic, CompressionType type, double observedRatio)
    {
        if (type == CompressionType.None)
            return;

        _estimates.GetOrAdd((topic, type), static _ => new Estimate())
            .Reset(Math.Max(1.0, observedRatio));
    }

    private sealed class Estimate
    {
        private readonly object _lock = new();
        private double _value = 1.0;

        internal double Value
        {
            get
            {
                lock (_lock)
                    return _value;
            }
        }

        internal double Update(double observedRatio)
        {
            lock (_lock)
            {
                if (observedRatio > _value)
                    _value = Math.Max(_value + DeterioratingStep, observedRatio);
                else if (observedRatio < _value)
                    _value = Math.Max(_value - ImprovingStep, observedRatio);

                return _value;
            }
        }

        internal void Reset(double value)
        {
            lock (_lock)
                _value = value;
        }
    }
}
