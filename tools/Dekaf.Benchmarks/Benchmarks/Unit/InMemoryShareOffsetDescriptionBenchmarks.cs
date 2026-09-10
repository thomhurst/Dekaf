using BenchmarkDotNet.Attributes;
using Dekaf.Admin;
using Dekaf.Testing;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Completed legacy inventory queries, including result snapshots and materialization.</summary>
[MemoryDiagnoser]
public class InMemoryShareOffsetDescriptionBenchmarks
{
    [Params(1, 32)] public int Groups { get; set; }
    private InMemoryAdminClient _admin = null!;
    private string[] _groups = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        _admin = new(cluster);
        _groups = new string[Groups];
        for (var index = 0; index < Groups; index++)
        {
            var group = _groups[index] = $"group-{index}";
            await _admin.AlterShareGroupOffsetsAsync(group,
                [new() { TopicPartition = new("orders", 0), StartOffset = 0 }]).ConfigureAwait(false);
            var result = await _admin.DescribeShareGroupOffsetsAsync(group).ConfigureAwait(false);
            if (result.Count != 1 || result[0].TopicPartition != new TopicPartition("orders", 0)
                || result[0].StartOffset != 0 || result[0].LeaderEpoch != 0 || result[0].Lag != 0)
                throw new InvalidOperationException("The inventory fixture changed its completed result.");
        }
    }

    [Benchmark]
    public async ValueTask<int> Describe()
    {
        var completed = 0;
        foreach (var group in _groups)
            completed += (await _admin.DescribeShareGroupOffsetsAsync(group).ConfigureAwait(false)).Count;
        return completed;
    }

    [GlobalCleanup]
    public async Task Cleanup() => await _admin.DisposeAsync().ConfigureAwait(false);
}
