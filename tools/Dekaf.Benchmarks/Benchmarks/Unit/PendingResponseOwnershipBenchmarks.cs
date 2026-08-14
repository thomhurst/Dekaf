using System.Threading.Tasks.Sources;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Protocol.Messages;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures the one-time array-return claim attached to each acknowledged produce request.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 5, iterationCount: 10)]
public class PendingResponseOwnershipBenchmarks
{
    private static readonly ReadyBatch[] EmptyBatches = [];
    private static readonly int[] EmptyGenerations = [];
    private readonly ExternalOwnershipSource _source = new();

    [Benchmark(Baseline = true)]
    public bool GuardPerRequest()
    {
        var pending = new BrokerSender.PendingResponse(
            default,
            EmptyBatches,
            EmptyGenerations,
            topicIds: null,
            apiVersion: 12,
            count: 1,
            encodedBytes: 0,
            dataBytes: 0,
            requestStartTime: 0,
            default);
        return pending.TryAbandonArrayOwnership();
    }

    [Benchmark]
    public bool PooledResponseClaim()
    {
        _source.Reset();
        var pending = BrokerSender.PendingResponse.Create(
            new PipelinedResponse<ProduceResponse>(_source, token: 0),
            EmptyBatches,
            EmptyGenerations,
            topicIds: null,
            apiVersion: 12,
            count: 1,
            encodedBytes: 0,
            dataBytes: 0,
            requestStartTime: 0,
            default);
        return pending.TryAbandonArrayOwnership();
    }

    private sealed class ExternalOwnershipSource :
        IPipelinedResponseSource<ProduceResponse>,
        IPipelinedResponseExternalOwnership
    {
        private int _readiness;

        public void Reset() => _readiness = 0;

        public bool TryRetainExternalOwner(int generation)
        {
            var readiness = Volatile.Read(ref _readiness);
            return generation == 0
                && Interlocked.CompareExchange(ref _readiness, readiness | 4, readiness) == readiness;
        }

        public bool TryReleaseExternalOwner(int generation) =>
            generation == 0 && (Interlocked.Or(ref _readiness, 8) & 8) == 0;

        public ProduceResponse GetResult(short token) => throw new NotSupportedException();

        public ValueTaskSourceStatus GetStatus(short token) => ValueTaskSourceStatus.Pending;

        public void OnCompleted(
            Action<object?> continuation,
            object? state,
            short token,
            ValueTaskSourceOnCompletedFlags flags) => throw new NotSupportedException();

        public void Abandon(short token)
        {
        }
    }
}
