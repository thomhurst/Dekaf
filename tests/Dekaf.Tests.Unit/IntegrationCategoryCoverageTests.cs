using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Dekaf.Tests.Unit;

public sealed class IntegrationCategoryCoverageTests
{
    [Test]
    public async Task EveryIntegrationTest_IsSelectedByACiCategory()
    {
        var root = FindRepositoryRoot();
        var groups = File.ReadAllText(Path.Combine(root, ".github/workflows/integration-groups.yml"));
        var categories = Regex.Matches(groups, @"(?m)^ {14}[\w-]+: ([A-Za-z][A-Za-z0-9,]+)\r?$")
            .SelectMany(match => match.Groups[1].Value.Split(',', StringSplitOptions.TrimEntries))
            .ToHashSet(StringComparer.Ordinal);
        // These virtual shards partition the parent category in the runner script.
        if (categories.Contains("ShareConsumerCore") && categories.Contains("ShareConsumerOther"))
            categories.Add("ShareConsumer");
        var ci = File.ReadAllText(Path.Combine(root, ".github/workflows/ci.yml"));
        foreach (Match match in Regex.Matches(ci, "run-integration-categories\\.sh net10\\.0 \"([^\"]+)\""))
            categories.UnionWith(match.Groups[1].Value.Split(',', StringSplitOptions.TrimEntries));

        await Assert.That(categories.Count).IsGreaterThan(20);
        var declarations = Directory.EnumerateFiles(Path.Combine(root, "tests/Dekaf.Tests.Integration"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"))
            .SelectMany(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path)
                .GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
            .ToArray();
        var classes = declarations.ToLookup(type => type.Identifier.ValueText);
        var missing = new List<string>();
        var testCount = 0;
        foreach (var type in declarations)
        {
            foreach (var method in type.Members.OfType<MethodDeclarationSyntax>())
            {
                if (!Attributes(method.AttributeLists).Any(attribute => AttributeName(attribute) == "Test"))
                    continue;
                testCount++;
                var selected = CategoryNames(method.AttributeLists)
                    .Concat(ClassCategories(type.Identifier.ValueText, classes, new HashSet<string>(StringComparer.Ordinal)))
                    .Any(categories.Contains);
                if (!selected)
                    missing.Add($"{type.Identifier.ValueText}.{method.Identifier.ValueText}");
            }
        }

        await Assert.That(testCount).IsGreaterThan(500);
        await Assert.That(missing).IsEmpty()
            .Because($"Every integration test must run in CI. Unselected tests: {string.Join(", ", missing)}");
    }

    private static IEnumerable<string> ClassCategories(string name,
        ILookup<string, ClassDeclarationSyntax> classes, HashSet<string> visited)
    {
        if (!visited.Add(name))
            yield break;
        foreach (var type in classes[name])
        {
            foreach (var category in CategoryNames(type.AttributeLists))
                yield return category;
            if (type.BaseList is null)
                continue;
            foreach (var parent in type.BaseList.Types)
                foreach (var category in ClassCategories(parent.Type.ToString(), classes, visited))
                    yield return category;
        }
    }

    private static IEnumerable<AttributeSyntax> Attributes(SyntaxList<AttributeListSyntax> lists) =>
        lists.SelectMany(list => list.Attributes);

    private static string AttributeName(AttributeSyntax attribute)
    {
        var name = attribute.Name switch
        {
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
            SimpleNameSyntax simple => simple.Identifier.ValueText,
            _ => attribute.Name.ToString()
        };
        return name.EndsWith("Attribute", StringComparison.Ordinal) ? name[..^9] : name;
    }

    private static IEnumerable<string> CategoryNames(SyntaxList<AttributeListSyntax> lists) =>
        Attributes(lists).Where(attribute => AttributeName(attribute) == "Category")
            .Select(attribute => attribute.ArgumentList!.Arguments[0].Expression)
            .OfType<LiteralExpressionSyntax>().Select(expression => expression.Token.ValueText);

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, ".github/workflows/integration-groups.yml")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Cannot find integration-groups.yml above the test output directory.");
    }
}
