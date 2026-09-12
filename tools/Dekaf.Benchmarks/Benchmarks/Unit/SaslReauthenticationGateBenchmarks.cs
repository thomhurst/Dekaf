using BenchmarkDotNet.Attributes;
using Dekaf.Networking;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Measures admission per Kafka request, amortized across each produce/fetch batch.</summary>
[MemoryDiagnoser]
public class SaslReauthenticationGateBenchmarks
{
    private readonly SaslReauthenticationGate _gate = new();

    [Benchmark]
    public async ValueTask AdmitRequest()
    {
        await _gate.EnterAsync(CancellationToken.None).ConfigureAwait(false);
        _gate.Exit();
    }
}
