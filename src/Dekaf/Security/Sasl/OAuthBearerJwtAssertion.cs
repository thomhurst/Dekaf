namespace Dekaf.Security.Sasl;

using System.Buffers;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal static class OAuthBearerJwtAssertion
{
    private static readonly HashSet<string> ReservedClaims = new(StringComparer.Ordinal)
    {
        "iss",
        "sub",
        "aud",
        "exp",
        "nbf",
        "iat",
        "jti"
    };

    internal static string Create(OAuthBearerConfig config, DateTimeOffset now)
    {
        var options = config.JwtBearer
            ?? throw new InvalidOperationException("JWT-bearer OAuth config requires JwtBearer options");

        var clientId = Required(config.ClientId, "OAuth client ID is required");
        var audience = Required(options.Audience, "JWT-bearer OAuth audience is required");
        var issuer = string.IsNullOrWhiteSpace(options.Issuer) ? clientId : options.Issuer!;
        var subject = string.IsNullOrWhiteSpace(options.Subject) ? clientId : options.Subject!;
        var privateKey = options.PrivateKey
            ?? throw new InvalidOperationException("JWT-bearer OAuth private key is required");
        if (options.AssertionLifetime <= TimeSpan.Zero)
            throw new InvalidOperationException("JWT-bearer assertion lifetime must be positive");

        ValidateAdditionalClaims(options.AdditionalClaims);

        var algorithm = ResolveAlgorithm(privateKey, options.SigningAlgorithm);
        var header = Base64UrlJson(writer =>
        {
            writer.WriteString("alg", AlgorithmName(algorithm));
            writer.WriteString("typ", "JWT");
            if (!string.IsNullOrWhiteSpace(options.KeyId))
                writer.WriteString("kid", options.KeyId);
        });

        var expiresAt = now.Add(options.AssertionLifetime);
        var payload = Base64UrlJson(writer =>
        {
            writer.WriteString("iss", issuer);
            writer.WriteString("sub", subject);
            writer.WriteString("aud", audience);
            writer.WriteNumber("iat", now.ToUnixTimeSeconds());
            writer.WriteNumber("exp", expiresAt.ToUnixTimeSeconds());
            writer.WriteString("jti", Guid.NewGuid().ToString("N"));

            if (options.AdditionalClaims is not null)
            {
                foreach (var (name, value) in options.AdditionalClaims)
                {
                    writer.WritePropertyName(name);
                    JsonSerializer.Serialize(writer, value, value?.GetType() ?? typeof(object));
                }
            }
        });

        var signingInput = $"{header}.{payload}";
        var signature = Sign(privateKey, algorithm, Encoding.ASCII.GetBytes(signingInput));
        return $"{signingInput}.{Base64Url.EncodeToString(signature)}";
    }

    private static string Required(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(message);
        return value;
    }

    private static void ValidateAdditionalClaims(IReadOnlyDictionary<string, object?>? claims)
    {
        if (claims is null)
            return;

        foreach (var name in claims.Keys)
        {
            if (ReservedClaims.Contains(name))
                throw new InvalidOperationException($"JWT-bearer additional claim '{name}' conflicts with a reserved assertion claim");
        }
    }

    private static string Base64UrlJson(Action<Utf8JsonWriter> writeProperties)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writeProperties(writer);
            writer.WriteEndObject();
        }

        return Base64Url.EncodeToString(buffer.WrittenSpan);
    }

    private static OAuthBearerJwtSigningAlgorithm ResolveAlgorithm(
        AsymmetricAlgorithm key,
        OAuthBearerJwtSigningAlgorithm? configured)
    {
        if (configured is not null)
            return configured.Value;

        return key switch
        {
            RSA => OAuthBearerJwtSigningAlgorithm.Rs256,
            ECDsa ecdsa when ecdsa.KeySize <= 256 => OAuthBearerJwtSigningAlgorithm.Es256,
            ECDsa ecdsa when ecdsa.KeySize <= 384 => OAuthBearerJwtSigningAlgorithm.Es384,
            ECDsa => OAuthBearerJwtSigningAlgorithm.Es512,
            _ => throw new InvalidOperationException("JWT-bearer OAuth private key must be RSA or ECDSA")
        };
    }

    private static string AlgorithmName(OAuthBearerJwtSigningAlgorithm algorithm) => algorithm switch
    {
        OAuthBearerJwtSigningAlgorithm.Rs256 => "RS256",
        OAuthBearerJwtSigningAlgorithm.Rs384 => "RS384",
        OAuthBearerJwtSigningAlgorithm.Rs512 => "RS512",
        OAuthBearerJwtSigningAlgorithm.Ps256 => "PS256",
        OAuthBearerJwtSigningAlgorithm.Ps384 => "PS384",
        OAuthBearerJwtSigningAlgorithm.Ps512 => "PS512",
        OAuthBearerJwtSigningAlgorithm.Es256 => "ES256",
        OAuthBearerJwtSigningAlgorithm.Es384 => "ES384",
        OAuthBearerJwtSigningAlgorithm.Es512 => "ES512",
        _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, null)
    };

    private static byte[] Sign(
        AsymmetricAlgorithm key,
        OAuthBearerJwtSigningAlgorithm algorithm,
        byte[] signingInput) => algorithm switch
    {
        OAuthBearerJwtSigningAlgorithm.Rs256 => SignRsa(key, signingInput, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1),
        OAuthBearerJwtSigningAlgorithm.Rs384 => SignRsa(key, signingInput, HashAlgorithmName.SHA384, RSASignaturePadding.Pkcs1),
        OAuthBearerJwtSigningAlgorithm.Rs512 => SignRsa(key, signingInput, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1),
        OAuthBearerJwtSigningAlgorithm.Ps256 => SignRsa(key, signingInput, HashAlgorithmName.SHA256, RSASignaturePadding.Pss),
        OAuthBearerJwtSigningAlgorithm.Ps384 => SignRsa(key, signingInput, HashAlgorithmName.SHA384, RSASignaturePadding.Pss),
        OAuthBearerJwtSigningAlgorithm.Ps512 => SignRsa(key, signingInput, HashAlgorithmName.SHA512, RSASignaturePadding.Pss),
        OAuthBearerJwtSigningAlgorithm.Es256 => SignEcdsa(key, signingInput, HashAlgorithmName.SHA256),
        OAuthBearerJwtSigningAlgorithm.Es384 => SignEcdsa(key, signingInput, HashAlgorithmName.SHA384),
        OAuthBearerJwtSigningAlgorithm.Es512 => SignEcdsa(key, signingInput, HashAlgorithmName.SHA512),
        _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, null)
    };

    private static byte[] SignRsa(
        AsymmetricAlgorithm key,
        byte[] signingInput,
        HashAlgorithmName hashAlgorithm,
        RSASignaturePadding padding)
    {
        if (key is not RSA rsa)
            throw new InvalidOperationException("Selected JWT-bearer signing algorithm requires an RSA private key");

        return rsa.SignData(signingInput, hashAlgorithm, padding);
    }

    private static byte[] SignEcdsa(
        AsymmetricAlgorithm key,
        byte[] signingInput,
        HashAlgorithmName hashAlgorithm)
    {
        if (key is not ECDsa ecdsa)
            throw new InvalidOperationException("Selected JWT-bearer signing algorithm requires an ECDSA private key");

        return ecdsa.SignData(signingInput, hashAlgorithm, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }
}
