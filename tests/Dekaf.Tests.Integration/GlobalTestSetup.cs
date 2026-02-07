using System.Runtime.CompilerServices;
using Dekaf.Compression;
using Dekaf.Compression.Lz4;
using Dekaf.Compression.Snappy;
using Dekaf.Compression.Zstd;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Registers all compression codecs at assembly load time to avoid
/// race conditions from concurrent [Before(Class)] registrations.
/// </summary>
internal static class GlobalTestSetup
{
    [ModuleInitializer]
    internal static void RegisterCompressionCodecs()
    {
        CompressionCodecRegistry.Default.AddLz4();
        CompressionCodecRegistry.Default.AddSnappy();
        CompressionCodecRegistry.Default.AddZstd();
    }
}
