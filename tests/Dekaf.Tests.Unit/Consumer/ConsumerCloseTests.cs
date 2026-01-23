using System.Buffers;
using Dekaf.Consumer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Consumer;

/// <summary>
/// Tests for consumer CloseAsync functionality.
/// </summary>
public class ConsumerCloseTests
{
    #region LeaveGroup Request Encoding Tests

    [Test]
    public async Task LeaveGroupRequest_V0_EncodedCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new LeaveGroupRequest
        {
            GroupId = "test-group",
            MemberId = "member-1"
        };
        request.Write(ref writer, version: 0);

        var expected = new List<byte>();
        // GroupId (STRING with INT16 length prefix)
        expected.AddRange(new byte[] { 0x00, 0x0A }); // length = 10
        expected.AddRange("test-group"u8.ToArray());
        // MemberId (STRING with INT16 length prefix)
        expected.AddRange(new byte[] { 0x00, 0x08 }); // length = 8
        expected.AddRange("member-1"u8.ToArray());

        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(expected.ToArray());
    }

    [Test]
    public async Task LeaveGroupRequest_V3_WithMembersArray()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new LeaveGroupRequest
        {
            GroupId = "test-group",
            Members =
            [
                new LeaveGroupRequestMember
                {
                    MemberId = "member-1",
                    GroupInstanceId = null
                }
            ]
        };
        request.Write(ref writer, version: 3);

        // Verify it can be written without errors
        await Assert.That(buffer.WrittenCount).IsGreaterThan(0);

        // Parse it back to verify structure (synchronous parsing)
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var groupId = reader.ReadString();
        var arrayLen = reader.ReadInt32();

        await Assert.That(groupId).IsEqualTo("test-group");
        await Assert.That(arrayLen).IsEqualTo(1);
    }

    [Test]
    public async Task LeaveGroupRequest_V4_Flexible_EncodedCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new LeaveGroupRequest
        {
            GroupId = "mygroup",
            Members =
            [
                new LeaveGroupRequestMember
                {
                    MemberId = "m1",
                    GroupInstanceId = "instance-1"
                }
            ]
        };
        request.Write(ref writer, version: 4);

        // Verify it can be written without errors (flexible encoding)
        await Assert.That(buffer.WrittenCount).IsGreaterThan(0);

        // Parse it back - v4 uses compact strings (synchronous parsing)
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var groupId = reader.ReadCompactString();
        var arrayLen = reader.ReadUnsignedVarInt() - 1;

        await Assert.That(groupId).IsEqualTo("mygroup");
        await Assert.That((int)arrayLen).IsEqualTo(1);
    }

    [Test]
    public async Task LeaveGroupRequest_V5_WithReason()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new LeaveGroupRequest
        {
            GroupId = "group",
            Members =
            [
                new LeaveGroupRequestMember
                {
                    MemberId = "member",
                    GroupInstanceId = null,
                    Reason = "Shutting down"
                }
            ]
        };
        request.Write(ref writer, version: 5);

        // Verify it can be written without errors
        await Assert.That(buffer.WrittenCount).IsGreaterThan(0);
    }

    #endregion

    #region LeaveGroup Response Parsing Tests

    [Test]
    public async Task LeaveGroupResponse_V0_CanBeParsed()
    {
        var data = new List<byte>();
        // ErrorCode (INT16)
        data.AddRange(new byte[] { 0x00, 0x00 }); // None

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (LeaveGroupResponse)LeaveGroupResponse.Read(ref reader, version: 0);

        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.ThrottleTimeMs).IsEqualTo(0);
        await Assert.That(response.Members).IsNull();
    }

    [Test]
    public async Task LeaveGroupResponse_V1_WithThrottle()
    {
        var data = new List<byte>();
        // ThrottleTimeMs (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x64 }); // 100ms
        // ErrorCode (INT16)
        data.AddRange(new byte[] { 0x00, 0x00 }); // None

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (LeaveGroupResponse)LeaveGroupResponse.Read(ref reader, version: 1);

        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.ThrottleTimeMs).IsEqualTo(100);
    }

    [Test]
    public async Task LeaveGroupResponse_V3_WithMembers()
    {
        var data = new List<byte>();
        // ThrottleTimeMs (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        // ErrorCode (INT16)
        data.AddRange(new byte[] { 0x00, 0x00 }); // None
        // Members array (INT32 length)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 }); // 1 member
        // Member entry:
        // MemberId (STRING)
        data.AddRange(new byte[] { 0x00, 0x03 }); // length = 3
        data.AddRange("m-1"u8.ToArray());
        // GroupInstanceId (NULLABLE_STRING)
        data.AddRange(new byte[] { 0xFF, 0xFF }); // null
        // ErrorCode (INT16)
        data.AddRange(new byte[] { 0x00, 0x00 }); // None

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (LeaveGroupResponse)LeaveGroupResponse.Read(ref reader, version: 3);

        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.Members).IsNotNull();
        await Assert.That(response.Members!.Count).IsEqualTo(1);
        await Assert.That(response.Members[0].MemberId).IsEqualTo("m-1");
        await Assert.That(response.Members[0].GroupInstanceId).IsNull();
        await Assert.That(response.Members[0].ErrorCode).IsEqualTo(ErrorCode.None);
    }

    [Test]
    public async Task LeaveGroupResponse_V4_Flexible_WithMembers()
    {
        var data = new List<byte>();
        // ThrottleTimeMs (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        // ErrorCode (INT16)
        data.AddRange(new byte[] { 0x00, 0x00 }); // None
        // Members COMPACT_ARRAY (varint length+1)
        data.Add(0x02); // 1 member (length+1)
        // Member entry:
        // MemberId (COMPACT_STRING)
        data.Add(0x04); // length+1 = 4
        data.AddRange("m-1"u8.ToArray());
        // GroupInstanceId (COMPACT_NULLABLE_STRING)
        data.Add(0x00); // null
        // ErrorCode (INT16)
        data.AddRange(new byte[] { 0x00, 0x00 }); // None
        // Member tagged fields
        data.Add(0x00);
        // Response tagged fields
        data.Add(0x00);

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (LeaveGroupResponse)LeaveGroupResponse.Read(ref reader, version: 4);

        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.Members).IsNotNull();
        await Assert.That(response.Members!.Count).IsEqualTo(1);
        await Assert.That(response.Members[0].MemberId).IsEqualTo("m-1");
    }

    [Test]
    public async Task LeaveGroupResponse_WithError_ParsedCorrectly()
    {
        var data = new List<byte>();
        // ErrorCode (INT16) - UnknownMemberId = 25
        data.AddRange(new byte[] { 0x00, 0x19 });

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (LeaveGroupResponse)LeaveGroupResponse.Read(ref reader, version: 0);

        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.UnknownMemberId);
    }

    #endregion

    #region Version Flexibility Tests

    [Test]
    [Arguments((short)0, false)]
    [Arguments((short)1, false)]
    [Arguments((short)2, false)]
    [Arguments((short)3, false)]
    [Arguments((short)4, true)]
    [Arguments((short)5, true)]
    public async Task LeaveGroupRequest_FlexibilityDetection(short version, bool expectedFlexible)
    {
        var isFlexible = LeaveGroupRequest.IsFlexibleVersion(version);
        await Assert.That(isFlexible).IsEqualTo(expectedFlexible);
    }

    [Test]
    [Arguments((short)0, (short)1, (short)0)]
    [Arguments((short)3, (short)1, (short)0)]
    [Arguments((short)4, (short)2, (short)1)]
    [Arguments((short)5, (short)2, (short)1)]
    public async Task LeaveGroupRequest_HeaderVersions(short apiVersion, short expectedRequestHeader, short expectedResponseHeader)
    {
        var requestHeaderVersion = LeaveGroupRequest.GetRequestHeaderVersion(apiVersion);
        var responseHeaderVersion = LeaveGroupRequest.GetResponseHeaderVersion(apiVersion);

        await Assert.That(requestHeaderVersion).IsEqualTo(expectedRequestHeader);
        await Assert.That(responseHeaderVersion).IsEqualTo(expectedResponseHeader);
    }

    #endregion

    #region Consumer CloseAsync Idempotency Tests

    [Test]
    public async Task KafkaConsumer_CloseAsync_IsIdempotent()
    {
        // Create a minimal consumer for testing
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "test-consumer"
            // Note: No GroupId, so no coordinator - tests basic close behavior
        };

        await using var consumer = new KafkaConsumer<string, string>(
            options,
            Serializers.String,
            Serializers.String);

        // First close should succeed
        await consumer.CloseAsync();

        // Second close should also succeed (idempotent)
        await consumer.CloseAsync();

        // If we got here without exceptions, the test passed
        // Verify consumer state reflects closed status
        var subscription = consumer.Subscription;
        await Assert.That(subscription.Count).IsEqualTo(0);
    }

    [Test]
    public async Task KafkaConsumer_CloseAsync_HandlesCancellation()
    {
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "test-consumer"
        };

        await using var consumer = new KafkaConsumer<string, string>(
            options,
            Serializers.String,
            Serializers.String);

        // Close with already-cancelled token should still work
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Should complete without throwing (graceful handling of cancellation)
        try
        {
            await consumer.CloseAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // This is acceptable - cancellation was respected
        }

        // Either graceful completion or caught cancellation is valid
        var subscription = consumer.Subscription;
        await Assert.That(subscription.Count).IsEqualTo(0);
    }

    [Test]
    public async Task KafkaConsumer_DisposeAsync_CallsCloseInternally()
    {
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "test-consumer"
        };

        var consumer = new KafkaConsumer<string, string>(
            options,
            Serializers.String,
            Serializers.String);

        // Dispose should work without prior close
        await consumer.DisposeAsync();

        // Consumer should be disposed - calling close should do nothing (already closed)
        await consumer.CloseAsync();

        // If we got here without exceptions, the test passed
        var subscription = consumer.Subscription;
        await Assert.That(subscription.Count).IsEqualTo(0);
    }

    #endregion
}
