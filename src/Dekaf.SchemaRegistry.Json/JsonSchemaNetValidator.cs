using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Json.Schema;
using RegistrySchema = Dekaf.SchemaRegistry.Schema;
using ValidatorSchemaRegistry = Json.Schema.SchemaRegistry;

namespace Dekaf.SchemaRegistry.Json;

/// <summary>
/// JSON Schema dialect used when a schema omits the <c>$schema</c> keyword.
/// </summary>
public enum JsonSchemaDialect
{
    /// <summary>
    /// JSON Schema Draft 7, the Confluent-compatible default.
    /// </summary>
    Draft7,

    /// <summary>
    /// JSON Schema Draft 2019-09.
    /// </summary>
    Draft201909,

    /// <summary>
    /// JSON Schema Draft 2020-12.
    /// </summary>
    Draft202012
}

/// <summary>
/// Configures JsonSchema.Net validation.
/// </summary>
public sealed class JsonSchemaNetValidatorOptions
{
    /// <summary>
    /// Dialect used when a schema has no <c>$schema</c> keyword. Default is Draft 7.
    /// </summary>
    public JsonSchemaDialect DefaultDialect { get; init; } = JsonSchemaDialect.Draft7;

    /// <summary>
    /// Whether <c>format</c> is treated as an assertion. Default is false, matching the JSON Schema specification.
    /// </summary>
    public bool RequireFormatValidation { get; init; }

    /// <summary>
    /// Maximum time allowed to resolve Schema Registry references. Default is 30 seconds.
    /// </summary>
    public TimeSpan ReferenceResolutionTimeout { get; init; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Compiles and weakly caches JsonSchema.Net validators by Schema Registry schema object.
/// </summary>
public sealed class JsonSchemaNetValidatorFactory : IJsonSchemaValidatorFactory
{
    private static readonly Uri RootBaseUri = new("https://schemas.dekaf.invalid/root.json");

    private readonly ISchemaRegistryClient _schemaRegistry;
    private readonly JsonSchemaNetValidatorOptions _options;
    private readonly ConditionalWeakTable<RegistrySchema, IJsonSchemaValidator> _validators = new();
    private readonly ConditionalWeakTable<RegistrySchema, IJsonSchemaValidator>.CreateValueCallback _createValidator;

    /// <summary>
    /// Creates a JsonSchema.Net validator factory.
    /// </summary>
    public JsonSchemaNetValidatorFactory(
        ISchemaRegistryClient schemaRegistry,
        JsonSchemaNetValidatorOptions? options = null)
    {
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _options = options ?? new JsonSchemaNetValidatorOptions();
        if (_options.ReferenceResolutionTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                _options.ReferenceResolutionTimeout,
                "Reference resolution timeout must be greater than zero.");
        }

        _createValidator = CreateValidator;
    }

    /// <inheritdoc />
    public IJsonSchemaValidator GetOrCreate(RegistrySchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        if (schema.SchemaType != SchemaType.Json)
            throw new ArgumentException("JSON Schema validation requires a JSON schema.", nameof(schema));

        return _validators.GetValue(schema, _createValidator);
    }

    private IJsonSchemaValidator CreateValidator(RegistrySchema schema)
    {
        var registry = new ValidatorSchemaRegistry();
        var buildOptions = new BuildOptions
        {
            Dialect = GetDialect(_options.DefaultDialect),
            SchemaRegistry = registry
        };
        var visited = new HashSet<SchemaReferenceKey>();
        RegisterReferences(schema, RootBaseUri, registry, buildOptions, visited);
        var compiled = JsonSchema.FromText(schema.SchemaString, buildOptions, RootBaseUri);
        return new JsonSchemaNetValidator(
            compiled,
            new EvaluationOptions
            {
                OutputFormat = OutputFormat.List,
                RequireFormatValidation = _options.RequireFormatValidation
            });
    }

    private void RegisterReferences(
        RegistrySchema schema,
        Uri baseUri,
        ValidatorSchemaRegistry registry,
        BuildOptions buildOptions,
        HashSet<SchemaReferenceKey> visited)
    {
        var references = schema.References;
        if (references is null)
            return;

        for (var i = 0; i < references.Count; i++)
        {
            var reference = references[i];
            var referenceUri = ResolveReferenceUri(baseUri, reference.Name);
            var key = new SchemaReferenceKey(reference.Subject, reference.Version, referenceUri);
            if (!visited.Add(key))
                continue;

            var registered = ResolveReference(reference);
            if (registered.Schema.SchemaType != SchemaType.Json)
            {
                throw new InvalidOperationException(
                    $"JSON Schema reference '{reference.Name}' resolved to {registered.Schema.SchemaType}.");
            }

            var compiled = JsonSchema.FromText(
                registered.Schema.SchemaString,
                buildOptions,
                referenceUri);
            registry.Register(referenceUri, compiled);
            RegisterReferences(registered.Schema, referenceUri, registry, buildOptions, visited);
        }
    }

    private RegisteredSchema ResolveReference(SchemaReference reference)
    {
        using var timeoutSource = new CancellationTokenSource(_options.ReferenceResolutionTimeout);
        try
        {
            return _schemaRegistry.GetSchemaBySubjectAsync(
                    reference.Subject,
                    reference.Version.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    timeoutSource.Token)
                .WaitAsync(timeoutSource.Token)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }
        catch (OperationCanceledException exception) when (timeoutSource.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"JSON Schema reference '{reference.Name}' resolution timed out.",
                exception);
        }
    }

