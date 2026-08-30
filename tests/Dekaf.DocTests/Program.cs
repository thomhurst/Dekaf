using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Dekaf.DocTests;

internal static partial class Program
{
    private static readonly CSharpParseOptions ParseOptions = new(
        LanguageVersion.Preview);

    private static readonly CSharpCompilationOptions CompilationOptions = new(
        OutputKind.DynamicallyLinkedLibrary,
        optimizationLevel: OptimizationLevel.Release,
        allowUnsafe: true,
        nullableContextOptions: NullableContextOptions.Enable);

    // Shared context validates API shape for fragments; it does not prove standalone prerequisites.
    private static readonly string Prelude = """
        using System;
        using System.Buffers;
        using System.Buffers.Binary;
        using System.Collections.Concurrent;
        using System.Collections.Generic;
        using System.Diagnostics;
        using System.IO;
        using System.Linq;
        using System.Net;
        using System.Net.Http;
        using System.Net.Security;
        using System.Security.Authentication;
        using System.Security.Cryptography;
        using System.Security.Cryptography.X509Certificates;
        using System.Runtime.CompilerServices;
        using System.Text;
        using System.Text.Json;
        using System.Text.Json.Serialization;
        using System.Text.Json.Serialization.Metadata;
        using System.Threading;
        using System.Threading.Tasks;
        using Amazon;
        using Avro;
        using Avro.Generic;
        using Azure.Identity;
        using Azure.Core;
        using BenchmarkDotNet.Attributes;
        using Dekaf;
        using Dekaf.Admin;
        using Dekaf.Compression;
        using Dekaf.Compression.Brotli;
        using Dekaf.Compression.Lz4;
        using Dekaf.Compression.Snappy;
        using Dekaf.Compression.Zstd;
        using Dekaf.Consumer;
        using Dekaf.Consumer.DeadLetter;
        using Dekaf.Errors;
        using Dekaf.Extensions.DependencyInjection;
        using Dekaf.Extensions.Hosting;
        using Dekaf.OpenTelemetry;
        using Dekaf.Networking;
        using Dekaf.Outbox;
        using Dekaf.Outbox.EntityFrameworkCore;
        using Dekaf.Producer;
        using Dekaf.Protocol;
        using Dekaf.Protocol.Messages;
        using Dekaf.Protocol.Records;
        using Dekaf.Retry;
        using Dekaf.SchemaRegistry;
        using Dekaf.SchemaRegistry.Avro;
        using Dekaf.SchemaRegistry.Json;
        using Dekaf.SchemaRegistry.Jsonata;
        using Dekaf.SchemaRegistry.Kms.Aws;
        using Dekaf.SchemaRegistry.Kms.Azure;
        using Dekaf.SchemaRegistry.Kms.Gcp;
        using Dekaf.SchemaRegistry.Kms.Vault;
        using Dekaf.SchemaRegistry.Protobuf;
        using Dekaf.Security;
        using Dekaf.Security.Sasl;
        using Dekaf.ShareConsumer;
        using Dekaf.Serialization;
        using Dekaf.Serialization.Json;
        using Dekaf.Serialization.Routing;
        using Dekaf.Testing;
        using Google.Cloud.Kms.V1;
        using Microsoft.AspNetCore.Builder;
        using Microsoft.AspNetCore.Mvc;
        using Microsoft.Extensions.Configuration;
        using Microsoft.Extensions.DependencyInjection;
        using Microsoft.Extensions.Hosting;
        using Microsoft.Extensions.Logging;
        using Microsoft.Extensions.Logging.Abstractions;
        using Microsoft.EntityFrameworkCore;
        using Dekaf.DocTests;
        using static Dekaf.DocTests.DocContext;
        """;

    public static int Main(string[] args)
    {
        var repositoryRoot = GetRepositoryRoot(args);
        var references = GetMetadataReferences();
        var snippets = ReadSnippets(repositoryRoot);
        var jsonGenerator = LoadJsonSourceGenerator();
        var failures = new List<Failure>();

        for (var index = 0; index < snippets.Count; index++)
        {
            var snippet = snippets[index];
            var source = BuildSource(snippet);
            var syntaxTree = CSharpSyntaxTree.ParseText(source, ParseOptions, snippet.Path);
            var outputKind = syntaxTree.GetCompilationUnitRoot().Members
                .Any(static member => member is Microsoft.CodeAnalysis.CSharp.Syntax.GlobalStatementSyntax)
                ? OutputKind.ConsoleApplication
                : OutputKind.DynamicallyLinkedLibrary;
            Compilation compilation = CSharpCompilation.Create(
                $"DekafDocSnippet{index}",
                [syntaxTree],
                references,
                CompilationOptions.WithOutputKind(outputKind));
            ImmutableArray<Diagnostic> generatorDiagnostics = [];

            if (snippet.Source.Contains("[JsonSerializable(", StringComparison.Ordinal))
            {
                CSharpGeneratorDriver
                    .Create([jsonGenerator], parseOptions: ParseOptions)
                    .RunGeneratorsAndUpdateCompilation(
                        compilation,
                        out compilation,
                        out generatorDiagnostics);
            }

            var errors = compilation.GetDiagnostics()
                .Concat(generatorDiagnostics)
                .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .ToArray();

            if (errors.Length == 0)
            {
                continue;
            }

            var shownErrors = errors.Take(5).Select(FormatDiagnostic);
            var omitted = errors.Length > 5 ? $"{Environment.NewLine}  ... {errors.Length - 5} more errors" : string.Empty;
            var display = $"{snippet.Id}:{Environment.NewLine}{string.Join(Environment.NewLine, shownErrors)}{omitted}";
            failures.Add(new Failure(display));
        }

        Console.WriteLine($"Compiled {snippets.Count} C# documentation snippets.");
        if (failures.Count == 0)
        {
            return 0;
        }

        Console.Error.WriteLine($"{failures.Count} documentation snippets failed compilation:");
        Console.Error.WriteLine(string.Join(Environment.NewLine + Environment.NewLine, failures.Select(static failure => failure.Display)));
        return 1;
    }

