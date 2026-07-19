using System.Buffers;
using BenchmarkDotNet.Attributes;
using Dekaf.Benchmarks.Infrastructure;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[ThroughputJob(warmupCount: 3, iterationCount: 5)]
public class ControlPlaneProtocolBenchmarks
{
    private const string DescribeGroupsV5Fixture =
        "AAAALwMAAAt3aXJlLWdyb3VwB1N0YWJsZQljb25zdW1lchNjb29wZXJhdGl2ZS1zdGlja3kDCW1lbWJlci1hC2luc3RhbmNlLWEQY2xpZW50LW1lbWJlci1hCy8xMjcuMC4wLjEFASNFZwWJq83vAAltZW1iZXItYgAQY2xpZW50LW1lbWJlci1iCy8xMjcuMC4wLjEFASNFZwWJq83vAAAAH/8AAEUNd2lyZS1ncm91cC1iBURlYWQJY29uc3VtZXITY29vcGVyYXRpdmUtc3RpY2t5AQAAH/8AAA==";

    private readonly ArrayBufferWriter<byte> _buffer = new(1024);
    private readonly DescribeGroupsRequest _describeGroupsRequest = new()
    {
        Groups = ["group-a", "group-b"],
        IncludeAuthorizedOperations = true
    };
    private readonly FindCoordinatorRequest _findCoordinatorRequest = new()
    {
        Key = "group-a",
        KeyType = CoordinatorType.Group
    };
    private readonly ListConfigResourcesRequest _listConfigResourcesRequest = new()
    {
        ResourceTypes = [2, 16, 32]
    };
    private byte[] _describeGroupsV5 = null!;

    [GlobalSetup]
    public void Setup() => _describeGroupsV5 = Convert.FromBase64String(DescribeGroupsV5Fixture);

    [Benchmark]
    public DescribeGroupsResponse ReadDescribeGroupsV5()
    {
        var reader = new KafkaProtocolReader(_describeGroupsV5);
        return (DescribeGroupsResponse)DescribeGroupsResponse.Read(ref reader, version: 5);
    }

    [Benchmark]
    public void WriteFindCoordinatorV6()
    {
        _buffer.Clear();
        var writer = new KafkaProtocolWriter(_buffer);
        _findCoordinatorRequest.Write(ref writer, version: 6);
    }

    [Benchmark]
    public void WriteDescribeGroupsV6()
    {
        _buffer.Clear();
        var writer = new KafkaProtocolWriter(_buffer);
        _describeGroupsRequest.Write(ref writer, version: 6);
    }

    [Benchmark]
    public void WriteListConfigResourcesV1()
    {
        _buffer.Clear();
        var writer = new KafkaProtocolWriter(_buffer);
        _listConfigResourcesRequest.Write(ref writer, version: 1);
    }
}
