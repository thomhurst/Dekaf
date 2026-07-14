using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Text;
using Dekaf.Consumer;

namespace Dekaf.Tests.Integration;

internal readonly record struct ConsumerGroupCrashObservation(
    int Partition,
    long CommittedOffset,
    string CommittedValue,
    long Offset,
    string Value);

/// <summary>
/// Runs a consumer group member in a child process so termination cannot execute
/// consumer disposal, LeaveGroup, or a terminal ConsumerGroupHeartbeat.
/// </summary>
internal sealed class ConsumerGroupCrashClientProcess : IAsyncDisposable
{
    internal const string EnabledVariable = "DEKAF_CONSUMER_GROUP_CRASH_CLIENT";
    internal const string BootstrapServersVariable = "DEKAF_CONSUMER_GROUP_CRASH_BOOTSTRAP_SERVERS";
    internal const string TopicVariable = "DEKAF_CONSUMER_GROUP_CRASH_TOPIC";
    internal const string GroupIdVariable = "DEKAF_CONSUMER_GROUP_CRASH_GROUP_ID";
    internal const string ClientIdVariable = "DEKAF_CONSUMER_GROUP_CRASH_CLIENT_ID";
    internal const string AssignorVariable = "DEKAF_CONSUMER_GROUP_CRASH_ASSIGNOR";
    internal const string OffsetCommitModeVariable = "DEKAF_CONSUMER_GROUP_CRASH_OFFSET_COMMIT_MODE";
    internal const string ReadyPipeVariable = "DEKAF_CONSUMER_GROUP_CRASH_READY_PIPE";
    private const int MaximumDiagnosticCharacters = 16_384;

    private readonly Process _process;
    private readonly NamedPipeServerStream _readyPipe;
    private readonly BoundedProcessDiagnostics _diagnostics;

    private ConsumerGroupCrashClientProcess(
        Process process,
        NamedPipeServerStream readyPipe,
        BoundedProcessDiagnostics diagnostics)
    {
        _process = process;
        _readyPipe = readyPipe;
        _diagnostics = diagnostics;
    }

    public static ConsumerGroupCrashClientProcess Start(
        string bootstrapServers,
        string topic,
        string groupId,
        string clientId,
        string assignor,
        OffsetCommitMode offsetCommitMode = OffsetCommitMode.Manual)
    {
        var pipeName = $"dekaf-consumer-group-crash-{Guid.NewGuid():N}";
        var readyPipe = new NamedPipeServerStream(
            pipeName,
            PipeDirection.In,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
        var process = new Process
        {
            StartInfo = CreateStartInfo(
                bootstrapServers,
                topic,
                groupId,
                clientId,
                assignor,
                offsetCommitMode,
                pipeName)
        };
        var diagnostics = new BoundedProcessDiagnostics(MaximumDiagnosticCharacters);

        process.OutputDataReceived += (_, eventArgs) => diagnostics.Append("stdout", eventArgs.Data);
        process.ErrorDataReceived += (_, eventArgs) => diagnostics.Append("stderr", eventArgs.Data);

        var started = false;
        try
        {
            started = process.Start();
            if (!started)
                throw new InvalidOperationException("Consumer group crash client did not start.");

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            return new ConsumerGroupCrashClientProcess(process, readyPipe, diagnostics);
        }
        catch
        {
            if (started)
                TryTerminateStartedProcess(process);

            process.Dispose();
            readyPipe.Dispose();
            throw;
        }
    }

    public async Task<ConsumerGroupCrashObservation> WaitUntilReadyAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            timeoutSource.Token,
            cancellationToken);

