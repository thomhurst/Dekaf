using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

public sealed class ClientQuotaMessageTests
{
    [Test]
    public async Task DescribeClientQuotasRequest_HasCorrectApiMetadata()
    {
        await Assert.That(DescribeClientQuotasRequest.ApiKey).IsEqualTo(ApiKey.DescribeClientQuotas);
        await Assert.That(DescribeClientQuotasRequest.IsFlexibleVersion(0)).IsFalse();
        await Assert.That(DescribeClientQuotasRequest.IsFlexibleVersion(1)).IsTrue();
        await Assert.That(DescribeClientQuotasRequest.GetRequestHeaderVersion(0)).IsEqualTo((short)1);
        await Assert.That(DescribeClientQuotasRequest.GetRequestHeaderVersion(1)).IsEqualTo((short)2);
        await Assert.That(DescribeClientQuotasRequest.GetResponseHeaderVersion(0)).IsEqualTo((short)0);
        await Assert.That(DescribeClientQuotasRequest.GetResponseHeaderVersion(1)).IsEqualTo((short)1);
    }

    [Test]
    public async Task DescribeClientQuotasRequest_V0_Legacy_EncodesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        var request = new DescribeClientQuotasRequest
        {
            Components =
            [
                new DescribeClientQuotasRequestComponent
                {
                    EntityType = "user",
                    MatchType = 0,
                    Match = "alice"
                }
            ],
            Strict = true
        };

        request.Write(ref writer, version: 0);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var componentCount = reader.ReadInt32();
        var entityType = reader.ReadString();
        var matchType = reader.ReadInt8();
        var match = reader.ReadString();
        var strict = reader.ReadBoolean();

