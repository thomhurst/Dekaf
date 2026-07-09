using System.Diagnostics;
using System.Text;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

internal sealed class TransactionalCrashClientProcess : IAsyncDisposable
{
    internal const string EnabledVariable = "DEKAF_TRANSACTION_CRASH_CLIENT";
    internal const string BootstrapServersVariable = "DEKAF_TRANSACTION_CRASH_BOOTSTRAP_SERVERS";
    internal const string TopicVariable = "DEKAF_TRANSACTION_CRASH_TOPIC";
    internal const string TransactionIdVariable = "DEKAF_TRANSACTION_CRASH_ID";
    internal const string KeyVariable = "DEKAF_TRANSACTION_CRASH_KEY";
    internal const string ValueVariable = "DEKAF_TRANSACTION_CRASH_VALUE";
    internal const string ReadyPathVariable = "DEKAF_TRANSACTION_CRASH_READY_PATH";
    internal const string BrokerAcknowledgedSignal = "broker-acknowledged";
    private const int MaximumDiagnosticCharacters = 16_384;

    private readonly Process _process;
    private readonly string _readyPath;
    private readonly BoundedProcessDiagnostics _diagnostics;

    private TransactionalCrashClientProcess(
        Process process,
        string readyPath,
        BoundedProcessDiagnostics diagnostics)
    {
        _process = process;
        _readyPath = readyPath;
        _diagnostics = diagnostics;
    }

    public static TransactionalCrashClientProcess Start(
        string bootstrapServers,
        string topic,
        string transactionId,
        string key,
        string value)
    {
        var readyPath = Path.Combine(
            Path.GetTempPath(),
            $"dekaf-transaction-crash-{Guid.NewGuid():N}.ready");
        var startInfo = CreateStartInfo(bootstrapServers, topic, transactionId, key, value, readyPath);
        var process = new Process { StartInfo = startInfo };
        var diagnostics = new BoundedProcessDiagnostics(MaximumDiagnosticCharacters);

        process.OutputDataReceived += (_, eventArgs) => diagnostics.Append("stdout", eventArgs.Data);
        process.ErrorDataReceived += (_, eventArgs) => diagnostics.Append("stderr", eventArgs.Data);

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("Transactional crash client did not start.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            return new TransactionalCrashClientProcess(process, readyPath, diagnostics);
        }
        catch
        {
            process.Dispose();
            DeleteSignalFiles(readyPath);
            throw;
        }
    }

    public async Task WaitUntilReadyAsync(TimeSpan timeout)
    {
        using var timeoutSource = new CancellationTokenSource(timeout);

        try
        {
            while (!File.Exists(_readyPath))
            {
                ThrowIfExitedBeforeReady();
                await Task.Delay(50, timeoutSource.Token);
            }

            var signal = await File.ReadAllTextAsync(_readyPath, timeoutSource.Token);
            if (!string.Equals(signal, BrokerAcknowledgedSignal, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Transactional crash client wrote an invalid readiness signal: '{signal}'.");
            }

            ThrowIfExitedBeforeReady();
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Transactional crash client did not acknowledge its open transaction within {timeout}." +
                Environment.NewLine + _diagnostics);
        }
    }