        try
        {
            var connectionTask = _readyPipe.WaitForConnectionAsync(linkedSource.Token);
            var exitTask = _process.WaitForExitAsync(CancellationToken.None);
            var completedTask = await Task.WhenAny(connectionTask, exitTask)
                .WaitAsync(linkedSource.Token);

            if (ReferenceEquals(completedTask, exitTask))
            {
                linkedSource.Cancel();
                try
                {
                    await connectionTask;
                }
                catch (OperationCanceledException) when (linkedSource.IsCancellationRequested)
                {
                }

                await exitTask;
                throw new InvalidOperationException(
                    $"Consumer group crash client exited before signaling readiness with code {_process.ExitCode}." +
                    Environment.NewLine + _diagnostics);
            }

            await connectionTask;
            using var reader = new StreamReader(
                _readyPipe,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 1024,
                leaveOpen: true);
            var signal = await reader.ReadLineAsync(linkedSource.Token);
            return ParseObservation(signal);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Consumer group crash client did not signal readiness within {timeout}." +
                Environment.NewLine + _diagnostics);
        }
    }

    public Task TerminateAsync(CancellationToken cancellationToken) =>
        ForceTerminateAsync(requireRunning: true, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        try
        {
            await ForceTerminateAsync(requireRunning: false, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _diagnostics.Append("cleanup", exception.ToString());
        }
        finally
        {
            _process.Dispose();
            await _readyPipe.DisposeAsync();
        }
    }

    private async Task ForceTerminateAsync(bool requireRunning, CancellationToken cancellationToken)
    {
        if (_process.HasExited)
        {
            if (requireRunning)
            {
                throw new InvalidOperationException(
                    $"Consumer group crash client exited before force termination with code {_process.ExitCode}." +
                    Environment.NewLine + _diagnostics);
            }

            return;
        }

        _process.Kill(entireProcessTree: true);

        using var timeoutSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            timeoutSource.Token,
            cancellationToken);
        try
        {
            await _process.WaitForExitAsync(linkedSource.Token);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            throw new TimeoutException(
                "Consumer group crash client did not exit after force termination." +
                Environment.NewLine + _diagnostics);
        }
    }

    private static ConsumerGroupCrashObservation ParseObservation(string? signal)
    {
        var fields = signal?.Split('|', count: 5);
        if (fields is not { Length: 5 } ||
            !int.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out var partition) ||
            !long.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var committedOffset) ||
            !long.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out var offset))
        {
            throw new InvalidOperationException(
                $"Consumer group crash client wrote invalid readiness signal '{signal}'.");
        }

        return new ConsumerGroupCrashObservation(
            partition,
            committedOffset,
            fields[2],
            offset,
            fields[4]);
    }

    private static void TryTerminateStartedProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(milliseconds: 10_000);
            }
        }
        catch (Exception)
        {
            // Startup cleanup is best-effort; preserve the original start failure.
        }
    }

    private static ProcessStartInfo CreateStartInfo(
        string bootstrapServers,
        string topic,
        string groupId,
        string clientId,
        string assignor,
        OffsetCommitMode offsetCommitMode,
        string pipeName)
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

        if (string.Equals(
                Path.GetFileNameWithoutExtension(processPath),
                "dotnet",
                StringComparison.OrdinalIgnoreCase))
        {
            startInfo.ArgumentList.Add(Environment.GetCommandLineArgs()[0]);
        }

        startInfo.ArgumentList.Add("--treenode-filter");
        startInfo.ArgumentList.Add("/*/*/ConsumerGroupCrashClient/*");
        startInfo.ArgumentList.Add("--maximum-parallel-tests");
        startInfo.ArgumentList.Add("1");
        startInfo.Environment[EnabledVariable] = "1";
        startInfo.Environment[BootstrapServersVariable] = bootstrapServers;
        startInfo.Environment[TopicVariable] = topic;
        startInfo.Environment[GroupIdVariable] = groupId;
        startInfo.Environment[ClientIdVariable] = clientId;
        startInfo.Environment[AssignorVariable] = assignor;
        startInfo.Environment[OffsetCommitModeVariable] = offsetCommitMode.ToString();
        startInfo.Environment[ReadyPipeVariable] = pipeName;
        return startInfo;
    }

}

