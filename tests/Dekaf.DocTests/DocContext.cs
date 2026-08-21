using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Dekaf;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.SchemaRegistry;
using Dekaf.Security;
using Dekaf.Security.Sasl;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dekaf.DocTests;

public static class DocContext
{
    public static readonly CancellationToken cancellationToken = default;
    public static readonly CancellationToken ct = default;
    public static readonly WebApplicationBuilder builder = WebApplication.CreateBuilder();
    public static readonly IServiceCollection services = new ServiceCollection();
    public static readonly IConfiguration configuration = new ConfigurationBuilder().Build();
    public static readonly IKafkaProducer<string, string> producer = null!;
    public static readonly IKafkaProducer<string, string> baseProducer = null!;
    public static readonly IKafkaConsumer<string, string> consumer = null!;
    public static readonly IAdminClient admin = null!;
    public static readonly ISchemaRegistryClient schemaRegistry = null!;
    public static readonly ISchemaRegistryClient registry = null!;
    public static readonly HttpClient httpClient = new();
    public static readonly ILogger logger = null!;
    public static readonly ILogger _logger = null!;
    public static readonly ILoggerFactory loggerFactory = null!;
    public static readonly TlsConfig tlsConfig = new();
    public static readonly GssapiConfig gssapiConfig = new();
    public static readonly OAuthBearerConfig oauthConfig = null!;
    public static readonly RSA rsaOrEcdsaPrivateKey = RSA.Create();
    public static readonly X509Certificate2 certificate = null!;
    public static readonly string caCert = "certificate";
    public static readonly string clientCert = "certificate";
    public static readonly string clientKey = "key";
    public static readonly string clientCertificatePem = "certificate";
    public static readonly string clientPrivateKeyPem = "key";
    public static readonly string privateKeyPassword = "password";
    public static readonly string apiKey = "api-key";
    public static readonly string apiSecret = "api-secret";
    public static readonly string orderJson = "{}";
    public static readonly string orderJson1 = "{}";
    public static readonly string orderJson2 = "{}";
    public static readonly string orderJson3 = "{}";
    public static readonly string eventJson = "{}";
    public static readonly string largeJsonPayload = "{}";
    public static readonly string correlationId = "correlation-id";
    public static readonly string traceId = "trace-id";
    public static readonly string userId = "user-id";
    public static readonly string orderId = "order-id";
    public static readonly bool isRetry;
    public static readonly bool SomeCondition;
    public static readonly Order order = new();
    public static readonly ConsumeResult<string, string> message = default;
    public static readonly IAsyncEnumerable<ConsumeResult<string, string>> downstream = null!;
    public static readonly ConcurrentDictionary<TopicPartition, TopicPartitionOffset> completedOffsets = new();

    public static ValueTask ProcessAsync<T>(T value) => ValueTask.CompletedTask;
    public static ValueTask ProcessAsync<T>(T value, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    public static ValueTask HandleMessageAsync<T>(T value) => ValueTask.CompletedTask;
    public static ValueTask HandleMessageAsync<T>(T value, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    public static ValueTask HandleOrderAsync<T>(T value) => ValueTask.CompletedTask;
    public static ValueTask HandleOrderAsync<T>(T value, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    public static void ProcessMessage<T>(T value) { }
    public static ValueTask ProcessMessageAsync<T>(T value) => ValueTask.CompletedTask;
    public static void ProcessOrder<T>(T value) { }
    public static ValueTask BulkInsertAsync<T>(IEnumerable<T> values) => ValueTask.CompletedTask;
}

public record Order
{
    public string Id { get; init; } = "order-id";
    public string CustomerId { get; init; } = "customer-id";
    public decimal Total { get; init; }
    public IReadOnlyList<string> Items { get; init; } = [];
}

public readonly record struct OrderKey(string Value)
{
    public string TenantId { get; init; } = "tenant-id";
    public string OrderId { get; init; } = "order-id";
}

public record OrderEvent;
public record OrderCreated : OrderEvent;
public record OrderCreatedEvent : OrderEvent;
public record MyCustomType;
public record MyType;
public record User;
public interface IEvent;
public interface IOrderRepository;
public interface ITokenService;

public sealed class MyRebalanceListener : IRebalanceListener
{
    public ValueTask OnPartitionsAssignedAsync(
        IEnumerable<TopicPartition> partitions,
        CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask OnPartitionsRevokedAsync(
        IEnumerable<TopicPartition> partitions,
        CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask OnPartitionsLostAsync(
        IEnumerable<TopicPartition> partitions,
        CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
