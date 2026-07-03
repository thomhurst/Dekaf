using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

public sealed class DelegationTokenMessageEncodingTests
{
    [Test]
    public async Task CreateDelegationTokenRequest_V3_Flexible_EncodesOwnerRenewersAndLifetime()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new CreateDelegationTokenRequest
        {
            Owner = new DelegationTokenPrincipalData
            {
                PrincipalType = "User",
                PrincipalName = "owner"
            },
            Renewers =
            [
                new DelegationTokenPrincipalData
                {
                    PrincipalType = "User",
                    PrincipalName = "renewer"
                }
            ],
            MaxLifetimeMs = 60000
        };

        request.Write(ref writer, version: 3);

        string ownerType;
        string? ownerName;
        int renewerCount;
        string? renewerType;
        string? renewerName;
        long maxLifetimeMs;
        bool end;
        {
            var reader = new KafkaProtocolReader(buffer.WrittenMemory);
            ownerType = reader.ReadCompactString()!;
            ownerName = reader.ReadCompactString();
            renewerCount = reader.ReadUnsignedVarInt();
            renewerType = reader.ReadCompactString();
            renewerName = reader.ReadCompactString();
            reader.SkipTaggedFields();
            maxLifetimeMs = reader.ReadInt64();
            reader.SkipTaggedFields();
            end = reader.End;
        }

        await Assert.That(ownerType).IsEqualTo("User");
        await Assert.That(ownerName).IsEqualTo("owner");
        await Assert.That(renewerCount).IsEqualTo(2);
        await Assert.That(renewerType).IsEqualTo("User");
        await Assert.That(renewerName).IsEqualTo("renewer");
        await Assert.That(maxLifetimeMs).IsEqualTo(60000);
        await Assert.That(end).IsTrue();
    }

    [Test]
    public async Task RenewDelegationTokenRequest_V1_Legacy_EncodesBytesAndPeriod()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new RenewDelegationTokenRequest
        {
            Hmac = [1, 2, 3],
            RenewPeriodMs = 120000
        };

        request.Write(ref writer, version: 1);

        byte[]? hmac;
        long renewPeriodMs;
        bool end;
        {
            var reader = new KafkaProtocolReader(buffer.WrittenMemory);
            hmac = reader.ReadBytes();
            renewPeriodMs = reader.ReadInt64();
            end = reader.End;
        }

        await Assert.That(hmac).IsEquivalentTo(new byte[] { 1, 2, 3 });
        await Assert.That(renewPeriodMs).IsEqualTo(120000);
        await Assert.That(end).IsTrue();
    }

    [Test]
    public async Task DescribeDelegationTokenResponse_V3_Flexible_CanBeParsed()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteCompactArray(
            [0],
            static (ref KafkaProtocolWriter w, int _, short version) =>
            {
                w.WriteCompactString("User");
                w.WriteCompactString("owner");
                w.WriteCompactString("User");
                w.WriteCompactString("requester");
                w.WriteInt64(1000);
                w.WriteInt64(2000);
                w.WriteInt64(3000);
                w.WriteCompactString("token-id");
                w.WriteCompactBytes([7, 8, 9]);
                w.WriteCompactArray(
                    [0],
                    static (ref KafkaProtocolWriter rw, int _, short _) =>
                    {
                        rw.WriteCompactString("User");
                        rw.WriteCompactString("renewer");
                        rw.WriteEmptyTaggedFields();
                    },
                    version);
                w.WriteEmptyTaggedFields();
            },
            (short)3);
        writer.WriteInt32(4);
        writer.WriteEmptyTaggedFields();

        DescribeDelegationTokenResponse response;
        {
            var reader = new KafkaProtocolReader(buffer.WrittenMemory);
            response = (DescribeDelegationTokenResponse)DescribeDelegationTokenResponse.Read(ref reader, version: 3);
        }

        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.ThrottleTimeMs).IsEqualTo(4);
        await Assert.That(response.Tokens.Count).IsEqualTo(1);
        await Assert.That(response.Tokens[0].PrincipalName).IsEqualTo("owner");
        await Assert.That(response.Tokens[0].TokenRequesterPrincipalName).IsEqualTo("requester");
        await Assert.That(response.Tokens[0].Hmac).IsEquivalentTo(new byte[] { 7, 8, 9 });
        await Assert.That(response.Tokens[0].Renewers[0].PrincipalName).IsEqualTo("renewer");
    }
}
