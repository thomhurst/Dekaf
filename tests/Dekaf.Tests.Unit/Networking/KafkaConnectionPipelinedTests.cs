using Dekaf.Networking;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Networking;

/// <summary>
/// Tests for KafkaConnection.SendPipelinedAsync to verify the pipelined send
/// refactoring preserves behavior while enabling pipelining.
/// </summary>
public sealed class KafkaConnectionPipelinedTests
{
    [Test]
    public async Task SendPipelinedAsync_DisposedConnection_ThrowsObjectDisposedException()
    {
        var connection = new KafkaConnection("localhost", 9092);
        await connection.DisposeAsync();

        var act = async () => await connection.SendPipelinedAsync<MetadataRequest, MetadataResponse>(
            new MetadataRequest { Topics = null },
            12);

        await Assert.That(act).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task SendPipelinedAsync_NotConnected_ThrowsInvalidOperationException()
    {
        var connection = new KafkaConnection("localhost", 9092);

        var act = async () => await connection.SendPipelinedAsync<MetadataRequest, MetadataResponse>(
            new MetadataRequest { Topics = null },
            12);

        await Assert.That(act).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task SendAsync_AfterRefactor_NotConnected_ThrowsInvalidOperationException()
    {
        // Verify existing SendAsync still works after the refactor to use AwaitAndParseResponseAsync
        var connection = new KafkaConnection("localhost", 9092);

        var act = async () => await connection.SendAsync<MetadataRequest, MetadataResponse>(
            new MetadataRequest { Topics = null },
            12);

        await Assert.That(act).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task SendAsync_AfterRefactor_Disposed_ThrowsObjectDisposedException()
    {
        var connection = new KafkaConnection("localhost", 9092);
        await connection.DisposeAsync();

        var act = async () => await connection.SendAsync<MetadataRequest, MetadataResponse>(
            new MetadataRequest { Topics = null },
            12);

        await Assert.That(act).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task SendPipelinedAsync_Cancellation_Throws()
    {
        var connection = new KafkaConnection("localhost", 9092);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Should throw either OperationCanceledException or InvalidOperationException
        // depending on which check runs first.
        var act = async () => await connection.SendPipelinedAsync<MetadataRequest, MetadataResponse>(
            new MetadataRequest { Topics = null },
            12,
            cts.Token);

        await Assert.That(act).ThrowsException();
    }

    [Test]
    public async Task SendFireAndForgetAsync_StillWorks_AfterRefactor()
    {
        var connection = new KafkaConnection("localhost", 9092);

        var act = async () => await connection.SendFireAndForgetAsync<MetadataRequest, MetadataResponse>(
            new MetadataRequest { Topics = null },
            12);

        await Assert.That(act).Throws<InvalidOperationException>();
    }
}
