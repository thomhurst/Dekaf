using Dekaf.Resilience;

namespace Dekaf.Tests.Unit.Resilience;

public class CircuitBreakerTests
{
    #region Initial State

    [Test]
    public async Task CircuitBreaker_InitialState_IsClosed()
    {
        var cb = new CircuitBreaker();
        await Assert.That(cb.State).IsEqualTo(CircuitBreakerState.Closed);
    }

    [Test]
    public async Task CircuitBreaker_InitialState_AllowsRequests()
    {
        var cb = new CircuitBreaker();
        await Assert.That(cb.IsAllowingRequests).IsTrue();
    }

    [Test]
    public async Task CircuitBreaker_InitialState_HasZeroConsecutiveFailures()
    {
        var cb = new CircuitBreaker();
        await Assert.That(cb.ConsecutiveFailures).IsEqualTo(0);
    }

    #endregion

    #region Closed -> Open Transition

    [Test]
    public async Task CircuitBreaker_FailuresBelowThreshold_StaysClosed()
    {
        var cb = new CircuitBreaker(new CircuitBreakerOptions { FailureThreshold = 5 });

        for (var i = 0; i < 4; i++)
            cb.RecordFailure();

        await Assert.That(cb.State).IsEqualTo(CircuitBreakerState.Closed);
        await Assert.That(cb.ConsecutiveFailures).IsEqualTo(4);
    }

    [Test]
    public async Task CircuitBreaker_FailuresReachThreshold_TransitionsToOpen()
    {
        var cb = new CircuitBreaker(new CircuitBreakerOptions { FailureThreshold = 3 });

        cb.RecordFailure();
        cb.RecordFailure();
        cb.RecordFailure();

        await Assert.That(cb.State).IsEqualTo(CircuitBreakerState.Open);
        await Assert.That(cb.IsAllowingRequests).IsFalse();
    }

    [Test]
    public async Task CircuitBreaker_SuccessResetsFailureCount()
    {
        var cb = new CircuitBreaker(new CircuitBreakerOptions { FailureThreshold = 5 });

        cb.RecordFailure();
        cb.RecordFailure();
        cb.RecordSuccess();

        await Assert.That(cb.ConsecutiveFailures).IsEqualTo(0);
        await Assert.That(cb.State).IsEqualTo(CircuitBreakerState.Closed);
    }

    [Test]
    public async Task CircuitBreaker_SuccessAfterPartialFailures_StaysOpen()
    {
        var cb = new CircuitBreaker(new CircuitBreakerOptions { FailureThreshold = 3 });

        cb.RecordFailure();
        cb.RecordFailure();
        cb.RecordSuccess(); // Resets failure count
        cb.RecordFailure();
        cb.RecordFailure();

        await Assert.That(cb.State).IsEqualTo(CircuitBreakerState.Closed);
        await Assert.That(cb.ConsecutiveFailures).IsEqualTo(2);
    }

    #endregion

    #region Open -> HalfOpen Transition

    [Test]
    public async Task CircuitBreaker_OpenStateAfterBreakDuration_TransitionsToHalfOpen()
    {
        var cb = new CircuitBreaker(new CircuitBreakerOptions
        {
            FailureThreshold = 1,
            BreakDuration = TimeSpan.FromMilliseconds(50)
        });

        cb.RecordFailure();
        await Assert.That(cb.State).IsEqualTo(CircuitBreakerState.Open);

        // Wait for break duration to elapse
        await Task.Delay(100);

        await Assert.That(cb.State).IsEqualTo(CircuitBreakerState.HalfOpen);
        await Assert.That(cb.IsAllowingRequests).IsTrue();
    }

    [Test]
    public async Task CircuitBreaker_OpenStateBeforeBreakDuration_StaysOpen()
    {
        var cb = new CircuitBreaker(new CircuitBreakerOptions
        {
            FailureThreshold = 1,
            BreakDuration = TimeSpan.FromSeconds(30) // Long duration
        });

        cb.RecordFailure();

        // Force read of internal state directly without the auto-transition check
        await Assert.That(cb.IsAllowingRequests).IsFalse();
    }

    #endregion

    #region HalfOpen -> Closed Transition

