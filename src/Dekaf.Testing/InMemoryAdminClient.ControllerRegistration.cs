using Dekaf.Admin;
using Dekaf.Errors;

namespace Dekaf.Testing;

public sealed partial class InMemoryAdminClient : IControllerRegistrationAdminClient
{
    /// <inheritdoc />
    public async ValueTask UnregisterControllerAsync(
        int controllerId,
        UnregisterControllerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(controllerId);
        var timeoutMs = options?.TimeoutMs ?? UnregisterControllerOptions.DefaultTimeoutMs;
        ArgumentOutOfRangeException.ThrowIfNegative(timeoutMs, nameof(options));
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        // Zero is an already-expired deadline, as in AdminClient.
        if (timeoutMs == 0)
            throw new KafkaTimeoutException(TimeoutKind.Api, TimeSpan.Zero, TimeSpan.Zero,
                $"{nameof(UnregisterControllerAsync)} timed out after 0 ms.");

        await ExecuteWithTimeoutAsync(async token =>
        {
            await ApplyAdminFaultAsync(token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            return true;
        }, timeoutMs, nameof(UnregisterControllerAsync), cancellationToken).ConfigureAwait(false);
    }
}
