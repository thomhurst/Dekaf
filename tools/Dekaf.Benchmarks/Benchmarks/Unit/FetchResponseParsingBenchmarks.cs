using BenchmarkDotNet.Attributes;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[ShortRunJob]
public class FetchResponseParsingBenchmarks
{
    private readonly byte[] _response = Convert.FromHexString(
        "00000000" + // ThrottleTimeMs
        "00000001" + // Topics
        "0005746F706963" + // Topic
        "00000001" + // Partitions
        "00000000" + // PartitionIndex
        "0000" + // ErrorCode
        "000000000000002A" + // HighWatermark
        "000000000000002A" + // LastStableOffset
        "0000000000000000" + // LogStartOffset
        "FFFFFFFF" + // AbortedTransactions
        "00000000"); // Records

    [GlobalSetup]
    public void WarmPoolsAndTopicCache() => ParseAndReturn();

    [Benchmark]
    public int ParseLegacyV6() => ParseAndReturn();

    [Benchmark]
    public int ParseLegacyV6FromSpan() => ParseAndReturnFromSpan();

    private int ParseAndReturn()
    {
        var reader = new KafkaProtocolReader(_response.AsMemory());
        var response = (FetchResponse)FetchResponse.Read(ref reader, version: 6);
        var topicCount = response.Responses.Count;
        response.ReturnToPool();
        return topicCount;
    }

    private int ParseAndReturnFromSpan()
    {
        var reader = new KafkaProtocolReader(_response.AsSpan());
        var response = (FetchResponse)FetchResponse.Read(ref reader, version: 6);
        var topicCount = response.Responses.Count;
        response.ReturnToPool();
        return topicCount;
    }
}
