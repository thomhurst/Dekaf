namespace Dekaf.Pipeline.Modules;

public class RunCompressionIntegrationTestsModule : RunIntegrationTestsModule
{
    protected override string Category => "Compression";

    protected override TimeSpan ModuleTimeout => TimeSpan.FromMinutes(30);

    protected override TimeSpan ProcessTimeout => TimeSpan.FromMinutes(20);

    protected override int? MaximumParallelTests => 4;
}
