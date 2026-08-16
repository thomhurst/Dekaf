using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using RegistrySchema = Dekaf.SchemaRegistry.Schema;

namespace Dekaf.SchemaRegistry.Json;

/// <summary>
/// Configures compiled streaming JSON Schema validation.
/// </summary>
public sealed class StreamingJsonSchemaValidatorOptions
{
    /// <summary>Maximum time allowed to resolve Schema Registry references. Default is 30 seconds.</summary>
    public TimeSpan ReferenceResolutionTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Maximum compiled schema nesting depth. Default is 128.</summary>
    public int MaxSchemaDepth { get; init; } = 128;
}

/// <summary>
/// Compiles and weakly caches allocation-free streaming validators by Schema Registry schema identity.
/// </summary>
public sealed class StreamingJsonSchemaValidatorFactory : IJsonSchemaValidatorFactory
{
    private static readonly Uri RootRetrievalUri = new("https://schemas.dekaf.invalid/root.json");

    private readonly ISchemaRegistryClient _schemaRegistry;
    private readonly StreamingJsonSchemaValidatorOptions _options;
    private readonly ConditionalWeakTable<RegistrySchema, IJsonSchemaValidator> _validators = new();
    private readonly ConditionalWeakTable<RegistrySchema, IJsonSchemaValidator>.CreateValueCallback _createValidator;

    /// <summary>Creates a streaming validator factory.</summary>
    public StreamingJsonSchemaValidatorFactory(
        ISchemaRegistryClient schemaRegistry,
        StreamingJsonSchemaValidatorOptions? options = null)
    {
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _options = options ?? new StreamingJsonSchemaValidatorOptions();
        if (_options.ReferenceResolutionTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                _options.ReferenceResolutionTimeout,
                "Reference resolution timeout must be greater than zero.");
        }

        if (_options.MaxSchemaDepth is < 1 or > 512)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                _options.MaxSchemaDepth,
                "Maximum schema depth must be between 1 and 512.");
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
        using var compiler = new SchemaCompiler(_schemaRegistry, _options);
        return new StreamingJsonSchemaValidator(compiler.Compile(schema, RootRetrievalUri));
    }
}

internal sealed class StreamingJsonSchemaValidator(CompiledSchemaNode root) : IJsonSchemaValidator
{
    private const int MaxReferenceDepth = 128;

    public void Validate(ReadOnlySpan<byte> payload, int schemaId)
    {
        var path = new JsonPathBuilder();
        var reader = new Utf8JsonReader(payload, new JsonReaderOptions
        {
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 128
        });

        try
        {
            if (!reader.Read())
                ThrowFailure(schemaId, "$parse", "$", null);

            if (!ValidateNode(ref reader, root, ref path, schemaId, out var failure))
            {
                while (reader.Read())
                    reader.Skip();
                throw failure!;
            }

            if (reader.Read())
                ThrowFailure(schemaId, "$parse", "$", null);
        }
        catch (JsonException exception)
        {
            throw new JsonSchemaValidationException(
                schemaId,
                "$parse",
                exception.Path ?? "$",
                $"JSON Schema validation failed for schema ID {schemaId} at '{exception.Path ?? "$"}' (keyword '$parse').",
                exception);
        }
    }

    private static bool ValidateNode(
        ref Utf8JsonReader reader,
        CompiledSchemaNode node,
        scoped ref JsonPathBuilder path,
        int schemaId,
        out JsonSchemaValidationException? failure,
        int referenceDepth = 0)
    {
        if (node.IsFalse)
            return Fail(schemaId, "$schema", ref path, out failure);

        if (node.Reference is not null)
        {
            if (referenceDepth == MaxReferenceDepth)
                return Fail(schemaId, "$ref", ref path, out failure);

            var referencedReader = reader;
            if (!ValidateNode(
                    ref referencedReader,
                    node.Reference,
                    ref path,
                    schemaId,
                    out failure,
                    referenceDepth + 1))
                return false;

            if (!node.HasLocalAssertions)
            {
                reader = referencedReader;
                return true;
            }
        }

        if (!MatchesType(ref reader, node.Types))
            return Fail(schemaId, "type", ref path, out failure);

        switch (reader.TokenType)
        {
            case JsonTokenType.StartObject:
                return ValidateObject(ref reader, node, ref path, schemaId, out failure);
            case JsonTokenType.StartArray:
                return ValidateArray(ref reader, node, ref path, schemaId, out failure);
            case JsonTokenType.String:
                return ValidateString(ref reader, node, ref path, schemaId, out failure);
            case JsonTokenType.Number:
                return ValidateNumber(ref reader, node, ref path, schemaId, out failure);
            default:
                failure = null;
                return true;
        }
    }

