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
    #region DescribeConfigs Request Tests

    [Test]
    public async Task DescribeConfigsRequest_V0_SingleTopic_EncodedCorrectly()
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
                    ResourceName = "test-topic",
                    ConfigurationKeys = null
                }
            ]
        };
        request.Write(ref writer, version: 0);

        var expected = new List<byte>();
        // Resources array (INT32 length)
        expected.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 });
        // ResourceType (INT8) = 2 (Topic)
        expected.Add(0x02);
        // ResourceName (STRING with INT16 length prefix)
        expected.AddRange(new byte[] { 0x00, 0x0A }); // length = 10
        expected.AddRange("test-topic"u8.ToArray());
        // ConfigurationKeys (nullable array, -1 for null)
        expected.AddRange(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF });

        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(expected.ToArray());
    }

    [Test]
    public async Task DescribeConfigsRequest_V1_WithIncludeSynonyms_EncodedCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new DescribeConfigsRequest
        {
            Resources =
            [
                new DescribeConfigsResource
                {
                    ResourceType = (sbyte)ConfigResourceType.Broker,
                    ResourceName = "0",
                    ConfigurationKeys = ["log.retention.ms"]
                }
            ],
            IncludeSynonyms = true
        };
        request.Write(ref writer, version: 1);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        // Read all values first
        var arrayLength = reader.ReadInt32();
        var resourceType = reader.ReadInt8();
        var resourceName = reader.ReadString();
        var keysLength = reader.ReadInt32();
        var keyName = reader.ReadString();
        var includeSynonyms = reader.ReadBoolean();

        // Now do all asserts
        await Assert.That(arrayLength).IsEqualTo(1);
        await Assert.That(resourceType).IsEqualTo((sbyte)4); // Broker
        await Assert.That(resourceName).IsEqualTo("0");
        await Assert.That(keysLength).IsEqualTo(1);
        await Assert.That(keyName).IsEqualTo("log.retention.ms");
        await Assert.That(includeSynonyms).IsTrue();
    }

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

    #endregion

    #region DescribeConfigs Response Tests

    [Test]
    public async Task DescribeConfigsResponse_V0_CanBeParsed()
    {
        var data = new List<byte>();
        // ThrottleTimeMs (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        // Results array (INT32 length)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 });
        // ErrorCode (INT16)
        data.AddRange(new byte[] { 0x00, 0x00 }); // None
        // ErrorMessage (STRING, null)
        data.AddRange(new byte[] { 0xFF, 0xFF });
        // ResourceType (INT8)
        data.Add(0x02); // Topic
        // ResourceName (STRING)
        data.AddRange(new byte[] { 0x00, 0x04 }); // length = 4
        data.AddRange("test"u8.ToArray());
        // Configs array (INT32 length)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 });
        // Config entry
        // Name (STRING)
        data.AddRange(new byte[] { 0x00, 0x0C }); // length = 12
        data.AddRange("retention.ms"u8.ToArray());
        // Value (STRING)
        data.AddRange(new byte[] { 0x00, 0x07 }); // length = 7
        data.AddRange("6048000"u8.ToArray());
        // ReadOnly (BOOLEAN)
        data.Add(0x00); // false
        // IsDefault (BOOLEAN) - v0 only
        data.Add(0x01); // true
        // IsSensitive (BOOLEAN)
        data.Add(0x00); // false

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (DescribeConfigsResponse)DescribeConfigsResponse.Read(ref reader, version: 0);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(0);
        await Assert.That(response.Results.Count).IsEqualTo(1);
        await Assert.That(response.Results[0].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.Results[0].ResourceType).IsEqualTo((sbyte)2);
        await Assert.That(response.Results[0].ResourceName).IsEqualTo("test");
        await Assert.That(response.Results[0].Configs.Count).IsEqualTo(1);
    }

    [Test]
    public async Task DescribeConfigsResponse_V1_WithSynonyms_CanBeParsed()
    {
        var data = new List<byte>();
        // ThrottleTimeMs (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x64 }); // 100ms
        // Results array (INT32 length)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 });
        // ErrorCode (INT16)
        data.AddRange(new byte[] { 0x00, 0x00 }); // None
        // ErrorMessage (STRING, null)
        data.AddRange(new byte[] { 0xFF, 0xFF });
        // ResourceType (INT8)
        data.Add(0x02); // Topic
        // ResourceName (STRING)
        data.AddRange(new byte[] { 0x00, 0x04 }); // length = 4
        data.AddRange("test"u8.ToArray());
        // Configs array (INT32 length)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 });
        // Config entry
        // Name (STRING)
        data.AddRange(new byte[] { 0x00, 0x0C }); // length = 12
        data.AddRange("retention.ms"u8.ToArray());
        // Value (STRING)
        data.AddRange(new byte[] { 0x00, 0x07 }); // length = 7
        data.AddRange("6048000"u8.ToArray());
        // ReadOnly (BOOLEAN)
        data.Add(0x00); // false
        // ConfigSource (INT8) - v1+
        data.Add(0x01); // DynamicTopicConfig
        // IsSensitive (BOOLEAN)
        data.Add(0x00); // false
        // Synonyms array (INT32 length)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 });
        // Synonym entry
        // Name (STRING)
        data.AddRange(new byte[] { 0x00, 0x0C }); // length = 12
        data.AddRange("retention.ms"u8.ToArray());
        // Value (STRING)
        data.AddRange(new byte[] { 0x00, 0x07 }); // length = 7
        data.AddRange("6048000"u8.ToArray());
        // Source (INT8)
        data.Add(0x01); // DynamicTopicConfig

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (DescribeConfigsResponse)DescribeConfigsResponse.Read(ref reader, version: 1);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(100);
        await Assert.That(response.Results[0].Configs[0].ConfigSource).IsEqualTo((sbyte)1);
        await Assert.That(response.Results[0].Configs[0].Synonyms).IsNotNull();
        await Assert.That(response.Results[0].Configs[0].Synonyms!.Count).IsEqualTo(1);
        await Assert.That(response.Results[0].Configs[0].Synonyms![0].Name).IsEqualTo("retention.ms");
    }

    #endregion

    #region AlterConfigs Request Tests

    [Test]
    public async Task AlterConfigsRequest_V0_SingleTopicConfig_EncodedCorrectly()
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
                    ResourceName = "test-topic",
                    Configs =
                    [
                        new AlterableConfig { Name = "retention.ms", Value = "3600000" }
                    ]
                }
            ],
            ValidateOnly = false
        };
        request.Write(ref writer, version: 0);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        // Read all values first
        var arrayLength = reader.ReadInt32();
        var resourceType = reader.ReadInt8();
        var resourceName = reader.ReadString();
        var configsLength = reader.ReadInt32();
        var configName = reader.ReadString();
        var configValue = reader.ReadString();
        var validateOnly = reader.ReadBoolean();

        // Now do all asserts
        await Assert.That(arrayLength).IsEqualTo(1);
        await Assert.That(resourceType).IsEqualTo((sbyte)2); // Topic
        await Assert.That(resourceName).IsEqualTo("test-topic");
        await Assert.That(configsLength).IsEqualTo(1);
        await Assert.That(configName).IsEqualTo("retention.ms");
        await Assert.That(configValue).IsEqualTo("3600000");
        await Assert.That(validateOnly).IsFalse();
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

    #endregion

    #region AlterConfigs Response Tests

    [Test]
    public async Task AlterConfigsResponse_V0_CanBeParsed()
    {
        var data = new List<byte>();
        // ThrottleTimeMs (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        // Responses array (INT32 length)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 });
        // ErrorCode (INT16)
        data.AddRange(new byte[] { 0x00, 0x00 }); // None
        // ErrorMessage (STRING, null)
        data.AddRange(new byte[] { 0xFF, 0xFF });
        // ResourceType (INT8)
        data.Add(0x02); // Topic
        // ResourceName (STRING)
        data.AddRange(new byte[] { 0x00, 0x04 }); // length = 4
        data.AddRange("test"u8.ToArray());

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (AlterConfigsResponse)AlterConfigsResponse.Read(ref reader, version: 0);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(0);
        await Assert.That(response.Responses.Count).IsEqualTo(1);
        await Assert.That(response.Responses[0].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.Responses[0].ResourceType).IsEqualTo((sbyte)2);
        await Assert.That(response.Responses[0].ResourceName).IsEqualTo("test");
    }

    [Test]
    public async Task AlterConfigsResponse_V0_WithError_CanBeParsed()
    {
        var data = new List<byte>();
        // ThrottleTimeMs (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        // Responses array (INT32 length)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 });
        // ErrorCode (INT16) - InvalidConfig = 40
        data.AddRange(new byte[] { 0x00, 0x28 });
        // ErrorMessage (STRING)
        data.AddRange(new byte[] { 0x00, 0x0E }); // length = 14
        data.AddRange("Invalid config"u8.ToArray());
        // ResourceType (INT8)
        data.Add(0x02); // Topic
        // ResourceName (STRING)
        data.AddRange(new byte[] { 0x00, 0x04 }); // length = 4
        data.AddRange("test"u8.ToArray());

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (AlterConfigsResponse)AlterConfigsResponse.Read(ref reader, version: 0);

        await Assert.That(response.Responses[0].ErrorCode).IsEqualTo(ErrorCode.InvalidConfig);
        await Assert.That(response.Responses[0].ErrorMessage).IsEqualTo("Invalid config");
    }

    #endregion

    #region IncrementalAlterConfigs Request Tests

    [Test]
    public async Task IncrementalAlterConfigsRequest_V0_SetOperation_EncodedCorrectly()
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
                    ResourceName = "test-topic",
                    Configs =
                    [
                        new IncrementalAlterableConfig
                        {
                            Name = "retention.ms",
                            ConfigOperation = (sbyte)ConfigAlterOperation.Set,
                            Value = "3600000"
                        }
                    ]
                }
            ],
            ValidateOnly = false
        };
        request.Write(ref writer, version: 0);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        // Read all values first
        var arrayLength = reader.ReadInt32();
        var resourceType = reader.ReadInt8();
        var resourceName = reader.ReadString();
        var configsLength = reader.ReadInt32();
        var configName = reader.ReadString();
        var configOperation = reader.ReadInt8();
        var configValue = reader.ReadString();
        var validateOnly = reader.ReadBoolean();

        // Now do all asserts
        await Assert.That(arrayLength).IsEqualTo(1);
        await Assert.That(resourceType).IsEqualTo((sbyte)2); // Topic
        await Assert.That(resourceName).IsEqualTo("test-topic");
        await Assert.That(configsLength).IsEqualTo(1);
        await Assert.That(configName).IsEqualTo("retention.ms");
        await Assert.That(configOperation).IsEqualTo((sbyte)0); // Set
        await Assert.That(configValue).IsEqualTo("3600000");
        await Assert.That(validateOnly).IsFalse();
    }

    [Test]
    public async Task IncrementalAlterConfigsRequest_V0_DeleteOperation_EncodedCorrectly()
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
                            Name = "retention.ms",
                            ConfigOperation = (sbyte)ConfigAlterOperation.Delete,
                            Value = null
                        }
                    ]
                }
            ],
            ValidateOnly = false
        };
        request.Write(ref writer, version: 0);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        // Skip to config operation and read values
        reader.ReadInt32(); // array length
        reader.ReadInt8(); // resource type
        reader.ReadString(); // resource name
        reader.ReadInt32(); // configs array length
        reader.ReadString(); // config name
        var configOperation = reader.ReadInt8();
        var configValue = reader.ReadString();

        // Now do all asserts
        await Assert.That(configOperation).IsEqualTo((sbyte)1); // Delete
        await Assert.That(configValue).IsNull();
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

    #endregion

    #region IncrementalAlterConfigs Response Tests

    [Test]
    public async Task IncrementalAlterConfigsResponse_V0_CanBeParsed()
    {
        var data = new List<byte>();
        // ThrottleTimeMs (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        // Responses array (INT32 length)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 });
        // ErrorCode (INT16)
        data.AddRange(new byte[] { 0x00, 0x00 }); // None
        // ErrorMessage (STRING, null)
        data.AddRange(new byte[] { 0xFF, 0xFF });
        // ResourceType (INT8)
        data.Add(0x02); // Topic
        // ResourceName (STRING)
        data.AddRange(new byte[] { 0x00, 0x04 }); // length = 4
        data.AddRange("test"u8.ToArray());

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (IncrementalAlterConfigsResponse)IncrementalAlterConfigsResponse.Read(ref reader, version: 0);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(0);
        await Assert.That(response.Responses.Count).IsEqualTo(1);
        await Assert.That(response.Responses[0].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.Responses[0].ResourceType).IsEqualTo((sbyte)2);
        await Assert.That(response.Responses[0].ResourceName).IsEqualTo("test");
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

    #endregion

    #region Version Flexibility Tests

    [Test]
    [Arguments((short)0, false)]
    [Arguments((short)1, false)]
    [Arguments((short)2, false)]
    [Arguments((short)3, false)]
    [Arguments((short)4, true)]
    public async Task DescribeConfigsRequest_FlexibilityDetection(short version, bool expectedFlexible)
    {
        var isFlexible = DescribeConfigsRequest.IsFlexibleVersion(version);
        await Assert.That(isFlexible).IsEqualTo(expectedFlexible);
    }

    [Test]
    [Arguments((short)0, false)]
    [Arguments((short)1, false)]
    [Arguments((short)2, true)]
    public async Task AlterConfigsRequest_FlexibilityDetection(short version, bool expectedFlexible)
    {
        var isFlexible = AlterConfigsRequest.IsFlexibleVersion(version);
        await Assert.That(isFlexible).IsEqualTo(expectedFlexible);
    }

    [Test]
    [Arguments((short)0, false)]
    [Arguments((short)1, true)]
    public async Task IncrementalAlterConfigsRequest_FlexibilityDetection(short version, bool expectedFlexible)
    {
        var isFlexible = IncrementalAlterConfigsRequest.IsFlexibleVersion(version);
        await Assert.That(isFlexible).IsEqualTo(expectedFlexible);
    }

    #endregion

    #region Header Version Tests

    [Test]
    [Arguments((short)0, (short)1, (short)0)]
    [Arguments((short)3, (short)1, (short)0)]
    [Arguments((short)4, (short)2, (short)1)]
    public async Task DescribeConfigsRequest_HeaderVersions(short apiVersion, short expectedRequestHeader, short expectedResponseHeader)
    {
        var requestHeaderVersion = DescribeConfigsRequest.GetRequestHeaderVersion(apiVersion);
        var responseHeaderVersion = DescribeConfigsRequest.GetResponseHeaderVersion(apiVersion);

        await Assert.That(requestHeaderVersion).IsEqualTo(expectedRequestHeader);
        await Assert.That(responseHeaderVersion).IsEqualTo(expectedResponseHeader);
    }

    [Test]
    [Arguments((short)0, (short)1, (short)0)]
    [Arguments((short)1, (short)1, (short)0)]
    [Arguments((short)2, (short)2, (short)1)]
    public async Task AlterConfigsRequest_HeaderVersions(short apiVersion, short expectedRequestHeader, short expectedResponseHeader)
    {
        var requestHeaderVersion = AlterConfigsRequest.GetRequestHeaderVersion(apiVersion);
        var responseHeaderVersion = AlterConfigsRequest.GetResponseHeaderVersion(apiVersion);

        await Assert.That(requestHeaderVersion).IsEqualTo(expectedRequestHeader);
        await Assert.That(responseHeaderVersion).IsEqualTo(expectedResponseHeader);
    }

    [Test]
    [Arguments((short)0, (short)1, (short)0)]
    [Arguments((short)1, (short)2, (short)1)]
    public async Task IncrementalAlterConfigsRequest_HeaderVersions(short apiVersion, short expectedRequestHeader, short expectedResponseHeader)
    {
        var requestHeaderVersion = IncrementalAlterConfigsRequest.GetRequestHeaderVersion(apiVersion);
        var responseHeaderVersion = IncrementalAlterConfigsRequest.GetResponseHeaderVersion(apiVersion);

        await Assert.That(requestHeaderVersion).IsEqualTo(expectedRequestHeader);
        await Assert.That(responseHeaderVersion).IsEqualTo(expectedResponseHeader);
    }

    #endregion
}
