#if NETSTANDARD2_0
namespace Dekaf.Compatibility;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;

internal class ArgumentExceptionCompat : System.ArgumentException
{
    public ArgumentExceptionCompat()
    {
    }

    public ArgumentExceptionCompat(string? message)
        : base(message)
    {
    }

    public ArgumentExceptionCompat(string? message, string? paramName)
        : base(message, paramName)
    {
    }

    public static void ThrowIfNullOrEmpty([NotNull] string? argument, string? paramName = null)
    {
        if (argument is null)
            throw new System.ArgumentNullException(paramName);
        if (argument.Length == 0)
            throw new System.ArgumentException("Value cannot be empty.", paramName);
    }

    public static void ThrowIfNullOrWhiteSpace([NotNull] string? argument, string? paramName = null)
    {
        if (argument is null)
            throw new System.ArgumentNullException(paramName);
        if (string.IsNullOrWhiteSpace(argument))
            throw new System.ArgumentException("Value cannot be empty or whitespace.", paramName);
    }
}

internal class ArgumentNullExceptionCompat : System.ArgumentNullException
{
    public ArgumentNullExceptionCompat()
    {
    }

    public ArgumentNullExceptionCompat(string? paramName)
        : base(paramName)
    {
    }

    public ArgumentNullExceptionCompat(string? paramName, string? message)
        : base(paramName, message)
    {
    }

    public static void ThrowIfNull([NotNull] object? argument, string? paramName = null)
    {
        if (argument is null)
            throw new System.ArgumentNullException(paramName);
    }

    public static void ThrowIfNullOrEmpty([NotNull] string? argument, string? paramName = null)
        => ArgumentExceptionCompat.ThrowIfNullOrEmpty(argument, paramName);
}

internal class ArgumentOutOfRangeExceptionCompat : System.ArgumentOutOfRangeException
{
    public ArgumentOutOfRangeExceptionCompat()
    {
    }

    public ArgumentOutOfRangeExceptionCompat(string? paramName)
        : base(paramName)
    {
    }

    public ArgumentOutOfRangeExceptionCompat(string? paramName, string? message)
        : base(paramName, message)
    {
    }

    public ArgumentOutOfRangeExceptionCompat(string? paramName, object? actualValue, string? message)
        : base(paramName, actualValue, message)
    {
    }

    public static void ThrowIfGreaterThan<T>(T value, T other, string? paramName = null)
        where T : IComparable<T>
    {
        if (value.CompareTo(other) > 0)
            throw new System.ArgumentOutOfRangeException(paramName, value, $"Value must be less than or equal to {other}.");
    }

    public static void ThrowIfGreaterThanOrEqual<T>(T value, T other, string? paramName = null)
        where T : IComparable<T>
    {
        if (value.CompareTo(other) >= 0)
            throw new System.ArgumentOutOfRangeException(paramName, value, $"Value must be less than {other}.");
    }

    public static void ThrowIfLessThan<T>(T value, T other, string? paramName = null)
        where T : IComparable<T>
    {
        if (value.CompareTo(other) < 0)
            throw new System.ArgumentOutOfRangeException(paramName, value, $"Value must be greater than or equal to {other}.");
    }

    public static void ThrowIfNegative<T>(T value, string? paramName = null)
        where T : struct, IComparable<T>
    {
        if (value.CompareTo(default) < 0)
            throw new System.ArgumentOutOfRangeException(paramName, value, "Value must be non-negative.");
    }

    public static void ThrowIfNegativeOrZero<T>(T value, string? paramName = null)
        where T : struct, IComparable<T>
    {
        if (value.CompareTo(default) <= 0)
            throw new System.ArgumentOutOfRangeException(paramName, value, "Value must be positive.");
    }

    public static void ThrowIfZero<T>(T value, string? paramName = null)
        where T : struct, IComparable<T>
    {
        if (value.CompareTo(default) == 0)
            throw new System.ArgumentOutOfRangeException(paramName, value, "Value must be non-zero.");
    }
}

internal static class ArrayCompat
{
    public const int MaxLength = 0x7FFFFFC7;

    public static T[] Empty<T>() => System.Array.Empty<T>();

    public static void Resize<T>(ref T[] array, int newSize) => System.Array.Resize(ref array, newSize);

    public static void Copy(System.Array sourceArray, System.Array destinationArray, int length)
        => System.Array.Copy(sourceArray, destinationArray, length);

    public static void Copy(System.Array sourceArray, int sourceIndex, System.Array destinationArray, int destinationIndex, int length)
        => System.Array.Copy(sourceArray, sourceIndex, destinationArray, destinationIndex, length);

