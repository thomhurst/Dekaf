using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Security.Sasl;
using Testcontainers.Kafka;

namespace Dekaf.Tests.Integration.Security;

[Category("Authentication")]
[NotInParallel("KerberosEnvironment")]
public sealed class GssapiAuthenticationIntegrationTests
{
    [Test]
    [RequiresLocalKerberos]
    [Timeout(120_000)]
    public async Task LocalKdc_AuthenticatesKafkaRoundTripAndRejectsWrongService(CancellationToken cancellationToken)
    {
        var bootstrapServers = Environment.GetEnvironmentVariable("DEKAF_KERBEROS_BOOTSTRAP");
        if (bootstrapServers is null)
        {
            await using var realm = new LocalKerberosRealm();
            await realm.InitializeAsync(cancellationToken);
            await using var kafka = new KerberosKafkaContainer(realm);
            await kafka.InitializeAsync();
            await realm.RunClientAsync(kafka.BootstrapServers, cancellationToken);
            return;
        }
        var topic = $"kerberos-{Guid.NewGuid():N}";
        await using var admin = new AdminClientBuilder().WithBootstrapServers(bootstrapServers)
            .WithGssapi(new GssapiConfig()).Build();
        await admin.CreateTopicsAsync([new NewTopic { Name = topic, NumPartitions = 1, ReplicationFactor = 1 }], cancellationToken: cancellationToken);
        var config = new GssapiConfig();
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(bootstrapServers).WithGssapi(config).BuildAsync(cancellationToken);
        await producer.ProduceAsync(topic, "kerberos-key", "kerberos-value", cancellationToken);
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(bootstrapServers).WithGssapi(config)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest).BuildAsync(cancellationToken);
        consumer.Assign(new TopicPartition(topic, 0));
        var record = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(15), cancellationToken);
        await Assert.That(record).IsNotNull();
        await Assert.That(record!.Value.Key).IsEqualTo("kerberos-key");
        await Assert.That(record.Value.Value).IsEqualTo("kerberos-value");

        await Assert.That(async () =>
        {
            await using var invalid = await Kafka.CreateProducer<string, string>()
                .WithBootstrapServers(bootstrapServers)
                .WithGssapi(new GssapiConfig { ServiceName = "missing-service" })
                .BuildAsync(cancellationToken);
        }).Throws<AuthenticationException>();
    }
}

public sealed class RequiresLocalKerberosAttribute() : SkipAttribute(
    "The local MIT Kerberos fixture requires Linux and Dekaf's net10.0 asset; netstandard2.0 has no GSSAPI implementation.")
{
    public override Task<bool> ShouldSkip(TestRegisteredContext context)
    {
#if NET8_0
        return Task.FromResult(true);
#else
        return Task.FromResult(!OperatingSystem.IsLinux());
#endif
    }
}

internal sealed class KerberosKafkaContainer(LocalKerberosRealm realm) : KafkaContainerDefault
{
    protected override KafkaBuilder ConfigureBuilder(KafkaBuilder builder) => base.ConfigureBuilder(builder)
        .WithResourceMapping(File.ReadAllBytes(realm.ServerKeytab), "/tmp/kafka.keytab")
        .WithResourceMapping(File.ReadAllBytes(realm.Configuration), "/tmp/krb5.conf")
        .WithEnvironment("KAFKA_OPTS", "-Djava.security.krb5.conf=/tmp/krb5.conf")
        .WithEnvironment("KAFKA_LISTENER_SECURITY_PROTOCOL_MAP", "PLAINTEXT:SASL_PLAINTEXT,BROKER:PLAINTEXT,CONTROLLER:PLAINTEXT")
        .WithEnvironment("KAFKA_SASL_ENABLED_MECHANISMS", "GSSAPI")
        .WithEnvironment("KAFKA_SASL_KERBEROS_SERVICE_NAME", "kafka")
        .WithEnvironment("KAFKA_LISTENER_NAME_PLAINTEXT_SASL_ENABLED_MECHANISMS", "GSSAPI")
        .WithEnvironment("KAFKA_LISTENER_NAME_PLAINTEXT_GSSAPI_SASL_JAAS_CONFIG",
            "com.sun.security.auth.module.Krb5LoginModule required useKeyTab=true storeKey=true " +
            $"doNotPrompt=true isInitiator=false keyTab=\"/tmp/kafka.keytab\" principal=\"kafka/{realm.BrokerHost}@{LocalKerberosRealm.Realm}\";");

    public override IAdminClient CreateAdminClient() => new AdminClientBuilder()
        .WithBootstrapServers(BootstrapServers).WithGssapi(new GssapiConfig()).Build();
}

