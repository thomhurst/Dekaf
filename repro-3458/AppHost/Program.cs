using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// REPRO_VARIANT selects which resources the AppHost declares.
var variant = builder.Configuration["REPRO_VARIANT"] ?? "issue";
Console.WriteLine($"[repro] variant={variant}");

switch (variant)
{
    case "issue":
    {
        var kafka = builder.AddDekafKafka("kafka").WithKafkaUI();
        builder.AddDekafSchemaRegistry("schema-registry", kafka);
        break;
    }
    case "kafka-only":
        builder.AddDekafKafka("kafka");
        break;
    case "kafka-ui":
        builder.AddDekafKafka("kafka").WithKafkaUI();
        break;
    case "registry":
    {
        var kafka = builder.AddDekafKafka("kafka");
        builder.AddDekafSchemaRegistry("schema-registry", kafka);
        break;
    }
    case "redis-tls":
        // Aspire's own integration with the same HTTPS certificate configuration pattern.
        builder.AddRedis("redis");
        break;
    case "plain-container":
        builder.AddContainer("nginx", "nginx", "1.27-alpine").WithHttpEndpoint(targetPort: 80);
        break;
    default:
        throw new InvalidOperationException($"Unknown variant {variant}");
}

builder.Build().Run();
