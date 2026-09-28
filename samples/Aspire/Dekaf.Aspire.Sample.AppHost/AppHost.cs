var builder = DistributedApplication.CreateBuilder(args);

var kafka = builder.AddDekafKafka("messaging")
    .WithKafkaUI();

var registry = builder.AddDekafSchemaRegistry("schema-registry", kafka);

builder.AddProject<Projects.Dekaf_Aspire_Sample_ApiService>("api")
    .WithHttpHealthCheck("/health")
    .WithReference(kafka)
    .WithReference(registry)
    .WaitFor(kafka)
    .WaitFor(registry);

builder.Build().Run();
