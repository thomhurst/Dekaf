using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

/// <summary>
/// Tests for ACL protocol message encoding/decoding.
/// </summary>
public class AclMessageEncodingTests
{
    #region DescribeAcls Request Tests

    [Test]
    public async Task DescribeAclsRequest_V0_EncodedCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new DescribeAclsRequest
        {
            ResourceTypeFilter = 2, // Topic
            ResourceNameFilter = "my-topic",
            PrincipalFilter = "User:alice",
            HostFilter = "*",
            Operation = 3, // Read
            PermissionType = 3 // Allow
        };
        request.Write(ref writer, version: 0);

        var expected = new List<byte>();
        // ResourceTypeFilter (INT8)
        expected.Add(0x02);
        // ResourceNameFilter (STRING with INT16 length prefix)
        expected.AddRange(new byte[] { 0x00, 0x08 }); // length = 8
        expected.AddRange("my-topic"u8.ToArray());
        // PrincipalFilter (STRING)
        expected.AddRange(new byte[] { 0x00, 0x0A }); // length = 10
        expected.AddRange("User:alice"u8.ToArray());
        // HostFilter (STRING)
        expected.AddRange(new byte[] { 0x00, 0x01 }); // length = 1
        expected.Add((byte)'*');
        // Operation (INT8)
        expected.Add(0x03);
        // PermissionType (INT8)
        expected.Add(0x03);

        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(expected.ToArray());
    }

    [Test]
    public async Task DescribeAclsRequest_V1_WithPatternType()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new DescribeAclsRequest
        {
            ResourceTypeFilter = 2, // Topic
            ResourceNameFilter = "my-topic",
            PatternTypeFilter = 4, // Prefixed
            PrincipalFilter = null,
            HostFilter = null,
            Operation = 1, // Any
            PermissionType = 1 // Any
        };
        request.Write(ref writer, version: 1);

        // Parse the written data to verify
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        var resourceType = reader.ReadInt8();
        var resourceName = reader.ReadString();
        var patternType = reader.ReadInt8();
        var principal = reader.ReadString();
        var host = reader.ReadString();
        var operation = reader.ReadInt8();
        var permissionType = reader.ReadInt8();

        await Assert.That(resourceType).IsEqualTo((sbyte)2);
        await Assert.That(resourceName).IsEqualTo("my-topic");
        await Assert.That(patternType).IsEqualTo((sbyte)4);
        await Assert.That(principal).IsNull();
        await Assert.That(host).IsNull();
        await Assert.That(operation).IsEqualTo((sbyte)1);
        await Assert.That(permissionType).IsEqualTo((sbyte)1);
    }

    [Test]
    public async Task DescribeAclsRequest_V2_Flexible()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new DescribeAclsRequest
        {
            ResourceTypeFilter = 3, // Group
            ResourceNameFilter = "my-group",
            PatternTypeFilter = 3, // Literal
            PrincipalFilter = "User:bob",
            HostFilter = "192.168.1.1",
            Operation = 3, // Read
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

        await Assert.That(resourceType).IsEqualTo((sbyte)3);
        await Assert.That(resourceName).IsEqualTo("my-group");
        await Assert.That(patternType).IsEqualTo((sbyte)3);
        await Assert.That(principal).IsEqualTo("User:bob");
        await Assert.That(host).IsEqualTo("192.168.1.1");
        await Assert.That(operation).IsEqualTo((sbyte)3);
        await Assert.That(permissionType).IsEqualTo((sbyte)3);
    }

    #endregion

    #region DescribeAcls Response Tests

    [Test]
    public async Task DescribeAclsResponse_V0_CanBeParsed()
    {
        var data = new List<byte>();
        // ThrottleTimeMs (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        // ErrorCode (INT16)
        data.AddRange(new byte[] { 0x00, 0x00 }); // None
        // ErrorMessage (nullable STRING)
        data.AddRange(new byte[] { 0xFF, 0xFF }); // null
        // Resources array (INT32 length + entries)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 }); // 1 entry
        // Resource entry:
        // ResourceType (INT8)
        data.Add(0x02); // Topic
        // ResourceName (STRING)
        data.AddRange(new byte[] { 0x00, 0x05 });
        data.AddRange("topic"u8.ToArray());
        // ACLs array
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 }); // 1 ACL
        // ACL entry:
        // Principal (STRING)
        data.AddRange(new byte[] { 0x00, 0x0A });
        data.AddRange("User:alice"u8.ToArray());
        // Host (STRING)
        data.AddRange(new byte[] { 0x00, 0x01 });
        data.Add((byte)'*');
        // Operation (INT8)
        data.Add(0x03); // Read
        // PermissionType (INT8)
        data.Add(0x03); // Allow

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (DescribeAclsResponse)DescribeAclsResponse.Read(ref reader, version: 0);

        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.Resources.Count).IsEqualTo(1);
        await Assert.That(response.Resources[0].ResourceType).IsEqualTo((sbyte)2);
        await Assert.That(response.Resources[0].ResourceName).IsEqualTo("topic");
        await Assert.That(response.Resources[0].Acls.Count).IsEqualTo(1);
        await Assert.That(response.Resources[0].Acls[0].Principal).IsEqualTo("User:alice");
        await Assert.That(response.Resources[0].Acls[0].Host).IsEqualTo("*");
        await Assert.That(response.Resources[0].Acls[0].Operation).IsEqualTo((sbyte)3);
        await Assert.That(response.Resources[0].Acls[0].PermissionType).IsEqualTo((sbyte)3);
    }

    [Test]
    public async Task DescribeAclsResponse_V2_Flexible_CanBeParsed()
    {
        var data = new List<byte>();
        // ThrottleTimeMs (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x64 }); // 100ms
        // ErrorCode (INT16)
        data.AddRange(new byte[] { 0x00, 0x00 }); // None
        // ErrorMessage (COMPACT_NULLABLE_STRING)
        data.Add(0x00); // null
        // Resources COMPACT_ARRAY (length+1 = 2, so 1 entry)
        data.Add(0x02);
        // Resource entry:
        // ResourceType (INT8)
        data.Add(0x02); // Topic
        // ResourceName (COMPACT_STRING)
        data.Add(0x06); // length+1 = 6
        data.AddRange("topic"u8.ToArray());
        // PatternType (INT8) - v1+
        data.Add(0x03); // Literal
        // ACLs COMPACT_ARRAY
        data.Add(0x02); // 1 ACL
        // ACL entry:
        // Principal (COMPACT_STRING)
        data.Add(0x0B); // length+1 = 11
        data.AddRange("User:alice"u8.ToArray());
        // Host (COMPACT_STRING)
        data.Add(0x02);
        data.Add((byte)'*');
        // Operation (INT8)
        data.Add(0x03); // Read
        // PermissionType (INT8)
        data.Add(0x03); // Allow
        // ACL tagged fields
        data.Add(0x00);
        // Resource tagged fields
        data.Add(0x00);
        // Response tagged fields
        data.Add(0x00);

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (DescribeAclsResponse)DescribeAclsResponse.Read(ref reader, version: 2);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(100);
        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.Resources.Count).IsEqualTo(1);
        await Assert.That(response.Resources[0].ResourceType).IsEqualTo((sbyte)2);
        await Assert.That(response.Resources[0].PatternType).IsEqualTo((sbyte)3);
        await Assert.That(response.Resources[0].Acls[0].Principal).IsEqualTo("User:alice");
    }

    #endregion

    #region CreateAcls Request Tests

    [Test]
    public async Task CreateAclsRequest_V0_EncodedCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new CreateAclsRequest
        {
            Creations =
            [
                new AclCreation
                {
                    ResourceType = 2, // Topic
                    ResourceName = "my-topic",
                    Principal = "User:alice",
                    Host = "*",
                    Operation = 4, // Write
                    PermissionType = 3 // Allow
                }
            ]
        };
        request.Write(ref writer, version: 0);

        // Parse to verify
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        var arrayLength = reader.ReadInt32();
        var resourceType = reader.ReadInt8();
        var resourceName = reader.ReadString();
        var principal = reader.ReadString();
        var host = reader.ReadString();
        var operation = reader.ReadInt8();
        var permissionType = reader.ReadInt8();

        await Assert.That(arrayLength).IsEqualTo(1);
        await Assert.That(resourceType).IsEqualTo((sbyte)2);
        await Assert.That(resourceName).IsEqualTo("my-topic");
        await Assert.That(principal).IsEqualTo("User:alice");
        await Assert.That(host).IsEqualTo("*");
        await Assert.That(operation).IsEqualTo((sbyte)4);
        await Assert.That(permissionType).IsEqualTo((sbyte)3);
    }

    [Test]
    public async Task CreateAclsRequest_V1_WithPatternType()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new CreateAclsRequest
        {
            Creations =
            [
                new AclCreation
                {
                    ResourceType = 2, // Topic
                    ResourceName = "my-prefix",
                    ResourcePatternType = 4, // Prefixed
                    Principal = "User:alice",
                    Host = "*",
                    Operation = 3, // Read
                    PermissionType = 3 // Allow
                }
            ]
        };
        request.Write(ref writer, version: 1);

        // Parse to verify
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        _ = reader.ReadInt32(); // array length
        _ = reader.ReadInt8(); // resource type
        _ = reader.ReadString(); // resource name
        var resourcePatternType = reader.ReadInt8();

        await Assert.That(resourcePatternType).IsEqualTo((sbyte)4);
    }

    [Test]
    public async Task CreateAclsRequest_V2_Flexible()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new CreateAclsRequest
        {
            Creations =
            [
                new AclCreation
                {
                    ResourceType = 3, // Group
                    ResourceName = "my-group",
                    ResourcePatternType = 3, // Literal
                    Principal = "User:bob",
                    Host = "*",
                    Operation = 3, // Read
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

        await Assert.That(arrayLengthPlus1).IsEqualTo(2); // length+1 = 2 means 1 element
        await Assert.That(resourceType).IsEqualTo((sbyte)3);
        await Assert.That(resourceName).IsEqualTo("my-group");
        await Assert.That(principal).IsEqualTo("User:bob");
    }

    #endregion

    #region CreateAcls Response Tests

    [Test]
    public async Task CreateAclsResponse_V0_CanBeParsed()
    {
        var data = new List<byte>();
        // ThrottleTimeMs (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        // Results array (INT32 length + entries)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 }); // 1 result
        // Result entry:
        // ErrorCode (INT16)
        data.AddRange(new byte[] { 0x00, 0x00 }); // None
        // ErrorMessage (nullable STRING)
        data.AddRange(new byte[] { 0xFF, 0xFF }); // null

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (CreateAclsResponse)CreateAclsResponse.Read(ref reader, version: 0);

        await Assert.That(response.Results.Count).IsEqualTo(1);
        await Assert.That(response.Results[0].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.Results[0].ErrorMessage).IsNull();
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

    #endregion

    #region DeleteAcls Request Tests

    [Test]
    public async Task DeleteAclsRequest_V0_EncodedCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new DeleteAclsRequest
        {
            Filters =
            [
                new DeleteAclsFilter
                {
                    ResourceTypeFilter = 2, // Topic
                    ResourceNameFilter = "my-topic",
                    PrincipalFilter = "User:alice",
                    HostFilter = null,
                    Operation = 1, // Any
                    PermissionType = 1 // Any
                }
            ]
        };
        request.Write(ref writer, version: 0);

        // Parse to verify
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        var arrayLength = reader.ReadInt32();
        var resourceType = reader.ReadInt8();
        var resourceName = reader.ReadString();
        var principal = reader.ReadString();
        var host = reader.ReadString();
        var operation = reader.ReadInt8();
        var permissionType = reader.ReadInt8();

        await Assert.That(arrayLength).IsEqualTo(1);
        await Assert.That(resourceType).IsEqualTo((sbyte)2);
        await Assert.That(resourceName).IsEqualTo("my-topic");
        await Assert.That(principal).IsEqualTo("User:alice");
        await Assert.That(host).IsNull();
        await Assert.That(operation).IsEqualTo((sbyte)1);
        await Assert.That(permissionType).IsEqualTo((sbyte)1);
    }

    [Test]
    public async Task DeleteAclsRequest_V2_Flexible()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new DeleteAclsRequest
        {
            Filters =
            [
                new DeleteAclsFilter
                {
                    ResourceTypeFilter = 1, // Any
                    ResourceNameFilter = null,
                    PatternTypeFilter = 1, // Any
                    PrincipalFilter = "User:alice",
                    HostFilter = null,
                    Operation = 1, // Any
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

        await Assert.That(arrayLengthPlus1).IsEqualTo(2);
        await Assert.That(resourceType).IsEqualTo((sbyte)1);
        await Assert.That(resourceName).IsNull();
        await Assert.That(patternType).IsEqualTo((sbyte)1);
        await Assert.That(principal).IsEqualTo("User:alice");
        await Assert.That(host).IsNull();
    }

    #endregion

    #region DeleteAcls Response Tests

    [Test]
    public async Task DeleteAclsResponse_V0_CanBeParsed()
    {
        var data = new List<byte>();
        // ThrottleTimeMs (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        // FilterResults array (INT32 length + entries)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 }); // 1 result
        // FilterResult entry:
        // ErrorCode (INT16)
        data.AddRange(new byte[] { 0x00, 0x00 }); // None
        // ErrorMessage (nullable STRING)
        data.AddRange(new byte[] { 0xFF, 0xFF }); // null
        // MatchingAcls array
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 }); // 1 ACL
        // MatchingAcl entry:
        // ErrorCode (INT16)
        data.AddRange(new byte[] { 0x00, 0x00 }); // None
        // ErrorMessage (nullable STRING)
        data.AddRange(new byte[] { 0xFF, 0xFF }); // null
        // ResourceType (INT8)
        data.Add(0x02); // Topic
        // ResourceName (STRING)
        data.AddRange(new byte[] { 0x00, 0x08 });
        data.AddRange("my-topic"u8.ToArray());
        // Principal (STRING)
        data.AddRange(new byte[] { 0x00, 0x0A });
        data.AddRange("User:alice"u8.ToArray());
        // Host (STRING)
        data.AddRange(new byte[] { 0x00, 0x01 });
        data.Add((byte)'*');
        // Operation (INT8)
        data.Add(0x04); // Write
        // PermissionType (INT8)
        data.Add(0x03); // Allow

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (DeleteAclsResponse)DeleteAclsResponse.Read(ref reader, version: 0);

        await Assert.That(response.FilterResults.Count).IsEqualTo(1);
        await Assert.That(response.FilterResults[0].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.FilterResults[0].MatchingAcls.Count).IsEqualTo(1);
        await Assert.That(response.FilterResults[0].MatchingAcls[0].ResourceType).IsEqualTo((sbyte)2);
        await Assert.That(response.FilterResults[0].MatchingAcls[0].ResourceName).IsEqualTo("my-topic");
        await Assert.That(response.FilterResults[0].MatchingAcls[0].Principal).IsEqualTo("User:alice");
    }

    [Test]
    public async Task DeleteAclsResponse_V2_Flexible_CanBeParsed()
    {
        var data = new List<byte>();
        // ThrottleTimeMs (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        // FilterResults COMPACT_ARRAY
        data.Add(0x02); // 1 result
        // FilterResult entry:
        // ErrorCode (INT16)
        data.AddRange(new byte[] { 0x00, 0x00 }); // None
        // ErrorMessage (COMPACT_NULLABLE_STRING)
        data.Add(0x00); // null
        // MatchingAcls COMPACT_ARRAY
        data.Add(0x02); // 1 ACL
        // MatchingAcl entry:
        // ErrorCode (INT16)
        data.AddRange(new byte[] { 0x00, 0x00 }); // None
        // ErrorMessage (COMPACT_NULLABLE_STRING)
        data.Add(0x00); // null
        // ResourceType (INT8)
        data.Add(0x02); // Topic
        // ResourceName (COMPACT_STRING)
        data.Add(0x09); // length+1 = 9
        data.AddRange("my-topic"u8.ToArray());
        // PatternType (INT8) - v1+
        data.Add(0x03); // Literal
        // Principal (COMPACT_STRING)
        data.Add(0x0B); // length+1 = 11
        data.AddRange("User:alice"u8.ToArray());
        // Host (COMPACT_STRING)
        data.Add(0x02);
        data.Add((byte)'*');
        // Operation (INT8)
        data.Add(0x04); // Write
        // PermissionType (INT8)
        data.Add(0x03); // Allow
        // MatchingAcl tagged fields
        data.Add(0x00);
        // FilterResult tagged fields
        data.Add(0x00);
        // Response tagged fields
        data.Add(0x00);

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (DeleteAclsResponse)DeleteAclsResponse.Read(ref reader, version: 2);

        await Assert.That(response.FilterResults[0].MatchingAcls[0].PatternType).IsEqualTo((sbyte)3);
        await Assert.That(response.FilterResults[0].MatchingAcls[0].Principal).IsEqualTo("User:alice");
    }

    #endregion

    #region Version Flexibility Tests

    [Test]
    [Arguments((short)0, false)]
    [Arguments((short)1, false)]
    [Arguments((short)2, true)]
    [Arguments((short)3, true)]
    public async Task DescribeAclsRequest_FlexibilityDetection(short version, bool expectedFlexible)
    {
        var isFlexible = DescribeAclsRequest.IsFlexibleVersion(version);
        await Assert.That(isFlexible).IsEqualTo(expectedFlexible);
    }

    [Test]
    [Arguments((short)0, false)]
    [Arguments((short)1, false)]
    [Arguments((short)2, true)]
    [Arguments((short)3, true)]
    public async Task CreateAclsRequest_FlexibilityDetection(short version, bool expectedFlexible)
    {
        var isFlexible = CreateAclsRequest.IsFlexibleVersion(version);
        await Assert.That(isFlexible).IsEqualTo(expectedFlexible);
    }

    [Test]
    [Arguments((short)0, false)]
    [Arguments((short)1, false)]
    [Arguments((short)2, true)]
    [Arguments((short)3, true)]
    public async Task DeleteAclsRequest_FlexibilityDetection(short version, bool expectedFlexible)
    {
        var isFlexible = DeleteAclsRequest.IsFlexibleVersion(version);
        await Assert.That(isFlexible).IsEqualTo(expectedFlexible);
    }

    #endregion

    #region Header Version Tests

    [Test]
    [Arguments((short)0, (short)1, (short)0)]
    [Arguments((short)1, (short)1, (short)0)]
    [Arguments((short)2, (short)2, (short)1)]
    [Arguments((short)3, (short)2, (short)1)]
    public async Task DescribeAclsRequest_HeaderVersions(short apiVersion, short expectedRequestHeader, short expectedResponseHeader)
    {
        var requestHeaderVersion = DescribeAclsRequest.GetRequestHeaderVersion(apiVersion);
        var responseHeaderVersion = DescribeAclsRequest.GetResponseHeaderVersion(apiVersion);

        await Assert.That(requestHeaderVersion).IsEqualTo(expectedRequestHeader);
        await Assert.That(responseHeaderVersion).IsEqualTo(expectedResponseHeader);
    }

    [Test]
    [Arguments((short)0, (short)1, (short)0)]
    [Arguments((short)1, (short)1, (short)0)]
    [Arguments((short)2, (short)2, (short)1)]
    [Arguments((short)3, (short)2, (short)1)]
    public async Task CreateAclsRequest_HeaderVersions(short apiVersion, short expectedRequestHeader, short expectedResponseHeader)
    {
        var requestHeaderVersion = CreateAclsRequest.GetRequestHeaderVersion(apiVersion);
        var responseHeaderVersion = CreateAclsRequest.GetResponseHeaderVersion(apiVersion);

        await Assert.That(requestHeaderVersion).IsEqualTo(expectedRequestHeader);
        await Assert.That(responseHeaderVersion).IsEqualTo(expectedResponseHeader);
    }

    [Test]
    [Arguments((short)0, (short)1, (short)0)]
    [Arguments((short)1, (short)1, (short)0)]
    [Arguments((short)2, (short)2, (short)1)]
    [Arguments((short)3, (short)2, (short)1)]
    public async Task DeleteAclsRequest_HeaderVersions(short apiVersion, short expectedRequestHeader, short expectedResponseHeader)
    {
        var requestHeaderVersion = DeleteAclsRequest.GetRequestHeaderVersion(apiVersion);
        var responseHeaderVersion = DeleteAclsRequest.GetResponseHeaderVersion(apiVersion);

        await Assert.That(requestHeaderVersion).IsEqualTo(expectedRequestHeader);
        await Assert.That(responseHeaderVersion).IsEqualTo(expectedResponseHeader);
    }

    #endregion
}
