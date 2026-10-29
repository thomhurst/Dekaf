using System.Buffers;
using Dekaf.Admin;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

/// <summary>
/// Tests for ACL protocol message encoding/decoding.
/// </summary>
public class AclMessageEncodingTests
{
    [Test]
    [Arguments((sbyte)3, (sbyte)3)]
    [Arguments((sbyte)7, (sbyte)13)]
    [Arguments((sbyte)7, (sbyte)14)]
    [Arguments((sbyte)5, (sbyte)15)]
    public async Task DescribeAclsRequest_V2_Flexible(sbyte resourceTypeCode, sbyte operationCode)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new DescribeAclsRequest
        {
            ResourceTypeFilter = resourceTypeCode,
            ResourceNameFilter = "my-group",
            PatternTypeFilter = 3, // Literal
            PrincipalFilter = "User:bob",
            HostFilter = "192.168.1.1",
            Operation = operationCode,
            PermissionType = 3 // Allow
        };
        request.Write(ref writer, version: 2);

        // Parse to verify
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        var resourceType = reader.ReadInt8();
        var resourceName = reader.ReadCompactString();
        var patternType = reader.ReadInt8();
        var principal = reader.ReadCompactString();
        var host = reader.ReadCompactString();
        var operation = reader.ReadInt8();
        var permissionType = reader.ReadInt8();
        reader.SkipTaggedFields();

