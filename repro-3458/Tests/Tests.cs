using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TUnit.Aspire;

public class AppFixture : AspireFixture<Projects.AppHost>
{
    protected override TimeSpan ResourceTimeout => TimeSpan.FromSeconds(180);

    protected override string[] Args =>
        [$"REPRO_VARIANT={Environment.GetEnvironmentVariable("REPRO_VARIANT") ?? "issue"}"];

    protected override void ConfigureBuilder(IDistributedApplicationTestingBuilder builder)
    {
        builder.Services.AddLogging(logging => logging
            .AddSimpleConsole(o => o.TimestampFormat = "HH:mm:ss.fff ")
            .SetMinimumLevel(LogLevel.Debug)
            .AddFilter("Aspire.Hosting", LogLevel.Debug)
            .AddFilter("Microsoft", LogLevel.Information));
    }
}

public class TUnitAspireFixtureTests
{
    [ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
    public required AppFixture Fixture { get; init; }

    [Test]
    public async Task FixtureStarts()
    {
        Console.WriteLine("[repro] fixture started");
        await Task.CompletedTask;
    }
}

public class PlainTestingBuilderTests
{
    [Test]
    [Timeout(240_000)]
    public async Task StartAsyncCompletes(CancellationToken cancellationToken)
    {
        var variant = Environment.GetEnvironmentVariable("REPRO_VARIANT") ?? "issue";
        await using var builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.AppHost>([$"REPRO_VARIANT={variant}"], cancellationToken);
        builder.Services.AddLogging(logging => logging
            .AddSimpleConsole(o => o.TimestampFormat = "HH:mm:ss.fff ")
            .SetMinimumLevel(LogLevel.Debug)
            .AddFilter("Microsoft", LogLevel.Information));

        await using var app = await builder.BuildAsync(cancellationToken);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(180));
        await app.StartAsync(cts.Token);
        Console.WriteLine("[repro] plain StartAsync completed");
    }
}
