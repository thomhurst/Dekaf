using Dekaf.Networking;

namespace Dekaf.Tests.Unit.Networking;

public class PipeMemoryPoolTests
{
    [Test]
    public async Task Rent_ReturnsMemoryOwner_WithRequestedSize()
    {
        using var pool = new PipeMemoryPool();

        using var owner = pool.Rent(1024);

        await Assert.That(owner.Memory.Length).IsGreaterThanOrEqualTo(1024);
    }

    [Test]
    public async Task Rent_DefaultSize_ReturnsAtLeast4096()
    {
        using var pool = new PipeMemoryPool();

        using var owner = pool.Rent();

        await Assert.That(owner.Memory.Length).IsGreaterThanOrEqualTo(4096);
    }

    [Test]
    public async Task Rent_AfterDispose_ThrowsObjectDisposedException()
    {
        var pool = new PipeMemoryPool();
        pool.Dispose();

        await Assert.That(() => pool.Rent(1024)).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task MemoryOwner_Dispose_DoesNotThrow()
    {
        using var pool = new PipeMemoryPool();
        var owner = pool.Rent(1024);

        // Should not throw
        owner.Dispose();

        // Double dispose should also not throw
        owner.Dispose();

        // If we got here without throwing, the test passed
        await Task.CompletedTask;
    }

    [Test]
    public async Task MemoryOwner_AfterDispose_ThrowsObjectDisposedException()
    {
        using var pool = new PipeMemoryPool();
        var owner = pool.Rent(1024);
        owner.Dispose();

        await Assert.That(() => _ = owner.Memory).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task Rent_BufferExceedingMaxArrayLength_FallsThrough()
    {
        using var pool = new PipeMemoryPool();

        // Request larger than default maxArrayLength (4 MB) — falls through to new allocation
        using var owner = pool.Rent(8 * 1024 * 1024);

        await Assert.That(owner.Memory.Length).IsGreaterThanOrEqualTo(8 * 1024 * 1024);
    }

    [Test]
    public async Task MultipleRents_ReturnIndependentBuffers()
    {
        using var pool = new PipeMemoryPool();

        using var owner1 = pool.Rent(1024);
        using var owner2 = pool.Rent(1024);

        // Write to both - they should be independent buffers
        owner1.Memory.Span[0] = 0xAA;
        owner2.Memory.Span[0] = 0xBB;

        await Assert.That(owner1.Memory.Span[0]).IsEqualTo((byte)0xAA);
        await Assert.That(owner2.Memory.Span[0]).IsEqualTo((byte)0xBB);
    }

    [Test]
    public async Task Pool_WorksWithPipeOptions()
    {
        using var pool = new PipeMemoryPool();

        // Verify the pool can be used with PipeOptions (the actual use case)
        var pipeOptions = new System.IO.Pipelines.PipeOptions(
            pool: pool,
            minimumSegmentSize: 4096,
            pauseWriterThreshold: 65536,
            resumeWriterThreshold: 32768);

        var pipe = new System.IO.Pipelines.Pipe(pipeOptions);

        // Write some data
        var memory = pipe.Writer.GetMemory(100);
        memory.Span[0] = 42;
        pipe.Writer.Advance(1);
        await pipe.Writer.FlushAsync();

        // Read it back
        var result = await pipe.Reader.ReadAsync();
        await Assert.That(result.Buffer.Length).IsEqualTo(1);
        await Assert.That(result.Buffer.First.Span[0]).IsEqualTo((byte)42);
        pipe.Reader.AdvanceTo(result.Buffer.End);

        // Complete both ends
        await pipe.Writer.CompleteAsync();
        await pipe.Reader.CompleteAsync();
    }
}
