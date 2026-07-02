using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

/// <summary>
/// Tests for admin protocol message encoding/decoding:
/// CreatePartitions, ListGroups, DescribeGroups, DeleteGroups, DeleteRecords.
/// </summary>
public class AdminMessageEncodingTests
{
    [Test]
    public async Task CreatePartitionsRequest_V2_Flexible_EncodedCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new CreatePartitionsRequest
        {
            Topics =
            [
                new CreatePartitionsTopic
                {
                    Name = "flex-topic",
                    Count = 10
                }
            ],
            TimeoutMs = 5000,
            ValidateOnly = false
        };
        request.Write(ref writer, version: 2);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        // Topics COMPACT_ARRAY (length+1)
        var topicsLengthPlus1 = reader.ReadUnsignedVarInt();
        var topicName = reader.ReadCompactString();
        var count = reader.ReadInt32();
        // Null assignments as COMPACT_NULLABLE_ARRAY (0 = null)
        var assignmentsIndicator = reader.ReadUnsignedVarInt();
        // Topic tagged fields
        reader.SkipTaggedFields();
        // TimeoutMs
        var timeoutMs = reader.ReadInt32();
        // ValidateOnly
        var validateOnly = reader.ReadBoolean();
        // Request tagged fields
        reader.SkipTaggedFields();

