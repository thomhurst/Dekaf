using Dekaf.Admin;

namespace Dekaf.Tests.Unit.Admin;

/// <summary>
/// Tests for SCRAM credential supporting types.
/// </summary>
public class ScramCredentialTypesTests
{
    #region ScramMechanism Tests

    [Test]
    public async Task ScramMechanism_Unknown_HasCorrectValue()
    {
        var unknown = ScramMechanism.Unknown;
        await Assert.That((int)unknown).IsEqualTo(0);
    }

    [Test]
    public async Task ScramMechanism_ScramSha256_HasCorrectValue()
    {
        var sha256 = ScramMechanism.ScramSha256;
        await Assert.That((int)sha256).IsEqualTo(1);
    }

    [Test]
    public async Task ScramMechanism_ScramSha512_HasCorrectValue()
    {
        var sha512 = ScramMechanism.ScramSha512;
        await Assert.That((int)sha512).IsEqualTo(2);
    }

    #endregion

    #region ScramCredentialInfo Tests

    [Test]
    public async Task ScramCredentialInfo_CanBeCreated()
    {
        var info = new ScramCredentialInfo
        {
            Mechanism = ScramMechanism.ScramSha256,
            Iterations = 4096
        };

        await Assert.That(info.Mechanism).IsEqualTo(ScramMechanism.ScramSha256);
        await Assert.That(info.Iterations).IsEqualTo(4096);
    }

    [Test]
    public async Task ScramCredentialInfo_Sha512_CanBeCreated()
    {
        var info = new ScramCredentialInfo
        {
            Mechanism = ScramMechanism.ScramSha512,
            Iterations = 8192
        };

        await Assert.That(info.Mechanism).IsEqualTo(ScramMechanism.ScramSha512);
        await Assert.That(info.Iterations).IsEqualTo(8192);
    }

    #endregion

    #region UserScramCredentialUpsertion Tests

    [Test]
    public async Task UserScramCredentialUpsertion_CanBeCreated()
    {
        var upsertion = new UserScramCredentialUpsertion
        {
            User = "testuser",
            Mechanism = ScramMechanism.ScramSha256,
            Iterations = 4096,
            Password = "secretpassword"
        };

        await Assert.That(upsertion.User).IsEqualTo("testuser");
        await Assert.That(upsertion.Mechanism).IsEqualTo(ScramMechanism.ScramSha256);
        await Assert.That(upsertion.Iterations).IsEqualTo(4096);
        await Assert.That(upsertion.Password).IsEqualTo("secretpassword");
        await Assert.That(upsertion.Salt).IsNull();
    }

    [Test]
    public async Task UserScramCredentialUpsertion_WithSalt_CanBeCreated()
    {
        var customSalt = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        var upsertion = new UserScramCredentialUpsertion
        {
            User = "testuser",
            Mechanism = ScramMechanism.ScramSha512,
            Iterations = 8192,
            Password = "secretpassword",
            Salt = customSalt
        };

        await Assert.That(upsertion.Salt).IsEquivalentTo(customSalt);
    }

    [Test]
    public async Task UserScramCredentialUpsertion_IsUserScramCredentialAlteration()
    {
        UserScramCredentialAlteration alteration = new UserScramCredentialUpsertion
        {
            User = "testuser",
            Mechanism = ScramMechanism.ScramSha256,
            Iterations = 4096,
            Password = "secretpassword"
        };

        await Assert.That(alteration).IsTypeOf<UserScramCredentialUpsertion>();
        await Assert.That(alteration.User).IsEqualTo("testuser");
    }

    #endregion

    #region UserScramCredentialDeletion Tests

    [Test]
    public async Task UserScramCredentialDeletion_CanBeCreated()
    {
        var deletion = new UserScramCredentialDeletion
        {
            User = "testuser",
            Mechanism = ScramMechanism.ScramSha256
        };

        await Assert.That(deletion.User).IsEqualTo("testuser");
        await Assert.That(deletion.Mechanism).IsEqualTo(ScramMechanism.ScramSha256);
    }

    [Test]
    public async Task UserScramCredentialDeletion_IsUserScramCredentialAlteration()
    {
        UserScramCredentialAlteration alteration = new UserScramCredentialDeletion
        {
            User = "testuser",
            Mechanism = ScramMechanism.ScramSha512
        };

        await Assert.That(alteration).IsTypeOf<UserScramCredentialDeletion>();
        await Assert.That(alteration.User).IsEqualTo("testuser");
    }

    #endregion

    #region Options Tests

    [Test]
    public async Task DescribeUserScramCredentialsOptions_HasDefaultTimeout()
    {
        var options = new DescribeUserScramCredentialsOptions();

        await Assert.That(options.TimeoutMs).IsEqualTo(30000);
    }

    [Test]
    public async Task DescribeUserScramCredentialsOptions_CanSetTimeout()
    {
        var options = new DescribeUserScramCredentialsOptions
        {
            TimeoutMs = 60000
        };

        await Assert.That(options.TimeoutMs).IsEqualTo(60000);
    }

    [Test]
    public async Task AlterUserScramCredentialsOptions_HasDefaultTimeout()
    {
        var options = new AlterUserScramCredentialsOptions();

        await Assert.That(options.TimeoutMs).IsEqualTo(30000);
    }

    [Test]
    public async Task AlterUserScramCredentialsOptions_CanSetTimeout()
    {
        var options = new AlterUserScramCredentialsOptions
        {
            TimeoutMs = 15000
        };

        await Assert.That(options.TimeoutMs).IsEqualTo(15000);
    }

    #endregion
}