    public static int FindIndex<T>(T[] array, Predicate<T> match) => System.Array.FindIndex(array, match);

    public static void Clear(System.Array array) => System.Array.Clear(array, 0, array.Length);

    public static void Clear(System.Array array, int index, int length) => System.Array.Clear(array, index, length);

    public static void Fill<T>(T[] array, T value)
    {
        for (var i = 0; i < array.Length; i++)
            array[i] = value;
    }
}

internal static class BinaryPrimitivesCompat
{
    public static short ReadInt16BigEndian(ReadOnlySpan<byte> source)
        => System.Buffers.Binary.BinaryPrimitives.ReadInt16BigEndian(source);

    public static int ReadInt32BigEndian(ReadOnlySpan<byte> source)
        => System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(source);

    public static int ReadInt32LittleEndian(ReadOnlySpan<byte> source)
        => System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(source);

    public static long ReadInt64BigEndian(ReadOnlySpan<byte> source)
        => System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(source);

    public static ushort ReadUInt16BigEndian(ReadOnlySpan<byte> source)
        => System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(source);

    public static uint ReadUInt32BigEndian(ReadOnlySpan<byte> source)
        => System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(source);

    public static uint ReadUInt32LittleEndian(ReadOnlySpan<byte> source)
        => System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(source);

    public static ulong ReadUInt64LittleEndian(ReadOnlySpan<byte> source)
        => System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(source);

    public static float ReadSingleBigEndian(ReadOnlySpan<byte> source)
        => BitConverter.ToSingle(ToEndianBytes(source, sizeof(float)), 0);

    public static double ReadDoubleBigEndian(ReadOnlySpan<byte> source)
        => BitConverter.ToDouble(ToEndianBytes(source, sizeof(double)), 0);

    public static void WriteInt16BigEndian(Span<byte> destination, short value)
        => System.Buffers.Binary.BinaryPrimitives.WriteInt16BigEndian(destination, value);

    public static void WriteInt32BigEndian(Span<byte> destination, int value)
        => System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(destination, value);

    public static void WriteInt64BigEndian(Span<byte> destination, long value)
        => System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(destination, value);

    public static void WriteUInt16BigEndian(Span<byte> destination, ushort value)
        => System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(destination, value);

    public static void WriteUInt32BigEndian(Span<byte> destination, uint value)
        => System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(destination, value);

    public static void WriteUInt64LittleEndian(Span<byte> destination, ulong value)
        => System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(destination, value);

    public static void WriteSingleBigEndian(Span<byte> destination, float value)
        => WriteEndianBytes(destination, BitConverter.GetBytes(value));

    public static void WriteDoubleBigEndian(Span<byte> destination, double value)
        => WriteEndianBytes(destination, BitConverter.GetBytes(value));

    private static byte[] ToEndianBytes(ReadOnlySpan<byte> source, int length)
    {
        var bytes = source.Slice(0, length).ToArray();
        if (BitConverter.IsLittleEndian)
            System.Array.Reverse(bytes);
        return bytes;
    }

    private static void WriteEndianBytes(Span<byte> destination, byte[] bytes)
    {
        if (BitConverter.IsLittleEndian)
            System.Array.Reverse(bytes);
        bytes.AsSpan().CopyTo(destination);
    }
}

internal static class BitOperationsCompat
{
    public static int Log2(uint value)
    {
        var result = 0;
        while ((value >>= 1) != 0)
            result++;
        return result;
    }

    public static int Log2(ulong value)
    {
        var result = 0;
        while ((value >>= 1) != 0)
            result++;
        return result;
    }

    public static int RoundUpToPowerOf2(int value)
    {
        if (value <= 1)
            return 1;

        var current = (uint)value - 1;
        current |= current >> 1;
        current |= current >> 2;
        current |= current >> 4;
        current |= current >> 8;
        current |= current >> 16;
        return (int)(current + 1);
    }

    public static uint RoundUpToPowerOf2(uint value)
    {
        if (value <= 1)
            return 1;

        value--;
        value |= value >> 1;
        value |= value >> 2;
        value |= value >> 4;
        value |= value >> 8;
        value |= value >> 16;
        return value + 1;
    }
}

internal static class GuidCompatibility
{
    public static Guid ReadBigEndian(ReadOnlySpan<byte> source)
    {
        Span<byte> bytes = stackalloc byte[16];
        bytes[0] = source[3];
        bytes[1] = source[2];
        bytes[2] = source[1];
        bytes[3] = source[0];
        bytes[4] = source[5];
        bytes[5] = source[4];
        bytes[6] = source[7];
        bytes[7] = source[6];
        source.Slice(8, 8).CopyTo(bytes.Slice(8));
        return new Guid(bytes.ToArray());
    }

