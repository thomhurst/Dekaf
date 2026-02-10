using Dekaf.Compression;
using Dekaf.Compression.Lz4;
using Dekaf.Compression.Snappy;
using Dekaf.Compression.Zstd;
using TUnit.Core.Helpers;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Global test setup that runs once before all tests in the session.
/// Ensures compression codecs are registered before any test runs.
/// The codec libraries also self-register via [ModuleInitializer],
/// but this provides a reliable fallback for test execution.
/// </summary>
internal sealed class GlobalTestSetup
{
    [Before(TestSession)]
    public static void RegisterCompressionCodecs()
    {
        CompressionCodecRegistry.Default.AddLz4();
        CompressionCodecRegistry.Default.AddSnappy();
        CompressionCodecRegistry.Default.AddZstd();
    }
}
