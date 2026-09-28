var builder = DistributedApplication.CreateBuilder(args);

var kafka = builder.AddDekafKafka("messaging");
builder.AddDekafSchemaRegistry("schema-registry", kafka);

builder.Build().Run();
