using Dekaf.Networking;
using NSubstitute;

namespace Dekaf.Tests.Unit.Networking;

public sealed class KafkaConnectionLeaseTests
{
    [Test]
    public async Task LeaseConnectionByIndex_RetiredSelection_RetriesAndReleasesLease()
    {
        await using var retiredConnection = new KafkaConnection("localhost", 9092);
        await using var activeConnection = new KafkaConnection("localhost", 9093);
        ((IRetirableKafkaConnection)retiredConnection).BeginRetirement();
        var pool = Substitute.For<IConnectionPool>();
        var selectionCount = 0;
        pool.GetConnectionByIndexAsync(1, 2, Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult<IKafkaConnection>(
                Interlocked.Increment(ref selectionCount) == 1
                    ? retiredConnection
                    : activeConnection));

        using (var lease = await pool.LeaseConnectionByIndexAsync(1, 2, CancellationToken.None))
        {
            await Assert.That(lease.Connection).IsSameReferenceAs(activeConnection);
            await Assert.That(((IRetirableKafkaConnection)activeConnection).LeaseCount).IsEqualTo(1);
        }

        _ = pool.Received(2).GetConnectionByIndexAsync(1, 2, Arg.Any<CancellationToken>());
        await Assert.That(((IRetirableKafkaConnection)activeConnection).LeaseCount).IsEqualTo(0);
    }

    [Test]
    public async Task LeaseConnection_NonRetirableConnection_ReturnsNoOpLease()
    {
        var connection = Substitute.For<IKafkaConnection>();
        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(1, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(connection));

        using var lease = await pool.LeaseConnectionAsync(1, CancellationToken.None);

        await Assert.That(lease.Connection).IsSameReferenceAs(connection);
    }
}
