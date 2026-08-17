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
            ("DKAVRO011", "[AvroRecord] public abstract partial class Value { public int Id { get; set; } }")
        ];

        foreach (var (id, declaration) in cases)
        {
            var diagnostics = RunGenerator(declaration);
            await Assert.That(diagnostics.Any(diagnostic => diagnostic.Id == id))
                .IsTrue()
                .Because($"Expected {id}, got: {string.Join(", ", diagnostics.Select(static diagnostic => diagnostic.Id))}");
        }
    }

    private static ImmutableArray<Diagnostic> RunGenerator(string declaration)
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
        return driver.GetRunResult().Diagnostics;
    }
}
