using System.Diagnostics;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Integration;

public enum TransactionalEosCrashPoint
{
    AfterInputConsumed,
    AfterOutputProduced,
    AfterOffsetsSent,
    CommitAcknowledgementRace,
    AfterCommitAcknowledged
}

internal sealed class TransactionalCrashClientProcess : IAsyncDisposable
{
    internal const string EnabledVariable = "DEKAF_TRANSACTION_CRASH_CLIENT";
    internal const string ExactlyOnceMode = "eos";
    internal const string BootstrapServersVariable = "DEKAF_TRANSACTION_CRASH_BOOTSTRAP_SERVERS";
    internal const string TopicVariable = "DEKAF_TRANSACTION_CRASH_TOPIC";
    internal const string TransactionIdVariable = "DEKAF_TRANSACTION_CRASH_ID";
    internal const string KeyVariable = "DEKAF_TRANSACTION_CRASH_KEY";
    internal const string ValueVariable = "DEKAF_TRANSACTION_CRASH_VALUE";
    internal const string ReadyPathVariable = "DEKAF_TRANSACTION_CRASH_READY_PATH";
    internal const string InputTopicVariable = "DEKAF_TRANSACTION_CRASH_INPUT_TOPIC";
    internal const string OutputTopicVariable = "DEKAF_TRANSACTION_CRASH_OUTPUT_TOPIC";
    internal const string ConsumerGroupVariable = "DEKAF_TRANSACTION_CRASH_CONSUMER_GROUP";
    internal const string ExpectedMessageCountVariable = "DEKAF_TRANSACTION_CRASH_EXPECTED_COUNT";
    internal const string CrashPointVariable = "DEKAF_TRANSACTION_CRASH_POINT";
    internal const string BrokerAcknowledgedSignal = "broker-acknowledged";
    private const int MaximumDiagnosticCharacters = 16_384;

    private readonly Process _process;
    private readonly string _readyPath;
    private readonly BoundedProcessDiagnostics _diagnostics;

