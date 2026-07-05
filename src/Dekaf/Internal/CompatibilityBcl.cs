using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Dekaf.Internal;

internal static class CompatibilityBcl
{
    public static int ArrayMaxLength
    {
        get
        {
#if NETSTANDARD2_0
            return 0x7FFFFFC7;
#else
            return Array.MaxLength;
#endif
        }
    }

    public static long TickCount64
    {
        get
        {
#if NETSTANDARD2_0
            return Stopwatch.GetTimestamp() * 1000 / Stopwatch.Frequency;
#else
            return Environment.TickCount64;
#endif
        }
    }

    public static DateTime UnixEpoch { get; } = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static TimeSpan GetElapsedTime(long startingTimestamp)
    {
#if NETSTANDARD2_0
        return GetElapsedTime(startingTimestamp, Stopwatch.GetTimestamp());
#else
        return Stopwatch.GetElapsedTime(startingTimestamp);
#endif
    }

    public static TimeSpan GetElapsedTime(long startingTimestamp, long endingTimestamp)
    {
#if NETSTANDARD2_0
        var elapsedTicks = endingTimestamp - startingTimestamp;
        var ticks = (long)(elapsedTicks * (TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency));
        return new TimeSpan(ticks);
#else
        return Stopwatch.GetElapsedTime(startingTimestamp, endingTimestamp);
#endif
    }

    public static T Clamp<T>(T value, T min, T max)
        where T : IComparable<T>
    {
        if (min.CompareTo(max) > 0)
            throw new ArgumentException("Minimum value must not exceed maximum value.", nameof(min));
        if (value.CompareTo(min) < 0)
            return min;
        return value.CompareTo(max) > 0 ? max : value;
    }

    public static ValueTask CompletedValueTask => default;

    public static ValueTask<T> FromException<T>(Exception exception)
    {
#if NETSTANDARD2_0
        return new ValueTask<T>(Task.FromException<T>(exception));
#else
        return ValueTask.FromException<T>(exception);
#endif
    }

    public static int RandomNext(int minValue, int maxValue)
        => CompatibilityRandom.Next(minValue, maxValue);

    public static int RandomNext(int maxValue)
        => CompatibilityRandom.Next(maxValue);

    public static double RandomNextDouble()
        => CompatibilityRandom.NextDouble();

    public static Guid ReadGuidBigEndian(ReadOnlySpan<byte> source)
    {
#if NETSTANDARD2_0
        if (source.Length < 16)
            throw new ArgumentException("Guid source must contain at least 16 bytes.", nameof(source));

        var bytes = source.Slice(0, 16).ToArray();
        Array.Reverse(bytes, 0, 4);
        Array.Reverse(bytes, 4, 2);
        Array.Reverse(bytes, 6, 2);
        return new Guid(bytes);
#else
        return new Guid(source, bigEndian: true);
#endif
    }

    public static bool TryWriteGuidBigEndian(Guid value, Span<byte> destination)
    {
        if (destination.Length < 16)
            return false;

#if NETSTANDARD2_0
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
        return true;
#else
        return value.TryWriteBytes(destination, bigEndian: true, out _);
#endif
    }

    public static double ReadDoubleBigEndian(ReadOnlySpan<byte> source)
    {
#if NETSTANDARD2_0
        if (source.Length < sizeof(double))
            throw new ArgumentException("Double source must contain at least 8 bytes.", nameof(source));

        Span<byte> bytes = stackalloc byte[sizeof(double)];
        source.Slice(0, sizeof(double)).CopyTo(bytes);
        if (BitConverter.IsLittleEndian)
            Reverse(bytes);
        return BitConverter.ToDouble(bytes.ToArray(), 0);
#else
        return System.Buffers.Binary.BinaryPrimitives.ReadDoubleBigEndian(source);
#endif
    }

    public static void WriteDoubleBigEndian(Span<byte> destination, double value)
    {
#if NETSTANDARD2_0
        if (destination.Length < sizeof(double))
            throw new ArgumentException("Double destination must contain at least 8 bytes.", nameof(destination));

        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        bytes.CopyTo(destination);
#else
        System.Buffers.Binary.BinaryPrimitives.WriteDoubleBigEndian(destination, value);
#endif
    }

    public static float ReadSingleBigEndian(ReadOnlySpan<byte> source)
    {
#if NETSTANDARD2_0
        if (source.Length < sizeof(float))
            throw new ArgumentException("Single source must contain at least 4 bytes.", nameof(source));

        Span<byte> bytes = stackalloc byte[sizeof(float)];
        source.Slice(0, sizeof(float)).CopyTo(bytes);
        if (BitConverter.IsLittleEndian)
            Reverse(bytes);
        return BitConverter.ToSingle(bytes.ToArray(), 0);
#else
        return System.Buffers.Binary.BinaryPrimitives.ReadSingleBigEndian(source);
#endif
    }

