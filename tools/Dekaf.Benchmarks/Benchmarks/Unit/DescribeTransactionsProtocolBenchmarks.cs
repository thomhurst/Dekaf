using System.Buffers;
using BenchmarkDotNet.Attributes;
using Dekaf.Benchmarks.Infrastructure;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[ThroughputJob(warmupCount: 3, iterationCount: 5)]
public class DescribeTransactionsProtocolBenchmarks
{
    private readonly ArrayBufferWriter<byte> _buffer = new(8 * 1024);
    private DescribeTransactionsRequest _request = null!;

    [Params((short)0, (short)1)]
    public short Version { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var transactionalIds = new string[128];
        for (var index = 0; index < transactionalIds.Length; index++)
            transactionalIds[index] = $"transaction-{index:D3}";

        _request = new DescribeTransactionsRequest
        {
            TransactionalIds = transactionalIds
        };
    }

    [Benchmark]
    public void WriteRequest()
    {
        _buffer.Clear();
        var writer = new KafkaProtocolWriter(_buffer);
        _request.Write(ref writer, Version);
    }
}