    public static void WriteBigEndian(Guid value, Span<byte> destination)
    {
        var bytes = value.ToByteArray();
        destination[0] = bytes[3];
        destination[1] = bytes[2];
        destination[2] = bytes[1];
        destination[3] = bytes[0];
        destination[4] = bytes[5];
        destination[5] = bytes[4];
        destination[6] = bytes[7];
        destination[7] = bytes[6];
        bytes.AsSpan(8, 8).CopyTo(destination.Slice(8));
    }
}

internal static class ConvertCompat
{
    public static byte[] FromBase64String(string s) => System.Convert.FromBase64String(s);

    public static string ToBase64String(byte[] inArray) => System.Convert.ToBase64String(inArray);

    public static string ToHexString(ReadOnlySpan<byte> bytes)
    {
        var chars = new char[bytes.Length * 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            var b = bytes[i];
            chars[i * 2] = GetHexChar(b >> 4);
            chars[(i * 2) + 1] = GetHexChar(b & 0xF);
        }

        return new string(chars);
    }

    private static char GetHexChar(int value)
        => (char)(value < 10 ? '0' + value : 'A' + (value - 10));
}

internal static class DnsCompat
{
    public static Task<IPAddress[]> GetHostAddressesAsync(string hostNameOrAddress, CancellationToken cancellationToken)
        => System.Net.Dns.GetHostAddressesAsync(hostNameOrAddress).WaitAsync(cancellationToken);

    public static Task<IPHostEntry> GetHostEntryAsync(string hostNameOrAddress, CancellationToken cancellationToken)
        => System.Net.Dns.GetHostEntryAsync(hostNameOrAddress).WaitAsync(cancellationToken);
}

internal static class FileCompat
{
    public static bool Exists(string? path) => System.IO.File.Exists(path);

    public static string ReadAllText(string path) => System.IO.File.ReadAllText(path);

    public static async Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var read = reader.ReadToEndAsync();
        await read.WaitAsync(cancellationToken).ConfigureAwait(false);
        return await read.ConfigureAwait(false);
    }

    public static IEnumerable<string> ReadLines(string path) => System.IO.File.ReadLines(path);
}

internal static class GCCompat
{
    public static T[] AllocateUninitializedArray<T>(int length, bool pinned = false) => new T[length];

    public static GCMemoryInfoCompat GetGCMemoryInfo()
        => new((long)System.Math.Min(long.MaxValue, (double)System.Environment.WorkingSet + (1024.0 * 1024.0 * 1024.0)));
}

internal readonly struct GCMemoryInfoCompat
{
    public GCMemoryInfoCompat(long totalAvailableMemoryBytes)
    {
        TotalAvailableMemoryBytes = totalAvailableMemoryBytes;
    }

    public long TotalAvailableMemoryBytes { get; }
}

internal static class HMACSHA256Compat
{
    public static byte[] HashData(byte[] key, byte[] source)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(key);
        return hmac.ComputeHash(source);
    }
}

internal static class HMACSHA512Compat
{
    public static byte[] HashData(byte[] key, byte[] source)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA512(key);
        return hmac.ComputeHash(source);
    }
}

internal static class MathCompat
{
    public static short Max(short val1, short val2) => val1 > val2 ? val1 : val2;
    public static uint Max(uint val1, uint val2) => val1 > val2 ? val1 : val2;
    public static ulong Max(ulong val1, ulong val2) => val1 > val2 ? val1 : val2;
    public static int Max(int val1, int val2) => System.Math.Max(val1, val2);
    public static long Max(long val1, long val2) => System.Math.Max(val1, val2);
    public static double Max(double val1, double val2) => System.Math.Max(val1, val2);
    public static short Min(short val1, short val2) => val1 < val2 ? val1 : val2;
    public static uint Min(uint val1, uint val2) => val1 < val2 ? val1 : val2;
    public static ulong Min(ulong val1, ulong val2) => val1 < val2 ? val1 : val2;
    public static int Min(int val1, int val2) => System.Math.Min(val1, val2);
    public static long Min(long val1, long val2) => System.Math.Min(val1, val2);
    public static double Min(double val1, double val2) => System.Math.Min(val1, val2);
    public static double Ceiling(double a) => System.Math.Ceiling(a);
    public static double Pow(double x, double y) => System.Math.Pow(x, y);
    public static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
    public static long Clamp(long value, long min, long max) => value < min ? min : value > max ? max : value;
    public static double Clamp(double value, double min, double max) => value < min ? min : value > max ? max : value;
}

