using System.Runtime.Intrinsics.X86;
using BenchmarkDotNet.Attributes;
using Dekaf.Protocol.Records;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Benchmarks CRC32C implementations across representative record-batch payload sizes.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class Crc32CBenchmarks
{
    private byte[] _data = null!;

    [Params(128, 512, 1024, 1536, 4096, 16384, 65536)]
    public int Size { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _data = new byte[Size];
        for (var i = 0; i < _data.Length; i++)
        {
            _data[i] = (byte)((i * 251) + (Size * 17));
        }
    }

    [Benchmark(Baseline = true, Description = "CRC32C auto")]
    public uint Auto()
        => Crc32C.Compute(_data);

    [Benchmark(Description = "CRC32C x86 scalar")]
    public uint X86Scalar()
        => Sse42.IsSupported ? Crc32C.ComputeHardwareX86Scalar(_data) : 0;

    [Benchmark(Description = "CRC32C x86 optimized")]
    public uint X86Optimized()
        => Sse42.X64.IsSupported ? Crc32C.ComputeHardwareX86Optimized(_data) : 0;

    [Benchmark(Description = "CRC32C software")]
    public uint Software()
        => Crc32C.ComputeSoftware(_data);
}
