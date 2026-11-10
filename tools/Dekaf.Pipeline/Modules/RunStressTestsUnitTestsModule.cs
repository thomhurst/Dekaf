namespace Dekaf.Pipeline.Modules;

public class RunStressTestsUnitTestsModule : TestBaseModule
{
    protected override IEnumerable<string> TestableFrameworks
    {
        get
        {
            // The stress harness targets net11.0 only, whichever framework the unit-test lane runs.
            yield return "net11.0";
        }
    }

    protected override string ProjectFileName => "Dekaf.StressTests.Tests.csproj";
}