    private static bool ValidateObject(
        ref Utf8JsonReader reader,
        CompiledSchemaNode node,
        scoped ref JsonPathBuilder path,
        int schemaId,
        out JsonSchemaValidationException? failure)
    {
        var requiredWordCount = (node.RequiredCount + 63) >> 6;
        Span<ulong> seenRequired = requiredWordCount == 0
            ? default
            : stackalloc ulong[requiredWordCount];
        seenRequired.Clear();
        var missingRequired = node.RequiredCount;
        var propertyCount = 0;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                if (missingRequired != 0)
                    return Fail(schemaId, "required", ref path, out failure);
                if (propertyCount < node.MinProperties)
                    return Fail(schemaId, "minProperties", ref path, out failure);
                if (propertyCount > node.MaxProperties)
                    return Fail(schemaId, "maxProperties", ref path, out failure);

                failure = null;
                return true;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
                return Fail(schemaId, "$parse", ref path, out failure);

            propertyCount++;
            var property = node.Properties?.Find(ref reader);
            var pathMark = path.Length;
            var pathAppended = property is not null ||
                !node.AllowsAdditionalProperties ||
                node.AdditionalProperties is not null;
            if (property is not null)
                path.AppendProperty(property.Name);
            else if (pathAppended)
                path.AppendProperty(ref reader);

            if (property is { RequiredIndex: >= 0 })
            {
                var word = property.RequiredIndex >> 6;
                var bit = 1UL << (property.RequiredIndex & 63);
                if ((seenRequired[word] & bit) == 0)
                {
                    seenRequired[word] |= bit;
                    missingRequired--;
                }
            }

            if (!reader.Read())
                return Fail(schemaId, "$parse", ref path, out failure);

            var propertySchema = property?.Schema ?? node.AdditionalProperties;
            if (property is null && !node.AllowsAdditionalProperties)
                return Fail(schemaId, "additionalProperties", ref path, out failure);

            if (propertySchema is not null)
            {
                if (!ValidateNode(ref reader, propertySchema, ref path, schemaId, out failure))
                    return false;
            }
            else
            {
                reader.Skip();
            }

            if (pathAppended)
                path.Truncate(pathMark);
        }

