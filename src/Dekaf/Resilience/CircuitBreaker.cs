using System.Diagnostics;

namespace Dekaf.Resilience;

/// <summary>
/// Thread-safe circuit breaker implementation that tracks per-broker health state.
/// </summary>
/// <remarks>
/// <para>
/// This implementation uses lock-free atomic operations for state transitions and failure counting.
/// The circuit breaker transitions through three states:
/// </para>
/// <list type="bullet">
/// <item><description><b>Closed</b>: Normal operation. Consecutive failures are counted.
/// When failures reach the threshold, the circuit opens.</description></item>
/// <item><description><b>Open</b>: All requests are rejected immediately. After the break duration
/// elapses, the circuit transitions to half-open.</description></item>
/// <item><description><b>HalfOpen</b>: A limited number of probe requests are allowed through.
/// If they succeed, the circuit closes. If any fail, the circuit reopens.</description></item>
/// </list>
/// <para>
/// The circuit breaker uses <see cref="Stopwatch.GetTimestamp"/> for high-resolution timing
/// without allocations, and <see cref="Interlocked"/> operations for thread-safe state management.
/// </para>
/// </remarks>
public sealed class CircuitBreaker : ICircuitBreaker
{
    private readonly int _failureThreshold;
    private readonly long _breakDurationTicks;
    private readonly int _halfOpenMaxAttempts;

    // State: 0 = Closed, 1 = Open, 2 = HalfOpen
    private volatile int _state;
    private int _consecutiveFailures;
    private int _halfOpenSuccessCount;
    private long _openedTimestamp;

    /// <summary>
    /// Creates a new circuit breaker with the specified options.
    /// </summary>
    /// <param name="options">The circuit breaker configuration options.</param>
    /// <exception cref="ArgumentNullException">Thrown when options is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any option is out of range.</exception>
    public CircuitBreaker(CircuitBreakerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        _failureThreshold = options.FailureThreshold;
        _breakDurationTicks = (long)(options.BreakDuration.TotalSeconds * Stopwatch.Frequency);
        _halfOpenMaxAttempts = options.HalfOpenMaxAttempts;
    }

    /// <summary>
    /// Creates a new circuit breaker with default options.
    /// </summary>
    public CircuitBreaker() : this(new CircuitBreakerOptions())
    {
    }

    /// <inheritdoc />
    public CircuitBreakerState State
    {
        get
        {
            var state = (CircuitBreakerState)_state;

            // Check if open circuit should transition to half-open
            if (state == CircuitBreakerState.Open && HasBreakDurationElapsed())
            {
                TryTransitionToHalfOpen();
                return (CircuitBreakerState)_state;
            }

            return state;
        }
    }

    /// <inheritdoc />
    public bool IsAllowingRequests
    {
        get
        {
            var state = State; // This triggers the open -> half-open check
            return state is CircuitBreakerState.Closed or CircuitBreakerState.HalfOpen;
        }
    }

    /// <inheritdoc />
    public int ConsecutiveFailures => Volatile.Read(ref _consecutiveFailures);

    /// <inheritdoc />
    public event Action<CircuitBreakerState>? StateChanged;

    /// <inheritdoc />
    public void RecordSuccess()
    {
        var currentState = (CircuitBreakerState)_state;

        switch (currentState)
        {
            case CircuitBreakerState.Closed:
                // Reset consecutive failures on success
                Interlocked.Exchange(ref _consecutiveFailures, 0);
                break;

            case CircuitBreakerState.HalfOpen:
                var successCount = Interlocked.Increment(ref _halfOpenSuccessCount);
                if (successCount >= _halfOpenMaxAttempts)
                {
                    TransitionTo(CircuitBreakerState.Closed);
                }
                break;

            case CircuitBreakerState.Open:
                // Success in open state should not happen (requests are rejected),
                // but handle gracefully by ignoring
                break;
        }
    }

    /// <inheritdoc />
    public void RecordFailure()
    {
        var currentState = (CircuitBreakerState)_state;

        switch (currentState)
        {
            case CircuitBreakerState.Closed:
                var failures = Interlocked.Increment(ref _consecutiveFailures);
                if (failures >= _failureThreshold)
                {
                    TransitionTo(CircuitBreakerState.Open);
                }
                break;

            case CircuitBreakerState.HalfOpen:
                // Any failure in half-open state reopens the circuit
                TransitionTo(CircuitBreakerState.Open);
                break;

            case CircuitBreakerState.Open:
                // Already open, no-op
                break;
        }
    }

    /// <inheritdoc />
    public void Reset()
    {
        TransitionTo(CircuitBreakerState.Closed);
    }

    private void TransitionTo(CircuitBreakerState newState)
    {
        var oldState = (CircuitBreakerState)Interlocked.Exchange(ref _state, (int)newState);

        if (oldState == newState)
            return;

        switch (newState)
        {
            case CircuitBreakerState.Open:
                _openedTimestamp = Stopwatch.GetTimestamp();
                break;

            case CircuitBreakerState.Closed:
                Interlocked.Exchange(ref _consecutiveFailures, 0);
                Interlocked.Exchange(ref _halfOpenSuccessCount, 0);
                break;

            case CircuitBreakerState.HalfOpen:
                Interlocked.Exchange(ref _halfOpenSuccessCount, 0);
                break;
        }

        StateChanged?.Invoke(newState);
    }

    private void TryTransitionToHalfOpen()
    {
        // Atomically transition from Open to HalfOpen
        if (Interlocked.CompareExchange(ref _state, (int)CircuitBreakerState.HalfOpen, (int)CircuitBreakerState.Open)
            == (int)CircuitBreakerState.Open)
        {
            Interlocked.Exchange(ref _halfOpenSuccessCount, 0);
            StateChanged?.Invoke(CircuitBreakerState.HalfOpen);
        }
    }

    private bool HasBreakDurationElapsed()
    {
        var elapsed = Stopwatch.GetTimestamp() - Volatile.Read(ref _openedTimestamp);
        return elapsed >= _breakDurationTicks;
    }
}
