namespace Dekaf.Pipeline.Modules;

public class RunProducerIntegrationTestsModule : RunIntegrationTestsModule
{
    protected override string Category => "Producer";

    protected override int? MaximumParallelTests => 4;
}
