using System.Buffers;
using BenchmarkDotNet.Attributes;
using Dekaf.Admin;
using Dekaf.Protocol;
using Dekaf.Testing;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Repeated descriptions of stable snapshots, including opaque bytes and missing groups.</summary>
[MemoryDiagnoser]
public class InMemoryClassicGroupDescriptionBenchmarks
{
    [Params(1, 16)]
    public int Groups { get; set; }

    [Params(false, true)]
    public bool MixedOutcomes { get; set; }

    private InMemoryAdminClient _admin = null!;
    private string[] _groups = null!;
    private readonly DescribeClassicGroupsOptions _options = new() { IncludeAuthorizedOperations = true };

    [GlobalSetup]
    public async Task Setup()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt16(0);
        writer.WriteInt32(1);
        writer.WriteString("orders");
        writer.WriteInt32(1);
        writer.WriteInt32(0);
        writer.WriteBytes([]);
        var assignment = buffer.WrittenMemory.ToArray();
        var cluster = new InMemoryKafkaCluster();
        _admin = new InMemoryAdminClient(cluster);
        _groups = new string[Groups];
        for (var i = 0; i < Groups; i++)
        {
            var id = $"group-{i}";
            _groups[i] = id;
            if (MixedOutcomes && i == Groups - 1)
                continue;
            cluster.SetClassicGroupDescription(new()
            {
                GroupId = id, State = "Stable", CoordinatorId = 1,
                ProtocolType = MixedOutcomes ? "connect" : "consumer", ProtocolData = "range",
                AuthorizedOperations = 123,
                Members = [new()
                {
                    MemberId = id, ClientId = "client", ClientHost = "host",
                    Metadata = assignment, AssignmentData = assignment
                }]
            });
        }

        var results = await Describe();
        if (results.Count != Groups)
            throw new InvalidOperationException("A result is required for every group.");
        for (var i = 0; i < Groups; i++)
        {
            var result = results[_groups[i]];
            var missing = MixedOutcomes && i == Groups - 1;
            if (result.ErrorCode != (missing ? ErrorCode.GroupIdNotFound : ErrorCode.None) ||
                (!missing && (result.Description?.Members.Count != 1 ||
                    result.Description.AuthorizedOperations != 123 ||
                    (result.Description.Members[0].Assignment is not null) == MixedOutcomes)))
                throw new InvalidOperationException("Invalid snapshot mapping or per-group outcome.");
        }
    }

    [Benchmark]
    public ValueTask<IReadOnlyDictionary<string, ClassicGroupDescriptionResult>> Describe() =>
        _admin.DescribeClassicGroupsAsync(_groups, _options);

    [GlobalCleanup]
    public async Task Cleanup() => await _admin.DisposeAsync();
}
