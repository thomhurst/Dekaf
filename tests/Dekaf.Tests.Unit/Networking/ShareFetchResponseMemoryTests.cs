using System.Buffers;
using System.Reflection;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Networking;

public sealed class ShareFetchResponseMemoryTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ErrorMessage_RemainsAvailableAfterRepeatedDisposal(bool retained)
    {
        var reservation = new Reservation();
        var response = new ShareFetchResponse
        {
            ErrorCode = ErrorCode.UnknownServerError,
            ErrorMessage = "broker diagnostic",
            Responses = [], NodeEndpoints = []
        };
        if (retained)
            response.PooledMemoryOwner = new PooledResponseBuffer(new byte[1], 1, false)
                .TransferOwnership(reservation);
        await Assert.That(response.ErrorMessage).IsEqualTo("broker diagnostic");
        response.Dispose();
        response.Dispose();
        await Assert.That(response.ErrorMessage).IsEqualTo("broker diagnostic");
        await Assert.That(response.PooledMemoryOwner).IsNull();
        await Assert.That(reservation.Disposals).IsEqualTo(retained ? 1 : 0);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task ManagedFrame_RetainsOnlyPooledMemoryOrActiveReservation(bool pooled, bool reserved)
    {
        var encoded = Encode([17, 23, 41, 59]);
        var pool = new ResponseBufferPool(1024 * 1024, managedArraysPerBucket: 1);
        var bytes = pooled ? pool.Pool.Rent(encoded.WrittenCount) : new byte[encoded.WrittenCount];
        encoded.WrittenSpan.CopyTo(bytes);
        var reservation = reserved ? new Reservation() : null;
        var parse = typeof(KafkaConnection).GetMethod("ParsePipelinedResponse", BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(typeof(ShareFetchRequest), typeof(ShareFetchResponse))
            .CreateDelegate<Func<PooledResponseBuffer, short, bool, IResponseMemoryReservation?, ShareFetchResponse>>();
        using var response = parse(new PooledResponseBuffer(bytes, encoded.WrittenCount, pooled, pool: pool), 1, false, reservation);
        await Assert.That(response.PooledMemoryOwner is not null).IsEqualTo(pooled || reserved);
        await Assert.That(response.Responses[0].Partitions[0].RecordBytes.ToArray()).IsEquivalentTo(new byte[] { 17, 23, 41, 59 });
        if (reservation is not null)
            await Assert.That(reservation.Disposals).IsEqualTo(0);
        response.Dispose();
        response.Dispose();
        if (reservation is not null)
            await Assert.That(reservation.Disposals).IsEqualTo(1);
    }

    private sealed class Reservation : IResponseMemoryReservation
    {
        public int Disposals { get; private set; }
        public void Dispose() => Disposals++;
    }

    [Test]
    public async Task NativeRecords_RemainReadableUntilPublicResponseDisposal()
    {
        byte[] records = [17, 23, 41, 59];
        var encoded = Encode(records);
        var pool = new ResponseBufferPool(1024 * 1024, managedArraysPerBucket: 1);
        var native = pool.RentNative(ResponseBufferPool.NativeMemoryThresholdBytes);
        encoded.WrittenSpan.CopyTo(native.GetSpan());
        try
        {
            using var response = KafkaConnection.ParseFetchResponse<ShareFetchRequest, ShareFetchResponse>(
                new PooledResponseBuffer(native, encoded.WrittenCount), 1);
            await Assert.That(response.Responses[0].Partitions[0].RecordBytes.ToArray()).IsEquivalentTo(records);
            await Assert.That(pool.RetainedNativeBufferCount).IsEqualTo(0);
            ((IDisposable)response).Dispose();
            response.Dispose();
            await Assert.That(pool.RetainedNativeBufferCount).IsEqualTo(1);
        }
        finally
        {
            pool.TrimNativeBuffers();
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task EmptyNativeResponse_ReleasesFrameImmediately(bool reserved)
    {
        var encoded = Encode([]);
        var reservation = reserved ? new Reservation() : null;
        var pool = new ResponseBufferPool(1024 * 1024, managedArraysPerBucket: 1);
        var native = pool.RentNative(ResponseBufferPool.NativeMemoryThresholdBytes);
        encoded.WrittenSpan.CopyTo(native.GetSpan());
        try
        {
            using var response = KafkaConnection.ParseFetchResponse<ShareFetchRequest, ShareFetchResponse>(
                new PooledResponseBuffer(native, encoded.WrittenCount), 1, reservation: reservation);

            await Assert.That(response.Responses[0].Partitions[0].RecordBytes.IsEmpty).IsTrue();
            await Assert.That(response.PooledMemoryOwner).IsNull();
            await Assert.That(pool.RetainedNativeBufferCount).IsEqualTo(1);
            if (reservation is not null)
                await Assert.That(reservation.Disposals).IsEqualTo(1);
            response.Dispose();
            await Assert.That(pool.RetainedNativeBufferCount).IsEqualTo(1);
            if (reservation is not null)
                await Assert.That(reservation.Disposals).IsEqualTo(1);
        }
        finally
        {
            pool.TrimNativeBuffers();
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task MalformedNativeResponse_ReturnsFrameWhenParsingFails(bool reserved)
    {
        var encoded = Encode([1, 2, 3]);
        var reservation = reserved ? new Reservation() : null;
        var pool = new ResponseBufferPool(1024 * 1024, managedArraysPerBucket: 1);
        var native = pool.RentNative(ResponseBufferPool.NativeMemoryThresholdBytes);
        encoded.WrittenSpan.CopyTo(native.GetSpan());
        var truncated = new PooledResponseBuffer(native, encoded.WrittenCount - 1);

        try
        {
            await Assert.That(() =>
            {
                using var unexpected = KafkaConnection.ParseFetchResponse<ShareFetchRequest, ShareFetchResponse>(truncated, 1, reservation: reservation);
            }).ThrowsException();
            await Assert.That(pool.RetainedNativeBufferCount).IsEqualTo(1);
            if (reservation is not null)
                await Assert.That(reservation.Disposals).IsEqualTo(1);
        }
        finally
        {
            pool.TrimNativeBuffers();
        }
    }

    private static ArrayBufferWriter<byte> Encode(ReadOnlySpan<byte> records)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(0); // throttle
        writer.WriteInt16(0); // top-level error
        writer.WriteCompactString(null);
        writer.WriteInt32(30000); // acquisition lock timeout
        writer.WriteUnsignedVarInt(2); // one topic
        writer.WriteUuid(Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"));
        writer.WriteUnsignedVarInt(2); // one partition
        writer.WriteInt32(0);
        writer.WriteInt16(0);
        writer.WriteCompactString(null);
        writer.WriteInt16(0); // acknowledgement error
        writer.WriteCompactString(null);
        writer.WriteInt32(1); // leader ID
        writer.WriteInt32(1); // leader epoch
        writer.WriteUnsignedVarInt(0); // leader tags
        writer.WriteUnsignedVarInt(records.Length + 1);
        writer.WriteRawBytes(records);
        writer.WriteUnsignedVarInt(2); // one acquired range
        writer.WriteInt64(0);
        writer.WriteInt64(0);
        writer.WriteInt16(1); // delivery count
        writer.WriteUnsignedVarInt(0); // acquired range tags
        writer.WriteUnsignedVarInt(0); // partition tags
        writer.WriteUnsignedVarInt(0); // topic tags
        writer.WriteUnsignedVarInt(1); // no node endpoints
        writer.WriteUnsignedVarInt(0); // response tags
        return buffer;
    }
}
