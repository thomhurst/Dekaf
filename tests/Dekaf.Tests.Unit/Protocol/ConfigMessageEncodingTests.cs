using System.Buffers;
using Dekaf.Admin;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

/// <summary>
/// Tests for config admin API protocol message encoding/decoding.
/// </summary>
public class ConfigMessageEncodingTests
{
    [Test]
    public async Task DescribeConfigsRequest_V4_Flexible_EncodedCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new DescribeConfigsRequest
        {
            Resources =
            [
                new DescribeConfigsResource
                {
                    ResourceType = (sbyte)ConfigResourceType.Topic,
                    ResourceName = "test",
                    ConfigurationKeys = null
                }
            ],
            IncludeSynonyms = true,
            IncludeDocumentation = true
        };
        request.Write(ref writer, version: 4);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        // Read all values first
        var arrayLength = reader.ReadUnsignedVarInt() - 1;
        var resourceType = reader.ReadInt8();
        var resourceName = reader.ReadCompactString();
        var keysLength = reader.ReadUnsignedVarInt();
        reader.SkipTaggedFields();
        var includeSynonyms = reader.ReadBoolean();
        var includeDocumentation = reader.ReadBoolean();

        // Now do all asserts
        await Assert.That(arrayLength).IsEqualTo(1);
        await Assert.That(resourceType).IsEqualTo((sbyte)2); // Topic
        await Assert.That(resourceName).IsEqualTo("test");
        await Assert.That(keysLength).IsEqualTo(0); // null
        await Assert.That(includeSynonyms).IsTrue();
        await Assert.That(includeDocumentation).IsTrue();
    }

    [Test]
    public async Task AlterConfigsRequest_V2_Flexible_EncodedCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new AlterConfigsRequest
        {
            Resources =
            [
                new AlterConfigsResource
                {
                    ResourceType = (sbyte)ConfigResourceType.Topic,
                    ResourceName = "test",
                    Configs =
                    [
                        new AlterableConfig { Name = "cleanup.policy", Value = "compact" }
                    ]
                }
            ],
            ValidateOnly = true
        };
        request.Write(ref writer, version: 2);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        // Read all values first
        var arrayLength = reader.ReadUnsignedVarInt() - 1;
        var resourceType = reader.ReadInt8();
        var resourceName = reader.ReadCompactString();
        var configsLength = reader.ReadUnsignedVarInt() - 1;
        var configName = reader.ReadCompactString();
        var configValue = reader.ReadCompactString();

        // Now do all asserts
        await Assert.That(arrayLength).IsEqualTo(1);
        await Assert.That(resourceType).IsEqualTo((sbyte)2); // Topic
        await Assert.That(resourceName).IsEqualTo("test");
        await Assert.That(configsLength).IsEqualTo(1);
        await Assert.That(configName).IsEqualTo("cleanup.policy");
        await Assert.That(configValue).IsEqualTo("compact");
    }

    [Test]
    public async Task IncrementalAlterConfigsRequest_V1_Flexible_EncodedCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new IncrementalAlterConfigsRequest
        {
            Resources =
            [
                new IncrementalAlterConfigsResource
                {
                    ResourceType = (sbyte)ConfigResourceType.Topic,
                    ResourceName = "test",
                    Configs =
                    [
                        new IncrementalAlterableConfig
                        {
                            Name = "cleanup.policy",
                            ConfigOperation = (sbyte)ConfigAlterOperation.Set,
                            Value = "compact"
                        }
                    ]
                }
            ],
            ValidateOnly = true
        };
        request.Write(ref writer, version: 1);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        // Read all values first
        var arrayLength = reader.ReadUnsignedVarInt() - 1;
        var resourceType = reader.ReadInt8();
        var resourceName = reader.ReadCompactString();
        var configsLength = reader.ReadUnsignedVarInt() - 1;
        var configName = reader.ReadCompactString();
        var configOperation = reader.ReadInt8();
        var configValue = reader.ReadCompactString();

        // Now do all asserts
        await Assert.That(arrayLength).IsEqualTo(1);
        await Assert.That(resourceType).IsEqualTo((sbyte)2); // Topic
        await Assert.That(resourceName).IsEqualTo("test");
        await Assert.That(configsLength).IsEqualTo(1);
        await Assert.That(configName).IsEqualTo("cleanup.policy");
        await Assert.That(configOperation).IsEqualTo((sbyte)0); // Set
        await Assert.That(configValue).IsEqualTo("compact");
    }

    [Test]
    public async Task IncrementalAlterConfigsResponse_V1_Flexible_CanBeParsed()
    {
        var data = new List<byte>();
        // ThrottleTimeMs (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x32 }); // 50ms
        // Responses COMPACT_ARRAY (length+1 = 2)
        data.Add(0x02);
        // ErrorCode (INT16)
        data.AddRange(new byte[] { 0x00, 0x00 }); // None
        // ErrorMessage (COMPACT_NULLABLE_STRING, null = 0)
        data.Add(0x00);
        // ResourceType (INT8)
        data.Add(0x02); // Topic
        // ResourceName (COMPACT_STRING)
        data.Add(0x05); // length+1 = 5
        data.AddRange("test"u8.ToArray());
        // Tagged fields
        data.Add(0x00);
        // Response tagged fields
        data.Add(0x00);

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (IncrementalAlterConfigsResponse)IncrementalAlterConfigsResponse.Read(ref reader, version: 1);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(50);
        await Assert.That(response.Responses.Count).IsEqualTo(1);
        await Assert.That(response.Responses[0].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.Responses[0].ResourceName).IsEqualTo("test");
    }

}
