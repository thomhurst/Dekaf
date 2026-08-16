using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Dekaf.SchemaRegistry;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public sealed class SchemaRegistryCompatibilityConfigurationTests
{
    [Test]
    [Arguments(SchemaCompatibilityLevel.None, "NONE")]
    [Arguments(SchemaCompatibilityLevel.Backward, "BACKWARD")]
    [Arguments(SchemaCompatibilityLevel.BackwardTransitive, "BACKWARD_TRANSITIVE")]
    [Arguments(SchemaCompatibilityLevel.Forward, "FORWARD")]
    [Arguments(SchemaCompatibilityLevel.ForwardTransitive, "FORWARD_TRANSITIVE")]
    [Arguments(SchemaCompatibilityLevel.Full, "FULL")]
    [Arguments(SchemaCompatibilityLevel.FullTransitive, "FULL_TRANSITIVE")]
    public async Task GetCompatibilityAsync_ParsesEverySupportedGlobalLevel(
        SchemaCompatibilityLevel expected,
        string wireValue)
    {
        using var handler = new CapturingHandler()
            .Enqueue(HttpStatusCode.OK, $$"""{ "compatibilityLevel": "{{wireValue}}" }""");
        using var client = CreateClient(handler);

        var result = await client.GetCompatibilityAsync();

        await Assert.That(result).IsEqualTo(expected);
        await Assert.That(handler.Requests).Count().IsEqualTo(1);
        await Assert.That(handler.Requests[0].Method).IsEqualTo(HttpMethod.Get);
        await Assert.That(handler.Requests[0].Uri.PathAndQuery).IsEqualTo("/config");
    }

    [Test]
    public async Task GetCompatibilityAsync_EscapesSubjectAsSinglePathSegment()
    {
        using var handler = new CapturingHandler()
            .Enqueue(HttpStatusCode.OK, """{ "compatibilityLevel": "FULL" }""");
        using var client = CreateClient(handler);

        var result = await client.GetCompatibilityAsync("orders/value?region=#eu west");

        await Assert.That(result).IsEqualTo(SchemaCompatibilityLevel.Full);
        await Assert.That(handler.Requests[0].Uri.PathAndQuery)
            .IsEqualTo("/config/orders%2Fvalue%3Fregion%3D%23eu%20west");
    }

    [Test]
    public async Task UpdateCompatibilityAsync_SendsWireValueAndReturnsAcknowledgedLevel()
    {
        using var handler = new CapturingHandler()
            .Enqueue(HttpStatusCode.OK, """{ "compatibility": "FULL_TRANSITIVE" }""");
        using var client = CreateClient(handler);

        var result = await client.UpdateCompatibilityAsync(
            SchemaCompatibilityLevel.Backward,
            "orders-value");

        await Assert.That(result).IsEqualTo(SchemaCompatibilityLevel.FullTransitive);
        await Assert.That(handler.Requests[0].Method).IsEqualTo(HttpMethod.Put);
        await Assert.That(handler.Requests[0].Uri.PathAndQuery).IsEqualTo("/config/orders-value");
        using var body = JsonDocument.Parse(handler.Requests[0].Body!);
        await Assert.That(body.RootElement.GetProperty("compatibility").GetString()).IsEqualTo("BACKWARD");
        await Assert.That(body.RootElement.EnumerateObject().Count()).IsEqualTo(1);
    }

    [Test]
    [Arguments("")]
    [Arguments(" ")]
    [Arguments("\t")]
    public async Task CompatibilityMethods_RejectEmptySubjectBeforeNetworkIo(string subject)
    {
        using var handler = new CapturingHandler();
        using var client = CreateClient(handler);

        _ = await Assert.ThrowsAsync<ArgumentException>(() => client.GetCompatibilityAsync(subject));
        _ = await Assert.ThrowsAsync<ArgumentException>(() => client.UpdateCompatibilityAsync(
            SchemaCompatibilityLevel.Backward,
            subject));

        await Assert.That(handler.Requests).IsEmpty();
    }

    [Test]
    public async Task UpdateCompatibilityAsync_RejectsUnknownEnumBeforeNetworkIo()
    {
        using var handler = new CapturingHandler();
        using var client = CreateClient(handler);

        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.UpdateCompatibilityAsync(
            (SchemaCompatibilityLevel)int.MaxValue));

        await Assert.That(handler.Requests).IsEmpty();
    }

    [Test]
    public async Task GetCompatibilityAsync_RejectsUnknownServerValue()
    {
        using var handler = new CapturingHandler()
            .Enqueue(HttpStatusCode.OK, """{ "compatibilityLevel": "FUTURE_MODE" }""");
        using var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<SchemaRegistryException>(
            () => client.GetCompatibilityAsync());

        await Assert.That(exception!.Message).Contains("FUTURE_MODE");
    }

    [Test]
    public async Task UpdateCompatibilityAsync_PreservesStructuredServerError()
    {
        using var handler = new CapturingHandler()
            .Enqueue(HttpStatusCode.UnprocessableEntity, """
                { "error_code": 42203, "message": "Invalid compatibility level" }
                """);
        using var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<SchemaRegistryException>(() => client.UpdateCompatibilityAsync(
            SchemaCompatibilityLevel.Full));

        await Assert.That(exception!.ErrorCode).IsEqualTo(42203);
        await Assert.That(exception.Message).Contains("Invalid compatibility level");
    }

    [Test]
    public async Task GetCompatibilityAsync_UsesAuthenticationAndFailoverPipeline()
    {
        using var handler = new CapturingHandler()
            .Enqueue(HttpStatusCode.ServiceUnavailable, "{}")
            .Enqueue(HttpStatusCode.OK, """{ "compatibilityLevel": "FORWARD" }""");
        using var client = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Urls = ["http://primary:8081", "http://secondary:8081"],
            Url = "http://ignored:8081",
            BasicAuthUserInfo = "user:secret"
        }, handler);

        var result = await client.GetCompatibilityAsync();

        await Assert.That(result).IsEqualTo(SchemaCompatibilityLevel.Forward);
        await Assert.That(handler.Requests).Count().IsEqualTo(2);
        await Assert.That(handler.Requests[0].Uri.Host).IsEqualTo("primary");
        await Assert.That(handler.Requests[1].Uri.Host).IsEqualTo("secondary");
        await Assert.That(handler.Requests.All(static request =>
            request.Authorization is { Scheme: "Basic", Parameter: "dXNlcjpzZWNyZXQ=" })).IsTrue();
    }

    [Test]
    public async Task GetCompatibilityAsync_RespectsCallerCancellation()
    {
        using var handler = new BlockingHandler();
        using var client = CreateClient(handler);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(
            () => client.GetCompatibilityAsync(cancellationToken: cancellation.Token));
    }

    [Test]
    public async Task GetCompatibilityAsync_UsesConfiguredRequestTimeout()
    {
        using var handler = new BlockingHandler();
        using var client = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = "http://schema-registry.local",
            RequestTimeoutMs = 50
        }, handler);

        _ = await Assert.ThrowsAsync<TaskCanceledException>(() => client.GetCompatibilityAsync());
    }

    private static SchemaRegistryClient CreateClient(HttpMessageHandler handler) =>
        new(new SchemaRegistryConfig { Url = "http://schema-registry.local" }, handler);

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode StatusCode, string Content)> _responses = new();

        internal List<CapturedRequest> Requests { get; } = [];

        internal CapturingHandler Enqueue(HttpStatusCode statusCode, string content)
        {
            _responses.Enqueue((statusCode, content));
            return this;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            Requests.Add(new CapturedRequest(
                request.Method,
                request.RequestUri!,
                request.Headers.Authorization,
                body));

            var (statusCode, content) = _responses.Count == 0
                ? (HttpStatusCode.OK, "{}")
                : _responses.Dequeue();
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class BlockingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            throw new UnreachableException();
        }
    }

    private sealed record CapturedRequest(
        HttpMethod Method,
        Uri Uri,
        AuthenticationHeaderValue? Authorization,
        string? Body);
}
