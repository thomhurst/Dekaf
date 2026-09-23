using System.Data.Common;
using Dekaf.Outbox;
using Dekaf.Outbox.EntityFrameworkCore;
using DotNet.Testcontainers.Builders;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Dekaf.Tests.Integration;

/// <summary>
/// The EF Core store's release against a server that runs every statement on its own
/// connection, so a statement held back from a cancelled round really runs after the release.
/// </summary>
[Category("MessagingPatterns")]
public sealed class OutboxRelationalLeaseTests
{
    private const int BucketCount = 4;

    [Test]
    public async Task SqlServer_StatementsOfTheRoundAStopCancelled_DoNotUndoTheRelease()
    {
        var password = $"Dekaf-{Guid.NewGuid():N}!";
        await using var database = new ContainerBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            .WithEnvironment("ACCEPT_EULA", "Y")
            .WithEnvironment("MSSQL_SA_PASSWORD", password)
            .WithPortBinding(1433, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("SQL Server is now ready for client connections"))
            .Build();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        await database.StartAsync(timeout.Token);
        var connection = new SqlConnectionStringBuilder
        {
            DataSource = $"{database.Hostname},{database.GetMappedPublicPort(1433)}",
            InitialCatalog = "outbox_leases",
            UserID = "sa", Password = password, TrustServerCertificate = true, Pooling = false
        }.ConnectionString;
        var gate = new GatedStatement();
        var factory = new PooledDbContextFactory<Context>(
            new DbContextOptionsBuilder<Context>().UseSqlServer(connection).AddInterceptors(gate).Options);
        await using (var context = await factory.CreateDbContextAsync(timeout.Token))
            await context.Database.EnsureCreatedAsync(timeout.Token);

        // A claim: the owner guard cannot refuse it, because the lease it takes has no owner.
        var claimer = new EfCoreOutboxStore<Context>(factory);
        gate.Arm(command => command.CommandText.Contains("UPDATE", StringComparison.Ordinal)
            && command.CommandText.Contains("[dekaf_outbox_leases]", StringComparison.Ordinal)
            && command.CommandText.Contains("[Owner] IS NULL", StringComparison.Ordinal));
        await AssertStragglerRefusedAsync(claimer, "a-claim");

        // A heartbeat of a relay that already has its row.
        var heartbeater = new EfCoreOutboxStore<Context>(factory);
        await Assert.That((await heartbeater.AcquireBucketLeasesAsync(Request("a-heartbeat"), timeout.Token)).Count)
            .IsEqualTo(BucketCount);
        gate.Arm(command => command.CommandText.Contains("UPDATE", StringComparison.Ordinal)
            && command.CommandText.Contains("[dekaf_outbox_relays]", StringComparison.Ordinal));
        await AssertStragglerRefusedAsync(heartbeater, "a-heartbeat");

        // Neither stopped relay is counted, so a peer takes the whole table at once.
        var peer = new EfCoreOutboxStore<Context>(factory);
        await Assert.That((await peer.AcquireBucketLeasesAsync(Request("b-peer"), timeout.Token)).Count)
            .IsEqualTo(BucketCount);

        async Task AssertStragglerRefusedAsync(EfCoreOutboxStore<Context> store, string relayId)
        {
            var cancelledRound = store.AcquireBucketLeasesAsync(Request(relayId), timeout.Token).AsTask();
            await gate.Held.WaitAsync(timeout.Token);
            await store.ReleaseBucketLeasesAsync(Request(relayId), [], timeout.Token);
            gate.Open();

            await Assert.That(await cancelledRound).IsEmpty();
            await Assert.That(gate.Affected).IsEqualTo(0);
            await using var context = await factory.CreateDbContextAsync(timeout.Token);
            var relay = await context.Set<OutboxRelayInstance>().AsNoTracking()
                .SingleAsync(r => r.RelayId == relayId, timeout.Token);
            await Assert.That(relay.StoppedAtUtc).IsNotNull();
            await Assert.That(await context.Set<OutboxLease>().CountAsync(l => l.Owner == relayId, timeout.Token))
                .IsEqualTo(0);
        }
    }

    private static OutboxLeaseRequest Request(string relayId) => new()
    {
        RelayId = relayId,
        BucketCount = BucketCount,
        LeaseDuration = TimeSpan.FromMinutes(10)
    };

    public sealed class Context(DbContextOptions<Context> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.UseDekafOutbox();
    }

    /// <summary>
    /// Holds back the next command that matches until <see cref="Open"/>, as the server runs a
    /// statement late that a provider which breaks the connection on cancellation left behind.
    /// </summary>
    private sealed class GatedStatement : DbCommandInterceptor
    {
        private TaskCompletionSource _held = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private TaskCompletionSource _open = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Func<DbCommand, bool>? _match;
        private DbCommand? _gated;

        public Task Held => Volatile.Read(ref _held).Task;

        public int? Affected { get; private set; }

        public void Arm(Func<DbCommand, bool> match)
        {
            Volatile.Write(ref _held, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
            Volatile.Write(ref _open, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
            Affected = null;
            Volatile.Write(ref _match, match);
        }

        public void Open() => Volatile.Read(ref _open).TrySetResult();

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref _match) is { } match && match(command)
                && Interlocked.CompareExchange(ref _match, null, match) == match)
            {
                _gated = command;
                Volatile.Read(ref _held).TrySetResult();
                await Volatile.Read(ref _open).Task;
            }

            return result;
        }

        public override ValueTask<int> NonQueryExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, int result,
            CancellationToken cancellationToken = default)
        {
            if (ReferenceEquals(command, _gated))
                Affected = result;
            return new(result);
        }
    }
}
