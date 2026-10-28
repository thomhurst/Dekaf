using System.Reflection;

namespace Dekaf.Tests.Unit.Admin;

/// <summary>
/// Expires an admin call's own deadline from inside a mocked request, so a test reaches the
/// stage it asserts on before the deadline fires. A short real-time <c>TimeoutMs</c> races
/// request setup instead: under load it can expire before the stage is entered.
/// </summary>
internal static class AdminDeadline
{
    private static readonly FieldInfo Source = typeof(CancellationToken).GetField("_source", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(nameof(CancellationToken), "_source");

    /// <summary>
    /// Cancels the source behind <paramref name="token"/>: the deadline source an admin call
    /// links to its caller's token and passes to the request. The caller's token stays
    /// uncancelled, so the call sees an expired deadline, not a caller cancellation.
    /// </summary>
    public static void Expire(CancellationToken token) =>
        ((CancellationTokenSource)Source.GetValue(token)!).Cancel();
}