    private static string GetRepositoryRoot(string[] args)
    {
        if (args is ["--repository-root", var path])
        {
            return Path.GetFullPath(path);
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Dekaf.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root. Pass --repository-root <path>.");
    }

    private static ImmutableArray<MetadataReference> GetMetadataReferences()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string platformAssemblies)
        {
            paths.UnionWith(platformAssemblies.Split(Path.PathSeparator));
        }

        paths.UnionWith(Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll")
            .Where(static path => !Path.GetFileName(path).Equals(
                "System.Text.Json.SourceGeneration.dll",
                StringComparison.OrdinalIgnoreCase)));
        return paths
            .Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();
    }

    private static IReadOnlyList<Snippet> ReadSnippets(string repositoryRoot)
    {
        var documentPaths = new[] { Path.Combine(repositoryRoot, "README.md") }
            .Concat(Directory.EnumerateFiles(Path.Combine(repositoryRoot, "docs", "docs"), "*.md", SearchOption.AllDirectories))
            .Concat(Directory.EnumerateFiles(Path.Combine(repositoryRoot, "docs", "docs"), "*.mdx", SearchOption.AllDirectories))
            .Order(StringComparer.Ordinal)
            .ToArray();
        ValidateShellSamples(repositoryRoot, documentPaths);
        var snippets = new List<Snippet>();

        foreach (var documentPath in documentPaths)
        {
            ReadDocumentSnippets(repositoryRoot, documentPath, snippets);
        }

        return snippets;
    }

    private static void ValidateShellSamples(string repositoryRoot, IEnumerable<string> documentPaths)
    {
        var packageIds = Directory
            .EnumerateFiles(Path.Combine(repositoryRoot, "src"), "*.csproj", SearchOption.AllDirectories)
            .Where(static path => !File.ReadAllText(path).Contains("<IsPackable>false</IsPackable>", StringComparison.Ordinal))
            .Select(static path =>
            {
                var project = File.ReadAllText(path);
                var match = PackageIdRegex().Match(project);
                return match.Success ? match.Groups[1].Value : Path.GetFileNameWithoutExtension(path);
            })
            .ToHashSet(StringComparer.Ordinal);

        foreach (var documentPath in documentPaths)
        {
            var markdown = File.ReadAllText(documentPath);
            foreach (Match match in AddPackageRegex().Matches(markdown))
            {
                var packageId = match.Groups[1].Value;
                if (!packageIds.Contains(packageId))
                {
                    throw new InvalidOperationException(
                        $"Documented package '{packageId}' has no packable project under src/.");
                }
            }

            foreach (Match match in RunProjectRegex().Matches(markdown))
            {
                var projectPath = match.Groups[1].Value.Trim('"', '\'');
                var resolvedProjectPath = Path.Combine(repositoryRoot, projectPath);
                if (!File.Exists(resolvedProjectPath) && !Directory.Exists(resolvedProjectPath))
                {
                    throw new InvalidOperationException(
                        $"Documented project '{projectPath}' does not exist.");
                }
            }
        }
    }

    private static void ReadDocumentSnippets(string repositoryRoot, string documentPath, List<Snippet> snippets)
    {
        var relativePath = Path.GetRelativePath(repositoryRoot, documentPath).Replace('\\', '/');
        var lines = File.ReadAllLines(documentPath);
        var suppressionLine = Array.FindIndex(
            lines,
            static line => line.Contains("<!-- doc-test-", StringComparison.Ordinal));
        if (suppressionLine >= 0)
        {
            throw new InvalidOperationException(
                $"Documentation test suppression at {relativePath}:{suppressionLine + 1} is not allowed.");
        }

        var ordinal = 0;

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            if (!CSharpFenceRegex().IsMatch(lines[lineIndex]))
            {
                continue;
            }

            ordinal++;
            var startLine = lineIndex + 2;
            var source = new StringBuilder();
            for (lineIndex++; lineIndex < lines.Length && !FenceEndRegex().IsMatch(lines[lineIndex]); lineIndex++)
            {
                source.Append(lines[lineIndex]).Append('\n');
            }

            if (lineIndex == lines.Length)
            {
                throw new InvalidOperationException($"Unclosed C# fence at {relativePath}:{startLine}.");
            }

            snippets.Add(new Snippet(relativePath, ordinal, startLine, source.ToString()));
        }
    }

    private static ISourceGenerator LoadJsonSourceGenerator()
    {
        var assemblyPath = Path.Combine(AppContext.BaseDirectory, "System.Text.Json.SourceGeneration.dll");
        var assembly = Assembly.LoadFrom(assemblyPath);
        var generatorType = assembly.GetType(
            "System.Text.Json.SourceGeneration.JsonSourceGenerator",
            throwOnError: true)!;
        return ((IIncrementalGenerator)Activator.CreateInstance(generatorType)!).AsSourceGenerator();
    }

    private static string BuildSource(Snippet snippet)
    {
        var source = snippet.Source;
        if (BuilderChainRegex().IsMatch(source))
        {
            source = BuildBuilderChain(snippet, source);
        }

        var snippetUsings = source
            .ReplaceLineEndings("\n")
            .Split('\n')
            .Select(static line => line.Trim())
            .Where(static line => line.StartsWith("using ", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);
        var prelude = string.Join(
            Environment.NewLine,
            Prelude
                .ReplaceLineEndings("\n")
                .Split('\n')
                .Where(line => !snippetUsings.Contains(line.Trim())));

        return $"{prelude}{Environment.NewLine}#line {snippet.StartLine} \"{snippet.Path}\"{Environment.NewLine}{source}";
    }

    private static string BuildBuilderChain(Snippet snippet, string source)
    {
        var genericArguments = source.Contains("OrderKey", StringComparison.Ordinal)
            ? "OrderKey, Order"
            : "string, string";
        var builder = snippet.Path.Contains("consumer", StringComparison.OrdinalIgnoreCase)
            ? $"Kafka.CreateConsumer<{genericArguments}>()"
            : $"Kafka.CreateProducer<{genericArguments}>()";
        var lines = source.ReplaceLineEndings("\n").Split('\n');
        var result = new StringBuilder();
        var segment = new List<string>();

        foreach (var line in lines)
        {
            if (line.Length > 0 && line[0] == '.' && segment.Count > 0)
            {
                AppendBuilderSegment(result, segment, builder);
                segment.Clear();
            }

            segment.Add(line);
        }

        AppendBuilderSegment(result, segment, builder);
        return result.ToString();
    }

    private static void AppendBuilderSegment(StringBuilder result, List<string> segment, string builder)
    {
        var firstCodeLine = segment.FindIndex(static line => !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith("//", StringComparison.Ordinal));
        if (firstCodeLine < 0)
        {
            foreach (var line in segment)
            {
                result.AppendLine(line);
            }

            return;
        }

        segment[firstCodeLine] = $"{builder}{segment[firstCodeLine]}";
        var lastCodeLine = segment.FindLastIndex(static line => !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith("//", StringComparison.Ordinal));
        var lineComment = segment[lastCodeLine].IndexOf("//", StringComparison.Ordinal);
        var code = lineComment < 0 ? segment[lastCodeLine] : segment[lastCodeLine][..lineComment];
        var comment = lineComment < 0 ? string.Empty : segment[lastCodeLine][lineComment..];
        if (!code.TrimEnd().EndsWith(';'))
        {
            code = $"{code.TrimEnd()}; ";
        }

        segment[lastCodeLine] = code + comment;
        foreach (var line in segment)
        {
            result.AppendLine(line);
        }
    }

    private static string FormatDiagnostic(Diagnostic diagnostic)
    {
        var span = diagnostic.Location.GetMappedLineSpan();
        var line = span.StartLinePosition.Line + 1;
        var column = span.StartLinePosition.Character + 1;
        return $"  {span.Path}:{line}:{column}: {diagnostic.Id}: {diagnostic.GetMessage()}";
    }

    [GeneratedRegex("^```(?:csharp|cs)\\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex CSharpFenceRegex();

    [GeneratedRegex("^```\\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex FenceEndRegex();

    [GeneratedRegex("(?s)^(\\s*(?://[^\\r\\n]*(?:\\r?\\n|$)\\s*)*)(\\.)", RegexOptions.CultureInvariant)]
    private static partial Regex BuilderChainRegex();

    [GeneratedRegex("<PackageId>([^<]+)</PackageId>", RegexOptions.CultureInvariant)]
    private static partial Regex PackageIdRegex();

    [GeneratedRegex("dotnet\\s+add\\s+package\\s+([A-Za-z0-9_.-]+)", RegexOptions.CultureInvariant)]
    private static partial Regex AddPackageRegex();

    [GeneratedRegex("dotnet\\s+run[^\\r\\n]*?--project\\s+([^\\s\\\\]+)", RegexOptions.CultureInvariant)]
    private static partial Regex RunProjectRegex();

    private sealed record Snippet(string Path, int Ordinal, int StartLine, string Source)
    {
        public string Id => $"{Path}#{Ordinal}";
    }

    private sealed record Failure(string Display);
}
