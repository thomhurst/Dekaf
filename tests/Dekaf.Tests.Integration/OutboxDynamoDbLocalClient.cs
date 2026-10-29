using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Amazon.Runtime.Internal;

namespace Dekaf.Tests.Integration;

/// <summary>Retries read transport failures from DynamoDB Local without replaying ambiguous writes.</summary>
internal class OutboxDynamoDbLocalClient(AWSCredentials credentials, AmazonDynamoDBConfig config)
    : AmazonDynamoDBClient(credentials, config)
{
    protected override void CustomizeRuntimePipeline(RuntimePipeline pipeline)
    {
        base.CustomizeRuntimePipeline(pipeline);
        // Let the SDK rewind and re-sign reads. The fault interceptor remains outside this
        // handler, so injected faults reach the store unchanged.
        pipeline.ReplaceHandler<RetryHandler>(new RetryHandler(new TransportRetryPolicy()));
    }

    private sealed class TransportRetryPolicy() : DefaultRetryPolicy(maxRetries: 2)
    {
        // Missing response headers do not prove that DynamoDB did not apply the request.
        // Replaying a conditional lease write can turn a success into a refusal; replaying
        // a counter update can apply it twice. Only read operations are safe to repeat.
        public override bool CanRetry(IExecutionContext executionContext) =>
            executionContext.RequestContext.OriginalRequest is
                QueryRequest or GetItemRequest or BatchGetItemRequest or ScanRequest or
                DescribeTableRequest or ListTablesRequest &&
            !executionContext.RequestContext.CancellationToken.IsCancellationRequested &&
            executionContext.ResponseContext.HttpResponse is null &&
            base.CanRetry(executionContext);

        public override bool RetryForException(IExecutionContext executionContext, Exception exception) =>
            exception is IOException or HttpRequestException { StatusCode: null };

        public override Task<bool> RetryForExceptionAsync(IExecutionContext executionContext, Exception exception) =>
            Task.FromResult(RetryForException(executionContext, exception));
    }
}
