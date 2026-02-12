using Dekaf.Admin;

namespace Dekaf.Tests.Unit.Builder;

public class AdminClientBuilderTests
{
    #region Build Validation

    [Test]
    public async Task Build_WithoutBootstrapServers_ThrowsInvalidOperationException()
    {
        var builder = new AdminClientBuilder();

        var act = () => builder.Build();

        await Assert.That(act).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Build_WithBootstrapServers_Succeeds()
    {
        await using var client = new AdminClientBuilder()
            .WithBootstrapServers("localhost:9092")
            .Build();

        await Assert.That(client).IsNotNull();
    }

    #endregion

    #region Chaining Tests

    [Test]
    public async Task WithBootstrapServers_ReturnsSameBuilder()
    {
        var builder = new AdminClientBuilder();
        var result = builder.WithBootstrapServers("localhost:9092");
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task WithClientId_ReturnsSameBuilder()
    {
        var builder = new AdminClientBuilder();
        var result = builder.WithClientId("my-admin");
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task UseTls_ReturnsSameBuilder()
    {
        var builder = new AdminClientBuilder();
        var result = builder.UseTls();
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task WithSaslPlain_ReturnsSameBuilder()
    {
        var builder = new AdminClientBuilder();
        var result = builder.WithSaslPlain("user", "pass");
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task WithSaslScramSha256_ReturnsSameBuilder()
    {
        var builder = new AdminClientBuilder();
        var result = builder.WithSaslScramSha256("user", "pass");
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task WithSaslScramSha512_ReturnsSameBuilder()
    {
        var builder = new AdminClientBuilder();
        var result = builder.WithSaslScramSha512("user", "pass");
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task AllMethods_CanBeChained()
    {
        await using var client = new AdminClientBuilder()
            .WithBootstrapServers("localhost:9092")
            .WithClientId("admin")
            .UseTls()
            .WithSaslPlain("user", "pass")
            .Build();

        await Assert.That(client).IsNotNull();
    }

    #endregion
}
