using TUnit.Core;

namespace Dekaf.Tests.Integration.Security;

/// <summary>
/// Skips a test when running on Windows. Used for the mutual-TLS tests, whose data exchange
/// fails on the Windows SChannel TLS stack with SEC_E_ILLEGAL_MESSAGE (0x80090326) even though
/// server-only TLS works. The tests still run on the Linux CI runner (OpenSSL), which exercises
/// the real mTLS path.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class SkipOnWindowsAttribute(string reason) : SkipAttribute(reason)
{
    public override Task<bool> ShouldSkip(TestRegisteredContext context) =>
        Task.FromResult(OperatingSystem.IsWindows());
}
