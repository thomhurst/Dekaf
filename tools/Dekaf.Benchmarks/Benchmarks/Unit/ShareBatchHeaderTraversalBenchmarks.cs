using System.Buffers;
using BenchmarkDotNet.Attributes;
using Dekaf.Protocol;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Measures validated borrowed header traversal without string materialization.</summary>
[MemoryDiagnoser]
public class ShareBatchHeaderTraversalBenchmarks
{
    private ShareBatchHeaders _headers;

    [Params(0, 2, 16)]
    public int HeaderCount { get; set; }

    [Params(4, 256)]
    public int KeyLength { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        var key = new string('h', KeyLength);
        for (var index = 0; index < HeaderCount; index++)
        {
            var header = new Header(key, index % 2 == 0 ? new byte[] { 37 } : null);
            HeaderProtocol.Write(in header, ref writer);
        }
        _headers = new ShareBatchHeaders(buffer.WrittenMemory, HeaderCount);
        var expected = HeaderCount * KeyLength + (HeaderCount + 1) / 2 * 37;
        if (Traverse() != expected)
            throw new InvalidOperationException("Header traversal changed bytes, count or null values.");
    }

    [Benchmark]
    public int Traverse()
    {
        var checksum = 0;
        foreach (var header in _headers)
        {
            checksum += header.KeyUtf8.Length;
            if (!header.IsValueNull)
                checksum += header.Value.Span[0];
        }
        return checksum;
    }
}
