namespace Dekaf.Tests.Unit.Internal;

using Dekaf.Internal;

[NotInParallel]
public class MemoryBudgetDispatchTests
{
    private const ulong TestBudget = 1200UL * 1024 * 1024;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    [Before(Test)]
    public void SetUp()
    {
        DekafMemoryBudget.ResetForTesting();
        DekafMemoryBudget.SetBudget(TestBudget);
    }

    [After(Test)]
    public void TearDown() => DekafMemoryBudget.ResetForTesting();

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task ConcurrentRegistration_AppliesLatestLimitToEveryInstance(bool useClientBudget, bool consumer)
    {
        var budget = CreateBudget(useClientBudget);
        var first = new FakeInstance();
        var second = new FakeInstance();
        var third = new FakeInstance();
        Register(budget, first, consumer);

        await OverlapChangesAsync(first,
            () => Register(budget, second, consumer),
            () => Register(budget, third, consumer));

        var expected = TestBudget / 3 / Divisor(consumer);
        await Assert.That(first.CurrentLimit).IsEqualTo(expected);
        await Assert.That(second.CurrentLimit).IsEqualTo(expected);
        await Assert.That(third.CurrentLimit).IsEqualTo(expected);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task ConcurrentUnregistration_AppliesLatestLargerLimit(bool useClientBudget, bool consumer)
    {
        var budget = CreateBudget(useClientBudget);
        var first = new FakeInstance();
        var second = new FakeInstance();
        var third = new FakeInstance();
        Register(budget, first, consumer);
        Register(budget, second, consumer);
        Register(budget, third, consumer);

        await OverlapChangesAsync(first,
            () => Unregister(budget, third, consumer),
            () => Unregister(budget, second, consumer));

        await Assert.That(first.CurrentLimit).IsEqualTo(TestBudget / Divisor(consumer));
    }

    [Test]
    [Arguments(false, false, false)]
    [Arguments(false, false, true)]
    [Arguments(false, true, false)]
    [Arguments(false, true, true)]
    [Arguments(true, false, false)]
    [Arguments(true, false, true)]
    [Arguments(true, true, false)]
    [Arguments(true, true, true)]
    public async Task ConcurrentReservations_AppliesLatestLimit(bool useClientBudget, bool consumer, bool release)
    {
        var budget = CreateBudget(useClientBudget);
        var instance = new FakeInstance();
        Register(budget, instance, consumer);
        var reservation = TestBudget / 4;
        if (release)
            budget.ReserveExplicit(reservation * 2);

        Action change = release
            ? () => budget.ReleaseExplicit(reservation)
            : () => budget.ReserveExplicit(reservation);
        await OverlapChangesAsync(instance, change, change);

        var expectedBudget = release ? TestBudget : TestBudget / 2;
        await Assert.That(instance.CurrentLimit).IsEqualTo(expectedBudget / Divisor(consumer));
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ConcurrentSetBudget_AppliesLatestLimit(bool percentage)
    {
        var instance = new FakeInstance();
        DekafMemoryBudget.RegisterConsumer(instance);
        Action first = percentage
            ? () => DekafMemoryBudget.SetBudget(0.20)
            : () => DekafMemoryBudget.SetBudget(TestBudget / 2);
        Action latest = percentage
            ? () => DekafMemoryBudget.SetBudget(0.30)
            : () => DekafMemoryBudget.SetBudget(TestBudget);

        await OverlapChangesAsync(instance, first, latest);

        await Assert.That(instance.CurrentLimit).IsEqualTo(Math.Max(DekafMemoryBudget.TotalBudget, 16UL * 1024 * 1024));
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ReentrantRegistration_DrainsLatestSnapshotAfterCurrentCallback(bool useClientBudget)
    {
        var budget = CreateBudget(useClientBudget);
        var first = new FakeInstance();
        var second = new FakeInstance();
        first.BeforeStore = () => budget.RegisterProducer(second);

        budget.RegisterProducer(first);

        var expected = TestBudget / 2 / DekafMemoryBudget.ProducerOverheadDivisor;
        await Assert.That(first.CurrentLimit).IsEqualTo(expected);
        await Assert.That(second.CurrentLimit).IsEqualTo(expected);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ThrowingCallback_DoesNotStrandPendingSnapshotOrSibling(bool useClientBudget)
    {
        var budget = CreateBudget(useClientBudget);
        var first = new FakeInstance();
        var second = new FakeInstance();
        var error = new InvalidOperationException("Budget callback failure");
        Exception? reported = null;
        DekafMemoryBudget.OnBookkeepingError = exception => reported = exception;
        first.BeforeStore = () =>
        {
            budget.RegisterProducer(second);
            throw error;
        };

        budget.RegisterProducer(first);

        var expected = TestBudget / 2 / DekafMemoryBudget.ProducerOverheadDivisor;
        await Assert.That(reported).IsSameReferenceAs(error);
        await Assert.That(first.CurrentLimit).IsEqualTo(expected);
        await Assert.That(second.CurrentLimit).IsEqualTo(expected);
    }

    private static IDekafMemoryBudget CreateBudget(bool useClientBudget) =>
        useClientBudget ? new ClientMemoryBudget(TestBudget) : DekafMemoryBudget.Global;

    private static ulong Divisor(bool consumer) => consumer ? 1UL : (ulong)DekafMemoryBudget.ProducerOverheadDivisor;

    private static void Register(IDekafMemoryBudget budget, IBudgetedInstance instance, bool consumer)
    {
        if (consumer)
            budget.RegisterConsumer(instance);
        else
            budget.RegisterProducer(instance);
    }

    private static void Unregister(IDekafMemoryBudget budget, IBudgetedInstance instance, bool consumer)
    {
        if (consumer)
            budget.UnregisterConsumer(instance);
        else
            budget.UnregisterProducer(instance);
    }

    private static async Task OverlapChangesAsync(FakeInstance instance, Action first, Action latest)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        instance.BeforeStore = () =>
        {
            entered.SetResult();
            release.Wait();
        };
        var firstTask = Task.Run(first);
        Task? latestTask = null;
        try
        {
            await entered.Task.WaitAsync(Timeout);
            latestTask = Task.Run(latest);
            // The newer mutation must finish while the old callback is paused. This
            // also verifies callbacks hold neither the budget lock nor a dispatch lock.
            await latestTask.WaitAsync(Timeout);
        }
        finally
        {
            release.Set();
            await firstTask.WaitAsync(Timeout);
            if (latestTask is not null)
                await latestTask.WaitAsync(Timeout);
        }
    }

    private sealed class FakeInstance : IBudgetedInstance
    {
        public ulong CurrentLimit { get; private set; }
        public Action? BeforeStore { get; set; }

        public void OnBudgetChanged(ulong newLimit)
        {
            var callback = BeforeStore;
            BeforeStore = null;
            callback?.Invoke();
            CurrentLimit = newLimit;
        }
    }
}