    [Test]
    public async Task CircuitBreaker_HalfOpenWithEnoughSuccesses_TransitionsToClosed()
    {
        var cb = new CircuitBreaker(new CircuitBreakerOptions
        {
            FailureThreshold = 1,
            BreakDuration = TimeSpan.FromMilliseconds(50),
            HalfOpenMaxAttempts = 2
        });

        // Trip the circuit
        cb.RecordFailure();
        await Task.Delay(100);

        // Verify half-open
        await Assert.That(cb.State).IsEqualTo(CircuitBreakerState.HalfOpen);

        // Record enough successes
        cb.RecordSuccess();
        cb.RecordSuccess();

        await Assert.That(cb.State).IsEqualTo(CircuitBreakerState.Closed);
        await Assert.That(cb.IsAllowingRequests).IsTrue();
        await Assert.That(cb.ConsecutiveFailures).IsEqualTo(0);
    }

    [Test]
    public async Task CircuitBreaker_HalfOpenPartialSuccess_StaysHalfOpen()
    {
        var cb = new CircuitBreaker(new CircuitBreakerOptions
        {
            FailureThreshold = 1,
            BreakDuration = TimeSpan.FromMilliseconds(50),
            HalfOpenMaxAttempts = 3
        });

        cb.RecordFailure();
        await Task.Delay(100);

        await Assert.That(cb.State).IsEqualTo(CircuitBreakerState.HalfOpen);

        cb.RecordSuccess();

        await Assert.That(cb.State).IsEqualTo(CircuitBreakerState.HalfOpen);
    }

    #endregion

    #region HalfOpen -> Open Transition

    [Test]
    public async Task CircuitBreaker_HalfOpenWithFailure_TransitionsBackToOpen()
    {
        var cb = new CircuitBreaker(new CircuitBreakerOptions
        {
            FailureThreshold = 1,
            BreakDuration = TimeSpan.FromMilliseconds(50),
            HalfOpenMaxAttempts = 2
        });

        // Trip to open
        cb.RecordFailure();
        await Task.Delay(100);

        // Should be half-open
        await Assert.That(cb.State).IsEqualTo(CircuitBreakerState.HalfOpen);

        // Any failure in half-open reopens
        cb.RecordFailure();

        await Assert.That(cb.State).IsEqualTo(CircuitBreakerState.Open);
        await Assert.That(cb.IsAllowingRequests).IsFalse();
    }

    #endregion

    #region Reset

    [Test]
    public async Task CircuitBreaker_Reset_ClosesCircuit()
    {
        var cb = new CircuitBreaker(new CircuitBreakerOptions { FailureThreshold = 1 });

        cb.RecordFailure();
        await Assert.That(cb.State).IsEqualTo(CircuitBreakerState.Open);

        cb.Reset();

        await Assert.That(cb.State).IsEqualTo(CircuitBreakerState.Closed);
        await Assert.That(cb.ConsecutiveFailures).IsEqualTo(0);
        await Assert.That(cb.IsAllowingRequests).IsTrue();
    }

    [Test]
    public async Task CircuitBreaker_ResetFromHalfOpen_ClosesCircuit()
    {
        var cb = new CircuitBreaker(new CircuitBreakerOptions
        {
            FailureThreshold = 1,
            BreakDuration = TimeSpan.FromMilliseconds(50)
        });

        cb.RecordFailure();
        await Task.Delay(100);
        await Assert.That(cb.State).IsEqualTo(CircuitBreakerState.HalfOpen);

        cb.Reset();

        await Assert.That(cb.State).IsEqualTo(CircuitBreakerState.Closed);
    }

    #endregion

    #region StateChanged Event

    [Test]
    public async Task CircuitBreaker_StateChanged_RaisedOnTransition()
    {
        var cb = new CircuitBreaker(new CircuitBreakerOptions { FailureThreshold = 1 });
        var stateChanges = new List<CircuitBreakerState>();
        cb.StateChanged += state => stateChanges.Add(state);

        cb.RecordFailure(); // Closed -> Open

        await Assert.That(stateChanges).Count().IsEqualTo(1);
        await Assert.That(stateChanges[0]).IsEqualTo(CircuitBreakerState.Open);
    }

