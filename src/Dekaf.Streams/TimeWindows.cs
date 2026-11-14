using System.Buffers;
using System.Buffers.Binary;
using Dekaf.Serialization;

namespace Dekaf.Streams;

/// <summary>Fixed-size time windows aligned to the Unix epoch.</summary>
public sealed class TimeWindows
{
    private TimeWindows(long sizeMs, long advanceMs, long graceMs, bool emitOnClose)
    {
        SizeMs = sizeMs;
        AdvanceMs = advanceMs;
        GraceMs = graceMs;
        IsEmitOnClose = emitOnClose;
    }

    internal long SizeMs { get; }

    internal long AdvanceMs { get; }

    internal long GraceMs { get; }

    /// <summary>Gets the window size.</summary>
    public TimeSpan Size => TimeSpan.FromMilliseconds(SizeMs);

    /// <summary>Gets the interval between window starts.</summary>
    public TimeSpan Advance => TimeSpan.FromMilliseconds(AdvanceMs);

    /// <summary>Gets how long after its end a window still accepts out-of-order records.</summary>
    public TimeSpan Grace => TimeSpan.FromMilliseconds(GraceMs);

    /// <summary>Gets whether a window's result is forwarded once, when the window closes.</summary>
    public bool IsEmitOnClose { get; }

    /// <summary>Creates non-overlapping windows of <paramref name="size"/>.</summary>
    public static TimeWindows Tumbling(TimeSpan size) => Hopping(size, size);

    /// <summary>Creates windows of <paramref name="size"/> that start every <paramref name="advance"/>.</summary>
    public static TimeWindows Hopping(TimeSpan size, TimeSpan advance)
    {
        var sizeMs = (long)size.TotalMilliseconds;
        var advanceMs = (long)advance.TotalMilliseconds;
        ArgumentOutOfRangeException.ThrowIfLessThan(sizeMs, 1, nameof(size));
        ArgumentOutOfRangeException.ThrowIfLessThan(advanceMs, 1, nameof(advance));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(advanceMs, sizeMs, nameof(advance));
        return new TimeWindows(sizeMs, advanceMs, graceMs: 0, emitOnClose: false);
    }

    /// <summary>Accepts out-of-order records for <paramref name="grace"/> after a window ends.</summary>
    public TimeWindows WithGrace(TimeSpan grace)
    {
        var graceMs = (long)grace.TotalMilliseconds;
        ArgumentOutOfRangeException.ThrowIfNegative(graceMs, nameof(grace));
        return new TimeWindows(SizeMs, AdvanceMs, graceMs, IsEmitOnClose);
    }

    /// <summary>Forwards each window's result once, when the window closes, instead of on every update.</summary>
    public TimeWindows EmitOnClose() => new(SizeMs, AdvanceMs, GraceMs, emitOnClose: true);
}

/// <summary>Creates serdes for <see cref="Windowed{TKey}"/> keys.</summary>
public static class WindowedSerde
{
    /// <summary>
    /// Creates a serde that writes the key followed by the window start as a big-endian 64-bit integer,
    /// the format of the Java time-windowed serde.
    /// </summary>
    /// <param name="keySerde">The serde of the inner key.</param>
    /// <param name="windowSize">The window size, used to compute the window end when reading.</param>
    public static ISerde<Windowed<TKey>> Create<TKey>(ISerde<TKey> keySerde, TimeSpan windowSize)
    {
        ArgumentNullException.ThrowIfNull(keySerde);
        return new Serde<TKey>(keySerde, (long)windowSize.TotalMilliseconds);
    }

    private sealed class Serde<TKey>(ISerde<TKey> inner, long sizeMs) : ISerde<Windowed<TKey>>
    {
        public void Serialize<TWriter>(Windowed<TKey> value, ref TWriter destination, SerializationContext context)
            where TWriter : IBufferWriter<byte>
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
        {
            inner.Serialize(value.Key, ref destination, context);
            BinaryPrimitives.WriteInt64BigEndian(destination.GetSpan(sizeof(long)), value.Start);
            destination.Advance(sizeof(long));
        }

        public Windowed<TKey> Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
        {
            if (data.Length < sizeof(long))
            {
                throw new StreamsException("A windowed key must end with an 8-byte window start.");
            }

            var keyLength = data.Length - sizeof(long);
            var start = BinaryPrimitives.ReadInt64BigEndian(data.Span[keyLength..]);
            return new Windowed<TKey>(inner.Deserialize(data[..keyLength], context), start, start + sizeMs);
        }
    }
}