        await Assert.That(componentCount).IsEqualTo(1);
        await Assert.That(entityType).IsEqualTo("user");
        await Assert.That(matchType).IsEqualTo((sbyte)0);
        await Assert.That(match).IsEqualTo("alice");
        await Assert.That(strict).IsTrue();
    }

    [Test]
    public async Task DescribeClientQuotasRequest_V1_Flexible_EncodesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        var request = new DescribeClientQuotasRequest
        {
            Components =
            [
                new DescribeClientQuotasRequestComponent
                {
                    EntityType = "client-id",
                    MatchType = 2,
                    Match = null
                }
            ],
            Strict = false
        };

        request.Write(ref writer, version: 1);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var componentCount = reader.ReadUnsignedVarInt() - 1;
        var entityType = reader.ReadCompactString();
        var matchType = reader.ReadInt8();
        var match = reader.ReadCompactString();
        reader.SkipTaggedFields();
        var strict = reader.ReadBoolean();
        reader.SkipTaggedFields();

        await Assert.That(componentCount).IsEqualTo(1);
        await Assert.That(entityType).IsEqualTo("client-id");
        await Assert.That(matchType).IsEqualTo((sbyte)2);
        await Assert.That(match).IsNull();
        await Assert.That(strict).IsFalse();
    }

    [Test]
    public async Task DescribeClientQuotasResponse_V0_Legacy_CanBeParsed()
    {
        var data = BuildDescribeResponse(version: 0);
        var reader = new KafkaProtocolReader(data);

        var response = (DescribeClientQuotasResponse)DescribeClientQuotasResponse.Read(ref reader, version: 0);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(25);
        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.ErrorMessage).IsNull();
        await Assert.That(response.Entries).IsNotNull();
        await Assert.That(response.Entries!.Count).IsEqualTo(1);
        await Assert.That(response.Entries[0].Entity.Count).IsEqualTo(2);
        await Assert.That(response.Entries[0].Entity[0].EntityType).IsEqualTo("user");
        await Assert.That(response.Entries[0].Entity[0].EntityName).IsEqualTo("alice");
        await Assert.That(response.Entries[0].Entity[1].EntityName).IsNull();
        await Assert.That(response.Entries[0].Values[0].Key).IsEqualTo("consumer_byte_rate");
        await Assert.That(response.Entries[0].Values[0].Value).IsEqualTo(1024.5);
    }

    [Test]
    public async Task DescribeClientQuotasResponse_V1_Flexible_CanBeParsed()
    {
        var data = BuildDescribeResponse(version: 1);
        var reader = new KafkaProtocolReader(data);

        var response = (DescribeClientQuotasResponse)DescribeClientQuotasResponse.Read(ref reader, version: 1);

        await Assert.That(response.Entries).IsNotNull();
        await Assert.That(response.Entries!.Count).IsEqualTo(1);
        await Assert.That(response.Entries[0].Entity[1].EntityType).IsEqualTo("client-id");
        await Assert.That(response.Entries[0].Values[0].Value).IsEqualTo(1024.5);
    }

    [Test]
    public async Task DescribeClientQuotasResponse_NullEntries_CanBeParsed()
    {
        var data = BuildDescribeResponse(version: 0, entriesNull: true);
        var reader = new KafkaProtocolReader(data);

        var response = (DescribeClientQuotasResponse)DescribeClientQuotasResponse.Read(ref reader, version: 0);

        await Assert.That(response.Entries).IsNull();
    }

    [Test]
    public async Task AlterClientQuotasRequest_HasCorrectApiMetadata()
    {
        await Assert.That(AlterClientQuotasRequest.ApiKey).IsEqualTo(ApiKey.AlterClientQuotas);
        await Assert.That(AlterClientQuotasRequest.IsFlexibleVersion(0)).IsFalse();
        await Assert.That(AlterClientQuotasRequest.IsFlexibleVersion(1)).IsTrue();
        await Assert.That(AlterClientQuotasRequest.GetRequestHeaderVersion(0)).IsEqualTo((short)1);
        await Assert.That(AlterClientQuotasRequest.GetRequestHeaderVersion(1)).IsEqualTo((short)2);
        await Assert.That(AlterClientQuotasRequest.GetResponseHeaderVersion(0)).IsEqualTo((short)0);
        await Assert.That(AlterClientQuotasRequest.GetResponseHeaderVersion(1)).IsEqualTo((short)1);
    }

    [Test]
    public async Task AlterClientQuotasRequest_V0_Legacy_EncodesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        var request = CreateAlterRequest(validateOnly: true);

        request.Write(ref writer, version: 0);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var entryCount = reader.ReadInt32();
        var entityCount = reader.ReadInt32();
        var entityType = reader.ReadString();
        var entityName = reader.ReadString();
        var opCount = reader.ReadInt32();
        var key = reader.ReadString();
        var value = reader.ReadFloat64();
        var remove = reader.ReadBoolean();
        var validateOnly = reader.ReadBoolean();

        await Assert.That(entryCount).IsEqualTo(1);
        await Assert.That(entityCount).IsEqualTo(1);
        await Assert.That(entityType).IsEqualTo("user");
        await Assert.That(entityName).IsEqualTo("alice");
        await Assert.That(opCount).IsEqualTo(1);
        await Assert.That(key).IsEqualTo("consumer_byte_rate");
        await Assert.That(value).IsEqualTo(2048.25);
        await Assert.That(remove).IsFalse();
        await Assert.That(validateOnly).IsTrue();
    }

    [Test]
    public async Task AlterClientQuotasRequest_V1_Flexible_EncodesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        var request = CreateAlterRequest(validateOnly: false);

        request.Write(ref writer, version: 1);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var entryCount = reader.ReadUnsignedVarInt() - 1;
        var entityCount = reader.ReadUnsignedVarInt() - 1;
        var entityType = reader.ReadCompactString();
        var entityName = reader.ReadCompactString();
        reader.SkipTaggedFields();
        var opCount = reader.ReadUnsignedVarInt() - 1;
        var key = reader.ReadCompactString();
        var value = reader.ReadFloat64();
        var remove = reader.ReadBoolean();
        reader.SkipTaggedFields();
        reader.SkipTaggedFields();
        var validateOnly = reader.ReadBoolean();
        reader.SkipTaggedFields();

        await Assert.That(entryCount).IsEqualTo(1);
        await Assert.That(entityCount).IsEqualTo(1);
        await Assert.That(entityType).IsEqualTo("user");
        await Assert.That(entityName).IsEqualTo("alice");
        await Assert.That(opCount).IsEqualTo(1);
        await Assert.That(key).IsEqualTo("consumer_byte_rate");
        await Assert.That(value).IsEqualTo(2048.25);
        await Assert.That(remove).IsFalse();
        await Assert.That(validateOnly).IsFalse();
    }

    [Test]
    public async Task AlterClientQuotasResponse_V0_Legacy_CanBeParsed()
    {
        var data = BuildAlterResponse(version: 0);
        var reader = new KafkaProtocolReader(data);

        var response = (AlterClientQuotasResponse)AlterClientQuotasResponse.Read(ref reader, version: 0);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(12);
        await Assert.That(response.Entries.Count).IsEqualTo(1);
        await Assert.That(response.Entries[0].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.Entries[0].ErrorMessage).IsNull();
        await Assert.That(response.Entries[0].Entity[0].EntityType).IsEqualTo("user");
        await Assert.That(response.Entries[0].Entity[0].EntityName).IsEqualTo("alice");
    }

    [Test]
    public async Task AlterClientQuotasResponse_V1_Flexible_CanBeParsed()
    {
        var data = BuildAlterResponse(version: 1);
        var reader = new KafkaProtocolReader(data);

        var response = (AlterClientQuotasResponse)AlterClientQuotasResponse.Read(ref reader, version: 1);

        await Assert.That(response.Entries.Count).IsEqualTo(1);
        await Assert.That(response.Entries[0].Entity[0].EntityType).IsEqualTo("user");
    }

    private static AlterClientQuotasRequest CreateAlterRequest(bool validateOnly) => new()
    {
        Entries =
        [
            new AlterClientQuotasRequestEntry
            {
                Entity =
                [
                    new AlterClientQuotasEntityData
                    {
                        EntityType = "user",
                        EntityName = "alice"
                    }
                ],
                Ops =
                [
                    new AlterClientQuotasOpData
                    {
                        Key = "consumer_byte_rate",
                        Value = 2048.25,
                        Remove = false
                    }
                ]
            }
        ],
        ValidateOnly = validateOnly
    };

    private static byte[] BuildDescribeResponse(short version, bool entriesNull = false)
    {
        var flexible = version >= 1;
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(25);
        writer.WriteInt16((short)ErrorCode.None);
        WriteNullableString(ref writer, null, flexible);

        if (entriesNull)
        {
            if (flexible)
            {
                writer.WriteUnsignedVarInt(0);
            }
            else
            {
                writer.WriteInt32(-1);
            }
        }
        else
        {
            WriteArrayLength(ref writer, 1, flexible);
            WriteDescribeEntry(ref writer, flexible);
        }

        if (flexible)
        {
            writer.WriteEmptyTaggedFields();
        }

        return buffer.WrittenSpan.ToArray();
    }

    private static byte[] BuildAlterResponse(short version)
    {
        var flexible = version >= 1;
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(12);
        WriteArrayLength(ref writer, 1, flexible);
        writer.WriteInt16((short)ErrorCode.None);
        WriteNullableString(ref writer, null, flexible);
        WriteArrayLength(ref writer, 1, flexible);
        WriteNullableString(ref writer, "user", flexible);
        WriteNullableString(ref writer, "alice", flexible);

        if (flexible)
        {
            writer.WriteEmptyTaggedFields();
            writer.WriteEmptyTaggedFields();
            writer.WriteEmptyTaggedFields();
        }

        return buffer.WrittenSpan.ToArray();
    }

    private static void WriteDescribeEntry(ref KafkaProtocolWriter writer, bool flexible)
    {
        WriteArrayLength(ref writer, 2, flexible);
        WriteNullableString(ref writer, "user", flexible);
        WriteNullableString(ref writer, "alice", flexible);
        if (flexible)
        {
            writer.WriteEmptyTaggedFields();
        }

        WriteNullableString(ref writer, "client-id", flexible);
        WriteNullableString(ref writer, null, flexible);
        if (flexible)
        {
            writer.WriteEmptyTaggedFields();
        }

        WriteArrayLength(ref writer, 1, flexible);
        WriteNullableString(ref writer, "consumer_byte_rate", flexible);
        writer.WriteFloat64(1024.5);
        if (flexible)
        {
            writer.WriteEmptyTaggedFields();
            writer.WriteEmptyTaggedFields();
        }
    }

    private static void WriteArrayLength(ref KafkaProtocolWriter writer, int length, bool flexible)
    {
        if (flexible)
        {
            writer.WriteUnsignedVarInt(length + 1);
        }
        else
        {
            writer.WriteInt32(length);
        }
    }

    private static void WriteNullableString(ref KafkaProtocolWriter writer, string? value, bool flexible)
    {
        if (flexible)
        {
            writer.WriteCompactNullableString(value);
        }
        else
        {
            writer.WriteString(value);
        }
    }
}
