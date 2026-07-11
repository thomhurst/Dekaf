using Dekaf.Consumer;
using Dekaf.Protocol;
using Dekaf.Protocol.Records;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class DirectFetchCleanupTests
{
    [Test]
    public async Task DisposeCompletedFetchResults_FaultedPeer_DisposesSuccessfulResult()
    {
        var memory = new TrackingPooledMemory();
        var pending = PendingFetchData.Create(
            "topic",
            0,
            Array.Empty<RecordBatch>(),
            memoryOwner: memory);
        var completed = Task.FromResult<List<PendingFetchData>?>([pending]);
        var faulted = Task.FromException<List<PendingFetchData>?>(new InvalidOperationException("fatal"));

        KafkaConsumer<string, string>.DisposeCompletedFetchResults([completed, faulted], 2);

        await Assert.That(memory.DisposeCount).IsEqualTo(1);
    }

    private sealed class TrackingPooledMemory : IPooledMemory
    {
        public ReadOnlyMemory<byte> Memory => ReadOnlyMemory<byte>.Empty;
        public int DisposeCount { get; private set; }
        public void Dispose() => DisposeCount++;
    }
}
