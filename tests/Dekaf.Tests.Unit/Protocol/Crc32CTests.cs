using System.Runtime.Intrinsics.X86;
using Dekaf.Protocol.Records;
using ArmCrc32 = System.Runtime.Intrinsics.Arm.Crc32;

namespace Dekaf.Tests.Unit.Protocol;

public class Crc32CTests
{
    [Test]
    public async Task Compute_EmptyInput_ReturnsZero()
    {
        await Assert.That(Crc32C.Compute([])).IsEqualTo(0u);
    }

    [Test]
    public async Task Compute_KnownVector_ReturnsCastagnoliChecksum()
    {
        await Assert.That(Crc32C.Compute("123456789"u8)).IsEqualTo(0xE3069283u);
    }

    [Test]
    public async Task Compute_MixedLengths_MatchesBitwiseReference()
    {
        foreach (var length in MixedLengths())
        {
            var data = CreateDeterministicBytes(length);
            var expected = ComputeBitwise(data);

            await Assert.That(Crc32C.Compute(data)).IsEqualTo(expected);
        }
    }

    [Test]
    public async Task Combine_MixedLengths_MatchesConcatenatedCompute()
    {
        foreach (var prefixLength in CombineLengths())
        {
            foreach (var suffixLength in CombineLengths())
            {
                var prefix = CreateDeterministicBytes(prefixLength);
                var suffix = CreateDeterministicBytes(suffixLength);
                var combined = new byte[prefix.Length + suffix.Length];
                prefix.CopyTo(combined, 0);
                suffix.CopyTo(combined, prefix.Length);

                var expected = Crc32C.Compute(combined);
                var actual = Crc32C.Combine(
                    Crc32C.Compute(prefix),
                    Crc32C.Compute(suffix),
                    suffix.Length);

                await Assert.That(actual).IsEqualTo(expected);
            }
        }
    }

    [Test]
    public async Task ComputeSoftware_MixedLengths_MatchesBitwiseReference()
    {
        foreach (var length in MixedLengths())
        {
            var data = CreateDeterministicBytes(length);
            var expected = ComputeBitwise(data);

            await Assert.That(Crc32C.ComputeSoftware(data)).IsEqualTo(expected);
        }
    }

    [Test]
    public async Task ComputeHardwareX86_WhenSupported_MatchesBitwiseReference()
    {
        if (!Sse42.IsSupported)
            return;

        foreach (var length in MixedLengths())
        {
            var data = CreateDeterministicBytes(length);
            var expected = ComputeBitwise(data);

            await Assert.That(Crc32C.ComputeHardwareX86(data)).IsEqualTo(expected);
        }
    }

    [Test]
    public async Task ComputeHardwareX86Scalar_WhenSupported_MatchesBitwiseReference()
    {
        if (!Sse42.IsSupported)
            return;

        foreach (var length in MixedLengths())
        {
            var data = CreateDeterministicBytes(length);
            var expected = ComputeBitwise(data);

            await Assert.That(Crc32C.ComputeHardwareX86Scalar(data)).IsEqualTo(expected);
        }
    }

    [Test]
    public async Task ComputeHardwareX86Optimized_WhenSupported_MatchesBitwiseReference()
    {
        if (!Sse42.X64.IsSupported)
            return;

        foreach (var length in MixedLengths())
        {
            var data = CreateDeterministicBytes(length);
            var expected = ComputeBitwise(data);

            await Assert.That(Crc32C.ComputeHardwareX86Optimized(data)).IsEqualTo(expected);
        }
    }

    [Test]
    public async Task ComputeHardwareArm_WhenSupported_MatchesBitwiseReference()
    {
        if (!ArmCrc32.IsSupported)
            return;

        foreach (var length in MixedLengths())
        {
            var data = CreateDeterministicBytes(length);
            var expected = ComputeBitwise(data);

            await Assert.That(Crc32C.ComputeHardwareArm(data)).IsEqualTo(expected);
        }
    }

    [Test]
    public async Task ComputeHardwareArmScalar_WhenSupported_MatchesBitwiseReference()
    {
        if (!ArmCrc32.IsSupported)
            return;

        foreach (var length in MixedLengths())
        {
            var data = CreateDeterministicBytes(length);
            var expected = ComputeBitwise(data);

            await Assert.That(Crc32C.ComputeHardwareArmScalar(data)).IsEqualTo(expected);
        }
    }

    [Test]
    public async Task ComputeHardwareArmOptimized_WhenSupported_MatchesBitwiseReference()
    {
        if (!ArmCrc32.Arm64.IsSupported)
            return;

        foreach (var length in MixedLengths())
        {
            var data = CreateDeterministicBytes(length);
            var expected = ComputeBitwise(data);

            await Assert.That(Crc32C.ComputeHardwareArmOptimized(data)).IsEqualTo(expected);
        }
    }

    private static byte[] CreateDeterministicBytes(int length)
    {
        var data = new byte[length];

        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)((i * 251) + (length * 17));
        }

        return data;
    }

    private static IEnumerable<int> MixedLengths()
    {
        for (var length = 0; length <= 512; length++)
        {
            yield return length;
        }

        foreach (var length in new[] { 513, 777, 1024, 1535, 1536, 1537, 2048, 4096, 8191, 8192, 16384, 65536 })
        {
            yield return length;
        }
    }

    private static int[] CombineLengths() =>
    [
        0, 1, 2, 3, 7, 8, 31, 32, 63, 64, 255, 512, 513, 1024, 4096, 65536, 1048576
    ];

    private static uint ComputeBitwise(ReadOnlySpan<byte> data)
    {
        const uint polynomial = 0x82F63B78;
        var crc = 0xFFFFFFFFu;

        foreach (var b in data)
        {
            crc ^= b;

            for (var i = 0; i < 8; i++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ polynomial : crc >> 1;
            }
        }

        return crc ^ 0xFFFFFFFFu;
    }
}
