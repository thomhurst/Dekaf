using System.Data.Common;
using Dekaf.Outbox;
using Dekaf.Outbox.EntityFrameworkCore;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Dekaf.Tests.Integration;

[Category("MessagingPatterns")]
public sealed class OutboxRelationalMetricsTests
{
    [Test]
    public async Task SqlServer_CombinesCountAndOldestInOneCommand_ForEmptyAndNonemptyTables()
    {
        var password = $"Dekaf-{Guid.NewGuid():N}!";
        IContainer? attempt = null;
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        await ContainerStartupRetry.RunAsync(
            () =>
            {
                attempt = new ContainerBuilder("mcr.microsoft.com/mssql/server:2022-latest")
                    .WithEnvironment("ACCEPT_EULA", "Y")
                    .WithEnvironment("MSSQL_SA_PASSWORD", password)
                    .WithPortBinding(1433, true)
                    .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("SQL Server is now ready for client connections"))
                    .Build();
                return attempt.StartAsync(timeout.Token);
            },
            // A failed attempt is disposed here, including the last one before RunAsync rethrows.
            () => attempt!.DisposeAsync(),
            ContainerStartupRetry.IsKnownTransient);
        await using var database = attempt!;
        var connection = new SqlConnectionStringBuilder
        {
            DataSource = $"{database.Hostname},{database.GetMappedPublicPort(1433)}",
            InitialCatalog = "outbox_metrics",
            UserID = "sa", Password = password, TrustServerCertificate = true, Pooling = false
        };
        var commands = new Commands();
        var options = new DbContextOptionsBuilder<Context>().UseSqlServer(connection.ConnectionString)
            .AddInterceptors(commands).Options;
        var factory = new PooledDbContextFactory<Context>(options);
        var store = new EfCoreOutboxStore<Context>(factory);
        await using var context = await factory.CreateDbContextAsync(timeout.Token);
        await context.Database.EnsureCreatedAsync(timeout.Token);

        commands.Count = 0;
        var empty = await store.GetPendingMetricsAsync(timeout.Token);
        await Assert.That(empty!.PendingCount).IsEqualTo(0);
        await Assert.That(empty.OldestCreatedAtUtc).IsNull();
        await Assert.That(commands.Count).IsEqualTo(1);

        var oldest = new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);
        foreach (var timestamp in new[] { oldest.AddHours(2), oldest, oldest.AddHours(1) })
            context.AddOutboxMessage(new OutboxMessage { MessageId = Guid.NewGuid(), Bucket = 0, Topic = "metrics", CreatedAtUtc = timestamp });
        await context.SaveChangesAsync(timeout.Token);
        commands.Count = 0;
        var sample = await store.GetPendingMetricsAsync(timeout.Token);
        await Assert.That(sample!.PendingCount).IsEqualTo(3);
        await Assert.That(sample.OldestCreatedAtUtc).IsEqualTo(oldest);
        await Assert.That(commands.Count).IsEqualTo(1);
        await Assert.That(commands.LastSql).Contains("COUNT_BIG");
        await Assert.That(commands.LastSql).Contains("MIN(");
        await Assert.That(await context.Set<OutboxMessage>().CountAsync(timeout.Token)).IsEqualTo(3);
    }

    public sealed class Context(DbContextOptions<Context> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.UseDekafOutbox();
    }

    private sealed class Commands : DbCommandInterceptor
    {
        public int Count;
        public string LastSql = string.Empty;
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Count++;
            LastSql = command.CommandText;
            return new(result);
        }
    }
}
