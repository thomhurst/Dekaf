using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Admin;

public sealed class AdminClientScramCredentialsTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task DescribeUserScramCredentialsAsync_MissingUser_PreservesExistingCredentials(bool missingFirst)
    {
        var existing = new DescribeUserScramCredentialsResult
        {
            User = "alice",
            CredentialInfos =
            [
                new() { Mechanism = (byte)ScramMechanism.ScramSha256, Iterations = 4096 },
                new() { Mechanism = (byte)ScramMechanism.ScramSha512, Iterations = 8192 }
            ]
        };
        var missing = MissingUser();
        var admin = CreateAdmin(new DescribeUserScramCredentialsResponse
        {
            Results = missingFirst ? [missing, existing] : [existing, missing]
        });
        await using var client = admin;

        var result = await admin.DescribeUserScramCredentialsAsync(["alice", "missing"]);

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result).ContainsKey("alice");
        await Assert.That(result["alice"].Count).IsEqualTo(2);
        await Assert.That(result["alice"][0].Mechanism).IsEqualTo(ScramMechanism.ScramSha256);
        await Assert.That(result["alice"][0].Iterations).IsEqualTo(4096);
        await Assert.That(result["alice"][1].Mechanism).IsEqualTo(ScramMechanism.ScramSha512);
        await Assert.That(result["alice"][1].Iterations).IsEqualTo(8192);
    }

    [Test]
    public async Task DescribeUserScramCredentialsAsync_OnlyMissingUser_ReturnsEmptyDictionary()
    {
        var admin = CreateAdmin(new DescribeUserScramCredentialsResponse { Results = [MissingUser()] });
        await using var client = admin;

        var result = await admin.DescribeUserScramCredentialsAsync(["missing"]);

        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task DescribeUserScramCredentialsAsync_OtherUserError_StillThrows()
    {
        var admin = CreateAdmin(new DescribeUserScramCredentialsResponse
        {
            Results =
            [
                MissingUser(),
                new()
                {
                    User = "denied",
                    ErrorCode = ErrorCode.ClusterAuthorizationFailed,
                    ErrorMessage = "denied",
                    CredentialInfos = []
                }
            ]
        });
        await using var client = admin;

        var exception = await Assert.ThrowsAsync<KafkaException>(
            () => admin.DescribeUserScramCredentialsAsync(["missing", "denied"]).AsTask());

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.ClusterAuthorizationFailed);
    }

    [Test]
    [Arguments(ErrorCode.ResourceNotFound)]
    [Arguments(ErrorCode.ClusterAuthorizationFailed)]
    public async Task DescribeUserScramCredentialsAsync_TopLevelError_StillThrows(ErrorCode errorCode)
    {
        var admin = CreateAdmin(new DescribeUserScramCredentialsResponse
        {
            ErrorCode = errorCode,
            Results = []
        });
        await using var client = admin;

        var exception = await Assert.ThrowsAsync<KafkaException>(
            () => admin.DescribeUserScramCredentialsAsync(["missing"]).AsTask());

        await Assert.That(exception!.ErrorCode).IsEqualTo(errorCode);
    }

    private static DescribeUserScramCredentialsResult MissingUser() => new()
    {
        User = "missing",
        ErrorCode = ErrorCode.ResourceNotFound,
        ErrorMessage = "Attempt to describe a user credential that does not exist: missing",
        CredentialInfos = []
    };

    private static AdminClient CreateAdmin(DescribeUserScramCredentialsResponse response)
    {
        var (admin, connection) = AdminClientIdempotentRetryTests.CreateAdminWithMockConnection(ApiKey.DescribeUserScramCredentials);
        connection.SendAsync<DescribeUserScramCredentialsRequest, DescribeUserScramCredentialsResponse>(
                Arg.Any<DescribeUserScramCredentialsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(response));
        return admin;
    }
}
