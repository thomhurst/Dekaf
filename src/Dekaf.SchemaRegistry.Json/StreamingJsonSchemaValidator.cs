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
    private const int SuppressFailureSchemaId = int.MinValue;

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

    public void ValidateRules(ReadOnlyMemory<byte> payload, int schemaId, bool failFast)
    {
        var reader = new Utf8JsonReader(payload.Span, new JsonReaderOptions
        {
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 128
        });
        if (!reader.Read())
            return;

        var path = new ValidationPathBuilder();
        List<ValidationRuleError>? violations = null;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        WalkValidationRules(
            ref reader,
            payload,
            root,
            ref path,
            now,
            failFast,
            ref violations);
        if (violations is not null)
            throw new ValidationRulesFailedException(violations);
    }

    private static bool WalkValidationRules(
        ref Utf8JsonReader reader,
        ReadOnlyMemory<byte> payload,
        CompiledSchemaNode node,
        scoped ref ValidationPathBuilder path,
        long now,
        bool failFast,
        ref List<ValidationRuleError>? violations,
        int referenceDepth = 0)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return true;

        var rules = node.ValidationRules;
        if (rules.Length != 0)
        {
            var value = GetCurrentValue(ref reader, payload);
            var memberCount = node.ValidationRuleMembers?.Count ?? 0;
            var memberValues = memberCount == 0
                ? null
                : CompiledValidationRule.GetMemberValues(memberCount);
            try
            {
                if (memberValues is not null)
                    node.ValidationRuleMembers!.Resolve(value, memberValues);

                for (var index = 0; index < rules.Length; index++)
                {
                    var compiledRule = rules[index];
                    try
                    {
                        var result = compiledRule.Evaluate(value, now, memberValues);
                        if (result.Kind == ValidationResultKind.Boolean ? !result.Boolean : result.String!.Length != 0)
                        {
                            (violations ??= []).Add(new ValidationRuleError(
                                compiledRule.Rule,
                                path.ToString(),
                                result.Kind == ValidationResultKind.String ? result.String : null));
                            if (failFast)
                                return false;
                        }
                    }
                    catch (SchemaRegistryRuleException exception)
                    {
                        (violations ??= []).Add(new ValidationRuleError(
                            compiledRule.Rule,
                            path.ToString(),
                            cause: exception));
                        if (failFast)
                            return false;
                    }
                }
            }
            finally
            {
                if (memberValues is not null)
                    Array.Clear(memberValues, 0, memberCount);
            }
        }

        if (node.Reference is not null)
        {
            if (referenceDepth == MaxReferenceDepth)
                throw new SchemaRegistryRuleException("Inline validation reference depth exceeded 128.");
            var referencedReader = reader;
            if (!WalkValidationRules(
                    ref referencedReader,
                    payload,
                    node.Reference,
                    ref path,
                    now,
                    failFast,
                    ref violations,
                    referenceDepth + 1))
                return false;
            if (!node.HasLocalValidationTraversal)
            {
                reader = referencedReader;
                return true;
            }
        }

        for (var index = 0; index < node.AllOf.Length; index++)
        {
            var branchReader = reader;
            if (!WalkValidationRules(
                    ref branchReader,
                    payload,
                    node.AllOf[index],
                    ref path,
                    now,
                    failFast,
                    ref violations,
                    referenceDepth))
                return false;
        }

        if (!WalkMatchingBranches(
                ref reader,
                payload,
                node.AnyOf,
                oneOnly: false,
                ref path,
                now,
                failFast,
                ref violations,
                referenceDepth))
            return false;
        if (!WalkMatchingBranches(
                ref reader,
                payload,
                node.OneOf,
                oneOnly: true,
                ref path,
                now,
                failFast,
                ref violations,
                referenceDepth))
            return false;

        return reader.TokenType switch
        {
            JsonTokenType.StartObject => WalkValidationObject(
                ref reader, payload, node, ref path, now, failFast, ref violations, referenceDepth),
            JsonTokenType.StartArray => WalkValidationArray(
                ref reader, payload, node, ref path, now, failFast, ref violations, referenceDepth),
            _ => true
        };
    }

    private static bool WalkMatchingBranches(
        ref Utf8JsonReader reader,
        ReadOnlyMemory<byte> payload,
        CompiledSchemaNode[] branches,
        bool oneOnly,
        scoped ref ValidationPathBuilder path,
        long now,
        bool failFast,
        ref List<ValidationRuleError>? violations,
        int referenceDepth)
    {
        for (var index = 0; index < branches.Length; index++)
        {
            var branchReader = reader;
            if (!MatchesValidationShape(ref branchReader, branches[index], referenceDepth))
                continue;
            branchReader = reader;
            if (!WalkValidationRules(
                    ref branchReader,
                    payload,
                    branches[index],
                    ref path,
                    now,
                    failFast,
                    ref violations,
                    referenceDepth))
                return false;
            if (oneOnly)
                break;
        }

        return true;
    }

    private static bool MatchesValidationShape(
        ref Utf8JsonReader reader,
        CompiledSchemaNode node,
        int referenceDepth)
    {
        var path = new JsonPathBuilder();
        return ValidateNode(
            ref reader,
            node,
            ref path,
            SuppressFailureSchemaId,
            out _,
            referenceDepth);
    }

    private static bool WalkValidationObject(
        ref Utf8JsonReader reader,
        ReadOnlyMemory<byte> payload,
        CompiledSchemaNode node,
        scoped ref ValidationPathBuilder path,
        long now,
        bool failFast,
        ref List<ValidationRuleError>? violations,
        int referenceDepth)
    {
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
                return true;
            var property = node.Properties?.Find(ref reader);
            var pathMark = path.Length;
            if (property is not null)
                path.AppendProperty(property.Name);
            else if (node.AdditionalProperties is not null)
                path.AppendMapKey(ref reader);
            if (!reader.Read())
                return true;

            var child = property?.IsDeclared == true ? property.Schema : node.AdditionalProperties;
            if (child is not null)
            {
                if (!WalkValidationRules(
                        ref reader,
                        payload,
                        child,
                        ref path,
                        now,
                        failFast,
                        ref violations,
                        referenceDepth))
                    return false;
            }
            else
            {
                reader.Skip();
            }
            path.Truncate(pathMark);
        }
        return true;
    }

    private static bool WalkValidationArray(
        ref Utf8JsonReader reader,
        ReadOnlyMemory<byte> payload,
        CompiledSchemaNode node,
        scoped ref ValidationPathBuilder path,
        long now,
        bool failFast,
        ref List<ValidationRuleError>? violations,
        int referenceDepth)
    {
        var index = 0;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            var child = index < node.PrefixItems.Length ? node.PrefixItems[index] : node.Items;
            var pathMark = path.Length;
            path.AppendIndex(index);
            if (child is not null)
            {
                if (!WalkValidationRules(
                        ref reader,
                        payload,
                        child,
                        ref path,
                        now,
                        failFast,
                        ref violations,
                        referenceDepth))
                    return false;
            }
            else
            {
                reader.Skip();
            }
            path.Truncate(pathMark);
            index++;
        }
        return true;
    }

    private static ReadOnlyMemory<byte> GetCurrentValue(
        ref Utf8JsonReader reader,
        ReadOnlyMemory<byte> payload)
    {
        var start = checked((int)reader.TokenStartIndex);
        var endReader = reader;
        endReader.Skip();
        return payload.Slice(start, checked((int)endReader.BytesConsumed) - start);
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

        for (var index = 0; index < node.AllOf.Length; index++)
        {
            var branchReader = reader;
            if (!ValidateNode(
                    ref branchReader,
                    node.AllOf[index],
                    ref path,
                    schemaId,
                    out failure,
                    referenceDepth))
                return false;
        }

        if (node.HasAnyOf)
        {
            var matched = false;
            var pathMark = path.Length;
            for (var index = 0; index < node.AnyOf.Length; index++)
            {
                var branchReader = reader;
                matched = ValidateNode(
                        ref branchReader,
                        node.AnyOf[index],
                        ref path,
                        SuppressFailureSchemaId,
                        out _,
                        referenceDepth);
                path.Truncate(pathMark);
                if (matched)
                    break;
            }
            if (!matched)
                return Fail(schemaId, "anyOf", ref path, out failure);
        }

        if (node.HasOneOf)
        {
            var matches = 0;
            var pathMark = path.Length;
            for (var index = 0; index < node.OneOf.Length; index++)
            {
                var branchReader = reader;
                var matched = ValidateNode(
                        ref branchReader,
                        node.OneOf[index],
                        ref path,
                        SuppressFailureSchemaId,
                        out _,
                        referenceDepth);
                path.Truncate(pathMark);
                if (matched && ++matches > 1)
                    break;
            }
            if (matches != 1)
                return Fail(schemaId, "oneOf", ref path, out failure);
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

            var declaredProperty = property?.IsDeclared == true;
            var propertySchema = declaredProperty ? property!.Schema : node.AdditionalProperties;
            if (!declaredProperty && !node.AllowsAdditionalProperties)
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

        var value = new JsonNumberView(reader.ValueSpan);
        if (node.Minimum is { } minimum)
        {
            var comparison = value.CompareTo(minimum);
            if (comparison < 0 || (node.ExclusiveMinimum && comparison == 0))
                return Fail(schemaId, node.ExclusiveMinimum ? "exclusiveMinimum" : "minimum", ref path, out failure);
        }

        if (node.Maximum is { } maximum)
        {
            var comparison = value.CompareTo(maximum);
            if (comparison > 0 || (node.ExclusiveMaximum && comparison == 0))
                return Fail(schemaId, node.ExclusiveMaximum ? "exclusiveMaximum" : "maximum", ref path, out failure);
        }

        if (node.MultipleOf is { } multipleOf && !value.IsMultipleOf(multipleOf))
            return Fail(schemaId, "multipleOf", ref path, out failure);

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
        var token = reader.ValueSpan;
        for (var i = 0; i < token.Length; i++)
        {
            if (token[i] is (byte)'.' or (byte)'e' or (byte)'E')
            {
                var value = new JsonNumberView(token);
                return value.Sign == 0 || value.Exponent >= 0;
            }
        }

        return true;
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
        if (schemaId == SuppressFailureSchemaId)
        {
            failure = null;
            return false;
        }

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
        "not", "if", "then", "else", "contains", "minContains",
        "maxContains", "uniqueItems", "pattern", "patternProperties", "propertyNames",
        "additionalItems", "dependentSchemas", "dependentRequired", "dependencies", "unevaluatedItems",
        "unevaluatedProperties", "enum", "const"
    };

    private readonly ISchemaRegistryClient _schemaRegistry;
    private readonly StreamingJsonSchemaValidatorOptions _options;
    private readonly List<JsonDocument> _documents = [];
    private readonly Dictionary<string, SchemaResource> _resourcesByUri = new(StringComparer.Ordinal);
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
        return CompileNode(
            root,
            root.Document.RootElement,
            string.Empty,
            root.EffectiveBaseUri,
            GetDialect(root.Document.RootElement, SchemaDialect.Draft7),
            0);
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
        var dialect = GetDialect(document.RootElement, SchemaDialect.Draft7);
        var root = new SchemaResource(
            schemaDocument,
            document.RootElement,
            string.Empty,
            effectiveBaseUri,
            dialect);
        _resourcesByUri[WithoutFragment(retrievalUri).AbsoluteUri] = root;
        IndexSchemaResources(schemaDocument, document.RootElement, string.Empty, retrievalUri, dialect);
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

    private void IndexSchemaResources(
        SchemaDocument document,
        JsonElement schema,
        string pointer,
        Uri baseUri,
        SchemaDialect dialect)
    {
        if (schema.ValueKind is JsonValueKind.True or JsonValueKind.False)
            return;
        if (schema.ValueKind != JsonValueKind.Object)
            return;

        var declaresResource = schema.TryGetProperty("$id", out _);
        baseUri = GetEffectiveBaseUri(schema, baseUri);
        dialect = GetDialect(schema, dialect);
        if (pointer.Length == 0 || declaresResource)
        {
            _resourcesByUri[WithoutFragment(baseUri).AbsoluteUri] = new SchemaResource(
                document,
                schema,
                pointer,
                baseUri,
                dialect);
        }

        IndexSchemaMap(document, schema, "$defs", pointer, baseUri, dialect);
        IndexSchemaMap(document, schema, "definitions", pointer, baseUri, dialect);
        IndexSchemaMap(document, schema, "properties", pointer, baseUri, dialect);
        IndexSchemaMap(document, schema, "patternProperties", pointer, baseUri, dialect);
        IndexSchemaMap(document, schema, "dependentSchemas", pointer, baseUri, dialect);

        IndexSchemaValue(document, schema, "additionalProperties", pointer, baseUri, dialect);
        IndexSchemaValue(document, schema, "unevaluatedProperties", pointer, baseUri, dialect);
        IndexSchemaValue(document, schema, "propertyNames", pointer, baseUri, dialect);
        IndexSchemaValue(document, schema, "contains", pointer, baseUri, dialect);
        IndexSchemaValue(document, schema, "not", pointer, baseUri, dialect);
        IndexSchemaValue(document, schema, "if", pointer, baseUri, dialect);
        IndexSchemaValue(document, schema, "then", pointer, baseUri, dialect);
        IndexSchemaValue(document, schema, "else", pointer, baseUri, dialect);
        IndexSchemaValue(document, schema, "items", pointer, baseUri, dialect);

        IndexSchemaArray(document, schema, "prefixItems", pointer, baseUri, dialect);
        IndexSchemaArray(document, schema, "allOf", pointer, baseUri, dialect);
        IndexSchemaArray(document, schema, "anyOf", pointer, baseUri, dialect);
        IndexSchemaArray(document, schema, "oneOf", pointer, baseUri, dialect);
    }

    private void IndexSchemaMap(
        SchemaDocument document,
        JsonElement schema,
        string keyword,
        string pointer,
        Uri baseUri,
        SchemaDialect dialect)
    {
        if (!schema.TryGetProperty(keyword, out var map) || map.ValueKind != JsonValueKind.Object)
            return;

        var mapPointer = AppendPointer(pointer, keyword);
        foreach (var property in map.EnumerateObject())
        {
            IndexSchemaResources(
                document,
                property.Value,
                AppendPointer(mapPointer, property.Name),
                baseUri,
                dialect);
        }
    }

    private void IndexSchemaValue(
        SchemaDocument document,
        JsonElement schema,
        string keyword,
        string pointer,
        Uri baseUri,
        SchemaDialect dialect)
    {
        if (!schema.TryGetProperty(keyword, out var value))
            return;

        if (value.ValueKind == JsonValueKind.Array)
        {
            IndexSchemaArray(document, schema, keyword, pointer, baseUri, dialect);
            return;
        }

        IndexSchemaResources(document, value, AppendPointer(pointer, keyword), baseUri, dialect);
    }

    private void IndexSchemaArray(
        SchemaDocument document,
        JsonElement schema,
        string keyword,
        string pointer,
        Uri baseUri,
        SchemaDialect dialect)
    {
        if (!schema.TryGetProperty(keyword, out var values) || values.ValueKind != JsonValueKind.Array)
            return;

        var arrayPointer = AppendPointer(pointer, keyword);
        var index = 0;
        foreach (var value in values.EnumerateArray())
        {
            IndexSchemaResources(
                document,
                value,
                AppendPointer(arrayPointer, index.ToString(CultureInfo.InvariantCulture)),
                baseUri,
                dialect);
            index++;
        }
    }

    private CompiledSchemaNode CompileNode(
        SchemaDocument document,
        JsonElement schema,
        string pointer,
        Uri baseUri,
        SchemaDialect dialect,
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

        node.ValidationRules = CompileValidationRules(schema, out var validationRuleMembers);
        node.ValidationRuleMembers = validationRuleMembers;
        baseUri = GetEffectiveBaseUri(schema, baseUri);
        dialect = GetDialect(schema, dialect);
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
                target.Dialect,
                depth + 1);

            if (dialect == SchemaDialect.Draft7)
                return node;
        }

        node.Types = ParseTypes(schema);
        node.MinProperties = GetNonNegativeInt32(schema, "minProperties", 0);
        node.MaxProperties = GetNonNegativeInt32(schema, "maxProperties", int.MaxValue);
        node.MinItems = GetNonNegativeInt32(schema, "minItems", 0);
        node.MaxItems = GetNonNegativeInt32(schema, "maxItems", int.MaxValue);
        node.MinLength = GetNonNegativeInt32(schema, "minLength", 0);
        node.MaxLength = GetNonNegativeInt32(schema, "maxLength", int.MaxValue);
        ParseNumericAssertions(schema, node);
        node.AllOf = CompileSchemaArray(document, schema, "allOf", pointer, baseUri, dialect, depth);
        node.HasAnyOf = schema.TryGetProperty("anyOf", out _);
        node.AnyOf = CompileSchemaArray(document, schema, "anyOf", pointer, baseUri, dialect, depth);
        node.HasOneOf = schema.TryGetProperty("oneOf", out _);
        node.OneOf = CompileSchemaArray(document, schema, "oneOf", pointer, baseUri, dialect, depth);
        CompileObjectKeywords(document, schema, pointer, baseUri, dialect, depth, node);
        CompileArrayKeywords(document, schema, pointer, baseUri, dialect, depth, node);
        RejectUnsupportedAssertions(schema, pointer);
        node.HasLocalAssertions = HasLocalAssertions(node);
        node.HasLocalValidationTraversal = node.ValidationRules.Length != 0 ||
            node.AllOf.Length != 0 || node.AnyOf.Length != 0 || node.OneOf.Length != 0 ||
            node.Properties is not null || node.AdditionalProperties is not null ||
            node.Items is not null || node.PrefixItems.Length != 0;
        return node;
    }

    private CompiledSchemaNode[] CompileSchemaArray(
        SchemaDocument document,
        JsonElement schema,
        string keyword,
        string pointer,
        Uri baseUri,
        SchemaDialect dialect,
        int depth)
    {
        if (!schema.TryGetProperty(keyword, out var elements))
            return [];
        if (elements.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException($"JSON Schema '{keyword}' at '{pointer}' must be an array.");

        var compiled = new List<CompiledSchemaNode>();
        var index = 0;
        foreach (var element in elements.EnumerateArray())
        {
            compiled.Add(CompileNode(
                document,
                element,
                AppendPointer(AppendPointer(pointer, keyword), index.ToString(CultureInfo.InvariantCulture)),
                baseUri,
                dialect,
                depth + 1));
            index++;
        }
        return [.. compiled];
    }

    private static CompiledValidationRule[] CompileValidationRules(
        JsonElement schema,
        out ValidationCelMemberTable? members)
    {
        members = null;
        if (!schema.TryGetProperty("confluent:rules", out var rules))
            return [];
        if (rules.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("JSON Schema 'confluent:rules' must be an array.");

        var compiled = new List<CompiledValidationRule>();
        var memberIndexes = new Dictionary<string, int>(StringComparer.Ordinal);
        var memberNames = new List<byte[]>();
        foreach (var element in rules.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("JSON Schema 'confluent:rules' entries must be objects.");
            var rule = new ValidationRule
            {
                Name = GetOptionalString(element, "name"),
                Doc = GetOptionalString(element, "doc"),
                Expr = GetOptionalString(element, "expr"),
                Sql = GetOptionalString(element, "sql")
            };
            compiled.Add(CompiledValidationRule.Compile(rule, memberIndexes, memberNames));
        }
        if (memberNames.Count != 0)
            members = new ValidationCelMemberTable([.. memberNames]);
        return [.. compiled];

        static string? GetOptionalString(JsonElement owner, string name) =>
            owner.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }

    private void CompileObjectKeywords(
        SchemaDocument document,
        JsonElement schema,
        string pointer,
        Uri baseUri,
        SchemaDialect dialect,
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
                    CompileNode(document, property.Value, propertyPointer, baseUri, dialect, depth + 1)));
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
                    entries.Add(new CompiledProperty(name, null, isDeclared: false));
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
            dialect,
            depth + 1);
    }

    private void CompileArrayKeywords(
        SchemaDocument document,
        JsonElement schema,
        string pointer,
        Uri baseUri,
        SchemaDialect dialect,
        int depth,
        CompiledSchemaNode node)
    {
        var hasPrefixItems = schema.TryGetProperty("prefixItems", out var prefixItems);
        var hasItems = schema.TryGetProperty("items", out var items);
        if (hasPrefixItems && hasItems && items.ValueKind == JsonValueKind.Array)
        {
            throw new NotSupportedException(
                $"JSON Schema at '{pointer}' cannot combine 'prefixItems' with array-form 'items'.");
        }

        if (hasPrefixItems && dialect == SchemaDialect.Draft202012)
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
                    dialect,
                    depth + 1);
                index++;
            }

            node.PrefixItems = prefixNodes;
        }

        if (hasItems)
        {
            if (items.ValueKind == JsonValueKind.Array)
            {
                if (dialect == SchemaDialect.Draft202012)
                {
                    throw new InvalidOperationException(
                        $"JSON Schema array-form 'items' at '{pointer}' is not valid in draft 2020-12.");
                }

                var itemNodes = new CompiledSchemaNode[items.GetArrayLength()];
                var index = 0;
                foreach (var item in items.EnumerateArray())
                {
                    itemNodes[index] = CompileNode(
                        document,
                        item,
                        AppendPointer(AppendPointer(pointer, "items"), index.ToString(CultureInfo.InvariantCulture)),
                        baseUri,
                        dialect,
                        depth + 1);
                    index++;
                }

                node.PrefixItems = itemNodes;
            }
            else
            {
                node.Items = CompileNode(document, items, AppendPointer(pointer, "items"), baseUri, dialect, depth + 1);
            }
        }
    }

    private ReferenceTarget ResolveTarget(Uri baseUri, string reference)
    {
        var targetUri = ResolveUri(baseUri, reference);
        if (!_resourcesByUri.TryGetValue(WithoutFragment(targetUri).AbsoluteUri, out var resource))
            throw new InvalidOperationException($"JSON Schema reference '{reference}' was not registered.");

        var pointer = Uri.UnescapeDataString(targetUri.Fragment);
        if (pointer.Length == 0)
            return new ReferenceTarget(
                resource.Document,
                resource.Element,
                resource.Pointer,
                resource.BaseUri,
                resource.Dialect);
        if (pointer[0] != '#')
            throw new InvalidOperationException($"JSON Schema reference '{reference}' has an invalid fragment.");
        pointer = pointer[1..];
        if (pointer.Length == 0)
            return new ReferenceTarget(
                resource.Document,
                resource.Element,
                resource.Pointer,
                resource.BaseUri,
                resource.Dialect);
        if (pointer[0] != '/')
            throw new NotSupportedException($"JSON Schema anchor reference '{reference}' is not supported.");

        var current = resource.Element;
        var currentBase = resource.BaseUri;
        var currentDialect = resource.Dialect;
        var absolutePointer = resource.Pointer;
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
            currentDialect = GetDialect(current, currentDialect);
            absolutePointer = AppendPointer(absolutePointer, segment);
            if (separator < 0)
                break;
            position = separator + 1;
        }

        return new ReferenceTarget(resource.Document, current, absolutePointer, currentBase, currentDialect);
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
            SetMinimum(node, CompiledJsonNumber.Parse(minimum), exclusive: false);
        if (schema.TryGetProperty("maximum", out var maximum))
            SetMaximum(node, CompiledJsonNumber.Parse(maximum), exclusive: false);
        if (schema.TryGetProperty("exclusiveMinimum", out var exclusiveMinimum))
        {
            if (exclusiveMinimum.ValueKind == JsonValueKind.Number)
            {
                SetMinimum(node, CompiledJsonNumber.Parse(exclusiveMinimum), exclusive: true);
            }
            else if (exclusiveMinimum.ValueKind == JsonValueKind.True && node.Minimum is not null)
            {
                node.ExclusiveMinimum = true;
            }
        }
        if (schema.TryGetProperty("exclusiveMaximum", out var exclusiveMaximum))
        {
            if (exclusiveMaximum.ValueKind == JsonValueKind.Number)
            {
                SetMaximum(node, CompiledJsonNumber.Parse(exclusiveMaximum), exclusive: true);
            }
            else if (exclusiveMaximum.ValueKind == JsonValueKind.True && node.Maximum is not null)
            {
                node.ExclusiveMaximum = true;
            }
        }
        if (schema.TryGetProperty("multipleOf", out var multipleOf))
        {
            var compiled = CompiledJsonNumber.Parse(multipleOf, requireCoefficient: true);
            if (compiled.Sign <= 0)
                throw new InvalidOperationException("JSON Schema 'multipleOf' must be greater than zero.");
            node.MultipleOf = compiled;
        }

        node.HasNumericAssertions = node.Minimum is not null ||
            node.Maximum is not null || node.MultipleOf is not null;
    }

    private static void SetMinimum(CompiledSchemaNode node, CompiledJsonNumber candidate, bool exclusive)
    {
        if (node.Minimum is not { } current)
        {
            node.Minimum = candidate;
            node.ExclusiveMinimum = exclusive;
            return;
        }

        var comparison = candidate.CompareTo(current);
        if (comparison > 0)
        {
            node.Minimum = candidate;
            node.ExclusiveMinimum = exclusive;
        }
        else if (comparison == 0)
        {
            node.ExclusiveMinimum |= exclusive;
        }
    }

    private static void SetMaximum(CompiledSchemaNode node, CompiledJsonNumber candidate, bool exclusive)
    {
        if (node.Maximum is not { } current)
        {
            node.Maximum = candidate;
            node.ExclusiveMaximum = exclusive;
            return;
        }

        var comparison = candidate.CompareTo(current);
        if (comparison < 0)
        {
            node.Maximum = candidate;
            node.ExclusiveMaximum = exclusive;
        }
        else if (comparison == 0)
        {
            node.ExclusiveMaximum |= exclusive;
        }
    }

    private static void RejectUnsupportedAssertions(JsonElement schema, string pointer)
    {
        foreach (var property in schema.EnumerateObject()
                     .Where(static property => UnsupportedAssertions.Contains(property.Name)))
        {
            throw new NotSupportedException(
                $"JSON Schema assertion '{property.Name}' at '{pointer}' is not supported by the streaming validator.");
        }
    }

    private static bool HasLocalAssertions(CompiledSchemaNode node) =>
        node.Types != JsonSchemaType.Any ||
        node.AllOf.Length != 0 ||
        node.HasAnyOf ||
        node.HasOneOf ||
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

    private static SchemaDialect GetDialect(JsonElement schema, SchemaDialect inherited)
    {
        if (schema.ValueKind != JsonValueKind.Object ||
            !schema.TryGetProperty("$schema", out var declaration))
            return inherited;

        var value = declaration.GetString()
            ?? throw new InvalidOperationException("JSON Schema $schema must be a string.");
        if (value.Contains("draft-04", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("draft-06", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("draft-07", StringComparison.OrdinalIgnoreCase))
            return SchemaDialect.Draft7;
        if (value.Contains("2019-09", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("2020-12", StringComparison.OrdinalIgnoreCase))
            return SchemaDialect.Draft202012;

        throw new NotSupportedException($"JSON Schema dialect '{value}' is not supported.");
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
    private readonly record struct SchemaResource(
        SchemaDocument Document,
        JsonElement Element,
        string Pointer,
        Uri BaseUri,
        SchemaDialect Dialect);
    private readonly record struct SchemaReferenceKey(string Subject, int Version, string Uri);
    private readonly record struct NodeKey(int DocumentId, string Pointer);
    private readonly record struct ReferenceTarget(
        SchemaDocument Document,
        JsonElement Element,
        string Pointer,
        Uri BaseUri,
        SchemaDialect Dialect);
}

internal enum SchemaDialect : byte
{
    Draft7,
    Draft202012
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
    internal CompiledValidationRule[] ValidationRules { get; set; } = [];
    internal ValidationCelMemberTable? ValidationRuleMembers { get; set; }
    internal CompiledSchemaNode[] AllOf { get; set; } = [];
    internal bool HasAnyOf { get; set; }
    internal CompiledSchemaNode[] AnyOf { get; set; } = [];
    internal bool HasOneOf { get; set; }
    internal CompiledSchemaNode[] OneOf { get; set; } = [];
    internal bool HasLocalValidationTraversal { get; set; }
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
    internal CompiledJsonNumber? Minimum { get; set; }
    internal CompiledJsonNumber? Maximum { get; set; }
    internal bool ExclusiveMinimum { get; set; }
    internal bool ExclusiveMaximum { get; set; }
    internal CompiledJsonNumber? MultipleOf { get; set; }
    internal bool HasNumericAssertions { get; set; }
}

internal struct ValidationPathBuilder
{
    [ThreadStatic]
    private static char[]? t_buffer;

    private char[] _buffer;

    public ValidationPathBuilder()
    {
        _buffer = t_buffer ??= new char[256];
        Length = 1;
        _buffer[0] = '$';
    }

    internal int Length { get; private set; }

    internal void AppendProperty(string name)
    {
        EnsureCapacity(name.Length + 1);
        _buffer[Length++] = '.';
        name.AsSpan().CopyTo(_buffer.AsSpan(Length));
        Length += name.Length;
    }

    internal void AppendMapKey(ref Utf8JsonReader reader)
    {
        var maximumLength = reader.ValueSpan.Length;
        EnsureCapacity((maximumLength * 2) + 4);
        _buffer[Length++] = '[';
        _buffer[Length++] = '"';
        var contentStart = Length;
        var written = reader.CopyString(_buffer.AsSpan(contentStart, maximumLength));
        var escapes = 0;
        for (var index = 0; index < written; index++)
        {
            if (_buffer[contentStart + index] is '\\' or '"')
                escapes++;
        }
        var destination = contentStart + written + escapes - 1;
        for (var source = contentStart + written - 1; source >= contentStart; source--)
        {
            var character = _buffer[source];
            _buffer[destination--] = character;
            if (character is '\\' or '"')
                _buffer[destination--] = '\\';
        }
        Length = contentStart + written + escapes;
        _buffer[Length++] = '"';
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

internal readonly struct CompiledJsonNumber
{
    private CompiledJsonNumber(int sign, byte[] digits, long exponent, UInt128 coefficient)
    {
        Sign = sign;
        Digits = digits;
        Exponent = exponent;
        Coefficient = coefficient;
    }

    internal int Sign { get; }
    internal byte[] Digits { get; }
    internal long Exponent { get; }
    internal UInt128 Coefficient { get; }

    internal static CompiledJsonNumber Parse(JsonElement element, bool requireCoefficient = false)
    {
        if (element.ValueKind != JsonValueKind.Number)
            throw new InvalidOperationException("JSON Schema numeric assertions must be numbers.");

        var utf8 = Encoding.UTF8.GetBytes(element.GetRawText());
        var value = new JsonNumberView(utf8);
        var digits = new byte[value.DigitCount];
        value.CopyDigitsTo(digits);
        var hasCoefficient = TryParseCoefficient(digits, out var coefficient);
        if (requireCoefficient && !hasCoefficient)
        {
            throw new NotSupportedException(
                "JSON Schema 'multipleOf' coefficients larger than UInt128 are not supported.");
        }

        return new CompiledJsonNumber(value.Sign, digits, value.Exponent, coefficient);
    }

    internal int CompareTo(CompiledJsonNumber other)
    {
        if (Sign != other.Sign)
            return Sign.CompareTo(other.Sign);
        if (Sign == 0)
            return 0;

        var comparison = JsonNumberMath.CompareMagnitude(
            Digits,
            Exponent,
            other.Digits,
            other.Exponent);
        return Sign > 0 ? comparison : -comparison;
    }

    private static bool TryParseCoefficient(ReadOnlySpan<byte> digits, out UInt128 coefficient)
    {
        coefficient = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            var digit = (uint)(digits[i] - (byte)'0');
            if (coefficient > (UInt128.MaxValue - digit) / 10)
                return false;
            coefficient = (coefficient * 10) + digit;
        }

        return true;
    }
}

internal readonly ref struct JsonNumberView
{
    private readonly ReadOnlySpan<byte> _value;
    private readonly int _firstDigit;
    private readonly int _lastDigit;

    internal JsonNumberView(ReadOnlySpan<byte> value)
    {
        _value = value;
        var mantissaStart = value[0] == (byte)'-' ? 1 : 0;
        var mantissaEnd = value.Length;
        var decimalPoint = -1;
        var exponent = 0L;
        var firstDigit = -1;
        var lastDigit = -1;
        var significantLength = 0;
        var digitCount = 0;

        for (var i = mantissaStart; i < value.Length; i++)
        {
            if (value[i] == (byte)'.')
            {
                decimalPoint = i;
            }
            else if (value[i] is (byte)'e' or (byte)'E')
            {
                mantissaEnd = i;
                exponent = ParseExponent(value[(i + 1)..]);
                break;
            }
            else if (value[i] != (byte)'0')
            {
                firstDigit = firstDigit < 0 ? i : firstDigit;
                significantLength++;
                digitCount = significantLength;
                lastDigit = i;
            }
            else if (firstDigit >= 0)
            {
                significantLength++;
            }
        }

        if (firstDigit < 0)
        {
            Sign = 0;
            DigitCount = 0;
            Exponent = 0;
            _firstDigit = 0;
            _lastDigit = -1;
            return;
        }

        var fractionalDigits = decimalPoint < 0 ? 0 : mantissaEnd - decimalPoint - 1;
        var trailingZeros = significantLength - digitCount;

        Sign = mantissaStart == 0 ? 1 : -1;
        DigitCount = digitCount;
        Exponent = JsonNumberMath.SaturatingAdd(
            JsonNumberMath.SaturatingAdd(exponent, -fractionalDigits),
            trailingZeros);
        _firstDigit = firstDigit;
        _lastDigit = lastDigit;
    }

    internal int Sign { get; }
    internal int DigitCount { get; }
    internal long Exponent { get; }

    internal int CompareTo(CompiledJsonNumber other)
    {
        if (Sign != other.Sign)
            return Sign.CompareTo(other.Sign);
        if (Sign == 0)
            return 0;

        var order = JsonNumberMath.SaturatingAdd(Exponent, DigitCount);
        var otherOrder = JsonNumberMath.SaturatingAdd(other.Exponent, other.Digits.Length);
        var comparison = order.CompareTo(otherOrder);
        if (comparison == 0)
        {
            var digits = GetDigits();
            var count = Math.Max(DigitCount, other.Digits.Length);
            for (var i = 0; i < count; i++)
            {
                var digit = i < DigitCount && digits.MoveNext() ? digits.Current : (byte)'0';
                var otherDigit = i < other.Digits.Length ? other.Digits[i] : (byte)'0';
                comparison = digit.CompareTo(otherDigit);
                if (comparison != 0)
                    break;
            }
        }

        return Sign > 0 ? comparison : -comparison;
    }

    internal bool IsMultipleOf(CompiledJsonNumber divisor)
    {
        if (Sign == 0)
            return true;
        if (Exponent < divisor.Exponent)
            return false;

        var modulus = divisor.Coefficient;
        if (modulus == 1)
            return true;

        var remainder = (UInt128)0;
        var digits = GetDigits();
        while (digits.MoveNext())
        {
            remainder = JsonNumberMath.MultiplyByTenModulo(remainder, modulus);
            remainder = JsonNumberMath.AddModulo(
                remainder,
                (uint)(digits.Current - (byte)'0') % modulus,
                modulus);
        }

        if (remainder == 0)
            return true;

        var exponentDifference = JsonNumberMath.PositiveDifference(Exponent, divisor.Exponent);
        var power = JsonNumberMath.Pow10Modulo(exponentDifference, modulus);
        return JsonNumberMath.MultiplyModulo(remainder, power, modulus) == 0;
    }

    internal void CopyDigitsTo(Span<byte> destination)
    {
        var digits = GetDigits();
        var index = 0;
        while (digits.MoveNext())
            destination[index++] = digits.Current;
    }

    private JsonDigitEnumerator GetDigits() => new(_value, _firstDigit, _lastDigit);

    private static long ParseExponent(ReadOnlySpan<byte> value)
    {
        var negative = value[0] == (byte)'-';
        var position = value[0] is (byte)'+' or (byte)'-' ? 1 : 0;
        var result = 0L;
        for (; position < value.Length; position++)
        {
            var digit = value[position] - (byte)'0';
            if (result > (long.MaxValue - digit) / 10)
                return negative ? long.MinValue : long.MaxValue;
            result = (result * 10) + digit;
        }

        return negative ? -result : result;
    }

    private ref struct JsonDigitEnumerator
    {
        private readonly ReadOnlySpan<byte> _value;
        private readonly int _last;
        private int _position;

        internal JsonDigitEnumerator(ReadOnlySpan<byte> value, int first, int last)
        {
            _value = value;
            _last = last;
            _position = first - 1;
            Current = 0;
        }

        internal byte Current { get; private set; }

        internal bool MoveNext()
        {
            while (++_position <= _last)
            {
                if (_value[_position] == (byte)'.')
                    continue;
                Current = _value[_position];
                return true;
            }

            return false;
        }
    }
}

internal static class JsonNumberMath
{
    internal static int CompareMagnitude(
        ReadOnlySpan<byte> leftDigits,
        long leftExponent,
        ReadOnlySpan<byte> rightDigits,
        long rightExponent)
    {
        var leftOrder = SaturatingAdd(leftExponent, leftDigits.Length);
        var rightOrder = SaturatingAdd(rightExponent, rightDigits.Length);
        var comparison = leftOrder.CompareTo(rightOrder);
        if (comparison != 0)
            return comparison;

        var count = Math.Max(leftDigits.Length, rightDigits.Length);
        for (var i = 0; i < count; i++)
        {
            var left = i < leftDigits.Length ? leftDigits[i] : (byte)'0';
            var right = i < rightDigits.Length ? rightDigits[i] : (byte)'0';
            comparison = left.CompareTo(right);
            if (comparison != 0)
                return comparison;
        }

        return 0;
    }

    internal static long SaturatingAdd(long value, long addend)
    {
        if (addend > 0 && value > long.MaxValue - addend)
            return long.MaxValue;
        if (addend < 0 && value < long.MinValue - addend)
            return long.MinValue;
        return value + addend;
    }

    internal static long PositiveDifference(long left, long right)
    {
        if (right < 0 && left > long.MaxValue + right)
            return long.MaxValue;
        return left - right;
    }

    internal static UInt128 AddModulo(UInt128 left, UInt128 right, UInt128 modulus) =>
        left >= modulus - right ? left - (modulus - right) : left + right;

    internal static UInt128 MultiplyByTenModulo(UInt128 value, UInt128 modulus)
    {
        var doubled = AddModulo(value, value, modulus);
        var quadrupled = AddModulo(doubled, doubled, modulus);
        var octupled = AddModulo(quadrupled, quadrupled, modulus);
        return AddModulo(octupled, doubled, modulus);
    }

    internal static UInt128 MultiplyModulo(UInt128 left, UInt128 right, UInt128 modulus)
    {
        var result = (UInt128)0;
        while (right != 0)
        {
            if ((right & 1) != 0)
                result = AddModulo(result, left, modulus);
            right >>= 1;
            if (right != 0)
                left = AddModulo(left, left, modulus);
        }

        return result;
    }

    internal static UInt128 Pow10Modulo(long exponent, UInt128 modulus)
    {
        var result = (UInt128)1 % modulus;
        var value = (UInt128)10 % modulus;
        while (exponent != 0)
        {
            if ((exponent & 1) != 0)
                result = MultiplyModulo(result, value, modulus);
            exponent >>= 1;
            if (exponent != 0)
                value = MultiplyModulo(value, value, modulus);
        }

        return result;
    }
}

internal sealed class CompiledProperty
{
    internal CompiledProperty(string name, CompiledSchemaNode? schema, bool isDeclared = true)
    {
        Name = name;
        Schema = schema;
        IsDeclared = isDeclared;
        Utf8Name = Encoding.UTF8.GetBytes(name);
        Hash = CompiledPropertyTable.Hash(Utf8Name);
    }

    internal string Name { get; }
    internal byte[] Utf8Name { get; }
    internal uint Hash { get; }
    internal CompiledSchemaNode? Schema { get; }
    internal bool IsDeclared { get; }
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
