using System.Collections.Immutable;
using Dekaf.SchemaRegistry.Avro.Poco;
using Dekaf.SchemaRegistry.Avro.Poco.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public sealed class AvroPocoGeneratorDiagnosticsTests
{
    private static readonly MetadataReference[] PlatformReferences =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Select(static path => MetadataReference.CreateFromFile(path))
        .Append(MetadataReference.CreateFromFile(typeof(AvroRecordAttribute).Assembly.Location))
        .ToArray();

    [Test]
    public async Task Generator_ReportsInvalidShapesAtBuildTime()
    {
        (string Id, string Declaration)[] cases =
        [
            ("DKAVRO001", "[AvroRecord] public class Value { public int Id { get; set; } }"),
            ("DKAVRO002", "[AvroRecord] public partial class Value { public Uri Link { get; set; } = null!; }"),
            ("DKAVRO003", "[AvroRecord] public partial class Value { [AvroField(Order=0)] public int A { get; set; } [AvroField(Order=0)] public int B { get; set; } }"),
            ("DKAVRO004", "[AvroRecord] public partial class Value { [AvroField(DefaultJson=\"null\")] public int Id { get; set; } }"),
            ("DKAVRO004", "[AvroRecord] public partial class Value { [AvroField(DefaultJson=\"\\\"00000000-0000-0000-0000-000000000000\\\"\")] public Guid Id { get; set; } }"),
            ("DKAVRO005", "[AvroRecord] public partial class Value { public Value Next { get; set; } = null!; }"),
            ("DKAVRO006", "[AvroRecord] public partial class Value { public int Id { get; } }"),
            ("DKAVRO007", "[AvroRecord] public partial class Value<T> { public int Id { get; set; } }"),
            ("DKAVRO008", "[AvroRecord] public partial class Value { [AvroField(Name=\"same\")] public int A { get; set; } [AvroField(Name=\"same\")] public int B { get; set; } }"),
            ("DKAVRO009", "[AvroRecord] public partial class Value { [AvroField(UnionTypes=new[] { typeof(string), typeof(Guid) })] public object Data { get; set; } = null!; }"),
            ("DKAVRO010", "[AvroRecord(Name=\"not-valid\")] public partial class Value { public int Id { get; set; } }"),
            ("DKAVRO011", "[AvroRecord] public abstract partial class Value { public int Id { get; set; } }"),
            ("DKAVRO012", "[AvroRecord] public partial class Value { public First A { get; set; } = new(); public Second B { get; set; } = new(); } [AvroRecord(Name=\"Shared\", Namespace=\"Example\")] public partial class First { public int Id { get; set; } } [AvroRecord(Name=\"Shared\", Namespace=\"Example\")] public partial class Second { public string Name { get; set; } = string.Empty; }"),
            ("DKAVRO012", "[AvroRecord] public partial class Value { public Other.Status A { get; set; } public Collision B { get; set; } = new(); } [AvroRecord(Name=\"Status\", Namespace=\"Other\")] public partial class Collision { public int Id { get; set; } } namespace Other { public enum Status { Ready } }"),
            ("DKAVRO010", "[AvroRecord] public partial class Value { public Status State { get; set; } } public enum Status { Café }"),
            ("DKAVRO014", "[AvroRecord] public partial class Value { public Status State { get; set; } } public enum Status { First = 1, Second = 1 }"),
            ("DKAVRO015", "[AvroRecord] public partial class Value { public Status State { get; set; } } public enum Status { }"),
            ("DKAVRO009", "[AvroRecord] public partial class Value { [AvroField(UnionTypes=new[] { typeof(Base), typeof(Derived) })] public object Data { get; set; } = null!; } [AvroRecord] public partial class Base { public int Id { get; set; } } [AvroRecord] public partial class Derived : Base { public string Name { get; set; } = string.Empty; }"),
            ("DKAVRO002", "[AvroRecord] public partial class Value { [AvroField(UnionTypes=new[] { typeof(int), typeof(string) })] public object Data { get; set; } = null!; }"),
            ("DKAVRO002", "[AvroRecord] public partial class Value { [AvroField(UnionTypes=new Type[] { typeof(string), null })] public object Data { get; set; } = null!; }"),
            ("DKAVRO013", "public class Base { public int Id { get; set; } } [AvroRecord] public partial class Value : Base { public string Name { get; set; } = string.Empty; }")
        ];

        foreach (var (id, declaration) in cases)
        {
            var diagnostics = RunGenerator(declaration);
            await Assert.That(diagnostics.Any(diagnostic => diagnostic.Id == id))
                .IsTrue()
                .Because($"Expected {id}, got: {string.Join(", ", diagnostics.Select(static diagnostic => diagnostic.Id))}");
        }
    }

    [Test]
    public async Task Generator_SupportedPublicContract_ReportsNoDekafDiagnostics()
    {
        var diagnostics = RunGenerator(
            "[AvroRecord] public partial class Value { " +
            "private readonly int _state; public int Id { get; set; } " +
            "public string Hidden { get; private set; } = string.Empty; }");

        await Assert.That(diagnostics.Any(static diagnostic =>
                diagnostic.Id.StartsWith("DKAVRO", StringComparison.Ordinal)))
            .IsFalse()
            .Because($"Supported declaration must not report diagnostics, got: " +
                $"{string.Join(", ", diagnostics.Select(static diagnostic => diagnostic.Id))}");
    }

    [Test]
    public async Task Generator_UsesUniqueHintNamesForCollidingSanitizedTypeNames()
    {
        const string declaration =
            "namespace A.B { [AvroRecord] public partial class C_D { public int Id { get; set; } } } " +
            "namespace A.B_C { [AvroRecord] public partial class D { public int Id { get; set; } } }";

        var runResult = RunGeneratorResult(declaration);
        var generatedSources = runResult.Results.Single().GeneratedSources;

        await Assert.That(runResult.Diagnostics).IsEmpty();
        await Assert.That(generatedSources.Count).IsEqualTo(2);
        await Assert.That(generatedSources.Select(static source => source.HintName).Distinct().Count()).IsEqualTo(2);
    }

    [Test]
    public async Task Generator_StopsCollectionWritesAfterOverflow()
    {
        const string declaration =
            "[AvroRecord] public partial class Collections { " +
            "public int[] Values { get; set; } = []; " +
            "public System.Collections.Generic.Dictionary<string, int> Mappings { get; set; } = []; }";

        var runResult = RunGeneratorResult(declaration);
        var generated = runResult.Results.Single().GeneratedSources.Single().SourceText.ToString();
        var overflowChecks = generated.Split(
            "if (writer.WrittenCount == int.MaxValue)",
            StringSplitOptions.None).Length - 1;

        await Assert.That(runResult.Diagnostics).IsEmpty();
        await Assert.That(overflowChecks).IsEqualTo(2);
    }

    private static ImmutableArray<Diagnostic> RunGenerator(string declaration)
        => RunGeneratorResult(declaration).Diagnostics;

    private static GeneratorDriverRunResult RunGeneratorResult(string declaration)
    {
        var source = "using System; using Dekaf.SchemaRegistry.Avro.Poco; " + declaration;
        var syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.Preview));
        var compilation = CSharpCompilation.Create(
            "GeneratorDiagnostics",
            [syntaxTree],
            PlatformReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new AvroPocoGenerator());

        driver = driver.RunGenerators(compilation);
        return driver.GetRunResult();
    }
}
