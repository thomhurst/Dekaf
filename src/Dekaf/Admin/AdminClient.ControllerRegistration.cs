using Dekaf.Errors;
using Dekaf.Networking;
using Dekaf.Protocol.Messages;

namespace Dekaf.Admin;

public sealed partial class AdminClient
{
    /// <inheritdoc cref="IControllerRegistrationAdminClient.UnregisterControllerAsync"/>
    public async ValueTask UnregisterControllerAsync(
        int controllerId,
        UnregisterControllerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(controllerId);
        var timeoutMs = options?.TimeoutMs ?? UnregisterControllerOptions.DefaultTimeoutMs;
        ArgumentOutOfRangeException.ThrowIfNegative(timeoutMs, nameof(options));

        // A replay after a lost response answers CONTROLLER_ID_NOT_REGISTERED. After a send that
        // may have applied, that is the requested outcome. A registration removed concurrently
        // by another client is also reported as success.
        var unregisterMayHaveApplied = false;
        var writeContext = new KafkaRequestWriteContext(CancellationToken.None);

        await WithRetryAsync(async attemptToken =>
        {
            await EnsureInitializedAsync(attemptToken).ConfigureAwait(false);
            var isRetryAttempt = unregisterMayHaveApplied;
            using var controllerLease = await LeaseControllerAsync(Protocol.ApiKey.UnregisterController, attemptToken).ConfigureAwait(false);
            var controller = controllerLease.Connection;
            var apiVersion = _metadataManager.GetNegotiatedApiVersion(
                controller,
                Protocol.ApiKey.UnregisterController,
                UnregisterControllerRequest.LowestSupportedVersion,
                UnregisterControllerRequest.HighestSupportedVersion);

            UnregisterControllerResponse response;
            try
            {
                response = await SendObservingWriteAsync<UnregisterControllerRequest, UnregisterControllerResponse>(
                    writeContext,
                    controller,
                    new UnregisterControllerRequest { ControllerId = controllerId },
                    apiVersion,
                    attemptToken).ConfigureAwait(false);
            }
            catch
            {
                unregisterMayHaveApplied |= writeContext.WriteStarted;
                throw;
            }

            if (isRetryAttempt && response.ErrorCode == Protocol.ErrorCode.ControllerIdNotRegistered)
                return;

            if (response.ErrorCode != Protocol.ErrorCode.None)
            {
                if (MayHaveAppliedDespiteError(response.ErrorCode))
                    unregisterMayHaveApplied = true;
                throw KafkaException.FromErrorCode(
                    response.ErrorCode,
                    response.ErrorMessage ?? $"UnregisterController failed: {response.ErrorCode}");
            }
        }, cancellationToken, timeoutMs).ConfigureAwait(false);
    }
}
