using System.Diagnostics.CodeAnalysis;

namespace Dekaf.Networking;

/// <summary>
/// Reused by one serialized broker request stream to distinguish pre-write cancellation
/// from an uncertain write and to keep observing a started request within a separate budget.
/// </summary>
internal sealed class KafkaRequestWriteContext
{
    internal KafkaRequestWriteContext(CancellationToken responseCancellationToken)
    {
        ResponseCancellationToken = responseCancellationToken;
        WriteStartedCallback = MarkWriteStarted;
    }

    internal CancellationToken ResponseCancellationToken { get; }
    internal Action WriteStartedCallback { get; }
    internal bool WriteStarted { get; private set; }
    internal void Reset() => WriteStarted = false;
    internal void MarkWriteStarted() => WriteStarted = true;
}

internal interface IKafkaRequestCancellationConnection
{
    ValueTask<TResponse> SendWithResponseCancellationAsync<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicMethods)] TRequest,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicMethods)] TResponse>(
        TRequest request, short apiVersion, KafkaRequestWriteContext context,
        CancellationToken cancellationToken)
        where TRequest : Protocol.IKafkaRequest<TResponse>
        where TResponse : Protocol.IKafkaResponse;
}

internal interface IKafkaRequestAfterWriteCancellationConnection
{
    // The caller's token bounds the request until its frame is written; after that only
    // the context's response token (or the request timeout) bounds the wait for the answer.
    ValueTask<TResponse> SendWithResponseCancellationAfterWriteAsync<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicMethods)] TRequest,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicMethods)] TResponse>(
        TRequest request, short apiVersion, KafkaRequestWriteContext context,
        CancellationToken cancellationToken)
        where TRequest : Protocol.IKafkaRequest<TResponse>
        where TResponse : Protocol.IKafkaResponse;
}
