using Dekaf.Networking;
using Dekaf.Protocol;
using Microsoft.Extensions.Logging;
using TUnit.Logging.Microsoft;

namespace Dekaf.Tests.Integration.NetworkFault;

/// <summary>
/// Counts the connections a client opens, by the first ApiVersions request of each handshake.
/// A fault test waits for a handshake that started after the fault was injected: proof that the
/// fault actually cut the client's connections, so a test cannot pass because the fault never bit.
/// </summary>
internal sealed class ConnectionHandshakeObserver : IDisposable
{
    // Every handshake opens with ApiVersions at this version; the identity follow-up uses the
    // highest version, so counting this one counts connections rather than requests.
    private const short HandshakeOpeningVersion = 3;

    private readonly object _sync = new();
    private readonly List<Waiter> _waiters = [];
    private readonly CapturingLoggerProvider _provider;
    private int _handshakes;

    public ConnectionHandshakeObserver()
    {
        _provider = new CapturingLoggerProvider(Observe);
    }

    public int Handshakes
    {
        get
        {
            lock (_sync)
            {
                return _handshakes;
            }
        }
    }

    public ILoggerFactory CreateLoggerFactory() =>
        LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddTUnit(TestContext.Current!);
            builder.AddProvider(_provider);
        });

    /// <summary>Completes once more than <paramref name="baseline"/> handshakes have started.</summary>
    public Task WaitForHandshakeAfterAsync(int baseline, CancellationToken cancellationToken)
    {
        Waiter waiter;
        lock (_sync)
        {
            if (_handshakes > baseline)
                return Task.CompletedTask;

            waiter = new Waiter(baseline);
            _waiters.Add(waiter);
        }

        return waiter.Completion.Task.WaitAsync(cancellationToken);
    }

    public void Dispose() => _provider.Dispose();

    private void Observe(CapturedLogEntry entry)
    {
        if (entry.CategoryName != typeof(KafkaConnection).FullName
            || entry.EventId.Id != KafkaConnection.SendingRequestEventId
            || !entry.TryGetProperty<ApiKey>("ApiKey", out var apiKey)
            || apiKey != ApiKey.ApiVersions
            || !entry.TryGetProperty<short>("Version", out var version)
            || version != HandshakeOpeningVersion)
        {
            return;
        }

        List<Waiter>? ready = null;
        lock (_sync)
        {
            _handshakes++;
            for (var i = _waiters.Count - 1; i >= 0; i--)
            {
                if (_handshakes > _waiters[i].Baseline)
                {
                    (ready ??= []).Add(_waiters[i]);
                    _waiters.RemoveAt(i);
                }
            }
        }

        if (ready is null)
            return;

        foreach (var waiter in ready)
            waiter.Completion.TrySetResult();
    }

    private sealed class Waiter(int baseline)
    {
        public int Baseline { get; } = baseline;

        public TaskCompletionSource Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
