using Dekaf.Resilience;

namespace Dekaf.Tests.Unit.Resilience;

public class CircuitBreakerOptionsTests
{
    [Test]
    public async Task CircuitBreakerOptions_DefaultValues_AreValid()
    {
        var options = new CircuitBreakerOptions();

        await Assert.That(options.FailureThreshold).IsEqualTo(5);
        await Assert.That(options.BreakDuration).IsEqualTo(TimeSpan.FromSeconds(30));
        await Assert.That(options.HalfOpenMaxAttempts).IsEqualTo(2);
    }

    [Test]
    public async Task CircuitBreakerOptions_Validate_AcceptsValidOptions()
    {
        var options = new CircuitBreakerOptions
        {
            FailureThreshold = 3,
            BreakDuration = TimeSpan.FromSeconds(10),
            HalfOpenMaxAttempts = 1
        };

        // Should not throw
        options.Validate();
        await Task.CompletedTask;
    }

    [Test]
    public async Task CircuitBreakerOptions_Validate_RejectsZeroFailureThreshold()
    {
        var options = new CircuitBreakerOptions { FailureThreshold = 0 };

        await Assert.That(() => options.Validate()).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task CircuitBreakerOptions_Validate_RejectsNegativeFailureThreshold()
    {
        var options = new CircuitBreakerOptions { FailureThreshold = -1 };

        await Assert.That(() => options.Validate()).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task CircuitBreakerOptions_Validate_RejectsZeroBreakDuration()
    {
        var options = new CircuitBreakerOptions { BreakDuration = TimeSpan.Zero };

        await Assert.That(() => options.Validate()).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task CircuitBreakerOptions_Validate_RejectsNegativeBreakDuration()
    {
        var options = new CircuitBreakerOptions { BreakDuration = TimeSpan.FromSeconds(-1) };

        await Assert.That(() => options.Validate()).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task CircuitBreakerOptions_Validate_RejectsZeroHalfOpenMaxAttempts()
    {
        var options = new CircuitBreakerOptions { HalfOpenMaxAttempts = 0 };

        await Assert.That(() => options.Validate()).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task CircuitBreakerOptions_CanBeCustomized()
    {
        var options = new CircuitBreakerOptions
        {
            FailureThreshold = 10,
            BreakDuration = TimeSpan.FromMinutes(1),
            HalfOpenMaxAttempts = 5
        };

        await Assert.That(options.FailureThreshold).IsEqualTo(10);
        await Assert.That(options.BreakDuration).IsEqualTo(TimeSpan.FromMinutes(1));
        await Assert.That(options.HalfOpenMaxAttempts).IsEqualTo(5);
    }
}