        await Assert.That(topicsLengthPlus1).IsEqualTo(2); // 1 element
        await Assert.That(topicName).IsEqualTo("flex-topic");
        await Assert.That(count).IsEqualTo(10);
        await Assert.That(assignmentsIndicator).IsEqualTo(0); // null
        await Assert.That(timeoutMs).IsEqualTo(5000);
        await Assert.That(validateOnly).IsFalse();
    }

    [Test]
    public async Task CreatePartitionsRequest_V3_MultipleTopics_EncodedCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new CreatePartitionsRequest
        {
            Topics =
            [
                new CreatePartitionsTopic { Name = "topic-a", Count = 4 },
                new CreatePartitionsTopic { Name = "topic-b", Count = 8 }
            ],
            TimeoutMs = 15000,
            ValidateOnly = false
        };
        request.Write(ref writer, version: 3);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        // COMPACT_ARRAY (length+1 = 3, so 2 elements)
        var topicsLengthPlus1 = reader.ReadUnsignedVarInt();
        // Topic A
        var nameA = reader.ReadCompactString();
        var countA = reader.ReadInt32();
        _ = reader.ReadUnsignedVarInt(); // null assignments
        reader.SkipTaggedFields();
        // Topic B
        var nameB = reader.ReadCompactString();
        var countB = reader.ReadInt32();
        _ = reader.ReadUnsignedVarInt(); // null assignments
        reader.SkipTaggedFields();

        await Assert.That(topicsLengthPlus1).IsEqualTo(3);
        await Assert.That(nameA).IsEqualTo("topic-a");
        await Assert.That(countA).IsEqualTo(4);
        await Assert.That(nameB).IsEqualTo("topic-b");
        await Assert.That(countB).IsEqualTo(8);
    }

    [Test]
    public async Task CreatePartitionsResponse_V2_Flexible_CanBeParsed()
    {
        var data = new List<byte>();
        // ThrottleTimeMs (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x32 }); // 50ms
        // Results COMPACT_ARRAY (length+1 = 2)
        data.Add(0x02);
        // Result: Name (COMPACT_STRING)
        data.Add(0x09); // length+1 = 9
        data.AddRange("my-topic"u8.ToArray());
        // ErrorCode (INT16)
        data.AddRange(new byte[] { 0x00, 0x00 }); // None
        // ErrorMessage (COMPACT_NULLABLE_STRING)
        data.Add(0x00); // null
        // Result tagged fields
        data.Add(0x00);
        // Response tagged fields
        data.Add(0x00);

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (CreatePartitionsResponse)CreatePartitionsResponse.Read(ref reader, version: 2);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(50);
        await Assert.That(response.Results.Count).IsEqualTo(1);
        await Assert.That(response.Results[0].Name).IsEqualTo("my-topic");
        await Assert.That(response.Results[0].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.Results[0].ErrorMessage).IsNull();
    }

    [Test]
    public async Task CreatePartitionsResponse_WithError_CanBeParsed()
    {
        var data = new List<byte>();
        // ThrottleTimeMs (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        // Results COMPACT_ARRAY (length+1 = 2)
        data.Add(0x02);
        // Result: Name (COMPACT_STRING)
        data.Add(0x07); // length+1 = 7
        data.AddRange("my-top"u8.ToArray());
        // ErrorCode (INT16) - InvalidPartitions = 37
        data.AddRange(new byte[] { 0x00, 0x25 });
        // ErrorMessage (COMPACT_NULLABLE_STRING)
        var errorMsg = "Partition count must be positive"u8.ToArray();
        data.Add((byte)(errorMsg.Length + 1)); // length+1
        data.AddRange(errorMsg);
        // Result tagged fields
        data.Add(0x00);
        // Response tagged fields
        data.Add(0x00);

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (CreatePartitionsResponse)CreatePartitionsResponse.Read(ref reader, version: 2);

        await Assert.That(response.Results[0].ErrorCode).IsEqualTo(ErrorCode.InvalidPartitions);
        await Assert.That(response.Results[0].ErrorMessage).IsNotNull();
    }

    [Test]
    public async Task ListGroupsRequest_V3_Flexible_TaggedFieldsOnly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new ListGroupsRequest();
        request.Write(ref writer, version: 3);

        // v3 is flexible but no state filter yet — just tagged fields
        var expected = new List<byte>();
        expected.Add(0x00); // empty tagged fields

        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(expected.ToArray());
    }

    [Test]
    public async Task ListGroupsRequest_V4_WithStatesFilter()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new ListGroupsRequest
        {
            StatesFilter = ["Stable", "Empty"]
        };
        request.Write(ref writer, version: 4);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        // StatesFilter COMPACT_ARRAY (length+1 = 3, so 2 elements)
        var statesLengthPlus1 = reader.ReadUnsignedVarInt();
        var state1 = reader.ReadCompactString();
        var state2 = reader.ReadCompactString();
        // Tagged fields
        reader.SkipTaggedFields();

        await Assert.That(statesLengthPlus1).IsEqualTo(3);
        await Assert.That(state1).IsEqualTo("Stable");
        await Assert.That(state2).IsEqualTo("Empty");
    }

    [Test]
    public async Task ListGroupsRequest_V5_WithStatesAndTypesFilter()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new ListGroupsRequest
        {
            StatesFilter = ["Stable"],
            TypesFilter = ["consumer"]
        };
        request.Write(ref writer, version: 5);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        // StatesFilter
        var statesLengthPlus1 = reader.ReadUnsignedVarInt();
        var state = reader.ReadCompactString();
        // TypesFilter
        var typesLengthPlus1 = reader.ReadUnsignedVarInt();
        var type = reader.ReadCompactString();

        await Assert.That(statesLengthPlus1).IsEqualTo(2);
        await Assert.That(state).IsEqualTo("Stable");
        await Assert.That(typesLengthPlus1).IsEqualTo(2);
        await Assert.That(type).IsEqualTo("consumer");
    }

    [Test]
    public async Task ListGroupsRequest_V4_NullStatesFilter_WritesEmptyArray()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new ListGroupsRequest
        {
            StatesFilter = null
        };
        request.Write(ref writer, version: 4);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        // Null StatesFilter → written as empty array (length+1 = 1)
        var statesLengthPlus1 = reader.ReadUnsignedVarInt();

        await Assert.That(statesLengthPlus1).IsEqualTo(1); // empty array
    }

    [Test]
    public async Task ListGroupsResponse_V1_WithThrottleTime_CanBeParsed()
    {
        var data = new List<byte>();
        // ThrottleTimeMs (INT32) — v1+
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x64 }); // 100ms
        // ErrorCode (INT16)
        data.AddRange(new byte[] { 0x00, 0x00 }); // None
        // Groups array (INT32 length)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 }); // 0 groups

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (ListGroupsResponse)ListGroupsResponse.Read(ref reader, version: 1);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(100);
        await Assert.That(response.Groups.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ListGroupsResponse_V3_Flexible_CanBeParsed()
    {
        var data = new List<byte>();
        // ThrottleTimeMs (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        // ErrorCode (INT16)
        data.AddRange(new byte[] { 0x00, 0x00 });
        // Groups COMPACT_ARRAY (length+1 = 2)
        data.Add(0x02);
        // Group: GroupId (COMPACT_STRING)
        data.Add(0x05); // length+1 = 5
        data.AddRange("grp1"u8.ToArray());
        // ProtocolType (COMPACT_STRING)
        data.Add(0x09); // length+1 = 9
        data.AddRange("consumer"u8.ToArray());
        // Group tagged fields
        data.Add(0x00);
        // Response tagged fields
        data.Add(0x00);

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (ListGroupsResponse)ListGroupsResponse.Read(ref reader, version: 3);

        await Assert.That(response.Groups.Count).IsEqualTo(1);
        await Assert.That(response.Groups[0].GroupId).IsEqualTo("grp1");
        await Assert.That(response.Groups[0].ProtocolType).IsEqualTo("consumer");
    }

    [Test]
    public async Task ListGroupsResponse_V4_WithGroupState_CanBeParsed()
    {
        var data = new List<byte>();
        // ThrottleTimeMs (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        // ErrorCode (INT16)
        data.AddRange(new byte[] { 0x00, 0x00 });
        // Groups COMPACT_ARRAY (length+1 = 2)
        data.Add(0x02);
        // Group: GroupId (COMPACT_STRING)
        data.Add(0x07); // length+1 = 7
        data.AddRange("my-grp"u8.ToArray());
        // ProtocolType (COMPACT_STRING)
        data.Add(0x09);
        data.AddRange("consumer"u8.ToArray());
        // GroupState (COMPACT_STRING) — v4+
        data.Add(0x07); // length+1 = 7
        data.AddRange("Stable"u8.ToArray());
        // Group tagged fields
        data.Add(0x00);
        // Response tagged fields
        data.Add(0x00);

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (ListGroupsResponse)ListGroupsResponse.Read(ref reader, version: 4);

        await Assert.That(response.Groups[0].GroupState).IsEqualTo("Stable");
        await Assert.That(response.Groups[0].GroupType).IsNull();
    }

    [Test]
    public async Task ListGroupsResponse_V5_WithGroupType_CanBeParsed()
    {
        var data = new List<byte>();
        // ThrottleTimeMs (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        // ErrorCode (INT16)
        data.AddRange(new byte[] { 0x00, 0x00 });
        // Groups COMPACT_ARRAY (length+1 = 2)
        data.Add(0x02);
        // Group: GroupId
        data.Add(0x05);
        data.AddRange("grp1"u8.ToArray());
        // ProtocolType
        data.Add(0x09);
        data.AddRange("consumer"u8.ToArray());
        // GroupState (v4+)
        data.Add(0x06);
        data.AddRange("Empty"u8.ToArray());
        // GroupType (v5+)
        data.Add(0x08);
        data.AddRange("classic"u8.ToArray());
        // Group tagged fields
        data.Add(0x00);
        // Response tagged fields
        data.Add(0x00);

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (ListGroupsResponse)ListGroupsResponse.Read(ref reader, version: 5);

        await Assert.That(response.Groups[0].GroupId).IsEqualTo("grp1");
        await Assert.That(response.Groups[0].GroupState).IsEqualTo("Empty");
        await Assert.That(response.Groups[0].GroupType).IsEqualTo("classic");
    }

    [Test]
    public async Task ListGroupsResponse_WithError_CanBeParsed()
    {
        var data = new List<byte>();
        // ThrottleTimeMs
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        // ErrorCode = CoordinatorNotAvailable (15)
        data.AddRange(new byte[] { 0x00, 0x0F });
        // Groups COMPACT_ARRAY (empty = length+1 = 1)
        data.Add(0x01);
        // Response tagged fields
        data.Add(0x00);

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (ListGroupsResponse)ListGroupsResponse.Read(ref reader, version: 3);

        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.CoordinatorNotAvailable);
        await Assert.That(response.Groups.Count).IsEqualTo(0);
    }

    [Test]
    public async Task DescribeGroupsRequest_V5_Flexible_EncodedCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new DescribeGroupsRequest
        {
            Groups = ["group-1"],
            IncludeAuthorizedOperations = false
        };
        request.Write(ref writer, version: 5);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        // Groups COMPACT_ARRAY
        var arrayLengthPlus1 = reader.ReadUnsignedVarInt();
        var group = reader.ReadCompactString();
        // IncludeAuthorizedOperations (v3+)
        var includeAuth = reader.ReadBoolean();
        // Tagged fields
        reader.SkipTaggedFields();

        await Assert.That(arrayLengthPlus1).IsEqualTo(2);
        await Assert.That(group).IsEqualTo("group-1");
        await Assert.That(includeAuth).IsFalse();
    }

    [Test]
    public async Task DescribeGroupsResponse_V1_WithThrottleTime_CanBeParsed()
    {
        var data = new List<byte>();
        // ThrottleTimeMs (INT32) — v1+
        data.AddRange(new byte[] { 0x00, 0x00, 0x01, 0xF4 }); // 500ms
        // Groups array (INT32 length = 0)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (DescribeGroupsResponse)DescribeGroupsResponse.Read(ref reader, version: 1);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(500);
        await Assert.That(response.Groups.Count).IsEqualTo(0);
    }

    [Test]
    public async Task DescribeGroupsResponse_V5_Flexible_CanBeParsed()
    {
        var data = new List<byte>();
        // ThrottleTimeMs (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x0A }); // 10ms
        // Groups COMPACT_ARRAY (length+1 = 2)
        data.Add(0x02);
        // Group entry:
        // ErrorCode (INT16)
        data.AddRange(new byte[] { 0x00, 0x00 });
        // GroupId (COMPACT_STRING)
        data.Add(0x04); // length+1 = 4
        data.AddRange("grp"u8.ToArray());
        // GroupState (COMPACT_STRING)
        data.Add(0x06);
        data.AddRange("Empty"u8.ToArray());
        // ProtocolType (COMPACT_STRING)
        data.Add(0x09);
        data.AddRange("consumer"u8.ToArray());
        // ProtocolData (COMPACT_NULLABLE_STRING)
        data.Add(0x00); // null
        // Members COMPACT_ARRAY (length+1 = 1 = empty)
        data.Add(0x01);
        // AuthorizedOperations (v3+)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x07 }); // 7
        // Group tagged fields
        data.Add(0x00);
        // Response tagged fields
        data.Add(0x00);

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (DescribeGroupsResponse)DescribeGroupsResponse.Read(ref reader, version: 5);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(10);
        var group = response.Groups[0];
        await Assert.That(group.GroupId).IsEqualTo("grp");
        await Assert.That(group.GroupState).IsEqualTo("Empty");
        await Assert.That(group.ProtocolType).IsEqualTo("consumer");
        await Assert.That(group.ProtocolData).IsNull();
        await Assert.That(group.Members.Count).IsEqualTo(0);
        await Assert.That(group.AuthorizedOperations).IsEqualTo(7);
    }

    [Test]
    public async Task DescribeGroupsResponse_V5_Flexible_WithMemberAssignment_CanBeParsed()
    {
        var assignmentBytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var metadataBytes = new byte[] { 0xAA, 0xBB };

        var data = new List<byte>();
        // ThrottleTimeMs
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        // Groups COMPACT_ARRAY (1 group)
        data.Add(0x02);
        // Group entry:
        data.AddRange(new byte[] { 0x00, 0x00 }); // ErrorCode
        data.Add(0x03); data.AddRange("g1"u8.ToArray()); // GroupId
        data.Add(0x07); data.AddRange("Stable"u8.ToArray()); // GroupState
        data.Add(0x09); data.AddRange("consumer"u8.ToArray()); // ProtocolType
        data.Add(0x06); data.AddRange("range"u8.ToArray()); // ProtocolData
        // Members COMPACT_ARRAY (1 member)
        data.Add(0x02);
        // Member:
        data.Add(0x03); data.AddRange("m1"u8.ToArray()); // MemberId (COMPACT_STRING)
        data.Add(0x00); // GroupInstanceId: null (v4+)
        data.Add(0x04); data.AddRange("cli"u8.ToArray()); // ClientId
        data.Add(0x06); data.AddRange("host1"u8.ToArray()); // ClientHost
        // MemberMetadata (COMPACT_BYTES: length+1)
        data.Add((byte)(metadataBytes.Length + 1));
        data.AddRange(metadataBytes);
        // MemberAssignment (COMPACT_BYTES: length+1)
        data.Add((byte)(assignmentBytes.Length + 1));
        data.AddRange(assignmentBytes);
        // Member tagged fields
        data.Add(0x00);
        // AuthorizedOperations
        data.AddRange(new byte[] { 0x80, 0x00, 0x00, 0x00 });
        // Group tagged fields
        data.Add(0x00);
        // Response tagged fields
        data.Add(0x00);

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (DescribeGroupsResponse)DescribeGroupsResponse.Read(ref reader, version: 5);

        var member = response.Groups[0].Members[0];
        await Assert.That(member.MemberId).IsEqualTo("m1");
        await Assert.That(member.GroupInstanceId).IsNull();
        await Assert.That(member.ClientId).IsEqualTo("cli");
        await Assert.That(member.ClientHost).IsEqualTo("host1");
        await Assert.That(member.MemberMetadata).IsEquivalentTo(metadataBytes);
        await Assert.That(member.MemberAssignment).IsEquivalentTo(assignmentBytes);
    }

    [Test]
    public async Task DescribeGroupsResponse_WithError_CanBeParsed()
    {
        var data = new List<byte>();
        // ThrottleTimeMs
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        // Groups COMPACT_ARRAY (1 group)
        data.Add(0x02);
        // Group entry:
        // ErrorCode = GroupIdNotFound (69)
        data.AddRange(new byte[] { 0x00, 0x45 });
        // GroupId
        data.Add(0x09); data.AddRange("bad-grp1"u8.ToArray());
        // GroupState (empty)
        data.Add(0x01);
        // ProtocolType (empty)
        data.Add(0x01);
        // ProtocolData (null)
        data.Add(0x00);
        // Members COMPACT_ARRAY (empty)
        data.Add(0x01);
        // AuthorizedOperations
        data.AddRange(new byte[] { 0x80, 0x00, 0x00, 0x00 });
        // Group tagged fields
        data.Add(0x00);
        // Response tagged fields
        data.Add(0x00);

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (DescribeGroupsResponse)DescribeGroupsResponse.Read(ref reader, version: 5);

        await Assert.That(response.Groups[0].ErrorCode).IsEqualTo(ErrorCode.GroupIdNotFound);
    }

    [Test]
    public async Task DeleteGroupsRequest_V2_Flexible_EncodedCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new DeleteGroupsRequest
        {
            GroupsNames = ["my-group"]
        };
        request.Write(ref writer, version: 2);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        var arrayLengthPlus1 = reader.ReadUnsignedVarInt();
        var group = reader.ReadCompactString();
        reader.SkipTaggedFields();

        await Assert.That(arrayLengthPlus1).IsEqualTo(2);
        await Assert.That(group).IsEqualTo("my-group");
    }

    [Test]
    public async Task DeleteGroupsResponse_V2_Flexible_CanBeParsed()
    {
        var data = new List<byte>();
        // ThrottleTimeMs (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x14 }); // 20ms
        // Results COMPACT_ARRAY (length+1 = 2)
        data.Add(0x02);
        // Result: GroupId (COMPACT_STRING)
        data.Add(0x08); // length+1 = 8
        data.AddRange("my-grp1"u8.ToArray());
        // ErrorCode (INT16) = None
        data.AddRange(new byte[] { 0x00, 0x00 });
        // Result tagged fields
        data.Add(0x00);
        // Response tagged fields
        data.Add(0x00);

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (DeleteGroupsResponse)DeleteGroupsResponse.Read(ref reader, version: 2);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(20);
        await Assert.That(response.Results[0].GroupId).IsEqualTo("my-grp1");
        await Assert.That(response.Results[0].ErrorCode).IsEqualTo(ErrorCode.None);
    }

    [Test]
    public async Task DeleteGroupsResponse_WithError_CanBeParsed()
    {
        var data = new List<byte>();
        // ThrottleTimeMs
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        // Results COMPACT_ARRAY (length+1 = 2)
        data.Add(0x02);
        // Result: GroupId
        data.Add(0x06);
        data.AddRange("grp-x"u8.ToArray());
        // ErrorCode = NonEmptyGroup (68)
        data.AddRange(new byte[] { 0x00, 0x44 });
        // Result tagged fields
        data.Add(0x00);
        // Response tagged fields
        data.Add(0x00);

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (DeleteGroupsResponse)DeleteGroupsResponse.Read(ref reader, version: 2);

        await Assert.That(response.Results[0].ErrorCode).IsEqualTo(ErrorCode.NonEmptyGroup);
    }

    [Test]
    public async Task DeleteRecordsRequest_V2_Flexible_EncodedCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new DeleteRecordsRequest
        {
            Topics =
            [
                new DeleteRecordsRequestTopic
                {
                    Name = "t1",
                    Partitions =
                    [
                        new DeleteRecordsRequestPartition { PartitionIndex = 0, Offset = 50 },
                        new DeleteRecordsRequestPartition { PartitionIndex = 1, Offset = 75 }
                    ]
                }
            ],
            TimeoutMs = 10000
        };
        request.Write(ref writer, version: 2);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        // Topics COMPACT_ARRAY
        var topicsLengthPlus1 = reader.ReadUnsignedVarInt();
        var topicName = reader.ReadCompactString();
        // Partitions COMPACT_ARRAY
        var partitionsLengthPlus1 = reader.ReadUnsignedVarInt();
        // Partition 0
        var idx0 = reader.ReadInt32();
        var offset0 = reader.ReadInt64();
        reader.SkipTaggedFields(); // partition tagged fields
        // Partition 1
        var idx1 = reader.ReadInt32();
        var offset1 = reader.ReadInt64();
        reader.SkipTaggedFields(); // partition tagged fields
        // Topic tagged fields
        reader.SkipTaggedFields();
        // TimeoutMs
        var timeoutMs = reader.ReadInt32();
        // Request tagged fields
        reader.SkipTaggedFields();

        await Assert.That(topicsLengthPlus1).IsEqualTo(2);
        await Assert.That(topicName).IsEqualTo("t1");
        await Assert.That(partitionsLengthPlus1).IsEqualTo(3); // 2 partitions
        await Assert.That(idx0).IsEqualTo(0);
        await Assert.That(offset0).IsEqualTo(50L);
        await Assert.That(idx1).IsEqualTo(1);
        await Assert.That(offset1).IsEqualTo(75L);
        await Assert.That(timeoutMs).IsEqualTo(10000);
    }

    [Test]
    public async Task DeleteRecordsResponse_V2_Flexible_CanBeParsed()
    {
        var data = new List<byte>();
        // ThrottleTimeMs (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x0A }); // 10ms
        // Topics COMPACT_ARRAY (length+1 = 2)
        data.Add(0x02);
        // Topic: Name (COMPACT_STRING)
        data.Add(0x03); // length+1 = 3
        data.AddRange("t1"u8.ToArray());
        // Partitions COMPACT_ARRAY (length+1 = 2)
        data.Add(0x02);
        // Partition:
        // PartitionIndex (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x03 }); // 3
        // LowWatermark (INT64)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x32 }); // 50
        // ErrorCode (INT16)
        data.AddRange(new byte[] { 0x00, 0x00 }); // None
        // Partition tagged fields
        data.Add(0x00);
        // Topic tagged fields
        data.Add(0x00);
        // Response tagged fields
        data.Add(0x00);

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (DeleteRecordsResponse)DeleteRecordsResponse.Read(ref reader, version: 2);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(10);
        await Assert.That(response.Topics[0].Name).IsEqualTo("t1");
        await Assert.That(response.Topics[0].Partitions[0].PartitionIndex).IsEqualTo(3);
        await Assert.That(response.Topics[0].Partitions[0].LowWatermark).IsEqualTo(50L);
        await Assert.That(response.Topics[0].Partitions[0].ErrorCode).IsEqualTo(ErrorCode.None);
    }

    [Test]
    public async Task DeleteRecordsResponse_WithError_CanBeParsed()
    {
        var data = new List<byte>();
        // ThrottleTimeMs
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        // Topics COMPACT_ARRAY (1 topic)
        data.Add(0x02);
        // Topic: Name
        data.Add(0x06);
        data.AddRange("topic"u8.ToArray());
        // Partitions COMPACT_ARRAY (1 partition)
        data.Add(0x02);
        // Partition:
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 }); // PartitionIndex = 0
        data.AddRange(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }); // LowWatermark = -1
        // ErrorCode = OffsetOutOfRange (1)
        data.AddRange(new byte[] { 0x00, 0x01 });
        // Partition tagged fields
        data.Add(0x00);
        // Topic tagged fields
        data.Add(0x00);
        // Response tagged fields
        data.Add(0x00);

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (DeleteRecordsResponse)DeleteRecordsResponse.Read(ref reader, version: 2);

        await Assert.That(response.Topics[0].Partitions[0].ErrorCode).IsEqualTo(ErrorCode.OffsetOutOfRange);
        await Assert.That(response.Topics[0].Partitions[0].LowWatermark).IsEqualTo(-1L);
    }

    [Test]
    public async Task DeleteRecordsRequest_WriteRead_RoundTrip_V2()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new DeleteRecordsRequest
        {
            Topics =
            [
                new DeleteRecordsRequestTopic
                {
                    Name = "rt-topic",
                    Partitions =
                    [
                        new DeleteRecordsRequestPartition { PartitionIndex = 0, Offset = long.MaxValue },
                        new DeleteRecordsRequestPartition { PartitionIndex = 5, Offset = 999 }
                    ]
                }
            ],
            TimeoutMs = 45000
        };
        request.Write(ref writer, version: 2);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        // Topics COMPACT_ARRAY
        var topicsLen = reader.ReadUnsignedVarInt();
        var name = reader.ReadCompactString();
        // Partitions COMPACT_ARRAY
        var partsLen = reader.ReadUnsignedVarInt();
        // Part 0
        var idx0 = reader.ReadInt32();
        var off0 = reader.ReadInt64();
        reader.SkipTaggedFields();
        // Part 1
        var idx1 = reader.ReadInt32();
        var off1 = reader.ReadInt64();
        reader.SkipTaggedFields();
        // Topic tagged fields
        reader.SkipTaggedFields();
        // TimeoutMs
        var timeout = reader.ReadInt32();
        reader.SkipTaggedFields();

        await Assert.That(topicsLen).IsEqualTo(2); // 1 topic
        await Assert.That(name).IsEqualTo("rt-topic");
        await Assert.That(partsLen).IsEqualTo(3); // 2 partitions
        await Assert.That(idx0).IsEqualTo(0);
        await Assert.That(off0).IsEqualTo(long.MaxValue);
        await Assert.That(idx1).IsEqualTo(5);
        await Assert.That(off1).IsEqualTo(999L);
        await Assert.That(timeout).IsEqualTo(45000);
    }

    [Test]
    public async Task DescribeGroupsRequest_WriteRead_RoundTrip_V5()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new DescribeGroupsRequest
        {
            Groups = ["group-alpha", "group-beta", "group-gamma"],
            IncludeAuthorizedOperations = true
        };
        request.Write(ref writer, version: 5);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        var arrayLen = reader.ReadUnsignedVarInt();
        var g1 = reader.ReadCompactString();
        var g2 = reader.ReadCompactString();
        var g3 = reader.ReadCompactString();
        var includeAuth = reader.ReadBoolean();
        reader.SkipTaggedFields();

        await Assert.That(arrayLen).IsEqualTo(4); // 3 groups
        await Assert.That(g1).IsEqualTo("group-alpha");
        await Assert.That(g2).IsEqualTo("group-beta");
        await Assert.That(g3).IsEqualTo("group-gamma");
        await Assert.That(includeAuth).IsTrue();
    }

}
