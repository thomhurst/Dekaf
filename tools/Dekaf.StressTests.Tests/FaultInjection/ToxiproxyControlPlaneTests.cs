using System.Net;
using System.Net.Http;
using System.Text;
using Dekaf.StressTests.FaultInjection;
using Newtonsoft.Json;
using Toxiproxy.Net;
using Toxiproxy.Net.Toxics;

namespace Dekaf.StressTests.Tests.FaultInjection;

public class ToxiproxyControlPlaneTests
{
    [Test]
    public async Task ExecuteAsync_RetriesJsonReaderExceptionUntilSuccess()
    {
        var attempts = 0;

        await FaultInjectionKafkaEnvironment.ExecuteToxiproxyControlPlaneAsync(
            "reset proxies",
            () =>
            {
                attempts++;
                if (attempts < 3)
                {
                    throw new JsonReaderException("invalid response");
                }

                return Task.CompletedTask;
            },
            CancellationToken.None,
            retryDelay: TimeSpan.Zero);

        await Assert.That(attempts).IsEqualTo(3);
    }

    [Test]
    public async Task ExecuteAsync_RetriesHttpRequestExceptionUntilSuccess()
    {
        var attempts = 0;

        await FaultInjectionKafkaEnvironment.ExecuteToxiproxyControlPlaneAsync(
            "add toxic",
            () =>
            {
                attempts++;
                if (attempts == 1)
                {
                    throw new HttpRequestException("control API unavailable");
                }

                return Task.CompletedTask;
            },
            CancellationToken.None,
            retryDelay: TimeSpan.Zero);

        await Assert.That(attempts).IsEqualTo(2);
    }

    [Test]
    public async Task ExecuteAsync_ExhaustedRetriesThrowsInfrastructureError()
    {
        var attempts = 0;

        var exception = await Assert.ThrowsAsync<ToxiproxyControlPlaneException>(() =>
            FaultInjectionKafkaEnvironment.ExecuteToxiproxyControlPlaneAsync(
                "reset proxies",
                () =>
                {
                    attempts++;
                    throw new JsonReaderException("invalid response");
                },
                CancellationToken.None,
                retryDelay: TimeSpan.Zero));

        await Assert.That(attempts).IsEqualTo(3);
        await Assert.That(exception!.Message).Contains("harness infrastructure");
        await Assert.That(exception.Message).Contains("reset proxies");
        await Assert.That(exception.InnerException).IsTypeOf<JsonReaderException>();
    }

    [Test]
    public async Task ExecuteAsync_DoesNotRetryNonTransientFailure()
    {
        var attempts = 0;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            FaultInjectionKafkaEnvironment.ExecuteToxiproxyControlPlaneAsync(
                "reset proxies",
                () =>
                {
                    attempts++;
                    throw new InvalidOperationException("bad test setup");
                },
                CancellationToken.None,
                retryDelay: TimeSpan.Zero));

        await Assert.That(attempts).IsEqualTo(1);
        await Assert.That(exception!.Message).IsEqualTo("bad test setup");
    }

    [Test]
    public async Task ExecuteToxicAddAsync_AmbiguousRealAdd_TreatsConflictAsSuccess()
    {
        var handler = new QueueHttpMessageHandler(
            JsonResponse(
                HttpStatusCode.OK,
                """{"name":"broker-1","listen":"0.0.0.0:19092","upstream":"broker-1:9092","enabled":true}"""),
            JsonResponse(HttpStatusCode.OK, "<html>"),
            JsonResponse(HttpStatusCode.OK, "<html>"),
            JsonResponse(HttpStatusCode.OK, """
                [{"name":"latency-upstream","type":"latency","stream":"upstream","toxicity":1,"attributes":{"latency":300,"jitter":0}}]
                """));
        var proxy = await CreateProxyAsync(handler);
        var toxic = CreateLatencyToxic();

        await FaultInjectionKafkaEnvironment.ExecuteToxiproxyToxicAddAsync(
            proxy,
            toxic,
            CancellationToken.None,
            retryDelay: TimeSpan.Zero);

        await Assert.That(handler.RequestCount).IsEqualTo(4);
        await Assert.That(handler.ToxicAddRequestCount).IsEqualTo(1);
    }

    [Test]
    public async Task ExecuteToxicAddAsync_FirstAttemptConflict_IsPreserved()
    {
        var handler = new QueueHttpMessageHandler(
            JsonResponse(
                HttpStatusCode.OK,
                """{"name":"broker-1","listen":"0.0.0.0:19092","upstream":"broker-1:9092","enabled":true}"""),
            JsonResponse(
                HttpStatusCode.Conflict,
                """{"error":"toxic already exists","status":409}"""));
        var proxy = await CreateProxyAsync(handler);
        var toxic = CreateLatencyToxic();

        var exception = await Assert.ThrowsAsync<ToxiProxiException>(() =>
            FaultInjectionKafkaEnvironment.ExecuteToxiproxyToxicAddAsync(
                proxy,
                toxic,
                CancellationToken.None,
                retryDelay: TimeSpan.Zero));

        await Assert.That(handler.RequestCount).IsEqualTo(2);
        await Assert.That(exception!.Message).IsEqualTo("duplicated entity");
    }

    private static Task<Proxy> CreateProxyAsync(QueueHttpMessageHandler handler)
    {
        var client = new Client(new StubHttpClientFactory(handler));
        return client.AddAsync(new Proxy
        {
            Name = "broker-1",
            Listen = "0.0.0.0:19092",
            Upstream = "broker-1:9092",
            Enabled = true
        });
    }

    private static LatencyToxic CreateLatencyToxic() =>
        new()
        {
            Name = "latency-upstream",
            Stream = ToxicDirection.UpStream,
            Toxicity = 1,
            Attributes = { Latency = 300 }
        };

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string content) =>
        new(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };

    private sealed class StubHttpClientFactory(QueueHttpMessageHandler handler) : IHttpClientFactory
    {
        public Uri BaseUrl { get; } = new("http://toxiproxy.test");

        public HttpClient Create() => new(handler, disposeHandler: false)
        {
            BaseAddress = BaseUrl
        };
    }

    private sealed class QueueHttpMessageHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);
        private readonly List<(HttpMethod Method, string Path)> _requests = [];

        internal int RequestCount => _requests.Count;

        internal int ToxicAddRequestCount => _requests.Count(static request =>
            request.Method == HttpMethod.Post
            && request.Path.EndsWith("/toxics", StringComparison.Ordinal));

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            _requests.Add((request.Method, request.RequestUri!.AbsolutePath));
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
