using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Amazon.Runtime.Internal;

namespace Dekaf.Tests.Integration;

/// <summary>Retries transport failures from DynamoDB Local without hiding service or injected faults.</summary>
internal class OutboxDynamoDbLocalClient(AWSCredentials credentials, AmazonDynamoDBConfig config)
    : AmazonDynamoDBClient(credentials, config)
{
    protected override void CustomizeRuntimePipeline(RuntimePipeline pipeline)
    {
        base.CustomizeRuntimePipeline(pipeline);
        // Let the SDK rewind and re-sign requests. The fault interceptor remains outside this
        // handler, so an injected AppliedThenLost result can never trigger another write.
        pipeline.ReplaceHandler<RetryHandler>(new RetryHandler(new TransportRetryPolicy()));
    }

    private sealed class TransportRetryPolicy() : DefaultRetryPolicy(maxRetries: 2)
    {
        public override bool CanRetry(IExecutionContext executionContext) =>
            !executionContext.RequestContext.CancellationToken.IsCancellationRequested &&
            executionContext.ResponseContext.HttpResponse is null &&
            base.CanRetry(executionContext);

        public override bool RetryForException(IExecutionContext executionContext, Exception exception) =>
            exception is IOException or HttpRequestException { StatusCode: null };

        public override Task<bool> RetryForExceptionAsync(IExecutionContext executionContext, Exception exception) =>
            Task.FromResult(RetryForException(executionContext, exception));
    }
}