    private static Uri ResolveReferenceUri(Uri baseUri, string name)
    {
        if (Uri.TryCreate(name, UriKind.Absolute, out var absolute))
            return absolute;

        return new Uri(baseUri, name);
    }

    private static Dialect GetDialect(JsonSchemaDialect dialect) => dialect switch
    {
        JsonSchemaDialect.Draft7 => Dialect.Draft07,
        JsonSchemaDialect.Draft201909 => Dialect.Draft201909,
        JsonSchemaDialect.Draft202012 => Dialect.Draft202012,
        _ => throw new ArgumentOutOfRangeException(nameof(dialect), dialect, "Unsupported JSON Schema dialect.")
    };

    private readonly record struct SchemaReferenceKey(string Subject, int Version, Uri Uri);
}

internal sealed class JsonSchemaNetValidator(
    JsonSchema schema,
    EvaluationOptions evaluationOptions) : IJsonSchemaValidator
{
    public void Validate(ReadOnlySpan<byte> payload, int schemaId)
    {
        JsonDocument? document = null;
        try
        {
            var reader = new Utf8JsonReader(payload);
            document = JsonDocument.ParseValue(ref reader);
            if (reader.Read())
                throw new JsonException("JSON payload contains trailing content.");
        }
        catch (JsonException exception)
        {
            document?.Dispose();
            throw new JsonSchemaValidationException(
                schemaId,
                "$parse",
                exception.Path ?? "$",
                $"JSON Schema validation failed for schema ID {schemaId} at '{exception.Path ?? "$"}' (keyword '$parse').",
                exception);
        }

        using (document)
        {
            var result = schema.Evaluate(document.RootElement, evaluationOptions);
            if (result.IsValid)
                return;

            var failure = FindFailure(result);
            var keyword = GetKeyword(failure?.Errors);
            var path = ToJsonPath(failure?.InstanceLocation.ToString(), document.RootElement);
            throw new JsonSchemaValidationException(
                schemaId,
                keyword,
                path,
                $"JSON Schema validation failed for schema ID {schemaId} at '{path}' (keyword '{keyword}').");
        }
    }

    private static EvaluationResults? FindFailure(EvaluationResults result)
    {
        if (result.Errors is { Count: > 0 })
            return result;

        var details = result.Details;
        if (details is null)
            return null;

        for (var i = 0; i < details.Count; i++)
        {
            var failure = FindFailure(details[i]);
            if (failure is not null)
                return failure;
        }

        return null;
    }

    private static string GetKeyword(Dictionary<string, string>? errors)
    {
        if (errors is not null)
        {
            foreach (var error in errors)
                return error.Key;
        }

        return "$schema";
    }

    private static string ToJsonPath(string? pointer, JsonElement root)
    {
        if (string.IsNullOrEmpty(pointer))
            return "$";

        var path = new StringBuilder("$");
        var current = root;
        var segmentStart = pointer[0] == '/' ? 1 : 0;
        while (segmentStart <= pointer.Length)
        {
            var separator = pointer.IndexOf('/', segmentStart);
            var segmentEnd = separator < 0 ? pointer.Length : separator;
            var segment = pointer[segmentStart..segmentEnd]
                .Replace("~1", "/", StringComparison.Ordinal)
                .Replace("~0", "~", StringComparison.Ordinal);

            if (current.ValueKind == JsonValueKind.Array &&
                int.TryParse(
                    segment,
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var index))
            {
                path.Append('[').Append(index).Append(']');
                current = index >= 0 && index < current.GetArrayLength()
                    ? current[index]
                    : default;
            }
            else
            {
                path.Append("['");
                AppendEscapedProperty(path, segment);
                path.Append("']");
                current = current.ValueKind == JsonValueKind.Object &&
                    current.TryGetProperty(segment, out var property)
                        ? property
                        : default;
            }

            if (separator < 0)
                break;
            segmentStart = separator + 1;
        }

        return path.ToString();
    }

    private static void AppendEscapedProperty(StringBuilder path, string property)
    {
        for (var i = 0; i < property.Length; i++)
        {
            var character = property[i];
            if (character is '\\' or '\'')
                path.Append('\\');
            path.Append(character);
        }
    }
}