        await Assert.That(resourceType).IsEqualTo(resourceTypeCode);
        await Assert.That(resourceName).IsEqualTo("my-group");
        await Assert.That(patternType).IsEqualTo((sbyte)3);
        await Assert.That(principal).IsEqualTo("User:bob");
        await Assert.That(host).IsEqualTo("192.168.1.1");
        await Assert.That(operation).IsEqualTo(operationCode);
        await Assert.That(permissionType).IsEqualTo((sbyte)3);
    }

    [Test]
    [Arguments((sbyte)2, (sbyte)3)]
    [Arguments((sbyte)7, (sbyte)13)]
    [Arguments((sbyte)7, (sbyte)14)]
    [Arguments((sbyte)5, (sbyte)15)]
    public async Task DescribeAclsResponse_V2_Flexible_CanBeParsed(sbyte resourceTypeCode, sbyte operationCode)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(100); // throttle time
        writer.WriteInt16(0); // error code
        writer.WriteCompactString(null); // error message
        writer.WriteUnsignedVarInt(2); // one resource
        writer.WriteInt8(resourceTypeCode);
        writer.WriteCompactString("topic");
        writer.WriteInt8(3); // Literal
        writer.WriteUnsignedVarInt(2); // one ACL
        writer.WriteCompactString("User:alice");
        writer.WriteCompactString("*");
        writer.WriteInt8(operationCode);
        writer.WriteInt8(3); // Allow
        writer.WriteUnsignedVarInt(0); // ACL tagged fields
        writer.WriteUnsignedVarInt(0); // resource tagged fields
        writer.WriteUnsignedVarInt(0); // response tagged fields

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (DescribeAclsResponse)DescribeAclsResponse.Read(ref reader, version: 2);

        await Assert.That((ResourceType)response.Resources[0].ResourceType).IsEqualTo((ResourceType)resourceTypeCode);
        await Assert.That((AclOperation)response.Resources[0].Acls[0].Operation).IsEqualTo((AclOperation)operationCode);
        await Assert.That(response.ThrottleTimeMs).IsEqualTo(100);
        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.Resources.Count).IsEqualTo(1);
        await Assert.That(response.Resources[0].PatternType).IsEqualTo((sbyte)3);
        await Assert.That(response.Resources[0].Acls[0].Principal).IsEqualTo("User:alice");
    }

    [Test]
    [Arguments((sbyte)3, (sbyte)3)]
    [Arguments((sbyte)7, (sbyte)13)]
    [Arguments((sbyte)7, (sbyte)14)]
    [Arguments((sbyte)5, (sbyte)15)]
    public async Task CreateAclsRequest_V2_Flexible(sbyte resourceTypeCode, sbyte operationCode)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new CreateAclsRequest
        {
            Creations =
            [
                new AclCreation
                {
                    ResourceType = resourceTypeCode,
                    ResourceName = "my-group",
                    ResourcePatternType = 3, // Literal
                    Principal = "User:bob",
                    Host = "*",
                    Operation = operationCode,
                    PermissionType = 3 // Allow
                }
            ]
        };
        request.Write(ref writer, version: 2);

        // Parse to verify
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        var arrayLengthPlus1 = reader.ReadUnsignedVarInt();
        var resourceType = reader.ReadInt8();
        var resourceName = reader.ReadCompactString();
        _ = reader.ReadInt8(); // pattern type
        var principal = reader.ReadCompactString();
        _ = reader.ReadCompactString(); // host
        var operation = reader.ReadInt8();

        await Assert.That(arrayLengthPlus1).IsEqualTo(2); // length+1 = 2 means 1 element
        await Assert.That(resourceType).IsEqualTo(resourceTypeCode);
        await Assert.That(resourceName).IsEqualTo("my-group");
        await Assert.That(principal).IsEqualTo("User:bob");
        await Assert.That(operation).IsEqualTo(operationCode);
    }

    [Test]
    public async Task CreateAclsResponse_V2_Flexible_CanBeParsed()
    {
        var data = new List<byte>();
        // ThrottleTimeMs (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x32 }); // 50ms
        // Results COMPACT_ARRAY (length+1 = 2, so 1 entry)
        data.Add(0x02);
        // Result entry:
        // ErrorCode (INT16)
        data.AddRange(new byte[] { 0x00, 0x00 }); // None
        // ErrorMessage (COMPACT_NULLABLE_STRING)
        data.Add(0x00); // null
        // Result tagged fields
        data.Add(0x00);
        // Response tagged fields
        data.Add(0x00);

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (CreateAclsResponse)CreateAclsResponse.Read(ref reader, version: 2);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(50);
        await Assert.That(response.Results.Count).IsEqualTo(1);
        await Assert.That(response.Results[0].ErrorCode).IsEqualTo(ErrorCode.None);
    }

    [Test]
    public async Task CreateAclsResponse_WithError_CanBeParsed()
    {
        var data = new List<byte>();
        // ThrottleTimeMs (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        // Results COMPACT_ARRAY
        data.Add(0x02);
        // Result entry:
        // ErrorCode (INT16) - CLUSTER_AUTHORIZATION_FAILED = 31
        data.AddRange(new byte[] { 0x00, 0x1F });
        // ErrorMessage (COMPACT_NULLABLE_STRING)
        data.Add(0x1B); // length+1 = 27
        data.AddRange("Cluster authorization failed"u8.ToArray().AsSpan(0, 26).ToArray());
        // Result tagged fields
        data.Add(0x00);
        // Response tagged fields
        data.Add(0x00);

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (CreateAclsResponse)CreateAclsResponse.Read(ref reader, version: 2);

        await Assert.That(response.Results[0].ErrorCode).IsEqualTo(ErrorCode.ClusterAuthorizationFailed);
        await Assert.That(response.Results[0].ErrorMessage).IsNotNull();
    }

    [Test]
    [Arguments((sbyte)1, (sbyte)1)]
    [Arguments((sbyte)7, (sbyte)13)]
    [Arguments((sbyte)7, (sbyte)14)]
    [Arguments((sbyte)5, (sbyte)15)]
    public async Task DeleteAclsRequest_V2_Flexible(sbyte resourceTypeCode, sbyte operationCode)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new DeleteAclsRequest
        {
            Filters =
            [
                new DeleteAclsFilter
                {
                    ResourceTypeFilter = resourceTypeCode,
                    ResourceNameFilter = null,
                    PatternTypeFilter = 1, // Any
                    PrincipalFilter = "User:alice",
                    HostFilter = null,
                    Operation = operationCode,
                    PermissionType = 1 // Any
                }
            ]
        };
        request.Write(ref writer, version: 2);

        // Parse to verify
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        var arrayLengthPlus1 = reader.ReadUnsignedVarInt();
        var resourceType = reader.ReadInt8();
        var resourceName = reader.ReadCompactString();
        var patternType = reader.ReadInt8();
        var principal = reader.ReadCompactString();
        var host = reader.ReadCompactString();
        var operation = reader.ReadInt8();

        await Assert.That(arrayLengthPlus1).IsEqualTo(2);
        await Assert.That(resourceType).IsEqualTo(resourceTypeCode);
        await Assert.That(resourceName).IsNull();
        await Assert.That(patternType).IsEqualTo((sbyte)1);
        await Assert.That(principal).IsEqualTo("User:alice");
        await Assert.That(host).IsNull();
        await Assert.That(operation).IsEqualTo(operationCode);
    }

    [Test]
    [Arguments((sbyte)2, (sbyte)4)]
    [Arguments((sbyte)7, (sbyte)13)]
    [Arguments((sbyte)7, (sbyte)14)]
    [Arguments((sbyte)5, (sbyte)15)]
    public async Task DeleteAclsResponse_V2_Flexible_CanBeParsed(sbyte resourceTypeCode, sbyte operationCode)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(0); // throttle time
        writer.WriteUnsignedVarInt(2); // one filter result
        writer.WriteInt16(0); // filter error code
        writer.WriteCompactString(null); // filter error message
        writer.WriteUnsignedVarInt(2); // one matching ACL
        writer.WriteInt16(0); // ACL error code
        writer.WriteCompactString(null); // ACL error message
        writer.WriteInt8(resourceTypeCode);
        writer.WriteCompactString("my-topic");
        writer.WriteInt8(3); // Literal
        writer.WriteCompactString("User:alice");
        writer.WriteCompactString("*");
        writer.WriteInt8(operationCode);
        writer.WriteInt8(3); // Allow
        writer.WriteUnsignedVarInt(0); // ACL tagged fields
        writer.WriteUnsignedVarInt(0); // filter result tagged fields
        writer.WriteUnsignedVarInt(0); // response tagged fields

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (DeleteAclsResponse)DeleteAclsResponse.Read(ref reader, version: 2);

        await Assert.That((ResourceType)response.FilterResults[0].MatchingAcls[0].ResourceType).IsEqualTo((ResourceType)resourceTypeCode);
        await Assert.That((AclOperation)response.FilterResults[0].MatchingAcls[0].Operation).IsEqualTo((AclOperation)operationCode);
        await Assert.That(response.FilterResults[0].MatchingAcls[0].PatternType).IsEqualTo((sbyte)3);
        await Assert.That(response.FilterResults[0].MatchingAcls[0].Principal).IsEqualTo("User:alice");
    }

}