/// <summary>
/// Child-process entry point. The parent kills this process while its consumer is
/// live, so no graceful group-leave path can run.
/// </summary>
public sealed class ConsumerGroupCrashClient
{
    [Test]
    public async Task ConsumeOneRecordAndHoldMembership()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable(ConsumerGroupCrashClientProcess.EnabledVariable),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        var bootstrapServers = GetRequiredEnvironmentVariable(
            ConsumerGroupCrashClientProcess.BootstrapServersVariable);
        var topic = GetRequiredEnvironmentVariable(ConsumerGroupCrashClientProcess.TopicVariable);
        var groupId = GetRequiredEnvironmentVariable(ConsumerGroupCrashClientProcess.GroupIdVariable);
        var clientId = GetRequiredEnvironmentVariable(ConsumerGroupCrashClientProcess.ClientIdVariable);
        var assignor = GetRequiredEnvironmentVariable(ConsumerGroupCrashClientProcess.AssignorVariable);
        var offsetCommitMode = Enum.Parse<OffsetCommitMode>(GetRequiredEnvironmentVariable(
            ConsumerGroupCrashClientProcess.OffsetCommitModeVariable));
        var pipeName = GetRequiredEnvironmentVariable(ConsumerGroupCrashClientProcess.ReadyPipeVariable);

        var builder = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(bootstrapServers)
            .WithClientId(clientId)
            .WithGroupId(groupId)
            .WithGroupRemoteAssignor(assignor)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithMaxPollRecords(1)
            .WithQueuedMinMessages(1)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory());

        if (offsetCommitMode == OffsetCommitMode.Manual)
            builder.WithOffsetCommitMode(OffsetCommitMode.Manual);

        await using var consumer = await builder.BuildAsync();

        consumer.Subscribe(topic);
        using var timeoutSource = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        await using var enumerator = consumer
            .ConsumeAsync(timeoutSource.Token)
            .GetAsyncEnumerator(timeoutSource.Token);
        if (!await enumerator.MoveNextAsync())
            throw new InvalidOperationException("Consumer group crash client completed before receiving a record.");

        var committedResult = enumerator.Current;
        var committedPartition = new TopicPartition(topic, committedResult.Partition);
        var otherPartitions = consumer.Assignment
            .Where(partition => partition != committedPartition)
            .ToArray();
        if (otherPartitions.Length > 0)
            consumer.Pause(otherPartitions);

        var committedExclusive = committedResult.Offset + 1;
        if (offsetCommitMode == OffsetCommitMode.Manual)
        {
            await consumer.CommitAsync(
                [new TopicPartitionOffset(topic, committedResult.Partition, committedExclusive)],
                timeoutSource.Token);
        }

        ConsumeResult<string, string> crashResult;
        do
        {
            if (!await enumerator.MoveNextAsync())
            {
                throw new InvalidOperationException(
                    "Consumer group crash client completed before receiving its uncommitted crash record.");
            }

            crashResult = enumerator.Current;
        }
        while (crashResult.Partition != committedResult.Partition);

        if (crashResult.Offset != committedResult.Offset + 1)
        {
            throw new InvalidOperationException(
                $"Consumer group crash client expected offset {committedResult.Offset + 1} after its commit, " +
                $"but received {crashResult.Offset}.");
        }

        await using var readyPipe = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.Out,
            PipeOptions.Asynchronous);
        await readyPipe.ConnectAsync(timeoutSource.Token);
        await using var writer = new StreamWriter(
            readyPipe,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            bufferSize: 1024,
            leaveOpen: true)
        {
            AutoFlush = true
        };
        await writer.WriteLineAsync(
            $"{committedResult.Partition.ToString(CultureInfo.InvariantCulture)}|" +
            $"{committedResult.Offset.ToString(CultureInfo.InvariantCulture)}|{committedResult.Value}|" +
            $"{crashResult.Offset.ToString(CultureInfo.InvariantCulture)}|{crashResult.Value}");

        await Task.Delay(Timeout.InfiniteTimeSpan, CancellationToken.None);
    }

    private static string GetRequiredEnvironmentVariable(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"Required environment variable '{name}' is not set.");
}
