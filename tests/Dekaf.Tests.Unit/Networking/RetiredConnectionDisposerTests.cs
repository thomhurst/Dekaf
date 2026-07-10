using Dekaf.Networking;
using Dekaf.Tests.Unit.Producer;

namespace Dekaf.Tests.Unit.Networking;

public sealed class RetiredConnectionDisposerTests
{
    [Test]
    public async Task DrainAndDisposeAsync_CancelledWhileLeased_DoesNotDisposeConnection()
    {
        var connection = new TestKafkaConnection();
        var retirableConnection = (IRetirableKafkaConnection)connection;
        await Assert.That(retirableConnection.TryAcquireLease()).IsTrue();
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        var exception = await Assert.That(async () =>
                await RetiredConnectionDisposer.DrainAndDisposeAsync(
                    connection,
                    cancellationSource.Token))
            .Throws<OperationCanceledException>();

        await Assert.That(exception).IsNotNull();
        await Assert.That(connection.DisposeCalls).IsEqualTo(0);
        retirableConnection.ReleaseLease();
    }
}