internal sealed class LocalKerberosRealm : IAsyncDisposable
{
    public const string Realm = "DEKAF.TEST";
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("dekaf-kerberos-");
    private Process? _kdc;
    private Task<string>? _kdcOutput;
    private Task<string>? _kdcError;
    public string BrokerHost { get; } = Environment.GetEnvironmentVariable("TESTCONTAINERS_HOST_OVERRIDE") ?? "127.0.0.1";
    public string Configuration => Path.Combine(_directory.FullName, "krb5.conf");
    public string ServerKeytab => Path.Combine(_directory.FullName, "server.keytab");
    private string KdcProfile => Path.Combine(_directory.FullName, "kdc.conf");

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        using var portReservation = new TcpListener(IPAddress.Loopback, 0);
        portReservation.Start();
        var port = ((IPEndPoint)portReservation.LocalEndpoint).Port;
        portReservation.Stop();
        await File.WriteAllTextAsync(Configuration, $$"""
            [libdefaults]
              default_realm = {{Realm}}
              dns_lookup_kdc = false
              dns_lookup_realm = false
              rdns = false
              udp_preference_limit = 1
            [realms]
              {{Realm}} = {
                kdc = 127.0.0.1:{{port}}
              }
            """, cancellationToken);
        await File.WriteAllTextAsync(KdcProfile, $$"""
            [kdcdefaults]
              kdc_ports = {{port}}
              kdc_tcp_ports = {{port}}
            [realms]
              {{Realm}} = {
                database_name = {{_directory.FullName}}/principal
                key_stash_file = {{_directory.FullName}}/stash
                acl_file = {{_directory.FullName}}/kadm5.acl
              }
            """, cancellationToken);
        await RunAsync("/usr/sbin/kdb5_util", ["create", "-s", "-P", "local-test-master", "-r", Realm], cancellationToken);
        await RunAsync("/usr/sbin/kadmin.local", ["-r", Realm, "-q", $"addprinc -randkey kafka/{BrokerHost}@{Realm}"], cancellationToken);
        await RunAsync("/usr/sbin/kadmin.local", ["-r", Realm, "-q", $"ktadd -k {ServerKeytab} kafka/{BrokerHost}@{Realm}"], cancellationToken);
        await RunAsync("/usr/sbin/kadmin.local", ["-r", Realm, "-q", $"addprinc -randkey client@{Realm}"], cancellationToken);
        var clientKeytab = Path.Combine(_directory.FullName, "client.keytab");
        await RunAsync("/usr/sbin/kadmin.local", ["-r", Realm, "-q", $"ktadd -k {clientKeytab} client@{Realm}"], cancellationToken);
        _kdc = Process.Start(StartInfo("/usr/sbin/krb5kdc", ["-n", "-r", Realm]))!;
        _kdcOutput = _kdc.StandardOutput.ReadToEndAsync(CancellationToken.None);
        _kdcError = _kdc.StandardError.ReadToEndAsync(CancellationToken.None);
        await TestWait.WaitForConditionAsync(async () =>
        {
            using var socket = new TcpClient();
            try { await socket.ConnectAsync(IPAddress.Loopback, port, cancellationToken); return true; }
            catch (SocketException) { return false; }
        }, static ready => ready, description: "isolated local KDC accepts TCP connections");
        var cache = Path.Combine(_directory.FullName, "ccache");
        await RunAsync("kinit", ["-kt", clientKeytab, "-c", cache, $"client@{Realm}"], cancellationToken);

    }

    public Task RunClientAsync(string bootstrapServers, CancellationToken cancellationToken)
    {
        // Native GSSAPI reads the environment inherited at process startup. Managed
        // Environment.SetEnvironmentVariable does not update native getenv on Unix.
        var processPath = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot find test executable.");
        var info = StartInfo(processPath, []);
        if (Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            info.ArgumentList.Add(Environment.GetCommandLineArgs()[0]);
        info.ArgumentList.Add("--treenode-filter");
        info.ArgumentList.Add("/*/*/GssapiAuthenticationIntegrationTests/*");
        info.ArgumentList.Add("--results-directory");
        info.ArgumentList.Add(Path.Combine(_directory.FullName, "results"));
        info.Environment["DEKAF_KERBEROS_BOOTSTRAP"] = bootstrapServers;
        info.Environment["KRB5CCNAME"] = $"FILE:{Path.Combine(_directory.FullName, "ccache")}";
        return RunAsync(info, cancellationToken);
    }

    private ProcessStartInfo StartInfo(string executable, string[] arguments)
    {
        var info = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in arguments)
            info.ArgumentList.Add(argument);
        info.Environment["KRB5_CONFIG"] = Configuration;
        info.Environment["KRB5_KDC_PROFILE"] = KdcProfile;
        return info;
    }

    private Task RunAsync(string executable, string[] arguments, CancellationToken cancellationToken)
        => RunAsync(StartInfo(executable, arguments), cancellationToken);

    private static async Task RunAsync(ProcessStartInfo info, CancellationToken cancellationToken)
    {
        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var stderr = process.StandardError.ReadToEndAsync(CancellationToken.None);
        try { await process.WaitForExitAsync(cancellationToken); }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
        }
        var output = await stdout;
        var error = await stderr;
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"{info.FileName} failed ({process.ExitCode}): {output} {error}");
    }

    public async ValueTask DisposeAsync()
    {
        if (_kdc is not null)
        {
            if (!_kdc.HasExited)
                _kdc.Kill(entireProcessTree: true);
            await _kdc.WaitForExitAsync();
            Console.WriteLine(await _kdcOutput!);
            Console.WriteLine(await _kdcError!);
            _kdc.Dispose();
        }
        _directory.Delete(recursive: true);
    }
}
