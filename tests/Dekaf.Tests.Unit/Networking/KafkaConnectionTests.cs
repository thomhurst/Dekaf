using Dekaf.Networking;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Networking;

/// <summary>
/// Tests for KafkaConnection, particularly the IsConnected fix
/// that ensures disposed connections report as disconnected.
/// </summary>
public sealed class KafkaConnectionTests
{
    [Test]
    public async Task IsConnected_BeforeConnect_ReturnsFalse()
    {
        var connection = new KafkaConnection("localhost", 9092);

        // Before ConnectAsync is called, there is no socket
        var isConnected = connection.IsConnected;

        await Assert.That(isConnected).IsFalse();
    }

    [Test]
    public async Task IsConnected_AfterDispose_ReturnsFalse()
    {
        var connection = new KafkaConnection("localhost", 9092);
        await connection.DisposeAsync();

        // After disposal, IsConnected must return false even if socket state is ambiguous
        var isConnected = connection.IsConnected;

        await Assert.That(isConnected).IsFalse();
    }

    [Test]
    public async Task IsConnected_DisposeWithoutConnect_ReturnsFalse()
    {
        // Connection created but never connected, then disposed
        var connection = new KafkaConnection(1, "localhost", 9092);
        await connection.DisposeAsync();

        var isConnected = connection.IsConnected;

        await Assert.That(isConnected).IsFalse();
    }

    [Test]
    public async Task DisposeAsync_CalledMultipleTimes_DoesNotThrow()
    {
        var connection = new KafkaConnection("localhost", 9092);

        await connection.DisposeAsync();
        await connection.DisposeAsync();
        await connection.DisposeAsync();
    }

    [Test]
    public async Task BrokerId_DefaultConstructor_IsNegativeOne()
    {
        var connection = new KafkaConnection("localhost", 9092);

        var brokerId = connection.BrokerId;

        await Assert.That(brokerId).IsEqualTo(-1);
    }

    [Test]
    public async Task BrokerId_WithExplicitId_ReturnsSetValue()
    {
        var connection = new KafkaConnection(42, "localhost", 9092);

        var brokerId = connection.BrokerId;

        await Assert.That(brokerId).IsEqualTo(42);
    }

    [Test]
    public async Task Host_ReturnsConstructorValue()
    {
        var connection = new KafkaConnection("my-broker.example.com", 9092);

        var host = connection.Host;

        await Assert.That(host).IsEqualTo("my-broker.example.com");
    }

    [Test]
    public async Task Port_ReturnsConstructorValue()
    {
        var connection = new KafkaConnection("localhost", 19092);

        var port = connection.Port;

        await Assert.That(port).IsEqualTo(19092);
    }

    [Test]
    public async Task SendAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var connection = new KafkaConnection("localhost", 9092);
        await connection.DisposeAsync();

        Func<Task> act = () => connection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
            new ApiVersionsRequest { ClientSoftwareName = "test", ClientSoftwareVersion = "1.0" },
            apiVersion: 3).AsTask();

        await Assert.That(act).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task ConnectAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var connection = new KafkaConnection("localhost", 9092);
        await connection.DisposeAsync();

        Func<Task> act = () => connection.ConnectAsync().AsTask();

        await Assert.That(act).Throws<ObjectDisposedException>();
    }
}
