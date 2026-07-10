using System.Collections;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using VerifyTUnit;
using static Dekaf.Tests.Unit.Protocol.WireFormatSnapshotSupport;

namespace Dekaf.Tests.Unit.Protocol;

[NotInParallel("FetchResponsePool")]
public class ResponseWireFormatSnapshotTests
{
    private const string FixtureResourceSegment = ".Protocol.ResponseFixtures.";

    private static readonly IReadOnlyDictionary<string, Type> RequestTypesByResponse = typeof(IKafkaMessage).Assembly
        .GetTypes()
        .Where(static type => !type.IsAbstract && GetRequestInterface(type) is not null)
        .OrderBy(static type => type.FullName, StringComparer.Ordinal)
        .ToDictionary(
            static type => GetRequestInterface(type)!.GetGenericArguments()[0].Name,
            StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, byte[]> ResponseFixtures = LoadResponseFixtures();

    [Test]
    [MethodDataSource(nameof(ResponseVersions))]
    public async Task DecodedResponse_MatchesGoldenSnapshot(string responseType, short version)
    {
        var fixtureName = $"{responseType}.v{version}";
        var bytes = ResponseFixtures[fixtureName];
        var requestType = RequestTypesByResponse[responseType];
        var type = GetRequestInterface(requestType)!.GetGenericArguments()[0];
        var decoded = KafkaResponseDeserializer.Deserialize(requestType, bytes, version);

        try
        {
            await Assert.That(decoded.Consumed).IsEqualTo(bytes.Length);

            var apiKey = GetStaticProperty<ApiKey>(type, "ApiKey");
            var snapshot = $$"""
                Response: {{type.FullName}}
                API key: {{apiKey}} ({{(short)apiKey}})
                Version: {{version}}
                Header version: {{decoded.HeaderVersion}}
                Body length: {{bytes.Length}} bytes

                {{HexDump.Format(bytes)}}

                Decoded:
                {{ResponseSnapshotNormalizer.Normalize(decoded.Response)}}
                """;

            var settings = new VerifyTests.VerifySettings();
            settings.UseDirectory("Snapshots");
            settings.UseFileName(fixtureName);
            settings.IgnoreParameters(nameof(responseType), nameof(version));
            settings.DisableRequireUniquePrefix();

            await Verifier.Verify(snapshot, settings);
        }
        finally
        {
            ReturnPooledResponse(decoded.Response);
        }
    }

    [Test]
    public async Task Fixtures_CoverEveryConcreteResponseVersion()
    {
        var responseTypes = typeof(IKafkaMessage).Assembly
            .GetTypes()
            .Where(static type => !type.IsAbstract && typeof(IKafkaResponse).IsAssignableFrom(type))
            .OrderBy(static type => type.FullName, StringComparer.Ordinal)
            .ToArray();
        var expectedFixtures = responseTypes
            .SelectMany(static type => SupportedVersions(type).Select(version => $"{type.Name}.v{version}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        await Assert.That(RequestTypesByResponse.Keys.Order(StringComparer.Ordinal))
            .IsEquivalentTo(responseTypes.Select(static type => type.Name).Order(StringComparer.Ordinal));
        await Assert.That(ResponseFixtures.Keys.Order(StringComparer.Ordinal)).IsEquivalentTo(expectedFixtures);
    }

    [Test]
    public async Task PopulatedFixtures_MatchDeterministicEncoder()
    {
        var populatedFixtures = DeterministicResponseFixtureFactory.CreateAll();
        foreach (var (fixtureName, expectedBytes) in populatedFixtures)
        {
            var actualBytes = ResponseFixtures[fixtureName];
            await Assert.That(actualBytes.AsSpan().SequenceEqual(expectedBytes)).IsTrue();
        }
    }

    public static IEnumerable<(string ResponseType, short Version)> ResponseVersions()
    {
        foreach (var responseType in RequestTypesByResponse.Keys.Order(StringComparer.Ordinal))
        {
            var requestType = RequestTypesByResponse[responseType];
            var type = GetRequestInterface(requestType)!.GetGenericArguments()[0];
            foreach (var version in SupportedVersions(type))
            {
                yield return (responseType, version);
            }
        }
    }

    private static IEnumerable<short> SupportedVersions(Type responseType)
    {
        var lowest = GetStaticProperty<short>(responseType, "LowestSupportedVersion");
        var highest = GetStaticProperty<short>(responseType, "HighestSupportedVersion");

        for (var version = lowest; version <= highest; version++)
        {
            yield return version;
        }
    }

    private static IReadOnlyDictionary<string, byte[]> LoadResponseFixtures()
    {
        var assembly = typeof(ResponseWireFormatSnapshotTests).Assembly;
        var resourcePrefix = $"{assembly.GetName().Name}{FixtureResourceSegment}";

        // These files are fixed inputs, never generated from the reader under test.
        return assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(resourcePrefix, StringComparison.Ordinal) &&
                           name.EndsWith(".bin", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToDictionary(
                name => name[resourcePrefix.Length..^4],
                name => ReadResource(assembly, name),
                StringComparer.Ordinal);
    }

    private static byte[] ReadResource(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Could not open embedded response fixture {resourceName}.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static void ReturnPooledResponse(IKafkaResponse response)
    {
        switch (response)
        {
            case ProduceResponse produceResponse:
                produceResponse.Return();
                break;
            case FetchResponse fetchResponse:
                ReturnFetchRecords(fetchResponse);
                fetchResponse.ReturnToPool();
                break;
        }
    }

    private static void ReturnFetchRecords(FetchResponse response)
    {
        foreach (var topic in response.Responses)
        {
            foreach (var partition in topic.Partitions)
            {
                if (partition.Records is not { } batches)
                {
                    continue;
                }

                foreach (var batch in batches)
                {
                    batch.Dispose();
                }

                if (batches is List<RecordBatch> pooledBatches)
                {
                    FetchResponsePartition.ReturnRecordBatchList(pooledBatches);
                }
            }
        }
    }

    private static class KafkaResponseDeserializer
    {
        private static readonly MethodInfo DeserializeCoreMethod = typeof(KafkaResponseDeserializer)
            .GetMethod(nameof(DeserializeCore), BindingFlags.NonPublic | BindingFlags.Static)!;

        public static DecodedResponse Deserialize(Type requestType, byte[] bytes, short version)
        {
            var requestInterface = GetRequestInterface(requestType)
                ?? throw new InvalidOperationException($"{requestType.FullName} is not a Kafka request.");
            var responseType = requestInterface.GetGenericArguments()[0];

            try
            {
                return (DecodedResponse)DeserializeCoreMethod
                    .MakeGenericMethod(requestType, responseType)
                    .Invoke(null, [bytes, version])!;
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }

        private static DecodedResponse DeserializeCore<TRequest, TResponse>(byte[] bytes, short version)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
        {
            var reader = new KafkaProtocolReader(bytes);
            var response = KafkaMessageMetadata<TRequest, TResponse>.ReadResponse(ref reader, version);
            return new DecodedResponse(
                response,
                checked((int)reader.Consumed),
                KafkaMessageMetadata<TRequest, TResponse>.GetResponseHeaderVersion(version));
        }
    }

    private static class ResponseSnapshotNormalizer
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public static string Normalize(IKafkaResponse response) => JsonSerializer.Serialize(
            NormalizeValue(response, new HashSet<object>(ReferenceEqualityComparer.Instance)),
            SerializerOptions);

        private static object? NormalizeValue(object? value, HashSet<object> ancestors)
        {
            if (value is null)
            {
                return null;
            }

            if (value is byte[] bytes)
            {
                return Convert.ToHexString(bytes);
            }

            if (value is ReadOnlyMemory<byte> readOnlyMemory)
            {
                return Convert.ToHexString(readOnlyMemory.Span);
            }

            if (value is Memory<byte> memory)
            {
                return Convert.ToHexString(memory.Span);
            }

            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || value is string or decimal or Guid or DateTime or DateTimeOffset or TimeSpan)
            {
                return value;
            }

            if (value is ProduceResponse produceResponse)
            {
                return new SortedDictionary<string, object?>(StringComparer.Ordinal)
                {
                    [nameof(ProduceResponse.NodeEndpoints)] = NormalizeValue(produceResponse.NodeEndpoints, ancestors),
                    [nameof(ProduceResponse.Responses)] = NormalizeValue(
                        produceResponse.Responses.Take(produceResponse.TopicCount).ToArray(),
                        ancestors),
                    [nameof(ProduceResponse.ThrottleTimeMs)] = produceResponse.ThrottleTimeMs
                };
            }

            if (value is ProduceResponseTopicData topicData)
            {
                return new SortedDictionary<string, object?>(StringComparer.Ordinal)
                {
                    [nameof(ProduceResponseTopicData.Name)] = topicData.Name,
                    [nameof(ProduceResponseTopicData.PartitionResponses)] = NormalizeValue(
                        (topicData.PartitionResponses ?? []).Take(topicData.PartitionCount).ToArray(),
                        ancestors)
                };
            }

            if (value is Record record)
            {
                return new SortedDictionary<string, object?>(StringComparer.Ordinal)
                {
                    [nameof(Record.Attributes)] = record.Attributes,
                    [nameof(Record.HeaderCount)] = record.HeaderCount,
                    [nameof(Record.Headers)] = NormalizeValue(
                        record.Headers?.Take(record.HeaderCount).ToArray(),
                        ancestors),
                    [nameof(Record.IsKeyNull)] = record.IsKeyNull,
                    [nameof(Record.IsValueNull)] = record.IsValueNull,
                    [nameof(Record.Key)] = NormalizeValue(record.Key, ancestors),
                    [nameof(Record.Length)] = record.Length,
                    [nameof(Record.OffsetDelta)] = record.OffsetDelta,
                    [nameof(Record.TimestampDelta)] = record.TimestampDelta,
                    [nameof(Record.Value)] = NormalizeValue(record.Value, ancestors)
                };
            }

            var trackReference = !type.IsValueType;
            if (trackReference && !ancestors.Add(value))
            {
                return "<cycle>";
            }

            try
            {
                if (value is IEnumerable enumerable)
                {
                    return enumerable.Cast<object?>()
                        .Select(item => NormalizeValue(item, ancestors))
                        .ToArray();
                }

                var normalized = new SortedDictionary<string, object?>(StringComparer.Ordinal);
                foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                             .Where(static property => property.CanRead && property.GetIndexParameters().Length == 0)
                             .OrderBy(static property => property.Name, StringComparer.Ordinal))
                {
                    normalized[property.Name] = NormalizeValue(property.GetValue(value), ancestors);
                }

                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public)
                             .OrderBy(static field => field.Name, StringComparer.Ordinal))
                {
                    normalized[field.Name] = NormalizeValue(field.GetValue(value), ancestors);
                }

                return normalized;
            }
            finally
            {
                if (trackReference)
                {
                    ancestors.Remove(value);
                }
            }
        }
    }

    private readonly record struct DecodedResponse(
        IKafkaResponse Response,
        int Consumed,
        short HeaderVersion);
}
