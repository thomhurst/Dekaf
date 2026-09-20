using System.Buffers;
using System.Runtime.Loader;
using BenchmarkDotNet.Attributes;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Exercises the netstandard2.0 buffer writer used by share-consumer deserialization.
/// Assembly loading and delegate binding happen once; each write reuses its buffer.
/// </summary>
[MemoryDiagnoser]
public class NetStandardBufferWriterBenchmarks
{
    private AssemblyLoadContext _loadContext = null!;
    private IBufferWriter<byte> _writer = null!;
    private Action _reset = null!;
    private Func<ReadOnlyMemory<byte>> _writtenMemory = null!;
    private byte[] _payload = null!;

    [Params(64, 4096)]
    public int PayloadSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _loadContext = new AssemblyLoadContext("netstandard-buffer-writer", isCollectible: true);
        var assembly = _loadContext.LoadFromAssemblyPath(Path.Combine(AppContext.BaseDirectory,
            "compatibility", "netstandard2.0", "Dekaf.dll"));
        var type = assembly.GetType("System.Buffers.ArrayBufferWriter`1", throwOnError: true)!
            .MakeGenericType(typeof(byte));
        _writer = (IBufferWriter<byte>)Activator.CreateInstance(type, PayloadSize)!;
        _reset = type.GetMethod("ResetWrittenCount")!.CreateDelegate<Action>(_writer);
        _writtenMemory = type.GetProperty("WrittenMemory")!.GetMethod!
            .CreateDelegate<Func<ReadOnlyMemory<byte>>>(_writer);
        _payload = new byte[PayloadSize];
        _payload.AsSpan().Fill(42);
        if (!WriteAndReset().Span.SequenceEqual(_payload))
            throw new InvalidOperationException("The compatibility writer did not preserve the payload.");
    }

    [Benchmark]
    public ReadOnlyMemory<byte> WriteAndReset()
    {
        _reset();
        _payload.CopyTo(_writer.GetSpan(PayloadSize));
        _writer.Advance(PayloadSize);
        return _writtenMemory();
    }

    [GlobalCleanup]
    public void Cleanup() => _loadContext.Unload();
}