    public static void WriteSingleBigEndian(Span<byte> destination, float value)
    {
#if NETSTANDARD2_0
        if (destination.Length < sizeof(float))
            throw new ArgumentException("Single destination must contain at least 4 bytes.", nameof(destination));

        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        bytes.CopyTo(destination);
#else
        System.Buffers.Binary.BinaryPrimitives.WriteSingleBigEndian(destination, value);
#endif
    }

    public static bool IsFinite(double value)
        => !double.IsNaN(value) && !double.IsInfinity(value);

    public static bool IsFinite(float value)
        => !float.IsNaN(value) && !float.IsInfinity(value);

    public static byte[] GetRandomBytes(int count)
    {
#if NETSTANDARD2_0
        var bytes = new byte[count];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return bytes;
#else
        return RandomNumberGenerator.GetBytes(count);
#endif
    }

    public static T[] AllocateUninitializedArray<T>(int length)
    {
#if NETSTANDARD2_0
        return new T[length];
#else
        return GC.AllocateUninitializedArray<T>(length);
#endif
    }

    public static T[] AllocateUninitializedArray<T>(int length, bool pinned)
    {
#if NETSTANDARD2_0
        return new T[length];
#else
        return GC.AllocateUninitializedArray<T>(length, pinned);
#endif
    }

    public static ulong GetTotalAvailableMemoryBytes(ulong fallback)
    {
#if NETSTANDARD2_0
        return fallback;
#else
        var available = (ulong)GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        return available == 0 ? fallback : available;
#endif
    }

    public static int GetCurrentProcessorId()
    {
#if NETSTANDARD2_0
        return Environment.CurrentManagedThreadId;
#else
        return Thread.GetCurrentProcessorId();
#endif
    }

    public static void FillArray<T>(T[] array, T value)
    {
#if NETSTANDARD2_0
        for (var i = 0; i < array.Length; i++)
        {
            array[i] = value;
        }
#else
        Array.Fill(array, value);
#endif
    }

    public static bool IsCompletedSuccessfully(Task task)
    {
#if NETSTANDARD2_0
        return task.Status == TaskStatus.RanToCompletion;
#else
        return task.IsCompletedSuccessfully;
#endif
    }

    public static int GetUtf8Bytes(string value, Span<byte> destination)
    {
#if NETSTANDARD2_0
        var bytes = Encoding.UTF8.GetBytes(value);
        bytes.CopyTo(destination);
        return bytes.Length;
#else
        return Encoding.UTF8.GetBytes(value, destination);
#endif
    }

    public static byte[] Sha256HashData(ReadOnlySpan<byte> data)
    {
#if NETSTANDARD2_0
        using var hash = SHA256.Create();
        return hash.ComputeHash(data.ToArray());
#else
        return SHA256.HashData(data);
#endif
    }

    public static byte[] Sha512HashData(ReadOnlySpan<byte> data)
    {
#if NETSTANDARD2_0
        using var hash = SHA512.Create();
        return hash.ComputeHash(data.ToArray());
#else
        return SHA512.HashData(data);
#endif
    }

    public static byte[] HmacSha256HashData(byte[] key, byte[] data)
    {
#if NETSTANDARD2_0
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(data);
#else
        return HMACSHA256.HashData(key, data);
#endif
    }

    public static byte[] HmacSha512HashData(byte[] key, byte[] data)
    {
#if NETSTANDARD2_0
        using var hmac = new HMACSHA512(key);
        return hmac.ComputeHash(data);
#else
        return HMACSHA512.HashData(key, data);
#endif
    }

    public static long DoubleToInt64Bits(double value)
    {
#if NETSTANDARD2_0
        var bytes = BitConverter.GetBytes(value);
        return BitConverter.ToInt64(bytes, 0);
#else
        return BitConverter.DoubleToInt64Bits(value);
#endif
    }

    public static byte[] Pbkdf2(
        byte[] password,
        byte[] salt,
        int iterations,
        HashAlgorithmName hashAlgorithm,
        int outputLength)
    {
#if NETSTANDARD2_0
        return Pbkdf2NetStandard(password, salt, iterations, hashAlgorithm, outputLength);
#else
        return Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, hashAlgorithm, outputLength);
#endif
    }

