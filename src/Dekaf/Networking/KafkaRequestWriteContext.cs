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
    ValueTask<TResponse> SendWithResponseCancellationAsync<TRequest, TResponse>(
        TRequest request, short apiVersion, KafkaRequestWriteContext context,
        CancellationToken cancellationToken)
        where TRequest : Protocol.IKafkaRequest<TResponse>
        where TResponse : Protocol.IKafkaResponse;
}
