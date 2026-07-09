using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Dekaf.Producer;
using Dekaf.StressTests.Metrics;

namespace Dekaf.StressTests.Diagnostics;

internal enum StallAction
{
    None,
    Capture,
    CaptureAndExit
}

internal sealed class StallDetector
{
    private readonly TimeSpan _captureAfter;
    private readonly TimeSpan _exitAfter;
    private long _lastMessageCount;
    private TimeSpan _lastProgressAt;
    private bool _captured;
    private bool _exited;

    internal StallDetector(TimeSpan captureAfter, TimeSpan exitAfter)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(captureAfter, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(exitAfter, captureAfter);

        _captureAfter = captureAfter;
        _exitAfter = exitAfter;
    }

    internal void Reset(long messageCount, TimeSpan now)
    {
        _lastMessageCount = messageCount;
        _lastProgressAt = now;
        _captured = false;
        _exited = false;
    }

    internal StallAction Observe(long messageCount, TimeSpan now)
    {
        if (messageCount != _lastMessageCount)
        {
            Reset(messageCount, now);
            return StallAction.None;
        }

        var stalledFor = now - _lastProgressAt;
        if (stalledFor >= _exitAfter && !_exited)
        {
            _exited = true;
            return StallAction.CaptureAndExit;
        }

        if (stalledFor >= _captureAfter && !_captured)
        {
            _captured = true;
            return StallAction.Capture;
        }

        return StallAction.None;
    }

    internal TimeSpan GetStallDuration(TimeSpan now) => now - _lastProgressAt;
}

/// <summary>
/// Watches active stress-scenario progress from a dedicated thread, independent of
/// thread-pool health. A prolonged stall captures managed stacks and live producer
/// diagnostics into a dedicated watchdog subdirectory. A five-minute stall captures
/// a final snapshot, then terminates the process before the CI timeout loses evidence.
/// </summary>
internal sealed class ProgressWatchdog : IDisposable
{
    internal const string ArtifactsDirectoryName = "watchdog";

    internal static readonly TimeSpan DefaultCaptureAfter = TimeSpan.FromSeconds(30);
    internal static readonly TimeSpan DefaultExitAfter = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan StackCaptureTimeout = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan DefaultProducerDiagnosticsTimeout = TimeSpan.FromSeconds(5);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _diagnosticsDirectory;
    private readonly TimeSpan _captureAfter;
    private readonly TimeSpan _exitAfter;
    private readonly TimeSpan _pollInterval;
    private readonly Action<int> _exitProcess;
    private readonly Func<string> _captureManagedStackReport;
    private readonly TimeSpan _producerDiagnosticsTimeout;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly AutoResetEvent _wakeUp = new(initialState: false);
    private readonly object _sync = new();
    private readonly Thread _thread;
    private ActiveRun? _activeRun;
    private long _nextRegistrationId;
    private int _captureSequence;
    private bool _disposed;

