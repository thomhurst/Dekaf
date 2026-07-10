using System.Reflection;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;

namespace Dekaf.Fuzzing;

internal interface IResponseParser
{
    void Parse(ref KafkaProtocolReader reader, short version);
}

internal sealed record ResponseParserRegistration(
    ApiKey ApiKey,
    Type ResponseType,
    short LowestSupportedVersion,
    short HighestSupportedVersion,
    IResponseParser Parser)
{
    public IEnumerable<short> SupportedVersions
    {
        get
        {
            for (var version = LowestSupportedVersion; version <= HighestSupportedVersion; version++)
            {
                yield return version;
            }
        }
    }
}

internal static class ResponseParserRegistry
{
    private const string ApiKeyPropertyName = "ApiKey";
    private const string LowestSupportedVersionPropertyName = "LowestSupportedVersion";
    private const string HighestSupportedVersionPropertyName = "HighestSupportedVersion";

    private static readonly IReadOnlyDictionary<(ApiKey ApiKey, short Version), ResponseParserRegistration>
        RegistrationsBySelection;

    static ResponseParserRegistry()
    {
        Registrations = typeof(IKafkaMessage).Assembly
            .GetTypes()
            .Where(static type => !type.IsAbstract && typeof(IKafkaResponse).IsAssignableFrom(type))
            .OrderBy(static type => GetStaticProperty<ApiKey>(type, ApiKeyPropertyName))
            .Select(CreateRegistration)
            .ToArray();

        RegistrationsBySelection = Registrations
            .SelectMany(static registration => registration.SupportedVersions
                .Select(version => new KeyValuePair<(ApiKey, short), ResponseParserRegistration>(
                    (registration.ApiKey, version),
                    registration)))
            .ToDictionary();
    }

    public static IReadOnlyList<ResponseParserRegistration> Registrations { get; }

    public static bool TryGet(
        ApiKey apiKey,
        short version,
        out ResponseParserRegistration registration) =>
        RegistrationsBySelection.TryGetValue((apiKey, version), out registration!);

    private static ResponseParserRegistration CreateRegistration(Type responseType)
    {
        var parserType = typeof(ResponseParser<>).MakeGenericType(responseType);
        var parser = (IResponseParser)(Activator.CreateInstance(parserType, nonPublic: true)
            ?? throw new InvalidOperationException($"Could not create parser for {responseType.FullName}."));

        return new ResponseParserRegistration(
            GetStaticProperty<ApiKey>(responseType, ApiKeyPropertyName),
            responseType,
            GetStaticProperty<short>(responseType, LowestSupportedVersionPropertyName),
            GetStaticProperty<short>(responseType, HighestSupportedVersionPropertyName),
            parser);
    }

    private static T GetStaticProperty<T>(Type type, string propertyName) =>
        (T)(type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
            ?? throw new InvalidOperationException($"{type.FullName} has no static {propertyName} property."));

    private sealed class ResponseParser<TResponse> : IResponseParser
        where TResponse : IKafkaResponse
    {
        private delegate IKafkaResponse ReadResponseDelegate(ref KafkaProtocolReader reader, short version);

        private static readonly ReadResponseDelegate ReadResponse = CreateReadResponseDelegate();

        public void Parse(ref KafkaProtocolReader reader, short version)
        {
            var response = (TResponse)ReadResponse(ref reader, version);
            ReturnPooledResources(response);
        }

        private static ReadResponseDelegate CreateReadResponseDelegate()
        {
            var method = typeof(TResponse).GetMethod(
                "Read",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: [typeof(KafkaProtocolReader).MakeByRefType(), typeof(short)],
                modifiers: null);

            if (method is null)
            {
                throw new InvalidOperationException(
                    $"{typeof(TResponse).FullName} must expose Read(ref KafkaProtocolReader, short).");
            }

            return (ReadResponseDelegate)Delegate.CreateDelegate(typeof(ReadResponseDelegate), method);
        }

        private static void ReturnPooledResources(TResponse response)
        {
            switch (response)
            {
                case ProduceResponse produceResponse:
                    produceResponse.Return();
                    break;
                case FetchResponse fetchResponse:
                    ReturnFetchRecords(fetchResponse);
                    fetchResponse.PooledMemoryOwner?.Dispose();
                    fetchResponse.PooledMemoryOwner = null;
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
                    if (partition.Records is not { } records)
                    {
                        continue;
                    }

                    foreach (var batch in records)
                    {
                        batch.Dispose();
                    }

                    partition.Records = null;
                    if (records is List<RecordBatch> recordList)
                    {
                        FetchResponsePartition.ReturnRecordBatchList(recordList);
                    }
                }
            }
        }
    }
}
