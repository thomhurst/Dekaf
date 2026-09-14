#if NET8_0
using System.Reflection;
using System.Runtime.Loader;

namespace Dekaf.Tests.Unit.Compatibility;

public sealed class NetStandardGssapiTests
{
    [Test]
    public async Task GetInitialResponse_RetainsExplicitUnsupportedRuntimeError()
    {
        var loadContext = new AssemblyLoadContext("netstandard-gssapi", isCollectible: true);
        try
        {
            var assembly = loadContext.LoadFromAssemblyPath(Path.Combine(AppContext.BaseDirectory,
                "compatibility", "netstandard2.0", "Dekaf.dll"));
            var configType = assembly.GetType("Dekaf.Security.Sasl.GssapiConfig", throwOnError: true)!;
            var authenticatorType = assembly.GetType("Dekaf.Security.Sasl.GssapiAuthenticator", throwOnError: true)!;
            using var authenticator = (IDisposable)Activator.CreateInstance(authenticatorType,
                Activator.CreateInstance(configType), "broker.example.com")!;
            var method = authenticatorType.GetMethod("GetInitialResponse")!;
            var exception = await Assert.That(() => method.Invoke(authenticator, null))
                .Throws<TargetInvocationException>();
            await Assert.That(exception!.InnerException).IsTypeOf<PlatformNotSupportedException>();
        }
        finally
        {
            loadContext.Unload();
        }
    }
}
#endif
