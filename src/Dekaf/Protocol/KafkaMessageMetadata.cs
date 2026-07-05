using System.Reflection;

namespace Dekaf.Protocol;

internal static class KafkaRequestMetadata<TRequest, TResponse>
    where TRequest : IKafkaRequest<TResponse>
    where TResponse : IKafkaResponse
{
#if NETSTANDARD2_0
    private static readonly Func<short, short> s_getRequestHeaderVersion =
        CreateVersionResolver("GetRequestHeaderVersion", 2);
    private static readonly Func<short, short> s_getResponseHeaderVersion =
        CreateVersionResolver("GetResponseHeaderVersion", 1);

    public static ApiKey ApiKey { get; } = GetApiKey();

    public static short GetRequestHeaderVersion(short version) => s_getRequestHeaderVersion(version);

    public static short GetResponseHeaderVersion(short version) => s_getResponseHeaderVersion(version);

    private static ApiKey GetApiKey()
    {
        var property = typeof(TRequest).GetProperty("ApiKey", BindingFlags.Public | BindingFlags.Static);
        if (property?.GetValue(null) is ApiKey apiKey)
            return apiKey;

        throw new InvalidOperationException($"{typeof(TRequest).FullName} does not expose a static ApiKey property.");
    }

    private static Func<short, short> CreateVersionResolver(string methodName, short defaultValue)
    {
        var method = typeof(TRequest).GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(short) },
            modifiers: null);
        if (method is null)
            return _ => defaultValue;

        return (Func<short, short>)Delegate.CreateDelegate(typeof(Func<short, short>), method);
    }
#else
    public static ApiKey ApiKey => TRequest.ApiKey;

    public static short GetRequestHeaderVersion(short version) => TRequest.GetRequestHeaderVersion(version);

    public static short GetResponseHeaderVersion(short version) => TRequest.GetResponseHeaderVersion(version);
#endif
}

internal static class KafkaResponseMetadata<TResponse>
    where TResponse : IKafkaResponse
{
#if NETSTANDARD2_0
    private delegate IKafkaResponse ReadResponseDelegate(ref KafkaProtocolReader reader, short version);

    private static readonly ReadResponseDelegate s_read = CreateReadDelegate();

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version) => s_read(ref reader, version);

    private static ReadResponseDelegate CreateReadDelegate()
    {
        var method = typeof(TResponse).GetMethod(
            "Read",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(KafkaProtocolReader).MakeByRefType(), typeof(short) },
            modifiers: null);
        if (method is null)
            throw new InvalidOperationException($"{typeof(TResponse).FullName} does not expose a static Read method.");

        return (ReadResponseDelegate)Delegate.CreateDelegate(typeof(ReadResponseDelegate), method);
    }
#else
    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version) => TResponse.Read(ref reader, version);
#endif
}
