using Dekaf.PackageSmoke.NetStandard20;
using Dekaf.PackageSmoke.AbstractionsAdapter;
using Dekaf.Producer;

var result = NetStandardPackageSmoke.Run();

if (!result.StartsWith("dekaf:", StringComparison.Ordinal))
{
    throw new InvalidOperationException($"Unexpected package smoke result: {result}");
}

await using IKafkaProducer<string, string> producer = new NoopProducerAdapter();
await producer.InitializeAsync();

var serializer = new AdapterStringSerializer();
if (serializer is not Dekaf.Serialization.ISerializer<string>)
{
    throw new InvalidOperationException("Abstractions-only serializer was not consumable by Dekaf.");
}

Console.WriteLine(result);
