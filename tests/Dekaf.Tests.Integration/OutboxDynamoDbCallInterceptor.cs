using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Amazon.Runtime.Internal;

namespace Dekaf.Tests.Integration;

/// <summary>What a store sees of one DynamoDB request.</summary>
internal enum OutboxDynamoDbFault
{
    None,

    /// <summary>DynamoDB refuses the request before it does anything: throttling.</summary>
    Throttled,

    /// <summary>
    /// DynamoDB applies the request and the answer never arrives: a timeout or a dropped
    /// connection. The caller cannot tell it from a request that was never applied.
    /// </summary>
    AppliedThenLost
}

/// <summary>
/// Stands between a store and DynamoDB, where a network stands in production: DynamoDB Local
/// neither throttles nor loses an answer on demand. It is the outermost handler of its
/// client's request pipeline, so it sees every request whatever the operation, and a fault
/// it injects reaches the store as thrown, past the SDK's retries and its exception event.
/// </summary>
/// <remarks>
/// Not a <see cref="System.Reflection.DispatchProxy"/> over <see cref="IAmazonDynamoDB"/>:
/// the interface has static abstract members on .NET 8 and later, and the .NET 8 proxy
/// generator emits a type the runtime refuses to load for such an interface.
/// </remarks>
internal sealed class OutboxDynamoDbCallInterceptor(
    Func<AmazonWebServiceRequest, ValueTask<OutboxDynamoDbFault>> beforeCall) : PipelineHandler
{
    /// <param name="beforeCall">Runs before every request, and decides what becomes of it. It
    /// may also write to the table itself, through another client, as a request that an
    /// earlier round abandoned would.</param>
    public static AmazonDynamoDBClient CreateClient(
        AWSCredentials credentials, AmazonDynamoDBConfig config,
        Func<AmazonWebServiceRequest, ValueTask<OutboxDynamoDbFault>> beforeCall) =>
        new InterceptedClient(credentials, config, beforeCall);

    public override async Task<T> InvokeAsync<T>(IExecutionContext executionContext)
    {
        var fault = await beforeCall(executionContext.RequestContext.OriginalRequest);
        if (fault == OutboxDynamoDbFault.Throttled)
            throw new ProvisionedThroughputExceededException("Injected: the table is throttled.");

        var response = await base.InvokeAsync<T>(executionContext);
        if (fault == OutboxDynamoDbFault.AppliedThenLost)
            throw new AmazonServiceException("Injected: the request was applied and its answer was lost.");

        return response;
    }

    private sealed class InterceptedClient(
        AWSCredentials credentials, AmazonDynamoDBConfig config,
        Func<AmazonWebServiceRequest, ValueTask<OutboxDynamoDbFault>> beforeCall)
        : AmazonDynamoDBClient(credentials, config)
    {
        // Called by the base constructor. A captured primary constructor parameter is stored
        // before the base constructor runs, so beforeCall is already there.
        protected override void CustomizeRuntimePipeline(RuntimePipeline pipeline)
        {
            base.CustomizeRuntimePipeline(pipeline);
            pipeline.AddHandler(new OutboxDynamoDbCallInterceptor(beforeCall));
        }
    }
}
