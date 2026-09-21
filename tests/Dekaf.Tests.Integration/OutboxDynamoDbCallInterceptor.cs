using System.Reflection;
using System.Runtime.ExceptionServices;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;

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
/// Stands between a store and the DynamoDB client, where a network stands in production.
/// <see cref="IAmazonDynamoDB"/> is far too wide to decorate by hand, and DynamoDB Local
/// neither throttles nor loses an answer on demand.
/// </summary>
// DispatchProxy derives the proxy type from this class at run time, so it cannot be sealed.
#pragma warning disable CA1852
internal class OutboxDynamoDbCallInterceptor : DispatchProxy
#pragma warning restore CA1852
{
    private static readonly MethodInfo InterceptedMethod = typeof(OutboxDynamoDbCallInterceptor)
        .GetMethod(nameof(InterceptedAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private IAmazonDynamoDB _inner = null!;
    private Func<string, AmazonWebServiceRequest, ValueTask<OutboxDynamoDbFault>> _beforeCall = null!;

    /// <param name="inner">The client that reaches the table.</param>
    /// <param name="beforeCall">Runs before every request, with the method name and the
    /// request, and decides what becomes of it. It may also write to the table itself, as a
    /// request that an earlier round abandoned would.</param>
    public static IAmazonDynamoDB Wrap(
        IAmazonDynamoDB inner, Func<string, AmazonWebServiceRequest, ValueTask<OutboxDynamoDbFault>> beforeCall)
    {
        var proxy = Create<IAmazonDynamoDB, OutboxDynamoDbCallInterceptor>();
        var interceptor = (OutboxDynamoDbCallInterceptor)proxy;
        interceptor._inner = inner;
        interceptor._beforeCall = beforeCall;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        // The request/response calls a store makes: Task<TResponse> Xxx(TRequest, CancellationToken).
        if (args is [AmazonWebServiceRequest request, CancellationToken]
            && targetMethod.ReturnType.IsGenericType
            && targetMethod.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            return InterceptedMethod.MakeGenericMethod(targetMethod.ReturnType.GetGenericArguments()[0])
                .Invoke(this, [targetMethod, args, request]);
        }

        return Forward(targetMethod, args);
    }

    private object? Forward(MethodInfo targetMethod, object?[]? args)
    {
        try
        {
            return targetMethod.Invoke(_inner, args);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Throw(exception.InnerException);
            throw;
        }
    }

    private async Task<TResponse> InterceptedAsync<TResponse>(
        MethodInfo targetMethod, object?[] args, AmazonWebServiceRequest request)
    {
        var fault = await _beforeCall(targetMethod.Name, request);
        if (fault == OutboxDynamoDbFault.Throttled)
            throw new ProvisionedThroughputExceededException("Injected: the table is throttled.");

        var response = await (Task<TResponse>)Forward(targetMethod, args)!;
        if (fault == OutboxDynamoDbFault.AppliedThenLost)
            throw new AmazonServiceException("Injected: the request was applied and its answer was lost.");

        return response;
    }
}
