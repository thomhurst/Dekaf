using System.Buffers;
using System.Buffers.Binary;
using Dekaf.Networking;

namespace Dekaf.Tests.Unit.Networking;

public sealed class IncrementalFrameConsumptionTests
{
    [Test]
    public async Task TryStartPartialFrame_LargeFrame_RentsBufferAndCopiesAvailableData()
    {
        var correlationId = 99;
        var totalPayloadSize = 1_048_576; // 1 MB
        var availableAfterHeader = 65_536; // 64 KB available beyond the 4-byte size header

        var frameData = BuildPartialFrame(correlationId, totalPayloadSize, availableAfterHeader);
        var buffer = new ReadOnlySequence<byte>(frameData);

        var context = new KafkaConnection.PartialFrameContext();
        var started = KafkaConnection.TryStartPartialFrame(
            ref buffer, ref context, ResponseBufferPool.Default);

        await Assert.That(started).IsTrue();
        await Assert.That(context.FrameSize).IsEqualTo(totalPayloadSize);
        await Assert.That(context.Offset).IsEqualTo(availableAfterHeader);
        await Assert.That(context.CorrelationId).IsEqualTo(correlationId);
        await Assert.That(buffer.Length).IsEqualTo(0);

        ReturnBuffer(ref context);
    }

    [Test]
    public async Task TryStartPartialFrame_FullFrameAvailable_ReturnsFalse()
    {
        // If the complete frame fits, TryStartPartialFrame should return false
        // (let TryReadResponse handle it instead)
        var frame = BuildCompleteFrame(42, 100);
        var buffer = new ReadOnlySequence<byte>(frame);

        var context = new KafkaConnection.PartialFrameContext();
        var started = KafkaConnection.TryStartPartialFrame(
            ref buffer, ref context, ResponseBufferPool.Default);

        await Assert.That(started).IsFalse();
        await Assert.That(context.IsActive).IsFalse();
    }

    [Test]
    public async Task TryStartPartialFrame_LessThan8Bytes_ReturnsFalse()
    {
        // Need at least 8 bytes (4 size + 4 correlation ID)
        var buffer = new ReadOnlySequence<byte>(new byte[7]);

        var context = new KafkaConnection.PartialFrameContext();
        var started = KafkaConnection.TryStartPartialFrame(
            ref buffer, ref context, ResponseBufferPool.Default);

        await Assert.That(started).IsFalse();
    }

    [Test]
    public async Task ContinuePartialFrame_CompletesAssembly()
    {
        var totalSize = 200;
        var firstChunk = 50;
        var secondChunk = 150;

        // Build full payload with a recognizable pattern
        var fullPayload = new byte[totalSize];
        for (var i = 0; i < totalSize; i++)
            fullPayload[i] = (byte)(i % 251); // prime to avoid period alignment

        // Simulate: first chunk already copied
        var responseArray = new byte[totalSize];
        Array.Copy(fullPayload, 0, responseArray, 0, firstChunk);

        var context = new KafkaConnection.PartialFrameContext
        {
            Buffer = responseArray,
            FrameSize = totalSize,
            Offset = firstChunk,
            CorrelationId = 77,
            IsPooled = false
        };

        // Provide remaining data
        var remaining = new ReadOnlySequence<byte>(fullPayload.AsMemory(firstChunk, secondChunk));
        var completed = KafkaConnection.ContinuePartialFrame(ref remaining, ref context);

        await Assert.That(completed).IsTrue();
        await Assert.That(context.Offset).IsEqualTo(totalSize);
        await Assert.That(remaining.Length).IsEqualTo(0);
        // Verify data integrity
        await Assert.That(responseArray.AsSpan(0, totalSize).SequenceEqual(fullPayload)).IsTrue();
    }

