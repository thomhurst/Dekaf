var builder = DistributedApplication.CreateBuilder(args);

var kafka = builder.AddDekafKafka("messaging")
    .WithKafkaUI();

var registry = builder.AddDekafSchemaRegistry("schema-registry", kafka);

builder.AddDotnetProject("api", "../Dekaf.Aspire.Sample.ApiService/Dekaf.Aspire.Sample.ApiService.csproj")
    .WithHttpHealthCheck("/health")
    .WithReference(kafka)
    .WithReference(registry)
    .WaitFor(kafka)
    .WaitFor(registry);

builder.Build().Run();