    internal ProgressWatchdog(
        string outputDirectory,
        TimeSpan? captureAfter = null,
        TimeSpan? exitAfter = null,
        TimeSpan? pollInterval = null,
        Action<int>? exitProcess = null,
        Func<string>? captureManagedStackReport = null,
        TimeSpan? producerDiagnosticsTimeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        _diagnosticsDirectory = Path.Combine(Path.GetFullPath(outputDirectory), ArtifactsDirectoryName);
        _captureAfter = captureAfter ?? DefaultCaptureAfter;
        _exitAfter = exitAfter ?? DefaultExitAfter;
        _pollInterval = pollInterval ?? DefaultPollInterval;
        _exitProcess = exitProcess ?? Environment.Exit;
        _captureManagedStackReport = captureManagedStackReport ?? CaptureManagedStackReport;
        _producerDiagnosticsTimeout = producerDiagnosticsTimeout ?? DefaultProducerDiagnosticsTimeout;

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_captureAfter, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_exitAfter, _captureAfter);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_pollInterval, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_producerDiagnosticsTimeout, TimeSpan.Zero);

        Directory.CreateDirectory(_diagnosticsDirectory);
        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "Dekaf stress progress watchdog"
        };
        _thread.Start();
    }

    internal IDisposable Track(
        ThroughputTracker throughput,
        string client,
        string scenario,
        Func<ProducerDeliveryDiagnosticsSnapshot?>? captureProducerDiagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(throughput);
        ArgumentException.ThrowIfNullOrWhiteSpace(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenario);

        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_activeRun is not null)
                throw new InvalidOperationException("Progress watchdog already tracks an active scenario.");

            var detector = new StallDetector(_captureAfter, _exitAfter);
            detector.Reset(throughput.MessageCount, _clock.Elapsed);
            var registrationId = ++_nextRegistrationId;
            _activeRun = new ActiveRun(
                registrationId,
                throughput,
                client,
                scenario,
                captureProducerDiagnostics,
                detector);
            _wakeUp.Set();
            return new Registration(this, registrationId);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;

            _disposed = true;
            _activeRun = null;
            _wakeUp.Set();
        }

        if (_thread.Join(StackCaptureTimeout + _producerDiagnosticsTimeout + TimeSpan.FromSeconds(5)))
            _wakeUp.Dispose();
    }

    private void Run()
    {
        while (true)
        {
            _wakeUp.WaitOne(_pollInterval);

            ActiveRun? activeRun;
            lock (_sync)
            {
                if (_disposed)
                    return;

                activeRun = _activeRun;
            }

            if (activeRun is null)
                continue;

            var now = _clock.Elapsed;
            var action = activeRun.Detector.Observe(activeRun.Throughput.MessageCount, now);
            if (action == StallAction.None || !IsCurrent(activeRun.RegistrationId))
                continue;

            try
            {
                CaptureDiagnostics(activeRun, action, activeRun.Detector.GetStallDuration(now));
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"PROGRESS WATCHDOG: diagnostic capture failed: {exception}");
            }

            if (action == StallAction.CaptureAndExit)
                _exitProcess(1);
        }
    }

    private bool IsCurrent(long registrationId)
    {
        lock (_sync)
            return _activeRun?.RegistrationId == registrationId;
    }

    private void StopTracking(long registrationId)
    {
        lock (_sync)
        {
            if (_activeRun?.RegistrationId != registrationId)
                return;

            _activeRun = null;
            _wakeUp.Set();
        }
    }

    private void CaptureDiagnostics(ActiveRun activeRun, StallAction action, TimeSpan stalledFor)
    {
        var capturedAt = DateTimeOffset.UtcNow;
        var sequence = Interlocked.Increment(ref _captureSequence);
        var kind = action == StallAction.CaptureAndExit ? "fatal" : "stall";
        var prefix = Path.Combine(
            _diagnosticsDirectory,
            $"watchdog-{Sanitize(activeRun.Client)}-{Sanitize(activeRun.Scenario)}-{capturedAt:yyyyMMdd-HHmmss}-{sequence:D2}-{kind}");

        Console.Error.WriteLine(
            $"PROGRESS WATCHDOG: {activeRun.Client} {activeRun.Scenario} made no message progress " +
            $"for {stalledFor.TotalSeconds:F0}s; capturing {kind} diagnostics in {_diagnosticsDirectory}");

        // Capture stacks first because the producer snapshot can contend on the lock
        // responsible for the stall. Its bounded capture runs afterwards so the fatal
        // exit path remains reachable even when that lock never becomes available.
        CaptureManagedStacks(
            $"{prefix}-stacks.txt",
            activeRun,
            capturedAt,
            stalledFor,
            _captureManagedStackReport);
        CaptureProducerDiagnostics($"{prefix}-producer.json", activeRun, capturedAt, stalledFor);

        if (action == StallAction.CaptureAndExit)
        {
            Console.Error.WriteLine(
                $"PROGRESS WATCHDOG: stall exceeded {_exitAfter.TotalMinutes:F0} minutes; exiting with code 1.");
        }
    }

    private static void CaptureManagedStacks(
        string path,
        ActiveRun activeRun,
        DateTimeOffset capturedAt,
        TimeSpan stalledFor,
        Func<string> captureManagedStackReport)
    {
        var header = new StringBuilder()
            .AppendLine("Dekaf stress progress watchdog managed thread stacks")
            .AppendLine($"CapturedAtUtc: {capturedAt:O}")
            .AppendLine($"ProcessId: {Environment.ProcessId}")
            .AppendLine($"Client: {activeRun.Client}")
            .AppendLine($"Scenario: {activeRun.Scenario}")
            .AppendLine($"MessageCount: {activeRun.Throughput.MessageCount}")
            .AppendLine($"StalledFor: {stalledFor}")
            .AppendLine()
            .ToString();

        try
        {
            WriteArtifact(path, header + captureManagedStackReport());
        }
        catch (Exception exception)
        {
            WriteArtifact(path, $"{header}dotnet-stack capture failed:{Environment.NewLine}{exception}");
            Console.Error.WriteLine($"PROGRESS WATCHDOG: managed stack capture failed: {exception.Message}");
        }
    }

    private static string CaptureManagedStackReport()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet-stack",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("report");
        startInfo.ArgumentList.Add("--process-id");
        startInfo.ArgumentList.Add(Environment.ProcessId.ToString());

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
            throw new InvalidOperationException("dotnet-stack failed to start.");

        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        var exited = process.WaitForExit((int)StackCaptureTimeout.TotalMilliseconds);
        if (!exited)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
        }

        Task.WaitAll(standardOutput, standardError);
        var body = new StringBuilder(standardOutput.Result);
        if (!string.IsNullOrWhiteSpace(standardError.Result))
            body.AppendLine().AppendLine("dotnet-stack stderr:").Append(standardError.Result);
        if (!exited)
            body.AppendLine().AppendLine($"dotnet-stack timed out after {StackCaptureTimeout}.");

        return body.ToString();
    }

    private void CaptureProducerDiagnostics(
        string path,
        ActiveRun activeRun,
        DateTimeOffset capturedAt,
        TimeSpan stalledFor)
    {
        if (activeRun.CaptureProducerDiagnostics is null)
            return;

        ProducerDeliveryDiagnosticsSnapshot? snapshot = null;
        Exception? captureException = null;
        var captureThread = new Thread(() =>
        {
            try
            {
                snapshot = activeRun.CaptureProducerDiagnostics();
            }
            catch (Exception exception)
            {
                captureException = exception;
            }
        })
        {
            IsBackground = true,
            Name = "Dekaf producer diagnostics capture"
        };
        captureThread.Start();

        if (!captureThread.Join(_producerDiagnosticsTimeout))
        {
            var error = $"Producer diagnostics capture timed out after {_producerDiagnosticsTimeout}.";
            WriteProducerDiagnosticsError(path, activeRun, capturedAt, error);
            Console.Error.WriteLine($"PROGRESS WATCHDOG: {error}");
            return;
        }

        if (captureException is not null)
        {
            WriteProducerDiagnosticsError(path, activeRun, capturedAt, captureException.ToString());
            Console.Error.WriteLine(
                $"PROGRESS WATCHDOG: producer diagnostics capture failed: {captureException.Message}");
            return;
        }

        var artifact = new
        {
            CapturedAtUtc = capturedAt,
            activeRun.Client,
            activeRun.Scenario,
            MessageCount = activeRun.Throughput.MessageCount,
            StalledFor = stalledFor,
            ProducerDeliveryDiagnostics = snapshot
        };
        WriteArtifact(path, JsonSerializer.Serialize(artifact, JsonOptions));
    }

    private static void WriteProducerDiagnosticsError(
        string path,
        ActiveRun activeRun,
        DateTimeOffset capturedAt,
        string error) =>
        WriteArtifact(path, JsonSerializer.Serialize(new
        {
            CapturedAtUtc = capturedAt,
            activeRun.Client,
            activeRun.Scenario,
            Error = error
        }, JsonOptions));

    private static void WriteArtifact(string path, string contents)
    {
        try
        {
            File.WriteAllText(path, contents);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"PROGRESS WATCHDOG: failed to write {path}: {exception.Message}");
        }
    }

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
            builder.Append(invalid.Contains(character) || char.IsWhiteSpace(character) ? '-' : char.ToLowerInvariant(character));
        return builder.ToString();
    }

    private sealed record ActiveRun(
        long RegistrationId,
        ThroughputTracker Throughput,
        string Client,
        string Scenario,
        Func<ProducerDeliveryDiagnosticsSnapshot?>? CaptureProducerDiagnostics,
        StallDetector Detector);

    private sealed class Registration(ProgressWatchdog owner, long registrationId) : IDisposable
    {
        private ProgressWatchdog? _owner = owner;

        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.StopTracking(registrationId);
    }
}