    internal Func<Process, Task>? BeforeForceTerminateForTestAsync { get; set; }

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
        var readyPath = CreateReadyPath();
        var startInfo = CreateStartInfo(bootstrapServers, topic, transactionId, key, value, readyPath);
        return StartProcess(startInfo, readyPath);
    }

    public static TransactionalCrashClientProcess StartExactlyOnce(
        string bootstrapServers,
        string inputTopic,
        string outputTopic,
        string consumerGroupId,
        string transactionId,
        int expectedMessageCount,
        TransactionalEosCrashPoint? crashPoint)
    {
        var readyPath = CreateReadyPath();
        var startInfo = CreateBaseStartInfo();
        startInfo.Environment[EnabledVariable] = ExactlyOnceMode;
        startInfo.Environment[BootstrapServersVariable] = bootstrapServers;
        startInfo.Environment[InputTopicVariable] = inputTopic;
        startInfo.Environment[OutputTopicVariable] = outputTopic;
        startInfo.Environment[ConsumerGroupVariable] = consumerGroupId;
        startInfo.Environment[TransactionIdVariable] = transactionId;
        startInfo.Environment[ExpectedMessageCountVariable] = expectedMessageCount.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        startInfo.Environment[CrashPointVariable] = crashPoint?.ToString() ?? string.Empty;
        startInfo.Environment[ReadyPathVariable] = readyPath;
        return StartProcess(startInfo, readyPath);
    }

    internal static string SignalFor(TransactionalEosCrashPoint crashPoint) =>
        $"eos-crash-point:{crashPoint}";

    private static string CreateReadyPath() =>
        Path.Combine(
            Path.GetTempPath(),
            $"dekaf-transaction-crash-{Guid.NewGuid():N}.ready");

    private static TransactionalCrashClientProcess StartProcess(
        ProcessStartInfo startInfo,
        string readyPath)
    {
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

    public Task WaitUntilReadyAsync(TimeSpan timeout) =>
        WaitUntilReadyAsync(BrokerAcknowledgedSignal, timeout);

    public async Task WaitUntilReadyAsync(string expectedSignal, TimeSpan timeout)
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
            if (!string.Equals(signal, expectedSignal, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Transactional crash client wrote readiness signal '{signal}', expected '{expectedSignal}'.");
            }

            ThrowIfExitedBeforeReady();
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Transactional crash client did not write signal '{expectedSignal}' within {timeout}." +
                Environment.NewLine + _diagnostics);
        }
    }

    public async Task WaitForSuccessfulExitAsync(TimeSpan timeout)
    {
        using var timeoutSource = new CancellationTokenSource(timeout);
        try
        {
            await _process.WaitForExitAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Transactional crash client did not exit within {timeout}." +
                Environment.NewLine + _diagnostics);
        }

        if (_process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Transactional crash client exited with code {_process.ExitCode}." +
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
        if (BeforeForceTerminateForTestAsync is not null)
        {
            await BeforeForceTerminateForTestAsync(_process);
        }

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
        var startInfo = CreateBaseStartInfo();
        startInfo.Environment[EnabledVariable] = "1";
        startInfo.Environment[BootstrapServersVariable] = bootstrapServers;
        startInfo.Environment[TopicVariable] = topic;
        startInfo.Environment[TransactionIdVariable] = transactionId;
        startInfo.Environment[KeyVariable] = key;
        startInfo.Environment[ValueVariable] = value;
        startInfo.Environment[ReadyPathVariable] = readyPath;
        return startInfo;
    }

    private static ProcessStartInfo CreateBaseStartInfo()
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

        await WriteSignalAsync(
            readyPath,
            TransactionalCrashClientProcess.BrokerAcknowledgedSignal);

        await Task.Delay(Timeout.InfiniteTimeSpan);
    }

    [Test]
    public async Task ProcessExactlyOnceUntilComplete()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable(TransactionalCrashClientProcess.EnabledVariable),
                TransactionalCrashClientProcess.ExactlyOnceMode,
                StringComparison.Ordinal))
        {
            return;
        }

        var bootstrapServers = GetRequiredEnvironmentVariable(
            TransactionalCrashClientProcess.BootstrapServersVariable);
        var inputTopic = GetRequiredEnvironmentVariable(
            TransactionalCrashClientProcess.InputTopicVariable);
        var outputTopic = GetRequiredEnvironmentVariable(
            TransactionalCrashClientProcess.OutputTopicVariable);
        var consumerGroupId = GetRequiredEnvironmentVariable(
            TransactionalCrashClientProcess.ConsumerGroupVariable);
        var transactionId = GetRequiredEnvironmentVariable(
            TransactionalCrashClientProcess.TransactionIdVariable);
        var readyPath = GetRequiredEnvironmentVariable(
            TransactionalCrashClientProcess.ReadyPathVariable);
        var expectedMessageCount = int.Parse(
            GetRequiredEnvironmentVariable(TransactionalCrashClientProcess.ExpectedMessageCountVariable),
            System.Globalization.CultureInfo.InvariantCulture);
        var crashPointText = Environment.GetEnvironmentVariable(
            TransactionalCrashClientProcess.CrashPointVariable);
        TransactionalEosCrashPoint? crashPoint = string.IsNullOrEmpty(crashPointText)
            ? null
            : Enum.Parse<TransactionalEosCrashPoint>(crashPointText, ignoreCase: false);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(bootstrapServers)
            .WithTransactionalId(transactionId)
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        await producer.InitTransactionsAsync();

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(bootstrapServers)
            .WithGroupId(consumerGroupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        var inputPartition = new TopicPartition(inputTopic, 0);
        await using var admin = Kafka.CreateAdminClient()
            .WithBootstrapServers(bootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .Build();
        var committedOffsets = await admin.ListConsumerGroupOffsetsAsync(consumerGroupId);
        var committedOffset = committedOffsets.GetValueOrDefault(inputPartition, 0);
        if (committedOffset < 0 || committedOffset > expectedMessageCount)
        {
            throw new InvalidOperationException(
                $"Committed input offset {committedOffset} is outside expected range 0..{expectedMessageCount}.");
        }

        if (committedOffset == expectedMessageCount)
        {
            return;
        }

        consumer.IncrementalAssign(
        [
            new TopicPartitionOffset(inputTopic, 0, committedOffset)
        ]);
        using var timeoutSource = new CancellationTokenSource(TimeSpan.FromSeconds(75));

        while (committedOffset < expectedMessageCount)
        {
            var message = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(45), timeoutSource.Token)
                ?? throw new TimeoutException(
                    $"EOS recovery stopped at input offset {committedOffset}; expected {expectedMessageCount}.");

            if (message.Partition != 0)
            {
                throw new InvalidOperationException(
                    $"EOS crash client received unexpected input partition {message.Partition}.");
            }

            if (crashPoint == TransactionalEosCrashPoint.AfterInputConsumed)
            {
                await SignalAndWaitAsync(readyPath, crashPoint.Value);
            }

            await using var transaction = producer.BeginTransaction();
            await transaction.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = outputTopic,
                Partition = 0,
                Key = message.Key,
                Value = ExactlyOnceCrashAtomicityTests.Transform(message.Value)
            }, timeoutSource.Token);

            if (crashPoint == TransactionalEosCrashPoint.AfterOutputProduced)
            {
                await SignalAndWaitAsync(readyPath, crashPoint.Value);
            }

            var offsets = new[]
            {
                new TopicPartitionOffset(message.Topic, message.Partition, message.Offset + 1)
            };
            await transaction.SendOffsetsToTransactionAsync(
                offsets,
                consumerGroupId,
                timeoutSource.Token);

            if (crashPoint == TransactionalEosCrashPoint.AfterOffsetsSent)
            {
                await SignalAndWaitAsync(readyPath, crashPoint.Value);
            }

            if (crashPoint == TransactionalEosCrashPoint.CommitAcknowledgementRace)
            {
                await ((Transaction<string, string>)transaction).CommitAfterRequestWrittenAsync(
                    async () =>
                    {
                        await WriteSignalAsync(
                            readyPath,
                            TransactionalCrashClientProcess.SignalFor(crashPoint.Value));
                        await Task.Delay(Timeout.InfiniteTimeSpan);
                    },
                    timeoutSource.Token);
            }
            else
            {
                await transaction.CommitAsync(timeoutSource.Token);
            }

            if (crashPoint == TransactionalEosCrashPoint.AfterCommitAcknowledged)
            {
                await SignalAndWaitAsync(readyPath, crashPoint.Value);
            }

            committedOffset = message.Offset + 1;
        }
    }

    private static async Task SignalAndWaitAsync(
        string readyPath,
        TransactionalEosCrashPoint crashPoint)
    {
        await WriteSignalAsync(
            readyPath,
            TransactionalCrashClientProcess.SignalFor(crashPoint));
        await Task.Delay(Timeout.InfiniteTimeSpan);
    }

    private static async Task WriteSignalAsync(
        string readyPath,
        string signal)
    {
        var temporaryReadyPath = readyPath + ".tmp";
        await File.WriteAllTextAsync(temporaryReadyPath, signal);
        File.Move(temporaryReadyPath, readyPath);
    }

    private static string GetRequiredEnvironmentVariable(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"Required environment variable '{name}' is not set.");
}
