using System.Reflection;

namespace Dekaf.Protocol;

internal static class KafkaMessageMetadata<TRequest, TResponse>
    where TRequest : IKafkaRequest<TResponse>
    where TResponse : IKafkaResponse
{
#if !NETSTANDARD2_0
    public static ApiKey ApiKey => TRequest.ApiKey;

    public static short LowestSupportedVersion => TRequest.LowestSupportedVersion;

    public static short HighestSupportedVersion => TRequest.HighestSupportedVersion;

    public static short GetRequestHeaderVersion(short version) => TRequest.GetRequestHeaderVersion(version);

    public static short GetResponseHeaderVersion(short version) => TRequest.GetResponseHeaderVersion(version);

    public static TResponse ReadResponse(ref KafkaProtocolReader reader, short version)
        => (TResponse)TResponse.Read(ref reader, version);

    public static int GetThrottleTimeMs(TResponse response) => response.ThrottleTimeMs;
#else
    private delegate short HeaderVersionDelegate(short version);
    private delegate IKafkaResponse ReadResponseDelegate(ref KafkaProtocolReader reader, short version);

    private static readonly HeaderVersionDelegate? s_getRequestHeaderVersion =
        CreateHeaderVersionDelegate(nameof(GetRequestHeaderVersion));
    private static readonly HeaderVersionDelegate? s_getResponseHeaderVersion =
        CreateHeaderVersionDelegate(nameof(GetResponseHeaderVersion));
    private static readonly ReadResponseDelegate s_readResponse = CreateReadResponseDelegate();
    private static readonly Func<TResponse, int> s_getThrottleTimeMs = CreateThrottleTimeDelegate();

    public static ApiKey ApiKey { get; } = ReadApiKey();

    public static short LowestSupportedVersion { get; } = ReadVersion(nameof(LowestSupportedVersion));

    public static short HighestSupportedVersion { get; } = ReadVersion(nameof(HighestSupportedVersion));

    public static short GetRequestHeaderVersion(short version)
        => s_getRequestHeaderVersion?.Invoke(version) ?? 2;

    public static short GetResponseHeaderVersion(short version)
        => s_getResponseHeaderVersion?.Invoke(version) ?? 1;

    public static TResponse ReadResponse(ref KafkaProtocolReader reader, short version)
        => (TResponse)s_readResponse(ref reader, version);

    public static int GetThrottleTimeMs(TResponse response) => s_getThrottleTimeMs(response);

    private static ApiKey ReadApiKey()
    {
        var property = typeof(TRequest).GetProperty(
            nameof(ApiKey),
            BindingFlags.Public | BindingFlags.Static);

        if (property is null || property.GetValue(null) is not ApiKey apiKey)
        {
            throw new InvalidOperationException(
                $"{typeof(TRequest).FullName} must expose a public static ApiKey property.");
        }

        return apiKey;
    }

    private static short ReadVersion(string propertyName)
    {
        var property = typeof(TRequest).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
        if (property is null || property.GetValue(null) is not short version)
        {
            throw new InvalidOperationException(
                $"{typeof(TRequest).FullName} must expose a public static {propertyName} property.");
        }

        return version;
    }

    private static HeaderVersionDelegate? CreateHeaderVersionDelegate(string name)
    {
        var method = typeof(TRequest).GetMethod(
            name,
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(short)],
            modifiers: null);

        return method is null
            ? null
            : (HeaderVersionDelegate)Delegate.CreateDelegate(typeof(HeaderVersionDelegate), method);
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
                $"{typeof(TResponse).FullName} must expose a public static Read(ref KafkaProtocolReader, short) method.");
        }

        return (ReadResponseDelegate)Delegate.CreateDelegate(typeof(ReadResponseDelegate), method);
    }

    private static Func<TResponse, int> CreateThrottleTimeDelegate()
    {
        var getter = typeof(TResponse).GetProperty(
            "ThrottleTimeMs",
            BindingFlags.Public | BindingFlags.Instance)?.GetMethod;

        return getter is null
            ? static _ => 0
            : (Func<TResponse, int>)Delegate.CreateDelegate(typeof(Func<TResponse, int>), getter);
    }
#endif
}
