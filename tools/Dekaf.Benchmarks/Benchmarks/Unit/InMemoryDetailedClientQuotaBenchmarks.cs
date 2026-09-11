using BenchmarkDotNet.Attributes;
using Dekaf.Admin;
using Dekaf.Testing;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Repeated updates to a fixed set of in-memory quota entities. The concurrent case
/// measures two administrative calls, including scheduling and start synchronization;
/// it is not an isolated Monitor cost. ThreadingDiagnoser reports observed contention.
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class InMemoryDetailedClientQuotaBenchmarks
{
    [Params(1, 32)]
    public int EntityCount { get; set; }

    private InMemoryAdminClient _admin = null!;
    private ClientQuotaAlteration[] _alterations = null!;
    private Barrier _start = null!;
    private Func<Task<IReadOnlyDictionary<ClientQuotaEntity, AdminMutationResult>>> _concurrentCall = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        _admin = new InMemoryAdminClient(new InMemoryKafkaCluster());
        _alterations = Enumerable.Range(0, EntityCount).Select(index => ClientQuotaAlteration.Set(
            ClientQuotaEntity.ForUser($"user-{index}"), "consumer_byte_rate", 4096)).ToArray();
        _start = new Barrier(2);
        _concurrentCall = ConcurrentCallAsync;
        var result = await Alter();
        if (result.Count != EntityCount || result.Values.Any(outcome => !outcome.IsSuccess))
            throw new InvalidOperationException("In-memory quota fixture did not apply every entity.");
        foreach (var concurrentResult in await ContendedAlter())
            if (concurrentResult.Count != EntityCount || concurrentResult.Values.Any(outcome => !outcome.IsSuccess))
                throw new InvalidOperationException("Concurrent quota fixture did not apply every entity.");
        var stored = await _admin.DescribeClientQuotasAsync(ClientQuotaFilter.All());
        if (stored.Count != EntityCount || stored.Values.Any(quota => quota["consumer_byte_rate"] != 4096))
            throw new InvalidOperationException("In-memory quota fixture stored incorrect values.");
    }

    [Benchmark]
    public ValueTask<IReadOnlyDictionary<ClientQuotaEntity, AdminMutationResult>> Alter() =>
        _admin.AlterClientQuotasDetailedAsync(_alterations);

    [Benchmark]
    public Task<IReadOnlyDictionary<ClientQuotaEntity, AdminMutationResult>[]> ContendedAlter() =>
        Task.WhenAll(Task.Run(_concurrentCall), Task.Run(_concurrentCall));

    private async Task<IReadOnlyDictionary<ClientQuotaEntity, AdminMutationResult>> ConcurrentCallAsync()
    {
        if (!_start.SignalAndWait(TimeSpan.FromSeconds(10)))
            throw new TimeoutException("The second quota mutation worker did not reach the start barrier.");
        return await _admin.AlterClientQuotasDetailedAsync(_alterations);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        _start.Dispose();
        await _admin.DisposeAsync();
    }
}
