using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Dekaf.Tests.Unit;

/// <summary>
/// The mutation test projects compile linked unit-test sources into their own assembly, so a
/// <c>[ModuleInitializer]</c> they do not link silently changes the environment those suites run in
/// (issue #3677: the per-record accumulator hooks never fired and the producer campaign's initial
/// test run failed for weeks). PR CI does not run the mutation suites, so this test keeps the links
/// in step with the unit project.
/// </summary>
public sealed class MutationProjectLinkTests
{
    [Test]
    public async Task EveryMutationProject_LinksEveryUnitTestModuleInitializer()
    {
        var root = FindRepositoryRoot();
        var unitDirectory = Path.Combine(root, "tests", "Dekaf.Tests.Unit");
        var initializers = Directory.EnumerateFiles(unitDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(unitDirectory, path).Split(Path.DirectorySeparatorChar)
                .Any(part => part is "bin" or "obj"))
            .Where(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetRoot()
                .DescendantNodes().OfType<AttributeSyntax>().Any(IsModuleInitializer))
            .Select(path => Path.GetRelativePath(unitDirectory, path).Replace('\\', '/'))
            .ToArray();
        var projects = Directory.GetFiles(Path.Combine(root, "tests"), "Dekaf.Tests.Mutation.*.csproj", SearchOption.AllDirectories);

        await Assert.That(initializers).Contains("Producer/AccumulatorTestHooks.cs");
        await Assert.That(projects.Length).IsGreaterThanOrEqualTo(4);
        var missing = new List<string>();
        foreach (var project in projects)
        {
            var linked = Regex.Matches(File.ReadAllText(project), @"<Compile\s+Include=""\.\.[\\/]Dekaf\.Tests\.Unit[\\/]([^""]+)""")
                .Select(match => match.Groups[1].Value.Replace('\\', '/'))
                .ToHashSet(StringComparer.Ordinal);
            foreach (var initializer in initializers)
                if (!linked.Contains(initializer))
                    missing.Add($"{Path.GetFileName(project)}: {initializer}");
        }

        await Assert.That(missing).IsEmpty()
            .Because($"Mutation projects must link every Dekaf.Tests.Unit module initializer. Missing: {string.Join(", ", missing)}");
    }

    private static bool IsModuleInitializer(AttributeSyntax attribute)
    {
        var name = attribute.Name switch
        {
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            SimpleNameSyntax simple => simple.Identifier.ValueText,
            _ => attribute.Name.ToString()
        };
        return name is "ModuleInitializer" or "ModuleInitializerAttribute";
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (Directory.Exists(Path.Combine(directory.FullName, "tests", "Dekaf.Tests.Unit")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Cannot find tests/Dekaf.Tests.Unit above the test output directory.");
    }
}