    public async Task TerminateAsync()
    {
        if (_process.HasExited)
        {
            throw new InvalidOperationException(
                $"Transactional crash client exited before it could be force-terminated with code {_process.ExitCode}." +
                Environment.NewLine + _diagnostics);
        }

        await ForceTerminateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!_process.HasExited)
            {
                await ForceTerminateAsync();
            }
        }
        finally
        {
            _process.Dispose();
            DeleteSignalFiles(_readyPath);
        }
    }

    private async Task ForceTerminateAsync()
    {
        _process.Kill(entireProcessTree: true);

        using var timeoutSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await _process.WaitForExitAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            throw new TimeoutException(
                "Transactional crash client did not exit after force termination." +
                Environment.NewLine + _diagnostics);
        }
    }

    private static ProcessStartInfo CreateStartInfo(
        string bootstrapServers,
        string topic,
        string transactionId,
        string key,
        string value,
        string readyPath)
    {
        var processPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Cannot locate the integration test executable.");
        var startInfo = new ProcessStartInfo(processPath)
        {
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        if (string.Equals(Path.GetFileNameWithoutExtension(processPath), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            startInfo.ArgumentList.Add(Environment.GetCommandLineArgs()[0]);
        }

        startInfo.ArgumentList.Add("--treenode-filter");
        startInfo.ArgumentList.Add("/*/*/TransactionalCrashClient/*");
        startInfo.ArgumentList.Add("--maximum-parallel-tests");
        startInfo.ArgumentList.Add("1");

        startInfo.Environment[EnabledVariable] = "1";
        startInfo.Environment[BootstrapServersVariable] = bootstrapServers;
        startInfo.Environment[TopicVariable] = topic;
        startInfo.Environment[TransactionIdVariable] = transactionId;
        startInfo.Environment[KeyVariable] = key;
        startInfo.Environment[ValueVariable] = value;
        startInfo.Environment[ReadyPathVariable] = readyPath;
        return startInfo;
    }

    private void ThrowIfExitedBeforeReady()
    {
        if (_process.HasExited)
        {
            throw new InvalidOperationException(
                $"Transactional crash client exited before signaling readiness with code {_process.ExitCode}." +
                Environment.NewLine + _diagnostics);
        }
    }

    private static void DeleteSignalFiles(string readyPath)
    {
        TryDelete(readyPath);
        TryDelete(readyPath + ".tmp");

        static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private sealed class BoundedProcessDiagnostics(int maximumCharacters)
    {
        private readonly StringBuilder _buffer = new();

        public void Append(string stream, string? message)
        {
            if (message is null)
            {
                return;
            }

            lock (_buffer)
            {
                _buffer.Append('[').Append(stream).Append("] ").AppendLine(message);
                if (_buffer.Length > maximumCharacters)
                {
                    _buffer.Remove(0, _buffer.Length - maximumCharacters);
                }
            }
        }

        public override string ToString()
        {
            lock (_buffer)
            {
                return _buffer.ToString();
            }
        }
    }
}

/// <summary>
/// Child-process entry point used by <see cref="TransactionCrashRecoveryTests"/>.
/// It is inert during ordinary discovery and activated only through the harness environment.
/// </summary>
public sealed class TransactionalCrashClient
{
    [Test]
    public async Task HoldBrokerAcknowledgedTransactionOpen()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable(TransactionalCrashClientProcess.EnabledVariable),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        var bootstrapServers = GetRequiredEnvironmentVariable(TransactionalCrashClientProcess.BootstrapServersVariable);
        var topic = GetRequiredEnvironmentVariable(TransactionalCrashClientProcess.TopicVariable);
        var transactionId = GetRequiredEnvironmentVariable(TransactionalCrashClientProcess.TransactionIdVariable);
        var key = GetRequiredEnvironmentVariable(TransactionalCrashClientProcess.KeyVariable);
        var value = GetRequiredEnvironmentVariable(TransactionalCrashClientProcess.ValueVariable);
        var readyPath = GetRequiredEnvironmentVariable(TransactionalCrashClientProcess.ReadyPathVariable);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(bootstrapServers)
            .WithTransactionalId(transactionId)
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await producer.InitTransactionsAsync();
        await using var transaction = producer.BeginTransaction();
        await transaction.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = key,
            Value = value
        }, CancellationToken.None);

        var temporaryReadyPath = readyPath + ".tmp";
        await File.WriteAllTextAsync(
            temporaryReadyPath,
            TransactionalCrashClientProcess.BrokerAcknowledgedSignal);
        File.Move(temporaryReadyPath, readyPath);

        await Task.Delay(Timeout.InfiniteTimeSpan);
    }

    private static string GetRequiredEnvironmentVariable(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"Required environment variable '{name}' is not set.");
}