    [Test]
    public async Task ContinuePartialFrame_PartialData_CopiesAndReturnsFalse()
    {
        var totalSize = 1000;
        var alreadyCopied = 100;
        var newChunk = 300;

        var context = new KafkaConnection.PartialFrameContext
        {
            Buffer = new byte[totalSize],
            FrameSize = totalSize,
            Offset = alreadyCopied,
            CorrelationId = 1,
            IsPooled = false
        };

        var chunkData = new byte[newChunk];
        var buffer = new ReadOnlySequence<byte>(chunkData);
        var completed = KafkaConnection.ContinuePartialFrame(ref buffer, ref context);

        await Assert.That(completed).IsFalse();
        await Assert.That(context.Offset).IsEqualTo(400); // 100 + 300
        await Assert.That(buffer.Length).IsEqualTo(0);
    }

    [Test]
    public async Task ContinuePartialFrame_MultipleChunks_AssemblesCorrectly()
    {
        var totalSize = 500;
        var payload = new byte[totalSize];
        for (var i = 0; i < totalSize; i++)
            payload[i] = (byte)(i % 251);

        var responseArray = new byte[totalSize];
        var context = new KafkaConnection.PartialFrameContext
        {
            Buffer = responseArray,
            FrameSize = totalSize,
            Offset = 0,
            CorrelationId = 5,
            IsPooled = false
        };

        // Feed in 5 chunks of 100 bytes each
        for (var chunk = 0; chunk < 5; chunk++)
        {
            var data = new ReadOnlySequence<byte>(payload.AsMemory(chunk * 100, 100));
            var completed = KafkaConnection.ContinuePartialFrame(ref data, ref context);

            if (chunk < 4)
                await Assert.That(completed).IsFalse();
            else
                await Assert.That(completed).IsTrue();
        }

        await Assert.That(responseArray.AsSpan(0, totalSize).SequenceEqual(payload)).IsTrue();
    }

    [Test]
    public async Task TryStartPartialFrame_CorrelationId_PreservedCorrectly()
    {
        var correlationId = int.MaxValue; // edge case
        var frameData = BuildPartialFrame(correlationId, 10000, 100);
        var buffer = new ReadOnlySequence<byte>(frameData);

        var context = new KafkaConnection.PartialFrameContext();
        KafkaConnection.TryStartPartialFrame(ref buffer, ref context, ResponseBufferPool.Default);

        await Assert.That(context.CorrelationId).IsEqualTo(correlationId);

        ReturnBuffer(ref context);
    }

    [Test]
    public async Task ContinuePartialFrame_ExtraDataBeyondFrame_OnlyConsumesNeeded()
    {
        var totalSize = 100;
        var alreadyCopied = 50;
        var bufferWithExtra = new byte[200]; // 200 bytes available but only 50 needed

        var context = new KafkaConnection.PartialFrameContext
        {
            Buffer = new byte[totalSize],
            FrameSize = totalSize,
            Offset = alreadyCopied,
            CorrelationId = 1,
            IsPooled = false
        };

        var buffer = new ReadOnlySequence<byte>(bufferWithExtra);
        var completed = KafkaConnection.ContinuePartialFrame(ref buffer, ref context);

        await Assert.That(completed).IsTrue();
        await Assert.That(context.Offset).IsEqualTo(totalSize);
        // Should have consumed only 50 bytes, leaving 150
        await Assert.That(buffer.Length).IsEqualTo(150);
    }

    private static byte[] BuildCompleteFrame(int correlationId, int payloadSize)
    {
        var frame = new byte[4 + payloadSize];
        BinaryPrimitives.WriteInt32BigEndian(frame, payloadSize);
        BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(4), correlationId);
        for (var i = 8; i < frame.Length; i++)
            frame[i] = (byte)(i % 256);
        return frame;
    }

    private static byte[] BuildPartialFrame(int correlationId, int totalPayloadSize, int availablePayload)
    {
        var frame = new byte[4 + availablePayload];
        BinaryPrimitives.WriteInt32BigEndian(frame, totalPayloadSize);
        if (availablePayload >= 4)
            BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(4), correlationId);
        for (var i = 8; i < frame.Length; i++)
            frame[i] = (byte)(i % 256);
        return frame;
    }

    private static void ReturnBuffer(ref KafkaConnection.PartialFrameContext context)
    {
        if (context.IsPooled && context.Buffer is not null)
            ResponseBufferPool.Default.Pool.Return(context.Buffer);
        context = default;
    }
}
