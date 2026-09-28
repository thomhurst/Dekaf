using Dekaf.Admin;
using Dekaf.Aspire.Sample.ApiService;
using Dekaf.Producer;
using Dekaf.SchemaRegistry;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Hosted services start in registration order, so this creates the topics before Dekaf initializes
// the clients registered below and before the share consumer worker subscribes.
builder.Services.AddHostedService<TopicInitializer>();

// Connection strings come from the AppHost's WithReference calls.
builder.AddDekafSchemaRegistryClient("schema-registry");
builder.AddDekafAdminClient("messaging");

// Orders are serialized with JSON Schema; the serializer registers the schema on first use.
builder.AddDekafProducer<string, Order>("messaging", configureBuilder: (services, producer) => producer
    .UseJsonSchemaRegistry(services.GetRequiredService<ISchemaRegistryClient>(), Order.JsonSchema));

// Tasks are queued on a share group (KIP-932): every worker competes for individual records.
builder.AddDekafProducer<string, string>("messaging");
builder.AddDekafShareConsumerService<TaskWorker, string, string>("messaging",
    configureBuilder: (_, consumer) => consumer.WithGroupId("task-workers"));
builder.Services.AddSingleton<TaskCounter>();

var app = builder.Build();

app.MapHealthChecks("/health");

// Lists each Dekaf health check and its status, for exploring the integration.
app.MapGet("/health/details", async (HealthCheckService health, CancellationToken cancellationToken) =>
{
    var report = await health.CheckHealthAsync(cancellationToken);
    return Results.Ok(report.Entries.ToDictionary(
        entry => entry.Key,
        entry => new { Status = entry.Value.Status.ToString(), entry.Value.Description, Error = entry.Value.Exception?.Message }));
});

app.MapGet("/cluster", async (IAdminClient admin, CancellationToken cancellationToken) =>
{
    var cluster = await admin.DescribeClusterAsync(cancellationToken);
    return Results.Ok(new { cluster.ClusterId, Brokers = cluster.Nodes.Count });
});

app.MapPost("/orders", async (Order order, IKafkaProducer<string, Order> producer, CancellationToken cancellationToken) =>
{
    var result = await producer.ProduceAsync("orders", order.Id, order, cancellationToken);
    return Results.Ok(new { result.Partition, result.Offset });
});

app.MapPost("/tasks/{text}", async (string text, IKafkaProducer<string, string> producer, CancellationToken cancellationToken) =>
{
    var result = await producer.ProduceAsync("tasks", text, text, cancellationToken);
    return Results.Ok(new { result.Partition, result.Offset });
});

app.MapGet("/tasks/processed", (TaskCounter counter) => Results.Ok(new { counter.Processed }));

app.Run();
