using System.Buffers;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

[MemoryDiagnoser]
public class ShareResponseFrameBench
{
    private delegate ShareFetchResponse Parser(PooledResponseBuffer buffer, short version,
        bool checkCrcs, IResponseMemoryReservation? reservation);
    private static readonly Parser Parse = typeof(KafkaConnection)
        .GetMethod("ParsePipelinedResponse", BindingFlags.NonPublic | BindingFlags.Static)!
        .MakeGenericMethod(typeof(ShareFetchRequest), typeof(ShareFetchResponse)).CreateDelegate<Parser>();
    private readonly ResponseBufferPool _pool = new(1024 * 1024, managedArraysPerBucket: 1);
    private byte[] _encoded = null!;

    [Params(0, 32768, 65536)] public int RecordBytes { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(0);
        writer.WriteInt16(0);
        writer.WriteCompactString(null);
        writer.WriteInt32(30000);
        writer.WriteUnsignedVarInt(2);
        writer.WriteUuid(Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"));
        writer.WriteUnsignedVarInt(2);
        writer.WriteInt32(0);
        writer.WriteInt16(0);
        writer.WriteCompactString(null);
        writer.WriteInt16(0);
        writer.WriteCompactString(null);
        writer.WriteInt32(1);
        writer.WriteInt32(1);
        writer.WriteUnsignedVarInt(0);
        writer.WriteUnsignedVarInt(RecordBytes + 1);
        var records = new byte[RecordBytes];
        Array.Fill(records, (byte)71);
        writer.WriteRawBytes(records);
        writer.WriteUnsignedVarInt(1);
        writer.WriteUnsignedVarInt(0);
        writer.WriteUnsignedVarInt(0);
        writer.WriteUnsignedVarInt(1);
        writer.WriteUnsignedVarInt(0);
        _encoded = buffer.WrittenSpan.ToArray();
        DecodeStableFrame();
        CopyAndDecodePooledFrame();
    }

    // A stable externally-owned frame isolates decode/ownership bookkeeping.
    // Neither product can invalidate these bytes by returning an array to a pool.
    [Benchmark]
    public int DecodeStableFrame() => Read(new PooledResponseBuffer(_encoded, _encoded.Length, false));

    // This second boundary includes an identical simulated receive copy and pool return.
    // No subsequent rental happens until all response bytes have been checked.
    [Benchmark]
    public int CopyAndDecodePooledFrame()
    {
        var buffer = _pool.Pool.Rent(_encoded.Length);
        _encoded.CopyTo(buffer, 0);
        return Read(new PooledResponseBuffer(buffer, _encoded.Length, true, pool: _pool));
    }

    private int Read(PooledResponseBuffer buffer)
    {
        var response = Parse(buffer, 1, false, null);
        try
        {
            var bytes = response.Responses[0].Partitions[0].RecordBytes.Span;
            if (bytes.Length != RecordBytes || (bytes.Length > 0 && (bytes[0] != 71 || bytes[^1] != 71)))
                throw new InvalidOperationException("ShareFetch frame content changed.");
            return bytes.Length;
        }
        finally
        {
            // Baseline has no disposal API. The same completed decode boundary applies.
            if ((object)response is IDisposable disposable) disposable.Dispose();
        }
    }
}
