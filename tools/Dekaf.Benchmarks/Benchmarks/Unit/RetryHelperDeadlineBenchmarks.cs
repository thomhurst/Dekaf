using BenchmarkDotNet.Attributes;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Retry;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// RetryHelper's deadline mode, which consumer commits, committed-offset fetches, offset resets
/// and every admin call run once per request (never per message). Successful calls are the
/// steady state. OneTransportRetry adds a single NETWORK_EXCEPTION retry with no backoff and
/// no metadata refresh, so it isolates the retry bookkeeping and failure classification.
/// </summary>
[MemoryDiagnoser]
public class RetryHelperDeadlineBenchmarks
{
    private static readonly RetryDeadline Deadline = new("Benchmark", TimeSpan.FromSeconds(30));
    private static readonly Func<ValueTask> VoidSuccess = static () => ValueTask.CompletedTask;
    private static readonly Func<ValueTask<int>> ResultSuccess = static () => ValueTask.FromResult(42);
    private static readonly Func<KafkaException, bool> NoMetadataRefresh = static _ => false;

    private MetadataManager _metadata = null!;
    private Func<ValueTask<int>> _failOnceThenSucceed = null!;
    private bool _failNext;

    [GlobalSetup]
    public void Setup()
    {
        _metadata = new MetadataManager(new NoConnectionPool(), ["localhost:9092"]);
        _failOnceThenSucceed = () =>
        {
            if (!_failNext)
                return ValueTask.FromResult(42);

            _failNext = false;
            // A fresh exception per failure, as a connection raises one: rethrowing one shared
            // instance would grow its captured stack trace on every iteration.
            return ValueTask.FromException<int>(new KafkaException(
                ErrorCode.NetworkException, "Connection closed by the broker (EOF).", isRetriable: true));
        };

        _failNext = true;
        if (OneTransportRetry().AsTask().GetAwaiter().GetResult() != 42 || _failNext)
            throw new InvalidOperationException("Expected exactly one retry before success.");
    }

    [GlobalCleanup]
    public void Cleanup() => _metadata.DisposeAsync().AsTask().GetAwaiter().GetResult();

    /// <summary>The shape of a consumer offset commit.</summary>
    [Benchmark(Baseline = true)]
    public ValueTask VoidSuccessCall() =>
        RetryHelper.WithRetryAsync(
            VoidSuccess,
            _metadata,
            CancellationToken.None,
            deadline: Deadline);

    /// <summary>The shape of a committed-offset fetch or an admin read.</summary>
    [Benchmark]
    public ValueTask<int> ResultSuccessCall() =>
        RetryHelper.WithRetryAsync(
            ResultSuccess,
            _metadata,
            CancellationToken.None,
            deadline: Deadline);

    [Benchmark]
    public ValueTask<int> OneTransportRetry()
    {
        _failNext = true;
        return RetryHelper.WithRetryAsync(
            _failOnceThenSucceed,
            _metadata,
            CancellationToken.None,
            retryBackoffMs: 0,
            retryBackoffMaxMs: 0,
            shouldRefreshMetadata: NoMetadataRefresh,
            deadline: Deadline);
    }

    private sealed class NoConnectionPool : IConnectionPool
    {
        public ValueTask<IKafkaConnection> GetConnectionAsync(int brokerId, CancellationToken token = default) =>
            throw new NotSupportedException();
        public ValueTask<IKafkaConnection> GetConnectionAsync(string host, int port, CancellationToken token = default) =>
            throw new NotSupportedException();
        public ValueTask<IKafkaConnection> GetConnectionByIndexAsync(int brokerId, int index, CancellationToken token = default) =>
            throw new NotSupportedException();
        public void RegisterBroker(int id, string host, int port) { }
        public ValueTask<int> ScaleConnectionGroupAsync(int id, int count, CancellationToken token = default) => ValueTask.FromResult(1);
        public ValueTask<IKafkaConnection?> ShrinkConnectionGroupAsync(int id, int count, CancellationToken token = default) =>
            ValueTask.FromResult<IKafkaConnection?>(null);
        public ValueTask RemoveConnectionAsync(int id) => ValueTask.CompletedTask;
        public ValueTask CloseAllAsync() => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
