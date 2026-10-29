using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Amazon.Runtime.Internal;

namespace Dekaf.Tests.Integration;

[Category("MessagingPatterns")]
[Timeout(30_000)]
public sealed class OutboxDynamoDbTransportTests
{
    [Test]
    [Arguments(nameof(QueryRequest), false)]
    [Arguments(nameof(QueryRequest), true)]
    [Arguments(nameof(GetItemRequest), false)]
    [Arguments(nameof(GetItemRequest), true)]
    [Arguments(nameof(BatchGetItemRequest), false)]
    [Arguments(nameof(BatchGetItemRequest), true)]
    [Arguments(nameof(ScanRequest), false)]
    [Arguments(nameof(ScanRequest), true)]
    [Arguments(nameof(DescribeTableRequest), false)]
    [Arguments(nameof(DescribeTableRequest), true)]
    [Arguments(nameof(ListTablesRequest), false)]
    [Arguments(nameof(ListTablesRequest), true)]
    public async Task LocalClient_RetriesReadTransportFailures(
        string operation, bool httpRequestException, CancellationToken cancellationToken)
    {
        await using var endpoint = new DynamoDbEndpoint();
        var transport = new FailingTransport(TransportError(httpRequestException), failures: 1);
        using var client = new FaultingClient(DynamoDbLocalContainer.CreateConfig(endpoint.ServiceUrl), transport);

        await ReadAsync(client, cancellationToken, operation);

        await Assert.That(transport.Attempts).IsEqualTo(2);
        await Assert.That(endpoint.RequestCount).IsEqualTo(1);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task LocalClient_BoundsTransportRetries(bool httpRequestException, CancellationToken cancellationToken)
    {
        await using var endpoint = new DynamoDbEndpoint();
        var transport = new FailingTransport(TransportError(httpRequestException), failures: int.MaxValue);
        using var client = new FaultingClient(DynamoDbLocalContainer.CreateConfig(endpoint.ServiceUrl), transport);

        if (httpRequestException)
            await Assert.That(async () => await ReadAsync(client, cancellationToken)).Throws<HttpRequestException>();
        else
            await Assert.That(async () => await ReadAsync(client, cancellationToken)).Throws<IOException>();

        await Assert.That(transport.Attempts).IsEqualTo(3);
        await Assert.That(endpoint.RequestCount).IsEqualTo(0);
    }

    [Test]
    public async Task LocalClient_DoesNotRetryCancellation(CancellationToken cancellationToken)
    {
        await using var endpoint = new DynamoDbEndpoint();
        var transport = new FailingTransport(new OperationCanceledException(cancellationToken), failures: int.MaxValue);
        using var client = new FaultingClient(DynamoDbLocalContainer.CreateConfig(endpoint.ServiceUrl), transport);

        await Assert.That(async () => await ReadAsync(client, cancellationToken)).Throws<OperationCanceledException>();

        await Assert.That(transport.Attempts).IsEqualTo(1);
        await Assert.That(endpoint.RequestCount).IsEqualTo(0);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task LocalClient_DoesNotRetryTransportFailureAfterCancellation(
        bool httpRequestException, CancellationToken cancellationToken)
    {
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        await using var endpoint = new DynamoDbEndpoint();
        var transport = new FailingTransport(
            TransportError(httpRequestException), failures: int.MaxValue, cancelBeforeFailure: cancelled);
        using var client = new FaultingClient(DynamoDbLocalContainer.CreateConfig(endpoint.ServiceUrl), transport);

        if (httpRequestException)
            await Assert.That(async () => await ReadAsync(client, cancelled.Token)).Throws<HttpRequestException>();
        else
            await Assert.That(async () => await ReadAsync(client, cancelled.Token)).Throws<IOException>();

        await Assert.That(cancelled.IsCancellationRequested).IsTrue();
        await Assert.That(transport.Attempts).IsEqualTo(1);
        await Assert.That(endpoint.RequestCount).IsEqualTo(0);
    }

    [Test]
    public async Task LocalClient_DoesNotRetryHttpStatusExceptions(CancellationToken cancellationToken)
    {
        await using var endpoint = new DynamoDbEndpoint();
        var transport = new FailingTransport(
            new HttpRequestException("Service failure.", null, HttpStatusCode.ServiceUnavailable), failures: int.MaxValue);
        using var client = new FaultingClient(DynamoDbLocalContainer.CreateConfig(endpoint.ServiceUrl), transport);

        await Assert.That(async () => await ReadAsync(client, cancellationToken)).Throws<HttpRequestException>();

        await Assert.That(transport.Attempts).IsEqualTo(1);
        await Assert.That(endpoint.RequestCount).IsEqualTo(0);
    }

    [Test]
    public async Task LocalClient_DoesNotRetryFailureAfterResponseHeaders(CancellationToken cancellationToken)
    {
        await using var endpoint = new DynamoDbEndpoint(truncateResponse: true);
        using var client = new OutboxDynamoDbLocalClient(
            new BasicAWSCredentials("dekaf", "dekaf"), DynamoDbLocalContainer.CreateConfig(endpoint.ServiceUrl));

        await Assert.That(async () => await ReadAsync(client, cancellationToken)).ThrowsException();

        await Assert.That(endpoint.RequestCount).IsEqualTo(1);
    }

    [Test]
    [Arguments(nameof(ConditionalCheckFailedException))]
    [Arguments(nameof(ProvisionedThroughputExceededException))]
    public async Task LocalClient_DoesNotRetryServiceErrors(string error, CancellationToken cancellationToken)
    {
        await using var endpoint = new DynamoDbEndpoint(error);
        using var client = new OutboxDynamoDbLocalClient(
            new BasicAWSCredentials("dekaf", "dekaf"), DynamoDbLocalContainer.CreateConfig(endpoint.ServiceUrl));

        if (error == nameof(ConditionalCheckFailedException))
            await Assert.That(async () => await WriteAsync(client, cancellationToken))
                .Throws<ConditionalCheckFailedException>();
        else
            await Assert.That(async () => await ReadAsync(client, cancellationToken))
                .Throws<ProvisionedThroughputExceededException>();

        await Assert.That(endpoint.RequestCount).IsEqualTo(1);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task LocalClient_DoesNotRetryInjectedFaults(bool appliedThenLost, CancellationToken cancellationToken)
    {
        await using var endpoint = new DynamoDbEndpoint();
        var calls = 0;
        using var client = OutboxDynamoDbCallInterceptor.CreateClient(
            new BasicAWSCredentials("dekaf", "dekaf"), DynamoDbLocalContainer.CreateConfig(endpoint.ServiceUrl), _ =>
            {
                calls++;
                return new(appliedThenLost ? OutboxDynamoDbFault.AppliedThenLost : OutboxDynamoDbFault.Throttled);
            });

        if (appliedThenLost)
            await Assert.That(async () => await WriteAsync(client, cancellationToken))
                .Throws<AmazonServiceException>();
        else
            await Assert.That(async () => await WriteAsync(client, cancellationToken))
                .Throws<ProvisionedThroughputExceededException>();

        await Assert.That(calls).IsEqualTo(1);
        await Assert.That(endpoint.RequestCount).IsEqualTo(appliedThenLost ? 1 : 0);
    }

    [Test]
    public async Task LocalClient_DoesNotReplayAppliedConditionalWriteWithoutResponseHeaders(
        CancellationToken cancellationToken)
    {
        // The endpoint consumes the write, then closes before sending any headers. A replay
        // gets a conditional refusal: no response does not mean the write was not applied.
        await using var endpoint = new DynamoDbEndpoint(
            nameof(ConditionalCheckFailedException), dropFirstResponse: true);
        using var client = new OutboxDynamoDbLocalClient(
            new BasicAWSCredentials("dekaf", "dekaf"), DynamoDbLocalContainer.CreateConfig(endpoint.ServiceUrl));

        await Assert.That(async () => await WriteAsync(client, cancellationToken, conditional: true))
            .Throws<IOException>();

        await Assert.That(endpoint.RequestCount).IsEqualTo(1);
    }

    [Test]
    [Arguments(nameof(PutItemRequest))]
    [Arguments(nameof(UpdateItemRequest))]
    [Arguments(nameof(DeleteItemRequest))]
    [Arguments(nameof(BatchWriteItemRequest))]
    [Arguments(nameof(TransactWriteItemsRequest))]
    public async Task LocalClient_DoesNotRetryMutations(string operation, CancellationToken cancellationToken)
    {
        await using var endpoint = new DynamoDbEndpoint();
        var transport = new FailingTransport(new IOException("Ambiguous write."), failures: int.MaxValue);
        using var client = new FaultingClient(DynamoDbLocalContainer.CreateConfig(endpoint.ServiceUrl), transport);

        await Assert.That(async () => await MutateAsync(client, operation, cancellationToken)).Throws<IOException>();

        await Assert.That(transport.Attempts).IsEqualTo(1);
        await Assert.That(endpoint.RequestCount).IsEqualTo(0);
    }

    private static Task MutateAsync(IAmazonDynamoDB client, string operation, CancellationToken cancellationToken) =>
        operation switch
        {
            nameof(PutItemRequest) => WriteAsync(client, cancellationToken),
            nameof(UpdateItemRequest) => client.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = "outbox", Key = new() { ["id"] = new() { S = "one" } },
                UpdateExpression = "ADD counter :one",
                ExpressionAttributeValues = new() { [":one"] = new() { N = "1" } }
            }, cancellationToken),
            nameof(DeleteItemRequest) => client.DeleteItemAsync(new DeleteItemRequest
            {
                TableName = "outbox", Key = new() { ["id"] = new() { S = "one" } }
            }, cancellationToken),
            nameof(BatchWriteItemRequest) => client.BatchWriteItemAsync(new BatchWriteItemRequest
            {
                RequestItems = new() { ["outbox"] = [new WriteRequest
                {
                    DeleteRequest = new() { Key = new() { ["id"] = new() { S = "one" } } }
                }] }
            }, cancellationToken),
            nameof(TransactWriteItemsRequest) => client.TransactWriteItemsAsync(new TransactWriteItemsRequest
            {
                TransactItems = [new TransactWriteItem
                {
                    Delete = new() { TableName = "outbox", Key = new() { ["id"] = new() { S = "one" } } }
                }]
            }, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };

    private static Task ReadAsync(
        IAmazonDynamoDB client, CancellationToken cancellationToken, string operation = nameof(QueryRequest)) =>
        operation switch
        {
            nameof(QueryRequest) => client.QueryAsync(new QueryRequest
            {
                TableName = "outbox",
                KeyConditionExpression = "id = :id",
                ExpressionAttributeValues = new() { [":id"] = new() { S = "one" } }
            }, cancellationToken),
            nameof(GetItemRequest) => client.GetItemAsync(new GetItemRequest
            {
                TableName = "outbox", Key = new() { ["id"] = new() { S = "one" } }
            }, cancellationToken),
            nameof(BatchGetItemRequest) => client.BatchGetItemAsync(new BatchGetItemRequest
            {
                RequestItems = new() { ["outbox"] = new KeysAndAttributes
                {
                    Keys = [new() { ["id"] = new() { S = "one" } }]
                } }
            }, cancellationToken),
            nameof(ScanRequest) => client.ScanAsync(new ScanRequest { TableName = "outbox" }, cancellationToken),
            nameof(DescribeTableRequest) => client.DescribeTableAsync("outbox", cancellationToken),
            nameof(ListTablesRequest) => client.ListTablesAsync(new ListTablesRequest(), cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };

    private static Task<PutItemResponse> WriteAsync(
        IAmazonDynamoDB client, CancellationToken cancellationToken, bool conditional = false) =>
        client.PutItemAsync(new PutItemRequest
        {
            TableName = "outbox",
            ConditionExpression = conditional ? "attribute_not_exists(id)" : null,
            Item = new() { ["id"] = new AttributeValue { S = "one" } }
        }, cancellationToken);

    private static Exception TransportError(bool httpRequestException) => httpRequestException
        ? new HttpRequestException("Injected transport failure.")
        : new IOException("Injected aborted read.");

    private sealed class FaultingClient(AmazonDynamoDBConfig config, FailingTransport transport)
        : OutboxDynamoDbLocalClient(new BasicAWSCredentials("dekaf", "dekaf"), config)
    {
        protected override void CustomizeRuntimePipeline(RuntimePipeline pipeline)
        {
            base.CustomizeRuntimePipeline(pipeline);
            pipeline.AddHandlerAfter<RetryHandler>(transport);
        }
    }

    // Fail inside the SDK retry handler, before any HTTP response. This also covers IOException
    // directly, without depending on HttpClient's own reconnect behavior or a flaky Docker socket.
    private sealed class FailingTransport(
        Exception error, int failures, CancellationTokenSource? cancelBeforeFailure = null) : PipelineHandler
    {
        public int Attempts { get; private set; }

        public override Task<T> InvokeAsync<T>(IExecutionContext executionContext)
        {
            if (++Attempts <= failures)
            {
                cancelBeforeFailure?.Cancel();
                return Task.FromException<T>(error);
            }
            return base.InvokeAsync<T>(executionContext);
        }
    }

    private sealed class DynamoDbEndpoint : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stopping = new();
        private readonly Task _serving;
        private readonly byte[] _response;
        private readonly bool _truncateResponse;
        private readonly bool _dropFirstResponse;
        private int _requestCount;

        public DynamoDbEndpoint(string? error = null, bool truncateResponse = false, bool dropFirstResponse = false)
        {
            _truncateResponse = truncateResponse;
            _dropFirstResponse = dropFirstResponse;
            var body = error is null ? "{}" : $"{{\"__type\":\"{error}\",\"message\":\"Refused\"}}";
            _response = Encoding.UTF8.GetBytes(
                $"HTTP/1.1 {(error is null ? "200 OK" : "400 Bad Request")}\r\nContent-Type: application/x-amz-json-1.0\r\nContent-Length: {Encoding.UTF8.GetByteCount(body) + (truncateResponse ? 1 : 0)}\r\n\r\n{body}");
            _listener.Start();
            ServiceUrl = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";
            _serving = ServeAsync();
        }

        public string ServiceUrl { get; }
        public int RequestCount => Volatile.Read(ref _requestCount);

        private async Task ServeAsync()
        {
            try
            {
                while (true)
                {
                    using var connection = await _listener.AcceptTcpClientAsync(_stopping.Token);
                    await using var stream = connection.GetStream();
                    using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
                    while (await reader.ReadLineAsync(_stopping.Token) is not null)
                    {
                        var contentLength = 0;
                        while (await reader.ReadLineAsync(_stopping.Token) is { Length: > 0 } line)
                        {
                            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                                contentLength = int.Parse(line.AsSpan("Content-Length:".Length), CultureInfo.InvariantCulture);
                        }
                        var body = new char[contentLength];
                        if (await reader.ReadBlockAsync(body, _stopping.Token) != body.Length)
                            throw new EndOfStreamException("Incomplete DynamoDB request.");
                        var requestCount = Interlocked.Increment(ref _requestCount);
                        if (_dropFirstResponse && requestCount == 1)
                            break;
                        await stream.WriteAsync(_response, _stopping.Token);
                        if (_truncateResponse)
                            break;
                    }
                }
            }
            catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
            {
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _stopping.CancelAsync();
            try
            {
                await _serving;
            }
            finally
            {
                _listener.Stop();
                _stopping.Dispose();
            }
        }
    }
}