internal static class EnvironmentCompat
{
    public static long TickCount64
        => (long)(
            System.Diagnostics.Stopwatch.GetTimestamp()
            * 1000.0
            / System.Diagnostics.Stopwatch.Frequency);
}

internal static class ObjectDisposedExceptionCompat
{
    public static void ThrowIf(bool condition, object? instance)
    {
        if (condition)
            throw new ObjectDisposedException(instance?.GetType().FullName);
    }
}

internal static class OperatingSystemCompat
{
    public static bool IsWindows()
        => System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
            System.Runtime.InteropServices.OSPlatform.Windows);
}

internal static class RandomCompat
{
    public static ThreadSafeRandom Shared { get; } = new();

    internal sealed class ThreadSafeRandom
    {
        private int _seed = System.Environment.TickCount;
        private readonly ThreadLocal<System.Random> _local;

        public ThreadSafeRandom()
        {
            _local = new ThreadLocal<System.Random>(() => new System.Random(Interlocked.Increment(ref _seed)));
        }

        public int Next(int maxValue) => _local.Value!.Next(maxValue);

        public int Next(int minValue, int maxValue) => _local.Value!.Next(minValue, maxValue);

        public double NextDouble() => _local.Value!.NextDouble();
    }
}

internal static class RandomNumberGeneratorCompat
{
    public static byte[] GetBytes(int count)
    {
        var bytes = new byte[count];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return bytes;
    }
}

internal static class Rfc2898DeriveBytesCompat
{
    public static byte[] Pbkdf2(
        byte[] password,
        byte[] salt,
        int iterations,
        HashAlgorithmName hashAlgorithm,
        int outputLength)
    {
        if (iterations <= 0)
            throw new System.ArgumentOutOfRangeException(nameof(iterations));

        using var hmac = CreateHmac(hashAlgorithm, password);
        var result = new byte[outputLength];
        var block = new byte[hmac.HashSize / 8];
        var saltAndBlock = new byte[salt.Length + 4];
        salt.CopyTo(saltAndBlock, 0);

        var offset = 0;
        for (var blockIndex = 1; offset < outputLength; blockIndex++)
        {
            saltAndBlock[salt.Length] = (byte)(blockIndex >> 24);
            saltAndBlock[salt.Length + 1] = (byte)(blockIndex >> 16);
            saltAndBlock[salt.Length + 2] = (byte)(blockIndex >> 8);
            saltAndBlock[salt.Length + 3] = (byte)blockIndex;

            var u = hmac.ComputeHash(saltAndBlock);
            u.CopyTo(block, 0);
            for (var i = 1; i < iterations; i++)
            {
                u = hmac.ComputeHash(u);
                for (var j = 0; j < block.Length; j++)
                    block[j] ^= u[j];
            }

            var count = System.Math.Min(block.Length, outputLength - offset);
            System.Array.Copy(block, 0, result, offset, count);
            offset += count;
        }

        return result;
    }

    private static HMAC CreateHmac(HashAlgorithmName hashAlgorithm, byte[] key)
    {
        if (hashAlgorithm == HashAlgorithmName.SHA256)
            return new System.Security.Cryptography.HMACSHA256(key);
        if (hashAlgorithm == HashAlgorithmName.SHA512)
            return new System.Security.Cryptography.HMACSHA512(key);

        throw new NotSupportedException($"PBKDF2 hash algorithm '{hashAlgorithm.Name}' is not supported.");
    }
}

internal static class SHA256Compat
{
    public static byte[] HashData(byte[] source)
    {
        using var hash = System.Security.Cryptography.SHA256.Create();
        return hash.ComputeHash(source);
    }

    public static byte[] HashData(ReadOnlySpan<byte> source) => HashData(source.ToArray());
}

internal static class SHA512Compat
{
    public static byte[] HashData(byte[] source)
    {
        using var hash = System.Security.Cryptography.SHA512.Create();
        return hash.ComputeHash(source);
    }
}

internal static class StopwatchCompat
{
    public static readonly long Frequency = System.Diagnostics.Stopwatch.Frequency;

    public static long GetTimestamp() => System.Diagnostics.Stopwatch.GetTimestamp();

    public static TimeSpan GetElapsedTime(long startingTimestamp)
        => GetElapsedTime(startingTimestamp, GetTimestamp());

    public static TimeSpan GetElapsedTime(long startingTimestamp, long endingTimestamp)
    {
        var ticks = (endingTimestamp - startingTimestamp) * (double)TimeSpan.TicksPerSecond / Frequency;
        return new TimeSpan((long)ticks);
    }
}

internal static class ThreadCompat
{
    public static int GetCurrentProcessorId() => System.Environment.CurrentManagedThreadId;
}
#endif
