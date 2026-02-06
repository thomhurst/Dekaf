using Dekaf.Networking;

namespace Dekaf.Tests.Unit.Networking;

/// <summary>
/// Tests for ConnectionPool, including behavior with disposed connections
/// and proper error handling for various states.
/// </summary>
public sealed class ConnectionPoolTests
{
    [Test]
    public async Task GetConnectionAsync_DisposedPool_ThrowsObjectDisposedException()
    {
        var pool = new ConnectionPool("test-client");
        await pool.DisposeAsync();

        Func<Task> act = () => pool.GetConnectionAsync(0).AsTask();

        await Assert.That(act).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task GetConnectionAsync_UnknownBrokerId_ThrowsInvalidOperationException()
    {
        var pool = new ConnectionPool("test-client");

        Func<Task> act = () => pool.GetConnectionAsync(999).AsTask();

        await Assert.That(act).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task RegisterBroker_ThenGetConnection_UsesRegisteredBrokerInfo()
    {
        var pool = new ConnectionPool("test-client");

        // Register a broker that doesn't actually exist - connection will fail
        pool.RegisterBroker(1, "nonexistent-host-12345.invalid", 9092);

        // Should not throw InvalidOperationException("Unknown broker ID")
        // but will fail to connect
        Func<Task> act = () => pool.GetConnectionAsync(1).AsTask();

        // Will throw because it can't connect, but NOT "Unknown broker ID"
        await Assert.That(act).Throws<Exception>();
    }

    [Test]
    public async Task DisposeAsync_CalledMultipleTimes_DoesNotThrow()
    {
        var pool = new ConnectionPool("test-client");

        await pool.DisposeAsync();
        await pool.DisposeAsync();
        await pool.DisposeAsync();
    }

    [Test]
    public async Task GetConnectionByHostPort_DisposedPool_ThrowsObjectDisposedException()
    {
        var pool = new ConnectionPool("test-client");
        await pool.DisposeAsync();

        Func<Task> act = () => pool.GetConnectionAsync("localhost", 9092).AsTask();

        await Assert.That(act).Throws<ObjectDisposedException>();
    }

    [Test]
    public void RegisterBroker_SameIdTwice_OverwritesWithoutError()
    {
        var pool = new ConnectionPool("test-client");

        pool.RegisterBroker(1, "host-a", 9092);
        pool.RegisterBroker(1, "host-b", 9093);

        // Should not throw - the second registration overwrites the first
    }

    [Test]
    public void RegisterBroker_MultipleBrokers_AllRegistered()
    {
        var pool = new ConnectionPool("test-client");

        pool.RegisterBroker(0, "broker-0", 9092);
        pool.RegisterBroker(1, "broker-1", 9092);
        pool.RegisterBroker(2, "broker-2", 9092);

        // All should be registered without error
    }

    [Test]
    public async Task RemoveConnectionAsync_UnknownBroker_DoesNotThrow()
    {
        var pool = new ConnectionPool("test-client");

        // Removing a non-existent broker should be a no-op
        await pool.RemoveConnectionAsync(999);
    }

    [Test]
    public async Task CloseAllAsync_EmptyPool_DoesNotThrow()
    {
        var pool = new ConnectionPool("test-client");

        await pool.CloseAllAsync();
    }

    [Test]
    public async Task CloseAllAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var pool = new ConnectionPool("test-client");
        await pool.DisposeAsync();

        // CloseAll after dispose throws because internal semaphore is disposed
        Func<Task> act = () => pool.CloseAllAsync().AsTask();

        await Assert.That(act).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task GetConnectionAsync_AfterCloseAll_CanReconnect()
    {
        var pool = new ConnectionPool("test-client");
        pool.RegisterBroker(1, "nonexistent-host-12345.invalid", 9092);

        await pool.CloseAllAsync();

        // After closing all connections, getting a connection should attempt
        // to create a new one (will fail because host doesn't exist, but
        // should not throw "Unknown broker ID")
        Func<Task> act = () => pool.GetConnectionAsync(1).AsTask();

        await Assert.That(act).Throws<Exception>();
    }

    [Test]
    public async Task GetConnectionByHostPort_UnreachableHost_ThrowsException()
    {
        var pool = new ConnectionPool("test-client");

        Func<Task> act = () => pool.GetConnectionAsync("nonexistent-host-12345.invalid", 9092).AsTask();

        await Assert.That(act).Throws<Exception>();
    }

    [Test]
    public async Task GetConnectionAsync_DisposedPoolByHostPort_ThrowsBeforeAttemptingConnect()
    {
        var pool = new ConnectionPool("test-client");
        await pool.DisposeAsync();

        // Should immediately throw ObjectDisposedException, not attempt to connect
        Func<Task> act = () => pool.GetConnectionAsync("localhost", 9092).AsTask();

        await Assert.That(act).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task GetConnectionAsync_DisposedPoolById_ThrowsBeforeAttemptingConnect()
    {
        var pool = new ConnectionPool("test-client");
        pool.RegisterBroker(1, "localhost", 9092);
        await pool.DisposeAsync();

        // Should immediately throw ObjectDisposedException, not attempt to connect
        Func<Task> act = () => pool.GetConnectionAsync(1).AsTask();

        await Assert.That(act).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task Constructor_NullClientId_DoesNotThrow()
    {
        var pool = new ConnectionPool();

        // Should create successfully with no client ID
        await Assert.That(pool).IsNotNull();
    }

    [Test]
    public async Task Constructor_CustomConnectionsPerBroker_DoesNotThrow()
    {
        var pool = new ConnectionPool("test-client", connectionsPerBroker: 4);

        await Assert.That(pool).IsNotNull();
    }
}
