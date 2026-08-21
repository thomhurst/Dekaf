using System.Text;
using AlibabaCloud.OpenApiClient.Models;
using AlibabaCloud.SDK.Kms20160120;
using AlibabaCloud.SDK.Kms20160120.Models;
using Aliyun.Credentials.Provider;
using CredentialClient = Aliyun.Credentials.Client;
using CredentialConfig = Aliyun.Credentials.Models.Config;

namespace Dekaf.SchemaRegistry.Kms.AliCloud;

internal sealed class AliCloudSdkKmsClientFactory : IAliCloudKmsClientFactory
{
    public IAliCloudKmsClient CreateClient(AliCloudKmsClientConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var config = new Config
        {
            Credential = CreateCredential(configuration),
            RegionId = configuration.RegionId,
            Protocol = "https",
            Endpoint = configuration.Endpoint,
            Ca = configuration.CertificateAuthority
        };
        return new AliCloudSdkKmsClient(new Client(config));
    }

    private static CredentialClient CreateCredential(AliCloudKmsClientConfiguration configuration)
    {
        if (string.Equals(configuration.CredentialType, "ram_role_arn", StringComparison.Ordinal))
        {
            var builder = new RamRoleArnCredentialProvider.Builder()
                .RoleArn(configuration.RoleArn)
                .RoleSessionName(configuration.RoleSessionName!)
                .Policy(configuration.Policy)
                .STSEndpoint(configuration.StsEndpoint)
                .ExternalId(configuration.RoleExternalId);

            if (configuration.RoleSessionExpiration is { } expiration)
                builder.DurationSeconds(expiration);

            if (configuration.AccessKeyId is not null)
            {
                builder
                    .AccessKeyId(configuration.AccessKeyId)
                    .AccessKeySecret(configuration.AccessKeySecret)
                    .SecurityToken(configuration.SecurityToken);
            }
            else
            {
                builder.CredentialsProvider(new DefaultCredentialsProvider());
            }

            return new CredentialClient(builder.Build());
        }

        if (!string.Equals(configuration.CredentialType, "default", StringComparison.Ordinal))
        {
            return new CredentialClient(new CredentialConfig
            {
                Type = configuration.SecurityToken is null ? "access_key" : "sts",
                AccessKeyId = configuration.AccessKeyId,
                AccessKeySecret = configuration.AccessKeySecret,
                SecurityToken = configuration.SecurityToken
            });
        }

        return new CredentialClient();
    }
}

internal sealed class AliCloudSdkKmsClient(Client client) : IAliCloudKmsClient
{
    private readonly Client _client = client ?? throw new ArgumentNullException(nameof(client));

    public async ValueTask<byte[]> EncryptAsync(
        string keyId,
        ReadOnlyMemory<byte> plaintext,
        CancellationToken cancellationToken = default)
    {
        var response = await _client.EncryptAsync(new EncryptRequest
            {
                KeyId = keyId,
                Plaintext = AliCloudKmsWireEncoding.EncodePlaintext(plaintext.Span)
            })
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        var ciphertext = response.Body?.CiphertextBlob;
        if (string.IsNullOrEmpty(ciphertext))
            return [];

        return AliCloudKmsWireEncoding.EncodeCiphertext(ciphertext);
    }

    public async ValueTask<byte[]> DecryptAsync(
        ReadOnlyMemory<byte> ciphertext,
        CancellationToken cancellationToken = default)
    {
        var response = await _client.DecryptAsync(new DecryptRequest
            {
                CiphertextBlob = AliCloudKmsWireEncoding.DecodeCiphertext(ciphertext.Span)
            })
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        var plaintext = response.Body?.Plaintext;
        return string.IsNullOrEmpty(plaintext) ? [] : AliCloudKmsWireEncoding.DecodePlaintext(plaintext);
    }
}

internal static class AliCloudKmsWireEncoding
{
    internal static string EncodePlaintext(ReadOnlySpan<byte> plaintext) => Convert.ToBase64String(plaintext);

    internal static byte[] DecodePlaintext(string plaintext) => Convert.FromBase64String(plaintext);

    internal static byte[] EncodeCiphertext(string ciphertext) => Encoding.UTF8.GetBytes(ciphertext);

    internal static string DecodeCiphertext(ReadOnlySpan<byte> ciphertext) => Encoding.UTF8.GetString(ciphertext);
}
