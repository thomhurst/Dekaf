using BenchmarkDotNet.Attributes;

namespace Dekaf.Benchmarks.Tests;

// Verifies the maintained entry point with real BDN children and allocation data.
[MemoryDiagnoser]
public class SmokeBenchmarks
{
    private int _value;
    private int _divisor = 3;
    private int _size = 1024;

    [GlobalSetup]
    public void Setup()
    {
        _value = 42;
        if (Environment.GetEnvironmentVariable("SMOKE_FAIL_SETUP") == "1")
            throw new InvalidOperationException("Intentional fixture validation failure.");
        Console.WriteLine($"SMOKE workload-pid={Environment.ProcessId}");
    }

    [Benchmark]
    public int NoAllocation() => _value / _divisor;

    [Benchmark]
    public byte[] Allocate() => new byte[_size];
}