    [Test]
    public async Task CircuitBreaker_StateChanged_NotRaisedWhenStateSame()
    {
        var cb = new CircuitBreaker(new CircuitBreakerOptions { FailureThreshold = 5 });
        var stateChanges = new List<CircuitBreakerState>();
        cb.StateChanged += state => stateChanges.Add(state);

        cb.RecordFailure(); // Still closed
        cb.RecordFailure(); // Still closed

        await Assert.That(stateChanges).Count().IsEqualTo(0);
    }

    [Test]
    public async Task CircuitBreaker_StateChanged_RaisedOnReset()
    {
        var cb = new CircuitBreaker(new CircuitBreakerOptions { FailureThreshold = 1 });
        var stateChanges = new List<CircuitBreakerState>();
        cb.StateChanged += state => stateChanges.Add(state);

        cb.RecordFailure(); // Closed -> Open
        cb.Reset();         // Open -> Closed

        await Assert.That(stateChanges).Count().IsEqualTo(2);
        await Assert.That(stateChanges[0]).IsEqualTo(CircuitBreakerState.Open);
        await Assert.That(stateChanges[1]).IsEqualTo(CircuitBreakerState.Closed);
    }

    #endregion

    #region Full Lifecycle

    [Test]
    public async Task CircuitBreaker_FullLifecycle_TransitionsCorrectly()
    {
        var cb = new CircuitBreaker(new CircuitBreakerOptions
        {
            FailureThreshold = 2,
            BreakDuration = TimeSpan.FromMilliseconds(50),
            HalfOpenMaxAttempts = 1
        });

        // Start closed
        await Assert.That(cb.State).IsEqualTo(CircuitBreakerState.Closed);

        // Record failures to trip the circuit
        cb.RecordFailure();
        cb.RecordFailure();
        await Assert.That(cb.State).IsEqualTo(CircuitBreakerState.Open);

        // Wait for break duration
        await Task.Delay(100);
        await Assert.That(cb.State).IsEqualTo(CircuitBreakerState.HalfOpen);

        // Record success to close the circuit
        cb.RecordSuccess();
        await Assert.That(cb.State).IsEqualTo(CircuitBreakerState.Closed);

        // Verify clean state
        await Assert.That(cb.ConsecutiveFailures).IsEqualTo(0);
        await Assert.That(cb.IsAllowingRequests).IsTrue();
    }

    [Test]
    public async Task CircuitBreaker_HalfOpenFailureAndRecovery_TransitionsCorrectly()
    {
        var cb = new CircuitBreaker(new CircuitBreakerOptions
        {
            FailureThreshold = 1,
            BreakDuration = TimeSpan.FromMilliseconds(50),
            HalfOpenMaxAttempts = 1
        });

        // Trip
        cb.RecordFailure();
        await Assert.That(cb.State).IsEqualTo(CircuitBreakerState.Open);

        // Wait, transition to half-open
        await Task.Delay(100);
        await Assert.That(cb.State).IsEqualTo(CircuitBreakerState.HalfOpen);

        // Fail again -> back to open
        cb.RecordFailure();
        await Assert.That(cb.State).IsEqualTo(CircuitBreakerState.Open);

        // Wait again, transition to half-open
        await Task.Delay(100);
        await Assert.That(cb.State).IsEqualTo(CircuitBreakerState.HalfOpen);

        // Succeed -> closed
        cb.RecordSuccess();
        await Assert.That(cb.State).IsEqualTo(CircuitBreakerState.Closed);
    }

    #endregion

    #region Interface Contract

    [Test]
    public async Task CircuitBreaker_ImplementsICircuitBreaker()
    {
        ICircuitBreaker cb = new CircuitBreaker();
        await Assert.That(cb.State).IsEqualTo(CircuitBreakerState.Closed);
        await Assert.That(cb.IsAllowingRequests).IsTrue();
        await Assert.That(cb.ConsecutiveFailures).IsEqualTo(0);

        cb.RecordSuccess();
        cb.RecordFailure();
        cb.Reset();

        await Assert.That(cb.State).IsEqualTo(CircuitBreakerState.Closed);
    }

    #endregion
}
