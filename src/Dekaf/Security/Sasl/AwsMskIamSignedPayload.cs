using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Dekaf.Security.Sasl;

internal static class AwsMskIamSignedPayload
{
    private const string Action = "kafka-cluster:Connect";
    private const string Algorithm = "AWS4-HMAC-SHA256";
    private const string Service = "kafka-cluster";
    private const string Version = "2020_10_22";
    private const string SignedHeaders = "host";
    private static readonly byte[] EmptyPayloadHash = SHA256.HashData([]);

    public static string Create(
        AwsCredentials credentials,
        string host,
        string region,
        string userAgent,
        DateTimeOffset now,
        TimeSpan expiration)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(region);
        ArgumentException.ThrowIfNullOrWhiteSpace(userAgent);

        if (expiration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(expiration), "Expiration must be positive");
        if (expiration.TotalSeconds > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(expiration), "Expiration is too large");

        var utc = now.ToUniversalTime();
        var dateStamp = utc.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var amzDate = utc.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var expires = ((int)expiration.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        var credentialScope = $"{dateStamp}/{region}/{Service}/aws4_request";
        var credential = $"{credentials.AccessKeyId}/{credentialScope}";

        var queryParameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["Action"] = Action,
            ["X-Amz-Algorithm"] = Algorithm,
            ["X-Amz-Credential"] = credential,
            ["X-Amz-Date"] = amzDate,
            ["X-Amz-Expires"] = expires,
            ["X-Amz-SignedHeaders"] = SignedHeaders
        };

        if (credentials.SessionToken is not null)
            queryParameters["X-Amz-Security-Token"] = credentials.SessionToken;

        var canonicalRequest = CreateCanonicalRequest(queryParameters, host);
        var stringToSign = string.Join(
            "\n",
            Algorithm,
            amzDate,
            credentialScope,
            Hex(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest))));

        var signature = CalculateSignature(credentials.SecretAccessKey, dateStamp, region, stringToSign);

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("version", Version);
            writer.WriteString("host", host);
            writer.WriteString("user-agent", userAgent);
            writer.WriteString("action", Action);
            writer.WriteString("x-amz-algorithm", Algorithm);
            writer.WriteString("x-amz-credential", credential);
            writer.WriteString("x-amz-date", amzDate);
            if (credentials.SessionToken is not null)
                writer.WriteString("x-amz-security-token", credentials.SessionToken);
            writer.WriteString("x-amz-signedheaders", SignedHeaders);
            writer.WriteString("x-amz-expires", expires);
            writer.WriteString("x-amz-signature", signature);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static string CreateCanonicalRequest(
        SortedDictionary<string, string> queryParameters,
        string host)
    {
        var canonicalQuery = string.Join(
            "&",
            queryParameters.Select(pair => $"{UriEncode(pair.Key)}={UriEncode(pair.Value)}"));

        return string.Join(
            "\n",
            "GET",
            "/",
            canonicalQuery,
            $"host:{host}\n",
            SignedHeaders,
            Hex(EmptyPayloadHash));
    }

    private static string CalculateSignature(
        string secretAccessKey,
        string dateStamp,
        string region,
        string stringToSign)
    {
        var dateKey = Hmac(Encoding.UTF8.GetBytes("AWS4" + secretAccessKey), dateStamp);
        var dateRegionKey = Hmac(dateKey, region);
        var dateRegionServiceKey = Hmac(dateRegionKey, Service);
        var signingKey = Hmac(dateRegionServiceKey, "aws4_request");
        return Hex(Hmac(signingKey, stringToSign));
    }

    private static byte[] Hmac(byte[] key, string message)
        => HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(message));

    private static string Hex(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string UriEncode(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var builder = new StringBuilder(bytes.Length);

        foreach (var b in bytes)
        {
            if ((b >= 'A' && b <= 'Z') ||
                (b >= 'a' && b <= 'z') ||
                (b >= '0' && b <= '9') ||
                b is (byte)'-' or (byte)'_' or (byte)'.' or (byte)'~')
            {
                builder.Append((char)b);
            }
            else
            {
                builder.Append('%');
                builder.Append(b.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return builder.ToString();
    }
}
