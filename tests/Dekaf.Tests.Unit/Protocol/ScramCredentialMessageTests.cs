using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

/// <summary>
/// Tests for SCRAM credential protocol message encoding/decoding.
/// </summary>
public class ScramCredentialMessageTests
{
    #region DescribeUserScramCredentialsRequest Tests

    [Test]
    public async Task DescribeUserScramCredentialsRequest_HasCorrectApiKey()
    {
        await Assert.That(DescribeUserScramCredentialsRequest.ApiKey).IsEqualTo(ApiKey.DescribeUserScramCredentials);
    }

    [Test]
    public async Task DescribeUserScramCredentialsRequest_IsFlexibleVersion()
    {
        await Assert.That(DescribeUserScramCredentialsRequest.IsFlexibleVersion(0)).IsTrue();
    }

    [Test]
    public async Task DescribeUserScramCredentialsRequest_HeaderVersions()
    {
        await Assert.That(DescribeUserScramCredentialsRequest.GetRequestHeaderVersion(0)).IsEqualTo((short)2);
        await Assert.That(DescribeUserScramCredentialsRequest.GetResponseHeaderVersion(0)).IsEqualTo((short)1);
    }

    [Test]
    public async Task DescribeUserScramCredentialsRequest_NullUsers_EncodesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new DescribeUserScramCredentialsRequest
        {
            Users = null
        };
        request.Write(ref writer, version: 0);

