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
    #region CreatePartitions Request Tests

    [Test]
    public async Task CreatePartitionsRequest_V0_Legacy_EncodedCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new CreatePartitionsRequest
        {
            Topics =
            [
                new CreatePartitionsTopic
                {
                    Name = "my-topic",
                    Count = 6
                }
            ],
            TimeoutMs = 30000,
            ValidateOnly = false
        };
        request.Write(ref writer, version: 0);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        // Topics array (INT32 length)
        var arrayLength = reader.ReadInt32();
        // Topic name (STRING)
        var topicName = reader.ReadString();
        // Count (INT32)
        var count = reader.ReadInt32();
        // Assignments (nullable ARRAY = -1 for null)
        var assignmentsLength = reader.ReadInt32();
        // TimeoutMs (INT32)
        var timeoutMs = reader.ReadInt32();
        // ValidateOnly (BOOLEAN)
        var validateOnly = reader.ReadBoolean();

        await Assert.That(arrayLength).IsEqualTo(1);
        await Assert.That(topicName).IsEqualTo("my-topic");
        await Assert.That(count).IsEqualTo(6);
        await Assert.That(assignmentsLength).IsEqualTo(-1);
        await Assert.That(timeoutMs).IsEqualTo(30000);
        await Assert.That(validateOnly).IsFalse();
    }

    [Test]
    public async Task CreatePartitionsRequest_V0_WithAssignments_EncodedCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new CreatePartitionsRequest
        {
            Topics =
            [
                new CreatePartitionsTopic
                {
                    Name = "t1",
                    Count = 3,
                    Assignments =
                    [
                        new CreatePartitionsAssignment { BrokerIds = [1, 2] }
                    ]
                }
            ],
            TimeoutMs = 10000,
            ValidateOnly = true
        };
        request.Write(ref writer, version: 0);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        _ = reader.ReadInt32(); // topics array length
        _ = reader.ReadString(); // topic name
        _ = reader.ReadInt32(); // count
        // Assignments array (INT32 length = 1)
        var assignmentsCount = reader.ReadInt32();
        // Assignment: BrokerIds array
        var brokerIdsCount = reader.ReadInt32();
        var brokerId1 = reader.ReadInt32();
        var brokerId2 = reader.ReadInt32();
        // TimeoutMs
        var timeoutMs = reader.ReadInt32();
        // ValidateOnly
        var validateOnly = reader.ReadBoolean();

        await Assert.That(assignmentsCount).IsEqualTo(1);
        await Assert.That(brokerIdsCount).IsEqualTo(2);
        await Assert.That(brokerId1).IsEqualTo(1);
        await Assert.That(brokerId2).IsEqualTo(2);
        await Assert.That(timeoutMs).IsEqualTo(10000);
        await Assert.That(validateOnly).IsTrue();
    }

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

    #endregion

    #region CreatePartitions Response Tests

    [Test]
    public async Task CreatePartitionsResponse_V0_Legacy_CanBeParsed()
    {
        var data = new List<byte>();
        // ThrottleTimeMs (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        // Results array (INT32 length)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 }); // 1 result
        // Result: Name (STRING)
        data.AddRange(new byte[] { 0x00, 0x08 }); // length = 8
        data.AddRange("my-topic"u8.ToArray());
        // ErrorCode (INT16)
        data.AddRange(new byte[] { 0x00, 0x00 }); // None
        // ErrorMessage (nullable STRING)
        data.AddRange(new byte[] { 0xFF, 0xFF }); // null

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (CreatePartitionsResponse)CreatePartitionsResponse.Read(ref reader, version: 0);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(0);
        await Assert.That(response.Results.Count).IsEqualTo(1);
        await Assert.That(response.Results[0].Name).IsEqualTo("my-topic");
        await Assert.That(response.Results[0].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.Results[0].ErrorMessage).IsNull();
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
    public async Task CreatePartitionsResponse_MultipleResults_CanBeParsed()
    {
        var data = new List<byte>();
        // ThrottleTimeMs
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        // Results array (INT32 length = 2)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x02 });
        // Result 1: Name
        data.AddRange(new byte[] { 0x00, 0x02 }); // length = 2
        data.AddRange("t1"u8.ToArray());
        data.AddRange(new byte[] { 0x00, 0x00 }); // None
        data.AddRange(new byte[] { 0xFF, 0xFF }); // null message
        // Result 2: Name
        data.AddRange(new byte[] { 0x00, 0x02 }); // length = 2
        data.AddRange("t2"u8.ToArray());
        data.AddRange(new byte[] { 0x00, 0x00 }); // None
        data.AddRange(new byte[] { 0xFF, 0xFF }); // null message

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (CreatePartitionsResponse)CreatePartitionsResponse.Read(ref reader, version: 0);

        await Assert.That(response.Results.Count).IsEqualTo(2);
        await Assert.That(response.Results[0].Name).IsEqualTo("t1");
        await Assert.That(response.Results[1].Name).IsEqualTo("t2");
    }

    #endregion

    #region ListGroups Request Tests

    [Test]
    public async Task ListGroupsRequest_V0_Legacy_EmptyBody()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new ListGroupsRequest();
        request.Write(ref writer, version: 0);

        // v0 has no body (no state filter, no type filter, not flexible)
        await Assert.That(buffer.WrittenCount).IsEqualTo(0);
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

    #endregion

    #region ListGroups Response Tests

    [Test]
    public async Task ListGroupsResponse_V0_Legacy_CanBeParsed()
    {
        var data = new List<byte>();
        // ErrorCode (INT16) — v0 has no ThrottleTimeMs
        data.AddRange(new byte[] { 0x00, 0x00 }); // None
        // Groups array (INT32 length)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 }); // 1 group
        // Group: GroupId (STRING)
        data.AddRange(new byte[] { 0x00, 0x08 }); // length = 8
        data.AddRange("my-group"u8.ToArray());
        // ProtocolType (STRING)
        data.AddRange(new byte[] { 0x00, 0x08 }); // length = 8
        data.AddRange("consumer"u8.ToArray());

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (ListGroupsResponse)ListGroupsResponse.Read(ref reader, version: 0);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(0);
        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.Groups.Count).IsEqualTo(1);
        await Assert.That(response.Groups[0].GroupId).IsEqualTo("my-group");
        await Assert.That(response.Groups[0].ProtocolType).IsEqualTo("consumer");
        await Assert.That(response.Groups[0].GroupState).IsNull();
        await Assert.That(response.Groups[0].GroupType).IsNull();
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

    #endregion

    #region DescribeGroups Request Tests

    [Test]
    public async Task DescribeGroupsRequest_V0_Legacy_EncodedCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new DescribeGroupsRequest
        {
            Groups = ["group-a", "group-b"]
        };
        request.Write(ref writer, version: 0);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        var arrayLength = reader.ReadInt32();
        var group1 = reader.ReadString();
        var group2 = reader.ReadString();

        await Assert.That(arrayLength).IsEqualTo(2);
        await Assert.That(group1).IsEqualTo("group-a");
        await Assert.That(group2).IsEqualTo("group-b");
    }

    [Test]
    public async Task DescribeGroupsRequest_V3_WithAuthorizedOperations()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new DescribeGroupsRequest
        {
            Groups = ["my-group"],
            IncludeAuthorizedOperations = true
        };
        request.Write(ref writer, version: 3);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        var arrayLength = reader.ReadInt32();
        var group = reader.ReadString();
        var includeAuth = reader.ReadBoolean();

        await Assert.That(arrayLength).IsEqualTo(1);
        await Assert.That(group).IsEqualTo("my-group");
        await Assert.That(includeAuth).IsTrue();
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

    #endregion

    #region DescribeGroups Response Tests

    [Test]
    public async Task DescribeGroupsResponse_V0_Legacy_CanBeParsed()
    {
        var data = new List<byte>();
        // No ThrottleTimeMs in v0
        // Groups array (INT32 length)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 }); // 1 group
        // Group entry:
        // ErrorCode (INT16)
        data.AddRange(new byte[] { 0x00, 0x00 }); // None
        // GroupId (STRING)
        data.AddRange(new byte[] { 0x00, 0x08 });
        data.AddRange("my-group"u8.ToArray());
        // GroupState (STRING)
        data.AddRange(new byte[] { 0x00, 0x06 });
        data.AddRange("Stable"u8.ToArray());
        // ProtocolType (STRING)
        data.AddRange(new byte[] { 0x00, 0x08 });
        data.AddRange("consumer"u8.ToArray());
        // ProtocolData (STRING)
        data.AddRange(new byte[] { 0x00, 0x05 });
        data.AddRange("range"u8.ToArray());
        // Members array (INT32 length = 1)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 });
        // Member:
        // MemberId (STRING)
        data.AddRange(new byte[] { 0x00, 0x06 });
        data.AddRange("mem-id"u8.ToArray());
        // No GroupInstanceId in v0
        // ClientId (STRING)
        data.AddRange(new byte[] { 0x00, 0x06 });
        data.AddRange("my-app"u8.ToArray());
        // ClientHost (STRING)
        data.AddRange(new byte[] { 0x00, 0x09 });
        data.AddRange("localhost"u8.ToArray());
        // MemberMetadata (BYTES with INT32 length = 0)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        // MemberAssignment (BYTES with INT32 length = 0)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (DescribeGroupsResponse)DescribeGroupsResponse.Read(ref reader, version: 0);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(0);
        await Assert.That(response.Groups.Count).IsEqualTo(1);

        var group = response.Groups[0];
        await Assert.That(group.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(group.GroupId).IsEqualTo("my-group");
        await Assert.That(group.GroupState).IsEqualTo("Stable");
        await Assert.That(group.ProtocolType).IsEqualTo("consumer");
        await Assert.That(group.ProtocolData).IsEqualTo("range");
        await Assert.That(group.Members.Count).IsEqualTo(1);
        await Assert.That(group.Members[0].MemberId).IsEqualTo("mem-id");
        await Assert.That(group.Members[0].GroupInstanceId).IsNull();
        await Assert.That(group.Members[0].ClientId).IsEqualTo("my-app");
        await Assert.That(group.Members[0].ClientHost).IsEqualTo("localhost");
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
    public async Task DescribeGroupsResponse_V3_WithAuthorizedOperations_CanBeParsed()
    {
        var data = new List<byte>();
        // ThrottleTimeMs
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        // Groups array (INT32 length = 1)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 });
        // Group entry:
        data.AddRange(new byte[] { 0x00, 0x00 }); // ErrorCode: None
        data.AddRange(new byte[] { 0x00, 0x02 }); // GroupId: "g1"
        data.AddRange("g1"u8.ToArray());
        data.AddRange(new byte[] { 0x00, 0x06 }); // GroupState: "Stable"
        data.AddRange("Stable"u8.ToArray());
        data.AddRange(new byte[] { 0x00, 0x08 }); // ProtocolType: "consumer"
        data.AddRange("consumer"u8.ToArray());
        data.AddRange(new byte[] { 0x00, 0x05 }); // ProtocolData: "range"
        data.AddRange("range"u8.ToArray());
        // Members (INT32 length = 0)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        // AuthorizedOperations (INT32) — v3+
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x0F }); // 15

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (DescribeGroupsResponse)DescribeGroupsResponse.Read(ref reader, version: 3);

        await Assert.That(response.Groups[0].AuthorizedOperations).IsEqualTo(15);
    }

    [Test]
    public async Task DescribeGroupsResponse_V4_WithGroupInstanceId_CanBeParsed()
    {
        var data = new List<byte>();
        // ThrottleTimeMs
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        // Groups array (INT32 length = 1)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 });
        // Group entry:
        data.AddRange(new byte[] { 0x00, 0x00 }); // ErrorCode
        data.AddRange(new byte[] { 0x00, 0x02 }); // GroupId: "g1"
        data.AddRange("g1"u8.ToArray());
        data.AddRange(new byte[] { 0x00, 0x06 }); // GroupState
        data.AddRange("Stable"u8.ToArray());
        data.AddRange(new byte[] { 0x00, 0x08 }); // ProtocolType
        data.AddRange("consumer"u8.ToArray());
        data.AddRange(new byte[] { 0x00, 0x05 }); // ProtocolData
        data.AddRange("range"u8.ToArray());
        // Members (INT32 length = 1)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 });
        // Member:
        data.AddRange(new byte[] { 0x00, 0x04 }); // MemberId: "mem1"
        data.AddRange("mem1"u8.ToArray());
        // GroupInstanceId (STRING) — v4+
        data.AddRange(new byte[] { 0x00, 0x08 }); // "static-1"
        data.AddRange("static-1"u8.ToArray());
        data.AddRange(new byte[] { 0x00, 0x05 }); // ClientId: "app-1"
        data.AddRange("app-1"u8.ToArray());
        data.AddRange(new byte[] { 0x00, 0x09 }); // ClientHost: "localhost"
        data.AddRange("localhost"u8.ToArray());
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 }); // MemberMetadata: empty
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 }); // MemberAssignment: empty
        // AuthorizedOperations (v3+)
        data.AddRange(new byte[] { 0x80, 0x00, 0x00, 0x00 }); // int.MinValue

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (DescribeGroupsResponse)DescribeGroupsResponse.Read(ref reader, version: 4);

        var member = response.Groups[0].Members[0];
        await Assert.That(member.MemberId).IsEqualTo("mem1");
        await Assert.That(member.GroupInstanceId).IsEqualTo("static-1");
        await Assert.That(member.ClientId).IsEqualTo("app-1");
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

    #endregion

    #region DeleteGroups Request Tests

    [Test]
    public async Task DeleteGroupsRequest_V0_Legacy_EncodedCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new DeleteGroupsRequest
        {
            GroupsNames = ["group-a", "group-b"]
        };
        request.Write(ref writer, version: 0);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        var arrayLength = reader.ReadInt32();
        var group1 = reader.ReadString();
        var group2 = reader.ReadString();

        await Assert.That(arrayLength).IsEqualTo(2);
        await Assert.That(group1).IsEqualTo("group-a");
        await Assert.That(group2).IsEqualTo("group-b");
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
    public async Task DeleteGroupsRequest_V0_SingleGroup_ByteLevel()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new DeleteGroupsRequest
        {
            GroupsNames = ["abc"]
        };
        request.Write(ref writer, version: 0);

        var expected = new List<byte>();
        // Array length (INT32) = 1
        expected.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 });
        // String "abc" (INT16 length prefix + bytes)
        expected.AddRange(new byte[] { 0x00, 0x03 });
        expected.AddRange("abc"u8.ToArray());

        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(expected.ToArray());
    }

    #endregion

    #region DeleteGroups Response Tests

    [Test]
    public async Task DeleteGroupsResponse_V0_Legacy_CanBeParsed()
    {
        var data = new List<byte>();
        // ThrottleTimeMs (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        // Results array (INT32 length = 1)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 });
        // Result: GroupId (STRING)
        data.AddRange(new byte[] { 0x00, 0x05 });
        data.AddRange("grp-1"u8.ToArray());
        // ErrorCode (INT16) = None
        data.AddRange(new byte[] { 0x00, 0x00 });

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (DeleteGroupsResponse)DeleteGroupsResponse.Read(ref reader, version: 0);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(0);
        await Assert.That(response.Results.Count).IsEqualTo(1);
        await Assert.That(response.Results[0].GroupId).IsEqualTo("grp-1");
        await Assert.That(response.Results[0].ErrorCode).IsEqualTo(ErrorCode.None);
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
    public async Task DeleteGroupsResponse_MultipleResults_CanBeParsed()
    {
        var data = new List<byte>();
        // ThrottleTimeMs
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        // Results array (INT32 length = 2)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x02 });
        // Result 1
        data.AddRange(new byte[] { 0x00, 0x02 }); // "g1"
        data.AddRange("g1"u8.ToArray());
        data.AddRange(new byte[] { 0x00, 0x00 }); // None
        // Result 2
        data.AddRange(new byte[] { 0x00, 0x02 }); // "g2"
        data.AddRange("g2"u8.ToArray());
        data.AddRange(new byte[] { 0x00, 0x44 }); // NonEmptyGroup

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (DeleteGroupsResponse)DeleteGroupsResponse.Read(ref reader, version: 0);

        await Assert.That(response.Results.Count).IsEqualTo(2);
        await Assert.That(response.Results[0].GroupId).IsEqualTo("g1");
        await Assert.That(response.Results[0].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.Results[1].GroupId).IsEqualTo("g2");
        await Assert.That(response.Results[1].ErrorCode).IsEqualTo(ErrorCode.NonEmptyGroup);
    }

    #endregion

    #region DeleteRecords Request Tests

    [Test]
    public async Task DeleteRecordsRequest_V0_Legacy_EncodedCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new DeleteRecordsRequest
        {
            Topics =
            [
                new DeleteRecordsRequestTopic
                {
                    Name = "my-topic",
                    Partitions =
                    [
                        new DeleteRecordsRequestPartition
                        {
                            PartitionIndex = 0,
                            Offset = 100
                        }
                    ]
                }
            ],
            TimeoutMs = 30000
        };
        request.Write(ref writer, version: 0);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        // Topics array (INT32)
        var topicsCount = reader.ReadInt32();
        // Topic name (STRING)
        var topicName = reader.ReadString();
        // Partitions array (INT32)
        var partitionsCount = reader.ReadInt32();
        // Partition: PartitionIndex (INT32)
        var partitionIndex = reader.ReadInt32();
        // Partition: Offset (INT64)
        var offset = reader.ReadInt64();
        // TimeoutMs (INT32)
        var timeoutMs = reader.ReadInt32();

        await Assert.That(topicsCount).IsEqualTo(1);
        await Assert.That(topicName).IsEqualTo("my-topic");
        await Assert.That(partitionsCount).IsEqualTo(1);
        await Assert.That(partitionIndex).IsEqualTo(0);
        await Assert.That(offset).IsEqualTo(100L);
        await Assert.That(timeoutMs).IsEqualTo(30000);
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
    public async Task DeleteRecordsRequest_V0_MultipleTopics_EncodedCorrectly()
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
                    Partitions = [new DeleteRecordsRequestPartition { PartitionIndex = 0, Offset = 10 }]
                },
                new DeleteRecordsRequestTopic
                {
                    Name = "t2",
                    Partitions = [new DeleteRecordsRequestPartition { PartitionIndex = 2, Offset = 20 }]
                }
            ],
            TimeoutMs = 5000
        };
        request.Write(ref writer, version: 0);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        var topicsCount = reader.ReadInt32();
        // Topic 1
        var name1 = reader.ReadString();
        var parts1Count = reader.ReadInt32();
        var idx1 = reader.ReadInt32();
        var off1 = reader.ReadInt64();
        // Topic 2
        var name2 = reader.ReadString();
        var parts2Count = reader.ReadInt32();
        var idx2 = reader.ReadInt32();
        var off2 = reader.ReadInt64();
        // TimeoutMs
        var timeoutMs = reader.ReadInt32();

        await Assert.That(topicsCount).IsEqualTo(2);
        await Assert.That(name1).IsEqualTo("t1");
        await Assert.That(parts1Count).IsEqualTo(1);
        await Assert.That(idx1).IsEqualTo(0);
        await Assert.That(off1).IsEqualTo(10L);
        await Assert.That(name2).IsEqualTo("t2");
        await Assert.That(parts2Count).IsEqualTo(1);
        await Assert.That(idx2).IsEqualTo(2);
        await Assert.That(off2).IsEqualTo(20L);
        await Assert.That(timeoutMs).IsEqualTo(5000);
    }

    #endregion

    #region DeleteRecords Response Tests

    [Test]
    public async Task DeleteRecordsResponse_V0_Legacy_CanBeParsed()
    {
        var data = new List<byte>();
        // ThrottleTimeMs (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        // Topics array (INT32 length = 1)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 });
        // Topic: Name (STRING)
        data.AddRange(new byte[] { 0x00, 0x08 });
        data.AddRange("my-topic"u8.ToArray());
        // Partitions array (INT32 length = 1)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 });
        // Partition: PartitionIndex (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 }); // 0
        // LowWatermark (INT64)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x64 }); // 100
        // ErrorCode (INT16) = None
        data.AddRange(new byte[] { 0x00, 0x00 });

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (DeleteRecordsResponse)DeleteRecordsResponse.Read(ref reader, version: 0);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(0);
        await Assert.That(response.Topics.Count).IsEqualTo(1);
        await Assert.That(response.Topics[0].Name).IsEqualTo("my-topic");
        await Assert.That(response.Topics[0].Partitions.Count).IsEqualTo(1);
        await Assert.That(response.Topics[0].Partitions[0].PartitionIndex).IsEqualTo(0);
        await Assert.That(response.Topics[0].Partitions[0].LowWatermark).IsEqualTo(100L);
        await Assert.That(response.Topics[0].Partitions[0].ErrorCode).IsEqualTo(ErrorCode.None);
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
    public async Task DeleteRecordsResponse_MultiplePartitions_CanBeParsed()
    {
        var data = new List<byte>();
        // ThrottleTimeMs
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        // Topics array (INT32 = 1)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 });
        // Topic name
        data.AddRange(new byte[] { 0x00, 0x02 }); data.AddRange("t1"u8.ToArray());
        // Partitions array (INT32 = 2)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x02 });
        // Partition 0
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 }); // index
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0A }); // low watermark = 10
        data.AddRange(new byte[] { 0x00, 0x00 }); // None
        // Partition 1
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 }); // index
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x14 }); // low watermark = 20
        data.AddRange(new byte[] { 0x00, 0x00 }); // None

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (DeleteRecordsResponse)DeleteRecordsResponse.Read(ref reader, version: 0);

        await Assert.That(response.Topics[0].Partitions.Count).IsEqualTo(2);
        await Assert.That(response.Topics[0].Partitions[0].PartitionIndex).IsEqualTo(0);
        await Assert.That(response.Topics[0].Partitions[0].LowWatermark).IsEqualTo(10L);
        await Assert.That(response.Topics[0].Partitions[1].PartitionIndex).IsEqualTo(1);
        await Assert.That(response.Topics[0].Partitions[1].LowWatermark).IsEqualTo(20L);
    }

    #endregion

    #region Version Flexibility Tests

    [Test]
    [Arguments((short)0, false)]
    [Arguments((short)1, false)]
    [Arguments((short)2, true)]
    [Arguments((short)3, true)]
    public async Task CreatePartitionsRequest_FlexibilityDetection(short version, bool expectedFlexible)
    {
        var isFlexible = CreatePartitionsRequest.IsFlexibleVersion(version);
        await Assert.That(isFlexible).IsEqualTo(expectedFlexible);
    }

    [Test]
    [Arguments((short)0, false)]
    [Arguments((short)2, false)]
    [Arguments((short)3, true)]
    [Arguments((short)5, true)]
    public async Task ListGroupsRequest_FlexibilityDetection(short version, bool expectedFlexible)
    {
        var isFlexible = ListGroupsRequest.IsFlexibleVersion(version);
        await Assert.That(isFlexible).IsEqualTo(expectedFlexible);
    }

    [Test]
    [Arguments((short)0, false)]
    [Arguments((short)4, false)]
    [Arguments((short)5, true)]
    public async Task DescribeGroupsRequest_FlexibilityDetection(short version, bool expectedFlexible)
    {
        var isFlexible = DescribeGroupsRequest.IsFlexibleVersion(version);
        await Assert.That(isFlexible).IsEqualTo(expectedFlexible);
    }

    [Test]
    [Arguments((short)0, false)]
    [Arguments((short)1, false)]
    [Arguments((short)2, true)]
    public async Task DeleteGroupsRequest_FlexibilityDetection(short version, bool expectedFlexible)
    {
        var isFlexible = DeleteGroupsRequest.IsFlexibleVersion(version);
        await Assert.That(isFlexible).IsEqualTo(expectedFlexible);
    }

    [Test]
    [Arguments((short)0, false)]
    [Arguments((short)1, false)]
    [Arguments((short)2, true)]
    public async Task DeleteRecordsRequest_FlexibilityDetection(short version, bool expectedFlexible)
    {
        var isFlexible = DeleteRecordsRequest.IsFlexibleVersion(version);
        await Assert.That(isFlexible).IsEqualTo(expectedFlexible);
    }

    #endregion

    #region Header Version Tests

    [Test]
    [Arguments((short)0, (short)1, (short)0)]
    [Arguments((short)1, (short)1, (short)0)]
    [Arguments((short)2, (short)2, (short)1)]
    [Arguments((short)3, (short)2, (short)1)]
    public async Task CreatePartitionsRequest_HeaderVersions(short apiVersion, short expectedRequestHeader, short expectedResponseHeader)
    {
        var requestHeaderVersion = CreatePartitionsRequest.GetRequestHeaderVersion(apiVersion);
        var responseHeaderVersion = CreatePartitionsRequest.GetResponseHeaderVersion(apiVersion);

        await Assert.That(requestHeaderVersion).IsEqualTo(expectedRequestHeader);
        await Assert.That(responseHeaderVersion).IsEqualTo(expectedResponseHeader);
    }

    [Test]
    [Arguments((short)0, (short)1, (short)0)]
    [Arguments((short)2, (short)1, (short)0)]
    [Arguments((short)3, (short)2, (short)1)]
    [Arguments((short)5, (short)2, (short)1)]
    public async Task ListGroupsRequest_HeaderVersions(short apiVersion, short expectedRequestHeader, short expectedResponseHeader)
    {
        var requestHeaderVersion = ListGroupsRequest.GetRequestHeaderVersion(apiVersion);
        var responseHeaderVersion = ListGroupsRequest.GetResponseHeaderVersion(apiVersion);

        await Assert.That(requestHeaderVersion).IsEqualTo(expectedRequestHeader);
        await Assert.That(responseHeaderVersion).IsEqualTo(expectedResponseHeader);
    }

    [Test]
    [Arguments((short)0, (short)1, (short)0)]
    [Arguments((short)4, (short)1, (short)0)]
    [Arguments((short)5, (short)2, (short)1)]
    public async Task DescribeGroupsRequest_HeaderVersions(short apiVersion, short expectedRequestHeader, short expectedResponseHeader)
    {
        var requestHeaderVersion = DescribeGroupsRequest.GetRequestHeaderVersion(apiVersion);
        var responseHeaderVersion = DescribeGroupsRequest.GetResponseHeaderVersion(apiVersion);

        await Assert.That(requestHeaderVersion).IsEqualTo(expectedRequestHeader);
        await Assert.That(responseHeaderVersion).IsEqualTo(expectedResponseHeader);
    }

    [Test]
    [Arguments((short)0, (short)1, (short)0)]
    [Arguments((short)1, (short)1, (short)0)]
    [Arguments((short)2, (short)2, (short)1)]
    public async Task DeleteGroupsRequest_HeaderVersions(short apiVersion, short expectedRequestHeader, short expectedResponseHeader)
    {
        var requestHeaderVersion = DeleteGroupsRequest.GetRequestHeaderVersion(apiVersion);
        var responseHeaderVersion = DeleteGroupsRequest.GetResponseHeaderVersion(apiVersion);

        await Assert.That(requestHeaderVersion).IsEqualTo(expectedRequestHeader);
        await Assert.That(responseHeaderVersion).IsEqualTo(expectedResponseHeader);
    }

    [Test]
    [Arguments((short)0, (short)1, (short)0)]
    [Arguments((short)1, (short)1, (short)0)]
    [Arguments((short)2, (short)2, (short)1)]
    public async Task DeleteRecordsRequest_HeaderVersions(short apiVersion, short expectedRequestHeader, short expectedResponseHeader)
    {
        var requestHeaderVersion = DeleteRecordsRequest.GetRequestHeaderVersion(apiVersion);
        var responseHeaderVersion = DeleteRecordsRequest.GetResponseHeaderVersion(apiVersion);

        await Assert.That(requestHeaderVersion).IsEqualTo(expectedRequestHeader);
        await Assert.That(responseHeaderVersion).IsEqualTo(expectedResponseHeader);
    }

    #endregion

    #region Request Write-Read Round-Trip Tests

    [Test]
    public async Task CreatePartitionsRequest_WriteRead_RoundTrip_V0()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new CreatePartitionsRequest
        {
            Topics =
            [
                new CreatePartitionsTopic
                {
                    Name = "round-trip",
                    Count = 12,
                    Assignments =
                    [
                        new CreatePartitionsAssignment { BrokerIds = [0, 1, 2] }
                    ]
                }
            ],
            TimeoutMs = 60000,
            ValidateOnly = true
        };
        request.Write(ref writer, version: 0);

        // Read back and verify structure
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        var topicsCount = reader.ReadInt32();
        var name = reader.ReadString();
        var count = reader.ReadInt32();
        var assignmentCount = reader.ReadInt32();
        var brokerIdCount = reader.ReadInt32();
        var b0 = reader.ReadInt32();
        var b1 = reader.ReadInt32();
        var b2 = reader.ReadInt32();
        var timeout = reader.ReadInt32();
        var validate = reader.ReadBoolean();

        await Assert.That(topicsCount).IsEqualTo(1);
        await Assert.That(name).IsEqualTo("round-trip");
        await Assert.That(count).IsEqualTo(12);
        await Assert.That(assignmentCount).IsEqualTo(1);
        await Assert.That(brokerIdCount).IsEqualTo(3);
        await Assert.That(b0).IsEqualTo(0);
        await Assert.That(b1).IsEqualTo(1);
        await Assert.That(b2).IsEqualTo(2);
        await Assert.That(timeout).IsEqualTo(60000);
        await Assert.That(validate).IsTrue();
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

    #endregion
}
