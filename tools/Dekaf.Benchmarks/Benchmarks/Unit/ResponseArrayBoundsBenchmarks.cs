using System.Buffers;
using BenchmarkDotNet.Attributes;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
public class ResponseArrayBoundsBenchmarks
{
    private ReadOnlyMemory<byte> _payload;
    private ReadOnlyMemory<byte> _consumerGroupDescribePayload;
    private ReadOnlyMemory<byte> _describeGroupsPayload;

    [GlobalSetup]
    public void Setup()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteUnsignedVarInt(101);
        for (var i = 0; i < 100; i++)
            writer.WriteInt32(i);
        _payload = buffer.WrittenMemory;

        buffer = new ArrayBufferWriter<byte>();
        writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(0);
        writer.WriteUnsignedVarInt(101);
        for (var i = 0; i < 100; i++)
        {
            writer.WriteInt16((short)ErrorCode.None);
            writer.WriteCompactString(string.Empty);
            writer.WriteCompactString(string.Empty);
            writer.WriteCompactNullableString(null);
            writer.WriteCompactNullableString(null);
            writer.WriteUnsignedVarInt(1);
            writer.WriteInt32(0);
            writer.WriteEmptyTaggedFields();
        }
        writer.WriteEmptyTaggedFields();
        _describeGroupsPayload = buffer.WrittenMemory;

        buffer = new ArrayBufferWriter<byte>();
        writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(0);
        writer.WriteUnsignedVarInt(101);
        for (var i = 0; i < 100; i++)
        {
            writer.WriteInt16((short)ErrorCode.None);
            writer.WriteCompactNullableString(null);
            writer.WriteCompactString(string.Empty);
            writer.WriteCompactString(string.Empty);
            writer.WriteInt32(0);
            writer.WriteInt32(0);
            writer.WriteCompactString(string.Empty);
            writer.WriteUnsignedVarInt(1);
            writer.WriteInt32(0);
            writer.WriteEmptyTaggedFields();
        }
        writer.WriteEmptyTaggedFields();
        _consumerGroupDescribePayload = buffer.WrittenMemory;
    }

    [Benchmark(Baseline = true)]
    public int[] ReadCompactArray_Unbounded()
    {
        var reader = new KafkaProtocolReader(_payload);
        return reader.ReadCompactArray(static (ref KafkaProtocolReader r) => r.ReadInt32());
    }

    [Benchmark]
    public int[] ReadCompactArray_MinimumSizeBound()
    {
        var reader = new KafkaProtocolReader(_payload);
        return reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => r.ReadInt32(),
            minElementSize: 4,
            maxCount: 1_000_000);
    }

    [Benchmark]
    public int[]? ReadCompactNullableArray_Unbounded()
    {
        var reader = new KafkaProtocolReader(_payload);
        return reader.ReadCompactNullableArray(static (ref KafkaProtocolReader r) => r.ReadInt32());
    }

    [Benchmark]
    public int[]? ReadCompactNullableArray_MinimumSizeBound()
    {
        var reader = new KafkaProtocolReader(_payload);
        return reader.ReadCompactNullableArray(
            static (ref KafkaProtocolReader r) => r.ReadInt32(),
            minElementSize: 4,
            maxCount: 1_000_000);
    }

    [Benchmark]
    public int ReadDescribeGroupsResponse()
    {
        var reader = new KafkaProtocolReader(_describeGroupsPayload);
        var response = (DescribeGroupsResponse)DescribeGroupsResponse.Read(ref reader, version: 5);
        return response.Groups.Count;
    }

    [Benchmark]
    public int ReadConsumerGroupDescribeResponse()
    {
        var reader = new KafkaProtocolReader(_consumerGroupDescribePayload);
        var response = (ConsumerGroupDescribeResponse)ConsumerGroupDescribeResponse.Read(ref reader, version: 0);
        return response.Groups.Count;
    }
}