        // Should write: 0 (null compact array) + 0 (empty tagged fields)
        await Assert.That(buffer.WrittenCount).IsEqualTo(2);
        await Assert.That(buffer.WrittenSpan[0]).IsEqualTo((byte)0); // null array
        await Assert.That(buffer.WrittenSpan[1]).IsEqualTo((byte)0); // empty tagged fields
    }

    [Test]
    public async Task DescribeUserScramCredentialsRequest_SingleUser_EncodesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new DescribeUserScramCredentialsRequest
        {
            Users = [new UserName { Name = "alice" }]
        };
        request.Write(ref writer, version: 0);

        var expected = new List<byte>();
        // COMPACT_ARRAY length+1 = 2 (1 element)
        expected.Add(0x02);
        // UserName entry:
        // Name: COMPACT_STRING "alice" (length+1 = 6)
        expected.Add(0x06);
        expected.AddRange("alice"u8.ToArray());
        // UserName tagged fields
        expected.Add(0x00);
        // Request tagged fields
        expected.Add(0x00);

        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(expected.ToArray());
    }

    [Test]
    public async Task DescribeUserScramCredentialsRequest_MultipleUsers_EncodesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new DescribeUserScramCredentialsRequest
        {
            Users = [
                new UserName { Name = "alice" },
                new UserName { Name = "bob" }
            ]
        };
        request.Write(ref writer, version: 0);

        var expected = new List<byte>();
        // COMPACT_ARRAY length+1 = 3 (2 elements)
        expected.Add(0x03);
        // First user: "alice"
        expected.Add(0x06); // length+1 = 6
        expected.AddRange("alice"u8.ToArray());
        expected.Add(0x00); // tagged fields
        // Second user: "bob"
        expected.Add(0x04); // length+1 = 4
        expected.AddRange("bob"u8.ToArray());
        expected.Add(0x00); // tagged fields
        // Request tagged fields
        expected.Add(0x00);

        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(expected.ToArray());
    }

    #endregion

    #region DescribeUserScramCredentialsResponse Tests

    [Test]
    public async Task DescribeUserScramCredentialsResponse_HasCorrectApiKey()
    {
        await Assert.That(DescribeUserScramCredentialsResponse.ApiKey).IsEqualTo(ApiKey.DescribeUserScramCredentials);
    }

    [Test]
    public async Task DescribeUserScramCredentialsResponse_CanBeParsed()
    {
        var data = new List<byte>();
        // ThrottleTimeMs (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x64 }); // 100ms
        // ErrorCode (INT16)
        data.AddRange(new byte[] { 0x00, 0x00 }); // None
        // ErrorMessage (COMPACT_NULLABLE_STRING) - null
        data.Add(0x00);
        // Results COMPACT_ARRAY (length+1 = 2, so 1 entry)
        data.Add(0x02);
        // Result entry:
        // User (COMPACT_STRING) "alice"
        data.Add(0x06); // length+1 = 6
        data.AddRange("alice"u8.ToArray());
        // ErrorCode (INT16)
        data.AddRange(new byte[] { 0x00, 0x00 }); // None
        // ErrorMessage (COMPACT_NULLABLE_STRING) - null
        data.Add(0x00);
        // CredentialInfos COMPACT_ARRAY (length+1 = 2, so 1 entry)
        data.Add(0x02);
        // CredentialInfo entry:
        // Mechanism (UINT8)
        data.Add(0x01); // SCRAM-SHA-256
        // Iterations (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x10, 0x00 }); // 4096
        // CredentialInfo tagged fields
        data.Add(0x00);
        // Result tagged fields
        data.Add(0x00);
        // Response tagged fields
        data.Add(0x00);

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (DescribeUserScramCredentialsResponse)DescribeUserScramCredentialsResponse.Read(ref reader, version: 0);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(100);
        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.ErrorMessage).IsNull();
        await Assert.That(response.Results.Count).IsEqualTo(1);

        var result = response.Results[0];
        await Assert.That(result.User).IsEqualTo("alice");
        await Assert.That(result.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(result.CredentialInfos.Count).IsEqualTo(1);
        await Assert.That(result.CredentialInfos[0].Mechanism).IsEqualTo((byte)1);
        await Assert.That(result.CredentialInfos[0].Iterations).IsEqualTo(4096);
    }

    [Test]
    public async Task DescribeUserScramCredentialsResponse_MultipleCredentials_CanBeParsed()
    {
        var data = new List<byte>();
        // ThrottleTimeMs (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 }); // 0ms
        // ErrorCode (INT16)
        data.AddRange(new byte[] { 0x00, 0x00 }); // None
        // ErrorMessage (COMPACT_NULLABLE_STRING) - null
        data.Add(0x00);
        // Results COMPACT_ARRAY (length+1 = 2, so 1 entry)
        data.Add(0x02);
        // Result entry:
        // User (COMPACT_STRING) "bob"
        data.Add(0x04); // length+1 = 4
        data.AddRange("bob"u8.ToArray());
        // ErrorCode (INT16)
        data.AddRange(new byte[] { 0x00, 0x00 }); // None
        // ErrorMessage (COMPACT_NULLABLE_STRING) - null
        data.Add(0x00);
        // CredentialInfos COMPACT_ARRAY (length+1 = 3, so 2 entries)
        data.Add(0x03);
        // First CredentialInfo:
        data.Add(0x01); // SCRAM-SHA-256
        data.AddRange(new byte[] { 0x00, 0x00, 0x10, 0x00 }); // 4096
        data.Add(0x00); // tagged fields
        // Second CredentialInfo:
        data.Add(0x02); // SCRAM-SHA-512
        data.AddRange(new byte[] { 0x00, 0x00, 0x20, 0x00 }); // 8192
        data.Add(0x00); // tagged fields
        // Result tagged fields
        data.Add(0x00);
        // Response tagged fields
        data.Add(0x00);

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (DescribeUserScramCredentialsResponse)DescribeUserScramCredentialsResponse.Read(ref reader, version: 0);

        await Assert.That(response.Results.Count).IsEqualTo(1);
        var result = response.Results[0];
        await Assert.That(result.User).IsEqualTo("bob");
        await Assert.That(result.CredentialInfos.Count).IsEqualTo(2);
        await Assert.That(result.CredentialInfos[0].Mechanism).IsEqualTo((byte)1);
        await Assert.That(result.CredentialInfos[0].Iterations).IsEqualTo(4096);
        await Assert.That(result.CredentialInfos[1].Mechanism).IsEqualTo((byte)2);
        await Assert.That(result.CredentialInfos[1].Iterations).IsEqualTo(8192);
    }

    #endregion

    #region AlterUserScramCredentialsRequest Tests

    [Test]
    public async Task AlterUserScramCredentialsRequest_HasCorrectApiKey()
    {
        await Assert.That(AlterUserScramCredentialsRequest.ApiKey).IsEqualTo(ApiKey.AlterUserScramCredentials);
    }

    [Test]
    public async Task AlterUserScramCredentialsRequest_IsFlexibleVersion()
    {
        await Assert.That(AlterUserScramCredentialsRequest.IsFlexibleVersion(0)).IsTrue();
    }

    [Test]
    public async Task AlterUserScramCredentialsRequest_HeaderVersions()
    {
        await Assert.That(AlterUserScramCredentialsRequest.GetRequestHeaderVersion(0)).IsEqualTo((short)2);
        await Assert.That(AlterUserScramCredentialsRequest.GetResponseHeaderVersion(0)).IsEqualTo((short)1);
    }

    [Test]
    public async Task AlterUserScramCredentialsRequest_Deletion_EncodesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new AlterUserScramCredentialsRequest
        {
            Deletions = [new ScramCredentialDeletion { Name = "alice", Mechanism = 1 }],
            Upsertions = []
        };
        request.Write(ref writer, version: 0);

        var expected = new List<byte>();
        // Deletions COMPACT_ARRAY (length+1 = 2)
        expected.Add(0x02);
        // Deletion entry:
        // Name: COMPACT_STRING "alice"
        expected.Add(0x06);
        expected.AddRange("alice"u8.ToArray());
        // Mechanism (UINT8)
        expected.Add(0x01);
        // Deletion tagged fields
        expected.Add(0x00);
        // Upsertions COMPACT_ARRAY (length+1 = 1, empty)
        expected.Add(0x01);
        // Request tagged fields
        expected.Add(0x00);

        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(expected.ToArray());
    }

    [Test]
    public async Task AlterUserScramCredentialsRequest_Upsertion_EncodesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var salt = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var saltedPassword = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };

        var request = new AlterUserScramCredentialsRequest
        {
            Deletions = [],
            Upsertions = [
                new ScramCredentialUpsertion
                {
                    Name = "bob",
                    Mechanism = 2,
                    Iterations = 8192,
                    Salt = salt,
                    SaltedPassword = saltedPassword
                }
            ]
        };
        request.Write(ref writer, version: 0);

        var expected = new List<byte>();
        // Deletions COMPACT_ARRAY (length+1 = 1, empty)
        expected.Add(0x01);
        // Upsertions COMPACT_ARRAY (length+1 = 2)
        expected.Add(0x02);
        // Upsertion entry:
        // Name: COMPACT_STRING "bob"
        expected.Add(0x04);
        expected.AddRange("bob"u8.ToArray());
        // Mechanism (UINT8)
        expected.Add(0x02);
        // Iterations (INT32)
        expected.AddRange(new byte[] { 0x00, 0x00, 0x20, 0x00 }); // 8192
        // Salt: COMPACT_BYTES (length+1 = 5)
        expected.Add(0x05);
        expected.AddRange(salt);
        // SaltedPassword: COMPACT_BYTES (length+1 = 5)
        expected.Add(0x05);
        expected.AddRange(saltedPassword);
        // Upsertion tagged fields
        expected.Add(0x00);
        // Request tagged fields
        expected.Add(0x00);

        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(expected.ToArray());
    }

    #endregion

    #region AlterUserScramCredentialsResponse Tests

    [Test]
    public async Task AlterUserScramCredentialsResponse_HasCorrectApiKey()
    {
        await Assert.That(AlterUserScramCredentialsResponse.ApiKey).IsEqualTo(ApiKey.AlterUserScramCredentials);
    }

    [Test]
    public async Task AlterUserScramCredentialsResponse_Success_CanBeParsed()
    {
        var data = new List<byte>();
        // ThrottleTimeMs (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 }); // 0ms
        // Results COMPACT_ARRAY (length+1 = 2, so 1 entry)
        data.Add(0x02);
        // Result entry:
        // User (COMPACT_STRING) "alice"
        data.Add(0x06); // length+1 = 6
        data.AddRange("alice"u8.ToArray());
        // ErrorCode (INT16)
        data.AddRange(new byte[] { 0x00, 0x00 }); // None
        // ErrorMessage (COMPACT_NULLABLE_STRING) - null
        data.Add(0x00);
        // Result tagged fields
        data.Add(0x00);
        // Response tagged fields
        data.Add(0x00);

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (AlterUserScramCredentialsResponse)AlterUserScramCredentialsResponse.Read(ref reader, version: 0);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(0);
        await Assert.That(response.Results.Count).IsEqualTo(1);
        await Assert.That(response.Results[0].User).IsEqualTo("alice");
        await Assert.That(response.Results[0].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.Results[0].ErrorMessage).IsNull();
    }

    [Test]
    public async Task AlterUserScramCredentialsResponse_MultipleResults_CanBeParsed()
    {
        var data = new List<byte>();
        // ThrottleTimeMs (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x32 }); // 50ms
        // Results COMPACT_ARRAY (length+1 = 3, so 2 entries)
        data.Add(0x03);
        // First result:
        data.Add(0x06); // "alice" length+1
        data.AddRange("alice"u8.ToArray());
        data.AddRange(new byte[] { 0x00, 0x00 }); // ErrorCode.None
        data.Add(0x00); // null error message
        data.Add(0x00); // tagged fields
        // Second result:
        data.Add(0x04); // "bob" length+1
        data.AddRange("bob"u8.ToArray());
        data.AddRange(new byte[] { 0x00, 0x00 }); // ErrorCode.None
        data.Add(0x00); // null error message
        data.Add(0x00); // tagged fields
        // Response tagged fields
        data.Add(0x00);

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (AlterUserScramCredentialsResponse)AlterUserScramCredentialsResponse.Read(ref reader, version: 0);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(50);
        await Assert.That(response.Results.Count).IsEqualTo(2);
        await Assert.That(response.Results[0].User).IsEqualTo("alice");
        await Assert.That(response.Results[1].User).IsEqualTo("bob");
    }

    #endregion
}
