using System.Buffers;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Dekaf;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Extensions.Hosting;
using Dekaf.Outbox;
using Dekaf.Producer;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Avro.Poco;
using Dekaf.Serialization;
using Dekaf.Security;
using Dekaf.Security.Sasl;
using Dekaf.ShareConsumer;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
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
    public static readonly IConfiguration config = configuration;
    public static readonly IKafkaProducer<string, string> producer = null!;
    public static readonly IKafkaProducer<string, string> baseProducer = null!;
    public static readonly ITopicProducer<string, string> topicProducer = null!;
    public static readonly IKafkaProducer<string, byte[]> eventProducer = null!;
    public static readonly IKafkaConsumer<string, string> consumer = null!;
    public static readonly IKafkaShareConsumer<string, string> shareConsumer = null!;
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
    public static readonly string bootstrapServers = "localhost:9092";
    public static readonly string eventData = "event-data";
    public static readonly string eventKey = "event-key";
    public static readonly string eventValue = "event-value";
    public static readonly string requestId = "request-id";
    public static readonly string tenantId = "tenant-id";
    public static readonly string region = "region";
    public static readonly string priority = "normal";
    public static readonly string originalTopic = "orders";
    public static readonly string key = "key";
    public static readonly string value = "value";
    public static readonly string auditEntry = "audit-entry";
    public static readonly string notification = "notification";
    public static readonly string event1 = "event-1";
    public static readonly string event2 = "event-2";
    public static readonly string event3 = "event-3";
    public static readonly TopicPartition partition = new("orders", 0);
    public static readonly long offset;
    public static readonly int retryCount;
    public static readonly int minSize = 1;
    public static readonly int queueDepth;
    public static readonly bool isRetry;
    public static readonly bool shouldCommit;
    public static readonly bool SomeCondition;
    public static readonly CancellationToken stoppingToken;
    public static readonly CancellationTokenSource cts = new();
    public static readonly Headers headers = Headers.Create();
    public static readonly byte[] messageBody = [];
    public static readonly byte[] checksumBytes = [];
    public static readonly Exception exception = new InvalidOperationException();
    public static readonly Order order = new();
    public static readonly ConsumeResult<string, string> message = default;
    public static readonly ConsumeResult<string, string> inputMessage = default;
    public static readonly ConsumeResult<string, string> outboxResult = default;
    public static readonly ProducerMessage<string, string>[] messages = [];
    public static readonly DownstreamHealth downstream = new();
    public static readonly ConcurrentDictionary<TopicPartition, TopicPartitionOffset> completedOffsets = new();
    public static readonly dynamic service = null!;
    public static readonly dynamic tracing = null!;
    public static readonly dynamic metrics = null!;
    public static readonly dynamic db = null!;
    public static readonly dynamic elasticClient = null!;
    public static readonly dynamic deadLetterProducer = null!;
    public static readonly dynamic record = null!;
    public static readonly dynamic fallbackDeserializer = null!;
    public static readonly dynamic orderDeserializer = null!;
    public static readonly dynamic paymentDeserializer = null!;
    public static readonly dynamic customerV1Deserializer = null!;
    public static readonly dynamic customerV2Deserializer = null!;
    public static readonly dynamic orderSerializer = null!;
    public static readonly dynamic paymentSerializer = null!;
    public static readonly dynamic migrationHandler = null!;
    public static readonly dynamic keyVault = null!;
    public static readonly dynamic _tokenService = null!;
    public static readonly dynamic secretManager = null!;
    public static readonly dynamic _metrics = null!;
    public static readonly dynamic _contextFactory = null!;
    public static readonly dynamic published = null!;
    public static readonly string connectionString = "Host=localhost";
    public static readonly IReadOnlyList<string> millionsOfItems = [];
    public static readonly string storedPreparedState = string.Empty;
    public static readonly ModelBuilder modelBuilder = new();
    public static readonly string result = "result";
    public static readonly IReadOnlyList<TopicPartitionOffset> offsets = [];
    public static readonly IReadOnlyList<EventData> events = [];

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
    public static ValueTask ProcessOrderAsync<T>(T value) => ValueTask.CompletedTask;
    public static ValueTask ProcessOrderAsync<T>(T value, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    public static ValueTask ProcessBatchAsync<T>(T value) => ValueTask.CompletedTask;
    public static ValueTask ParkOnRetryTopicAsync<T>(T value) => ValueTask.CompletedTask;
    public static ValueTask ParkOnRetryTopicAsync<T>(T value, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    public static ValueTask UpdateUserAggregateAsync<T1, T2>(T1 key, T2 values) => ValueTask.CompletedTask;
    public static ValueTask AnalyzeSampleAsync<T>(T value) => ValueTask.CompletedTask;
    public static ValueTask SaveBatchAsync<T>(T value) => ValueTask.CompletedTask;
    public static ValueTask SaveBatchAsync<T>(T value, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    public static ValueTask SaveAsync<T>(T value) => ValueTask.CompletedTask;
    public static ValueTask SaveAsync<T>(T value, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    public static ValueTask SaveResultAsync<T1, T2>(T1 result, T2 transaction) => ValueTask.CompletedTask;
    public static ValueTask SaveOffsetAsync<T1, T2>(T1 offset, T2 transaction) => ValueTask.CompletedTask;
    public static ValueTask HandlePartitionRecordAsync<T>(T value) => ValueTask.CompletedTask;
    public static ValueTask HandlePartitionRecordAsync<T1, T2, T3, T4>(T1 value1, T2 value2, T3 value3, T4 value4) => ValueTask.CompletedTask;
    public static ValueTask HandleOrderAsync<T1, T2, T3>(T1 value1, T2 value2, T3 value3) => ValueTask.CompletedTask;
    public static bool CanProcess<T>(T value) => true;
    public static bool ShouldFlushOffset<T>(T value) => true;
    public static ValueTask<bool> IsAlreadyProcessedAsync<T>(T value) => new(false);
    public static DeserializedPayload ExpensiveDeserialization<T>(T value) => new();
    public static void Process<T>(T value) { }
    public static IReadOnlyList<TopicPartitionOffset> GetCompletedOffsets() => [];
    public static IReadOnlyList<TopicPartitionOffset> GetCompletedOffsets<T>(T value) => [];
    public static ValueTask<IReadOnlyList<TopicPartitionOffset>> LoadOffsetsFromDatabaseAsync() => new([]);
    public static ValueTask SaveOffsetAsync(TopicPartition topicPartition, long nextOffset) => ValueTask.CompletedTask;
    public static ValueTask SaveOffsetAsync<T1, T2, T3>(T1 value1, T2 value2, T3 value3) => ValueTask.CompletedTask;
    public static ValueTask<IReadOnlyList<Order>> GetPendingOrdersAsync() => new([]);
    public static IEnumerable<ProducerMessage<string, string>> GetMillionsOfMessages() => [];
    public static byte[] ComputeSignature(byte[] input) => [];
    public static ValueTask<IdentityToken> GetTokenFromIdentityProviderAsync(CancellationToken cancellationToken) => new(new IdentityToken());
    public static ValueTask<AwsCredentialSet> LoadCredentialsAsync(CancellationToken cancellationToken) => new(new AwsCredentialSet());
    public static ValueTask<string> GenerateMskIamTokenAsync(CancellationToken cancellationToken) => new("token");
    public static ValueTask<OAuthBearerToken> GetSchemaRegistryTokenAsync(CancellationToken cancellationToken) => new(new OAuthBearerToken
    {
        TokenValue = "token",
        PrincipalName = "documentation-user",
        Expiration = DateTimeOffset.UtcNow.AddMinutes(5)
    });
    public static ValueTask<byte[]> FetchEncryptionKeyAsync<T>(T value) => new([]);
    public static byte[] Encrypt(byte[] input, byte[] encryptionKey) => input;
    public static byte[] Decrypt(ReadOnlyMemory<byte> input, byte[] encryptionKey) => input.ToArray();
    public static byte[] GetData() => [];
}

[AvroRecord(Name = "Order", Namespace = "example.orders")]
public partial record Order
{
    public string Id { get; init; } = "order-id";
    public string CustomerId { get; init; } = "customer-id";
    [AvroField(Precision = 12, Scale = 2)]
    public decimal Total { get; init; }
    public List<string> Items { get; init; } = [];
}

public readonly record struct OrderKey(string Value)
{
    public string TenantId { get; init; } = "tenant-id";
    public string OrderId { get; init; } = "order-id";
}

public record OrderEvent
{
    public string OrderId { get; init; } = "order-id";
    public string Status { get; init; } = "created";
}

public record OrderCreated : OrderEvent
{
    public decimal Amount { get; init; }
}

public record OrderCreatedEvent : OrderEvent;
public record MyCustomType;
public record MyType;
public record User;
public sealed record ProcessedMessage(string Key, string Value, DateTimeOffset Timestamp);
public record LogEntry;
public sealed record DeserializedPayload
{
    public string Type { get; init; } = "Order";
}

public sealed record IdentityToken
{
    public string AccessToken { get; init; } = "token";
    public DateTimeOffset ExpiresAt { get; init; } = DateTimeOffset.UtcNow.AddMinutes(5);
    public string Subject { get; init; } = "subject";
}

public sealed record AwsCredentialSet
{
    public string AccessKeyId { get; init; } = "access-key";
    public string SecretAccessKey { get; init; } = "secret-key";
    public string? SessionToken { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
}

public sealed record EventData
{
    public string Type { get; init; } = "event";
    public byte[] Serialize() => System.Text.Encoding.UTF8.GetBytes(Type);
}

public sealed record DownstreamHealth
{
    public bool IsHealthy { get; init; } = true;
}
public interface IEvent;
public interface IOrderRepository
{
    ValueTask SaveAsync(Order order, CancellationToken cancellationToken = default);
}

public interface ITokenService
{
    ValueTask<IdentityToken> GetAccessTokenAsync(string? scope, CancellationToken cancellationToken);
}

public interface IKafkaSettings
{
    string BootstrapServers { get; }
}

public sealed class KafkaSettings : IKafkaSettings
{
    public string BootstrapServers { get; init; } = "localhost:9092";
}

public sealed class OrderSerializer : ISerde<Order>
{
    public void Serialize<TWriter>(Order value, ref TWriter destination, SerializationContext context)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
    }

    public Order Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) => new();
}

public class TracingInterceptor<TKey, TValue> : IProducerInterceptor<TKey, TValue>
{
    public ProducerMessage<TKey, TValue> OnSend(ProducerMessage<TKey, TValue> message) => message;
    public void OnAcknowledgement(RecordMetadata metadata, Exception? exception) { }
}

public class MetricsInterceptor<TKey, TValue> : IConsumerInterceptor<TKey, TValue>
{
    public ConsumeResult<TKey, TValue> OnConsume(ConsumeResult<TKey, TValue> result) => result;
    public void OnCommit(IReadOnlyList<TopicPartitionOffset> offsets) { }
}

public sealed class AuditInterceptor : IProducerInterceptor<string, string>
{
    public ProducerMessage<string, string> OnSend(ProducerMessage<string, string> message) => message;
    public void OnAcknowledgement(RecordMetadata metadata, Exception? exception) { }
}

public sealed class OrderProducerInterceptor : IProducerInterceptor<string, Order>
{
    public ProducerMessage<string, Order> OnSend(ProducerMessage<string, Order> message) => message;
    public void OnAcknowledgement(RecordMetadata metadata, Exception? exception) { }
}

public sealed class OrderConsumerInterceptor : IConsumerInterceptor<string, Order>
{
    public ConsumeResult<string, Order> OnConsume(ConsumeResult<string, Order> result) => result;
    public void OnCommit(IReadOnlyList<TopicPartitionOffset> offsets) { }
}

public class OrderConsumerService : KafkaConsumerService<string, string>
{
    public OrderConsumerService() : base(null!, null!) { }
    protected override IEnumerable<string> Topics => ["orders"];
    protected override ValueTask ProcessAsync(ConsumeResult<string, string> result, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}

public class OrderProcessorService : KafkaConsumerService<string, Order>
{
    public OrderProcessorService() : base(null!, null!) { }
    protected override IEnumerable<string> Topics => ["orders"];
    protected override ValueTask ProcessAsync(ConsumeResult<string, Order> result, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}

public sealed class OrderService : KafkaConsumerService<string, string>
{
    public OrderService() : base(null!, null!) { }
    protected override IEnumerable<string> Topics => ["orders"];
    protected override ValueTask ProcessAsync(ConsumeResult<string, string> result, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}

public sealed class PaymentService : KafkaConsumerService<string, string>
{
    public PaymentService() : base(null!, null!) { }
    protected override IEnumerable<string> Topics => ["payments"];
    protected override ValueTask ProcessAsync(ConsumeResult<string, string> result, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
public class OrderDbContext : DbContext
{
    public DbSet<Order> Orders => Set<Order>();
}
public class OrdersContext : DbContext
{
    public DbSet<Order> Orders => Set<Order>();
}
public class SecondContext : DbContext;
public sealed class OrderValidationException : Exception;
public sealed class TransientException : Exception;
public sealed class PoisonMessageException : Exception;
public class OrderProcessor : KafkaConsumerService<string, string>
{
    public OrderProcessor() : base(null!, null!) { }
    protected override IEnumerable<string> Topics => ["orders"];
    protected override ValueTask ProcessAsync(ConsumeResult<string, string> result, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}

public sealed class EnterprisePolicyHandler(HttpMessageHandler innerHandler)
    : DelegatingHandler(innerHandler);

public sealed class EncryptingOrderSerde(object keyVault) : IAsyncSerde<Order>
{
    public ValueTask SerializeAsync(
        Order value,
        IBufferWriter<byte> destination,
        SerializationContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        destination.Write(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(value));
        GC.KeepAlive(keyVault);
        return ValueTask.CompletedTask;
    }

    public ValueTask<Order> DeserializeAsync(
        ReadOnlyMemory<byte> data,
        SerializationContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        GC.KeepAlive(keyVault);
        return new(System.Text.Json.JsonSerializer.Deserialize<Order>(data.Span)!);
    }
}

public sealed class DynamoDbOutboxStore : IOutboxStore
{
    public ValueTask<IReadOnlyList<int>> AcquireBucketLeasesAsync(
        OutboxLeaseRequest request,
        CancellationToken cancellationToken = default) => new([]);

    public ValueTask<IReadOnlyList<int>> GetBucketsWithPendingAsync(
        IReadOnlyList<int> buckets,
        CancellationToken cancellationToken = default) => new([]);

    public ValueTask<IReadOnlyList<OutboxMessage>> GetNextBatchAsync(
        int bucket,
        int maxCount,
        CancellationToken cancellationToken = default) => new([]);

    public ValueTask MarkPublishedAsync(
        int bucket,
        IReadOnlyList<OutboxMessage> publishedMessages,
        CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}

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