        return Fail(schemaId, "$parse", ref path, out failure);
    }

    private static bool ValidateArray(
        ref Utf8JsonReader reader,
        CompiledSchemaNode node,
        scoped ref JsonPathBuilder path,
        int schemaId,
        out JsonSchemaValidationException? failure)
    {
        var index = 0;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                if (index < node.MinItems)
                    return Fail(schemaId, "minItems", ref path, out failure);
                if (index > node.MaxItems)
                    return Fail(schemaId, "maxItems", ref path, out failure);

                failure = null;
                return true;
            }

            var itemSchema = index < node.PrefixItems.Length
                ? node.PrefixItems[index]
                : node.Items;
            var pathMark = path.Length;
            path.AppendIndex(index);
            if (itemSchema is not null)
            {
                if (!ValidateNode(ref reader, itemSchema, ref path, schemaId, out failure))
                    return false;
            }
            else
            {
                reader.Skip();
            }

            path.Truncate(pathMark);
            index++;
        }

        return Fail(schemaId, "$parse", ref path, out failure);
    }

    private static bool ValidateString(
        ref Utf8JsonReader reader,
        CompiledSchemaNode node,
        scoped ref JsonPathBuilder path,
        int schemaId,
        out JsonSchemaValidationException? failure)
    {
        if (node.MinLength == 0 && node.MaxLength == int.MaxValue)
        {
            failure = null;
            return true;
        }

        var length = GetStringScalarCount(ref reader);
        if (length < node.MinLength)
            return Fail(schemaId, "minLength", ref path, out failure);
        if (length > node.MaxLength)
            return Fail(schemaId, "maxLength", ref path, out failure);

        failure = null;
        return true;
    }

    private static bool ValidateNumber(
        ref Utf8JsonReader reader,
        CompiledSchemaNode node,
        scoped ref JsonPathBuilder path,
        int schemaId,
        out JsonSchemaValidationException? failure)
    {
        if (!node.HasNumericAssertions)
        {
            failure = null;
            return true;
        }

        var value = reader.GetDouble();
        if (value < node.Minimum || (node.ExclusiveMinimum && value == node.Minimum))
            return Fail(schemaId, node.ExclusiveMinimum ? "exclusiveMinimum" : "minimum", ref path, out failure);
        if (value > node.Maximum || (node.ExclusiveMaximum && value == node.Maximum))
            return Fail(schemaId, node.ExclusiveMaximum ? "exclusiveMaximum" : "maximum", ref path, out failure);

        if (node.MultipleOf > 0)
        {
            var quotient = value / node.MultipleOf;
            if (Math.Abs(quotient - Math.Round(quotient)) > Math.Abs(quotient) * 1e-12)
                return Fail(schemaId, "multipleOf", ref path, out failure);
        }

        failure = null;
        return true;
    }

    private static bool MatchesType(ref Utf8JsonReader reader, JsonSchemaType allowed)
    {
        if (allowed == JsonSchemaType.Any)
            return true;

        return reader.TokenType switch
        {
            JsonTokenType.StartObject => (allowed & JsonSchemaType.Object) != 0,
            JsonTokenType.StartArray => (allowed & JsonSchemaType.Array) != 0,
            JsonTokenType.String => (allowed & JsonSchemaType.String) != 0,
            JsonTokenType.True or JsonTokenType.False => (allowed & JsonSchemaType.Boolean) != 0,
            JsonTokenType.Null => (allowed & JsonSchemaType.Null) != 0,
            JsonTokenType.Number => (allowed & JsonSchemaType.Number) != 0 ||
                ((allowed & JsonSchemaType.Integer) != 0 && IsInteger(ref reader)),
            _ => false
        };
    }

    private static bool IsInteger(ref Utf8JsonReader reader)
    {
        if (reader.TryGetDecimal(out var decimalValue))
            return decimal.Truncate(decimalValue) == decimalValue;

        var value = reader.GetDouble();
        return double.IsFinite(value) && Math.Truncate(value) == value;
    }

    private static int GetStringScalarCount(ref Utf8JsonReader reader)
    {
        if (!reader.ValueIsEscaped)
            return CountUtf8Scalars(reader.ValueSpan);

        var maximumLength = reader.ValueSpan.Length;
        byte[]? rented = null;
        Span<byte> decoded = maximumLength <= 512
            ? stackalloc byte[maximumLength]
            : (rented = ArrayPool<byte>.Shared.Rent(maximumLength));
        try
        {
            var written = reader.CopyString(decoded);
            return CountUtf8Scalars(decoded[..written]);
        }
        finally
        {
            if (rented is not null)
                ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static int CountUtf8Scalars(ReadOnlySpan<byte> value)
    {
        var count = 0;
        while (!value.IsEmpty)
        {
            var status = Rune.DecodeFromUtf8(value, out _, out var consumed);
            if (status != OperationStatus.Done)
                throw new JsonException("Invalid UTF-8 JSON string.");
            value = value[consumed..];
            count++;
        }

        return count;
    }

    private static bool Fail(
        int schemaId,
        string keyword,
        scoped ref JsonPathBuilder path,
        out JsonSchemaValidationException? failure)
    {
        var jsonPath = path.ToString();
        failure = new JsonSchemaValidationException(
            schemaId,
            keyword,
            jsonPath,
            $"JSON Schema validation failed for schema ID {schemaId} at '{jsonPath}' (keyword '{keyword}').");
        return false;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowFailure(int schemaId, string keyword, string path, Exception? innerException)
    {
        throw new JsonSchemaValidationException(
            schemaId,
            keyword,
            path,
            $"JSON Schema validation failed for schema ID {schemaId} at '{path}' (keyword '{keyword}').",
            innerException);
    }
}

internal sealed class SchemaCompiler : IDisposable
{
    private const int MaxRequiredProperties = 4_096;

    private static readonly HashSet<string> UnsupportedAssertions = new(StringComparer.Ordinal)
    {
        "allOf", "anyOf", "oneOf", "not", "if", "then", "else", "contains", "minContains",
        "maxContains", "uniqueItems", "pattern", "patternProperties", "propertyNames",
        "additionalItems", "dependentSchemas", "dependentRequired", "dependencies", "unevaluatedItems",
        "unevaluatedProperties", "enum", "const"
    };

    private readonly ISchemaRegistryClient _schemaRegistry;
    private readonly StreamingJsonSchemaValidatorOptions _options;
    private readonly List<JsonDocument> _documents = [];
    private readonly Dictionary<string, SchemaDocument> _documentsByUri = new(StringComparer.Ordinal);
    private readonly Dictionary<NodeKey, CompiledSchemaNode> _compiledNodes = [];
    private int _nextDocumentId;

    internal SchemaCompiler(
        ISchemaRegistryClient schemaRegistry,
        StreamingJsonSchemaValidatorOptions options)
    {
        _schemaRegistry = schemaRegistry;
        _options = options;
    }

    internal CompiledSchemaNode Compile(RegistrySchema schema, Uri retrievalUri)
    {
        var root = AddDocument(schema, retrievalUri);
        var visited = new HashSet<SchemaReferenceKey>();
        RegisterReferences(root, schema, visited);
        return CompileNode(root, root.Document.RootElement, string.Empty, root.EffectiveBaseUri, 0);
    }

    public void Dispose()
    {
        for (var i = 0; i < _documents.Count; i++)
            _documents[i].Dispose();
    }

    private SchemaDocument AddDocument(RegistrySchema schema, Uri retrievalUri)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(schema.SchemaString);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("Registered JSON Schema is not valid JSON.", exception);
        }

        _documents.Add(document);
        var effectiveBaseUri = GetEffectiveBaseUri(document.RootElement, retrievalUri);
        var schemaDocument = new SchemaDocument(_nextDocumentId++, document, effectiveBaseUri);
        _documentsByUri[WithoutFragment(retrievalUri).AbsoluteUri] = schemaDocument;
        _documentsByUri[WithoutFragment(effectiveBaseUri).AbsoluteUri] = schemaDocument;
        return schemaDocument;
    }

    private void RegisterReferences(
        SchemaDocument parent,
        RegistrySchema schema,
        HashSet<SchemaReferenceKey> visited)
    {
        var references = schema.References;
        if (references is null)
            return;

        for (var i = 0; i < references.Count; i++)
        {
            var reference = references[i];
            var referenceUri = ResolveUri(parent.EffectiveBaseUri, reference.Name);
            var key = new SchemaReferenceKey(reference.Subject, reference.Version, referenceUri.AbsoluteUri);
            if (!visited.Add(key))
                continue;

            var registered = ResolveReference(reference);
            if (registered.Schema.SchemaType != SchemaType.Json)
            {
                throw new InvalidOperationException(
                    $"JSON Schema reference '{reference.Name}' resolved to {registered.Schema.SchemaType}.");
            }

            var child = AddDocument(registered.Schema, referenceUri);
            RegisterReferences(child, registered.Schema, visited);
        }
    }

    private RegisteredSchema ResolveReference(SchemaReference reference)
    {
        using var timeoutSource = new CancellationTokenSource(_options.ReferenceResolutionTimeout);
        try
        {
            return _schemaRegistry.GetSchemaBySubjectAsync(
                    reference.Subject,
                    reference.Version.ToString(CultureInfo.InvariantCulture),
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

    private CompiledSchemaNode CompileNode(
        SchemaDocument document,
        JsonElement schema,
        string pointer,
        Uri baseUri,
        int depth)
    {
        if (depth > _options.MaxSchemaDepth)
            throw new InvalidOperationException($"JSON Schema exceeds maximum depth {_options.MaxSchemaDepth}.");

        var key = new NodeKey(document.Id, pointer);
        if (_compiledNodes.TryGetValue(key, out var existing))
            return existing;

        var node = new CompiledSchemaNode();
        _compiledNodes.Add(key, node);

        if (schema.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            node.IsFalse = schema.ValueKind == JsonValueKind.False;
            return node;
        }

        if (schema.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException($"JSON Schema at '{pointer}' must be an object or boolean.");

        baseUri = GetEffectiveBaseUri(schema, baseUri);
        if (schema.TryGetProperty("$ref", out var referenceElement))
        {
            var referenceText = referenceElement.GetString()
                ?? throw new InvalidOperationException("JSON Schema $ref must be a string.");
            var target = ResolveTarget(baseUri, referenceText);
            node.Reference = CompileNode(
                target.Document,
                target.Element,
                target.Pointer,
                target.BaseUri,
                depth + 1);
        }

        node.Types = ParseTypes(schema);
        node.MinProperties = GetNonNegativeInt32(schema, "minProperties", 0);
        node.MaxProperties = GetNonNegativeInt32(schema, "maxProperties", int.MaxValue);
        node.MinItems = GetNonNegativeInt32(schema, "minItems", 0);
        node.MaxItems = GetNonNegativeInt32(schema, "maxItems", int.MaxValue);
        node.MinLength = GetNonNegativeInt32(schema, "minLength", 0);
        node.MaxLength = GetNonNegativeInt32(schema, "maxLength", int.MaxValue);
        ParseNumericAssertions(schema, node);
        CompileObjectKeywords(document, schema, pointer, baseUri, depth, node);
        CompileArrayKeywords(document, schema, pointer, baseUri, depth, node);
        RejectUnsupportedAssertions(schema, pointer);
        node.HasLocalAssertions = HasLocalAssertions(node);
        return node;
    }

    private void CompileObjectKeywords(
        SchemaDocument document,
        JsonElement schema,
        string pointer,
        Uri baseUri,
        int depth,
        CompiledSchemaNode node)
    {
        HashSet<string>? required = null;
        if (schema.TryGetProperty("required", out var requiredElement))
        {
            if (requiredElement.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException($"JSON Schema 'required' at '{pointer}' must be an array.");

            required = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in requiredElement.EnumerateArray())
            {
                var name = item.GetString()
                    ?? throw new InvalidOperationException("JSON Schema required names must be strings.");
                if (!required.Add(name))
                    throw new InvalidOperationException($"JSON Schema required name '{name}' is duplicated.");
                if (required.Count > MaxRequiredProperties)
                {
                    throw new NotSupportedException(
                        $"JSON Schema has more than {MaxRequiredProperties} required properties.");
                }
            }
        }

        var entries = new List<CompiledProperty>();
        if (schema.TryGetProperty("properties", out var propertiesElement))
        {
            if (propertiesElement.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException($"JSON Schema 'properties' at '{pointer}' must be an object.");

            foreach (var property in propertiesElement.EnumerateObject())
            {
                var propertyPointer = AppendPointer(AppendPointer(pointer, "properties"), property.Name);
                entries.Add(new CompiledProperty(
                    property.Name,
                    CompileNode(document, property.Value, propertyPointer, baseUri, depth + 1)));
            }
        }

        if (required is not null)
        {
            foreach (var name in required)
            {
                var found = false;
                for (var i = 0; i < entries.Count; i++)
                {
                    if (entries[i].Name == name)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                    entries.Add(new CompiledProperty(name, null));
            }

            var requiredIndex = 0;
            for (var i = 0; i < entries.Count; i++)
            {
                if (required.Contains(entries[i].Name))
                    entries[i].RequiredIndex = requiredIndex++;
            }

            node.RequiredCount = requiredIndex;
        }

        if (entries.Count != 0)
            node.Properties = new CompiledPropertyTable(entries);

        if (!schema.TryGetProperty("additionalProperties", out var additionalProperties))
            return;

        if (additionalProperties.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            node.AllowsAdditionalProperties = additionalProperties.ValueKind == JsonValueKind.True;
            return;
        }

        node.AdditionalProperties = CompileNode(
            document,
            additionalProperties,
            AppendPointer(pointer, "additionalProperties"),
            baseUri,
            depth + 1);
    }

    private void CompileArrayKeywords(
        SchemaDocument document,
        JsonElement schema,
        string pointer,
        Uri baseUri,
        int depth,
        CompiledSchemaNode node)
    {
        if (schema.TryGetProperty("prefixItems", out var prefixItems))
        {
            if (prefixItems.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException($"JSON Schema 'prefixItems' at '{pointer}' must be an array.");

            var prefixNodes = new CompiledSchemaNode[prefixItems.GetArrayLength()];
            var index = 0;
            foreach (var item in prefixItems.EnumerateArray())
            {
                prefixNodes[index] = CompileNode(
                    document,
                    item,
                    AppendPointer(AppendPointer(pointer, "prefixItems"), index.ToString(CultureInfo.InvariantCulture)),
                    baseUri,
                    depth + 1);
                index++;
            }

            node.PrefixItems = prefixNodes;
        }

        if (schema.TryGetProperty("items", out var items))
        {
            if (items.ValueKind == JsonValueKind.Array)
            {
                var itemNodes = new CompiledSchemaNode[items.GetArrayLength()];
                var index = 0;
                foreach (var item in items.EnumerateArray())
                {
                    itemNodes[index] = CompileNode(
                        document,
                        item,
                        AppendPointer(AppendPointer(pointer, "items"), index.ToString(CultureInfo.InvariantCulture)),
                        baseUri,
                        depth + 1);
                    index++;
                }

                node.PrefixItems = itemNodes;
            }
            else
            {
                node.Items = CompileNode(document, items, AppendPointer(pointer, "items"), baseUri, depth + 1);
            }
        }
    }

    private ReferenceTarget ResolveTarget(Uri baseUri, string reference)
    {
        var targetUri = ResolveUri(baseUri, reference);
        if (!_documentsByUri.TryGetValue(WithoutFragment(targetUri).AbsoluteUri, out var document))
            throw new InvalidOperationException($"JSON Schema reference '{reference}' was not registered.");

        var pointer = Uri.UnescapeDataString(targetUri.Fragment);
        if (pointer.Length == 0)
            return new ReferenceTarget(document, document.Document.RootElement, string.Empty, document.EffectiveBaseUri);
        if (pointer[0] != '#')
            throw new InvalidOperationException($"JSON Schema reference '{reference}' has an invalid fragment.");
        pointer = pointer[1..];
        if (pointer.Length == 0)
            return new ReferenceTarget(document, document.Document.RootElement, string.Empty, document.EffectiveBaseUri);
        if (pointer[0] != '/')
            throw new NotSupportedException($"JSON Schema anchor reference '{reference}' is not supported.");

        var current = document.Document.RootElement;
        var currentBase = document.EffectiveBaseUri;
        var position = 1;
        while (position <= pointer.Length)
        {
            var separator = pointer.IndexOf('/', position);
            var end = separator < 0 ? pointer.Length : separator;
            var segment = pointer[position..end]
                .Replace("~1", "/", StringComparison.Ordinal)
                .Replace("~0", "~", StringComparison.Ordinal);
            if (current.ValueKind == JsonValueKind.Object)
            {
                if (!current.TryGetProperty(segment, out current))
                    throw new InvalidOperationException($"JSON Schema reference '{reference}' was not found.");
            }
            else if (current.ValueKind == JsonValueKind.Array &&
                     int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out var index) &&
                     index >= 0 && index < current.GetArrayLength())
            {
                current = current[index];
            }
            else
            {
                throw new InvalidOperationException($"JSON Schema reference '{reference}' was not found.");
            }

            currentBase = GetEffectiveBaseUri(current, currentBase);
            if (separator < 0)
                break;
            position = separator + 1;
        }

        return new ReferenceTarget(document, current, pointer, currentBase);
    }

    private static JsonSchemaType ParseTypes(JsonElement schema)
    {
        if (!schema.TryGetProperty("type", out var typeElement))
            return JsonSchemaType.Any;

        if (typeElement.ValueKind == JsonValueKind.String)
            return ParseType(typeElement.GetString()!);
        if (typeElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("JSON Schema 'type' must be a string or array.");

        var types = JsonSchemaType.None;
        foreach (var item in typeElement.EnumerateArray())
            types |= ParseType(item.GetString() ?? throw new InvalidOperationException("JSON Schema types must be strings."));
        return types;
    }

    private static JsonSchemaType ParseType(string type) => type switch
    {
        "object" => JsonSchemaType.Object,
        "array" => JsonSchemaType.Array,
        "string" => JsonSchemaType.String,
        "number" => JsonSchemaType.Number,
        "integer" => JsonSchemaType.Integer,
        "boolean" => JsonSchemaType.Boolean,
        "null" => JsonSchemaType.Null,
        _ => throw new InvalidOperationException($"Unknown JSON Schema type '{type}'.")
    };

    private static void ParseNumericAssertions(JsonElement schema, CompiledSchemaNode node)
    {
        if (schema.TryGetProperty("minimum", out var minimum))
            node.Minimum = minimum.GetDouble();
        if (schema.TryGetProperty("maximum", out var maximum))
            node.Maximum = maximum.GetDouble();
        if (schema.TryGetProperty("exclusiveMinimum", out var exclusiveMinimum))
        {
            if (exclusiveMinimum.ValueKind == JsonValueKind.Number)
            {
                node.Minimum = exclusiveMinimum.GetDouble();
                node.ExclusiveMinimum = true;
            }
            else if (exclusiveMinimum.ValueKind == JsonValueKind.True)
            {
                node.ExclusiveMinimum = true;
            }
        }
        if (schema.TryGetProperty("exclusiveMaximum", out var exclusiveMaximum))
        {
            if (exclusiveMaximum.ValueKind == JsonValueKind.Number)
            {
                node.Maximum = exclusiveMaximum.GetDouble();
                node.ExclusiveMaximum = true;
            }
            else if (exclusiveMaximum.ValueKind == JsonValueKind.True)
            {
                node.ExclusiveMaximum = true;
            }
        }
        if (schema.TryGetProperty("multipleOf", out var multipleOf))
        {
            node.MultipleOf = multipleOf.GetDouble();
            if (node.MultipleOf <= 0)
                throw new InvalidOperationException("JSON Schema 'multipleOf' must be greater than zero.");
        }

        node.HasNumericAssertions = !double.IsNegativeInfinity(node.Minimum) ||
            !double.IsPositiveInfinity(node.Maximum) || node.MultipleOf > 0;
    }

    private static void RejectUnsupportedAssertions(JsonElement schema, string pointer)
    {
        foreach (var property in schema.EnumerateObject())
        {
            if (UnsupportedAssertions.Contains(property.Name))
            {
                throw new NotSupportedException(
                    $"JSON Schema assertion '{property.Name}' at '{pointer}' is not supported by the streaming validator.");
            }
        }
    }

    private static bool HasLocalAssertions(CompiledSchemaNode node) =>
        node.Types != JsonSchemaType.Any ||
        node.Properties is not null ||
        node.RequiredCount != 0 ||
        !node.AllowsAdditionalProperties ||
        node.AdditionalProperties is not null ||
        node.Items is not null ||
        node.PrefixItems.Length != 0 ||
        node.MinProperties != 0 || node.MaxProperties != int.MaxValue ||
        node.MinItems != 0 || node.MaxItems != int.MaxValue ||
        node.MinLength != 0 || node.MaxLength != int.MaxValue ||
        node.HasNumericAssertions;

    private static int GetNonNegativeInt32(JsonElement schema, string propertyName, int defaultValue)
    {
        if (!schema.TryGetProperty(propertyName, out var value))
            return defaultValue;
        if (!value.TryGetInt32(out var result) || result < 0)
            throw new InvalidOperationException($"JSON Schema '{propertyName}' must be a non-negative integer.");
        return result;
    }

    private static Uri GetEffectiveBaseUri(JsonElement schema, Uri baseUri)
    {
        if (schema.ValueKind == JsonValueKind.Object && schema.TryGetProperty("$id", out var id))
        {
            var idText = id.GetString() ?? throw new InvalidOperationException("JSON Schema $id must be a string.");
            return ResolveUri(baseUri, idText);
        }

        return baseUri;
    }

    private static Uri ResolveUri(Uri baseUri, string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute))
            return absolute;
        return new Uri(baseUri, value);
    }

    private static Uri WithoutFragment(Uri uri)
    {
        if (string.IsNullOrEmpty(uri.Fragment))
            return uri;
        var builder = new UriBuilder(uri) { Fragment = string.Empty };
        return builder.Uri;
    }

    private static string AppendPointer(string pointer, string segment) =>
        $"{pointer}/{segment.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal)}";

    private sealed record SchemaDocument(int Id, JsonDocument Document, Uri EffectiveBaseUri);
    private readonly record struct SchemaReferenceKey(string Subject, int Version, string Uri);
    private readonly record struct NodeKey(int DocumentId, string Pointer);
    private readonly record struct ReferenceTarget(
        SchemaDocument Document,
        JsonElement Element,
        string Pointer,
        Uri BaseUri);
}

[Flags]
internal enum JsonSchemaType : byte
{
    None = 0,
    Object = 1,
    Array = 2,
    String = 4,
    Number = 8,
    Integer = 16,
    Boolean = 32,
    Null = 64,
    Any = Object | Array | String | Number | Integer | Boolean | Null
}

internal sealed class CompiledSchemaNode
{
    internal bool IsFalse { get; set; }
    internal bool HasLocalAssertions { get; set; }
    internal JsonSchemaType Types { get; set; } = JsonSchemaType.Any;
    internal CompiledSchemaNode? Reference { get; set; }
    internal CompiledPropertyTable? Properties { get; set; }
    internal int RequiredCount { get; set; }
    internal bool AllowsAdditionalProperties { get; set; } = true;
    internal CompiledSchemaNode? AdditionalProperties { get; set; }
    internal CompiledSchemaNode? Items { get; set; }
    internal CompiledSchemaNode[] PrefixItems { get; set; } = [];
    internal int MinProperties { get; set; }
    internal int MaxProperties { get; set; } = int.MaxValue;
    internal int MinItems { get; set; }
    internal int MaxItems { get; set; } = int.MaxValue;
    internal int MinLength { get; set; }
    internal int MaxLength { get; set; } = int.MaxValue;
    internal double Minimum { get; set; } = double.NegativeInfinity;
    internal double Maximum { get; set; } = double.PositiveInfinity;
    internal bool ExclusiveMinimum { get; set; }
    internal bool ExclusiveMaximum { get; set; }
    internal double MultipleOf { get; set; }
    internal bool HasNumericAssertions { get; set; }
}

internal sealed class CompiledProperty
{
    internal CompiledProperty(string name, CompiledSchemaNode? schema)
    {
        Name = name;
        Schema = schema;
        Utf8Name = Encoding.UTF8.GetBytes(name);
        Hash = CompiledPropertyTable.Hash(Utf8Name);
    }

    internal string Name { get; }
    internal byte[] Utf8Name { get; }
    internal uint Hash { get; }
    internal CompiledSchemaNode? Schema { get; }
    internal int RequiredIndex { get; set; } = -1;
}

internal sealed class CompiledPropertyTable
{
    private readonly CompiledProperty[] _properties;
    private readonly int[] _buckets;

    internal CompiledPropertyTable(List<CompiledProperty> properties)
    {
        _properties = [.. properties];
        var capacity = 4;
        while (capacity < properties.Count * 2)
            capacity <<= 1;
        _buckets = new int[capacity];
        for (var i = 0; i < _properties.Length; i++)
        {
            var bucket = (int)(_properties[i].Hash & (uint)(capacity - 1));
            while (_buckets[bucket] != 0)
                bucket = (bucket + 1) & (capacity - 1);
            _buckets[bucket] = i + 1;
        }
    }

    internal CompiledProperty? Find(ref Utf8JsonReader reader)
    {
        if (!reader.ValueIsEscaped)
            return Find(reader.ValueSpan);

        var maximumLength = reader.ValueSpan.Length;
        byte[]? rented = null;
        Span<byte> decoded = maximumLength <= 256
            ? stackalloc byte[maximumLength]
            : (rented = ArrayPool<byte>.Shared.Rent(maximumLength));
        try
        {
            var written = reader.CopyString(decoded);
            return Find(decoded[..written]);
        }
        finally
        {
            if (rented is not null)
                ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private CompiledProperty? Find(ReadOnlySpan<byte> name)
    {
        var hash = Hash(name);
        var bucket = (int)(hash & (uint)(_buckets.Length - 1));
        while (true)
        {
            var stored = _buckets[bucket];
            if (stored == 0)
                return null;
            var property = _properties[stored - 1];
            if (property.Hash == hash && name.SequenceEqual(property.Utf8Name))
                return property;
            bucket = (bucket + 1) & (_buckets.Length - 1);
        }
    }

    internal static uint Hash(ReadOnlySpan<byte> value)
    {
        const uint offset = 2166136261;
        const uint prime = 16777619;
        var hash = offset;
        for (var i = 0; i < value.Length; i++)
            hash = (hash ^ value[i]) * prime;
        return hash;
    }
}

internal struct JsonPathBuilder
{
    [ThreadStatic]
    private static char[]? t_buffer;

    private char[] _buffer;

    public JsonPathBuilder()
    {
        _buffer = t_buffer ??= new char[256];
        Length = 1;
        _buffer[0] = '$';
    }

    internal int Length { get; private set; }

    internal void AppendProperty(string name)
    {
        EnsureCapacity(name.Length + 4);
        _buffer[Length++] = '[';
        _buffer[Length++] = '\'';
        for (var i = 0; i < name.Length; i++)
        {
            var character = name[i];
            if (character is '\\' or '\'')
            {
                EnsureCapacity(1);
                _buffer[Length++] = '\\';
            }
            EnsureCapacity(1);
            _buffer[Length++] = character;
        }
        EnsureCapacity(2);
        _buffer[Length++] = '\'';
        _buffer[Length++] = ']';
    }

    internal void AppendProperty(ref Utf8JsonReader reader)
    {
        var maximumLength = reader.ValueSpan.Length;
        EnsureCapacity((maximumLength * 2) + 4);
        _buffer[Length++] = '[';
        _buffer[Length++] = '\'';
        var contentStart = Length;
        var written = reader.CopyString(_buffer.AsSpan(contentStart, maximumLength));
        var escapes = 0;
        for (var i = 0; i < written; i++)
        {
            if (_buffer[contentStart + i] is '\\' or '\'')
                escapes++;
        }

        var destination = contentStart + written + escapes - 1;
        for (var source = contentStart + written - 1; source >= contentStart; source--)
        {
            var character = _buffer[source];
            _buffer[destination--] = character;
            if (character is '\\' or '\'')
                _buffer[destination--] = '\\';
        }

        Length = contentStart + written + escapes;
        _buffer[Length++] = '\'';
        _buffer[Length++] = ']';
    }

    internal void AppendIndex(int index)
    {
        EnsureCapacity(13);
        _buffer[Length++] = '[';
        _ = index.TryFormat(_buffer.AsSpan(Length), out var written, provider: CultureInfo.InvariantCulture);
        Length += written;
        _buffer[Length++] = ']';
    }

    internal void Truncate(int length) => Length = length;

    public override string ToString() => new(_buffer, 0, Length);

    private void EnsureCapacity(int additionalLength)
    {
        if (Length + additionalLength <= _buffer.Length)
            return;

        var expanded = new char[Math.Max(Length + additionalLength, _buffer.Length * 2)];
        _buffer.AsSpan(0, Length).CopyTo(expanded);
        _buffer = expanded;
        t_buffer = expanded;
    }
}