#if NETSTANDARD2_0
    private static byte[] Pbkdf2NetStandard(
        byte[] password,
        byte[] salt,
        int iterations,
        HashAlgorithmName hashAlgorithm,
        int outputLength)
    {
        if (iterations <= 0)
            throw new ArgumentOutOfRangeException(nameof(iterations));
        if (outputLength < 0)
            throw new ArgumentOutOfRangeException(nameof(outputLength));

        using var hmac = CreatePbkdf2Hmac(hashAlgorithm, password);
        var hashLength = hmac.HashSize / 8;
        var blockCount = (outputLength + hashLength - 1) / hashLength;
        var result = new byte[outputLength];
        var saltAndBlock = new byte[salt.Length + 4];
        Buffer.BlockCopy(salt, 0, saltAndBlock, 0, salt.Length);

        var destinationOffset = 0;
        for (var block = 1; block <= blockCount; block++)
        {
            WriteInt32BigEndian(saltAndBlock, salt.Length, block);
            var u = hmac.ComputeHash(saltAndBlock);
            var t = (byte[])u.Clone();

            for (var i = 1; i < iterations; i++)
            {
                u = hmac.ComputeHash(u);
                for (var j = 0; j < t.Length; j++)
                {
                    t[j] ^= u[j];
                }
            }

            var bytesToCopy = Math.Min(hashLength, outputLength - destinationOffset);
            Buffer.BlockCopy(t, 0, result, destinationOffset, bytesToCopy);
            destinationOffset += bytesToCopy;
        }

        return result;
    }

    private static HMAC CreatePbkdf2Hmac(HashAlgorithmName hashAlgorithm, byte[] password)
    {
        if (hashAlgorithm == HashAlgorithmName.SHA256)
            return new HMACSHA256(password);
        if (hashAlgorithm == HashAlgorithmName.SHA384)
            return new HMACSHA384(password);
        if (hashAlgorithm == HashAlgorithmName.SHA512)
            return new HMACSHA512(password);

        throw new NotSupportedException($"PBKDF2 hash algorithm '{hashAlgorithm.Name}' is not supported.");
    }

    private static void WriteInt32BigEndian(byte[] buffer, int offset, int value)
    {
        unchecked
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }
    }
#endif

    public static bool FixedTimeEquals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
#if NETSTANDARD2_0
        if (left.Length != right.Length)
            return false;

        var differences = 0;
        for (var i = 0; i < left.Length; i++)
        {
            differences |= left[i] ^ right[i];
        }

        return differences == 0;
#else
        return CryptographicOperations.FixedTimeEquals(left, right);
#endif
    }

    public static void ZeroMemory(Span<byte> buffer)
    {
#if NETSTANDARD2_0
        buffer.Clear();
#else
        CryptographicOperations.ZeroMemory(buffer);
#endif
    }

    public static string Base64UrlEncode(ReadOnlySpan<byte> data)
    {
        return Convert.ToBase64String(data.ToArray())
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static string ToHexStringLower(ReadOnlySpan<byte> bytes)
    {
        const string hex = "0123456789abcdef";
        var chars = new char[bytes.Length * 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            chars[i * 2] = hex[bytes[i] >> 4];
            chars[(i * 2) + 1] = hex[bytes[i] & 0xF];
        }

        return new string(chars);
    }

    public static async Task<IPAddress[]> GetHostAddressesAsync(string host, CancellationToken cancellationToken)
    {
#if NETSTANDARD2_0
        var task = Dns.GetHostAddressesAsync(host);
        return await task.WaitAsync(cancellationToken).ConfigureAwait(false);
#else
        return await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
#endif
    }

    public static async Task<IPHostEntry> GetHostEntryAsync(string host, CancellationToken cancellationToken)
    {
#if NETSTANDARD2_0
        var task = Dns.GetHostEntryAsync(host);
        return await task.WaitAsync(cancellationToken).ConfigureAwait(false);
#else
        return await Dns.GetHostEntryAsync(host, cancellationToken).ConfigureAwait(false);
#endif
    }

    private static void Reverse(Span<byte> bytes)
    {
        for (int left = 0, right = bytes.Length - 1; left < right; left++, right--)
        {
            (bytes[left], bytes[right]) = (bytes[right], bytes[left]);
        }
    }
}

internal static class CompatibilityRandom
{
    [ThreadStatic]
    private static Random? t_random;

    public static int Next(int minValue, int maxValue)
        => Current.Next(minValue, maxValue);

    public static int Next(int maxValue)
        => Current.Next(maxValue);

    public static double NextDouble()
        => Current.NextDouble();

    private static Random Current
    {
        get
        {
#if NETSTANDARD2_0
            var threadId = Environment.CurrentManagedThreadId;
#else
            var threadId = Environment.CurrentManagedThreadId;
#endif
            return t_random ??= new Random(unchecked(Environment.TickCount * 31 + threadId));
        }
    }
}
