using System.Text.Json;
using Dekaf.SchemaRegistry;

namespace Dekaf.Tests.Unit.SchemaRegistry;

/// <summary>
/// Mock implementation of ISchemaRegistryClient for unit testing.
/// </summary>
internal sealed class MockSchemaRegistryClient : IFormattedSchemaRegistryClient, ISchemaRegistryCache
{
    private readonly Dictionary<int, Schema> _schemasById = new();
    private readonly Dictionary<(Guid Guid, string? Format), Schema> _schemasByGuid = new();
    private readonly Dictionary<string, List<(int Version, int Id, Schema Schema)>> _schemasBySubject = new();
    private readonly Dictionary<(string Namespace, string Name), List<Association>> _associationsByResource = new();
    private readonly Queue<Task<IReadOnlyList<Association>>> _associationLookupResponses = new();
    private TaskCompletionSource? _getSchemaEntered;
    private TaskCompletionSource? _getSchemaRelease;
    private TaskCompletionSource? _getOrRegisterSchemaEntered;
    private TaskCompletionSource? _getOrRegisterSchemaRelease;
    private TaskCompletionSource? _associationLookupEntered;
    private TaskCompletionSource? _associationLookupRelease;
    private int _associationLookupCallCount;
    private int _nextId = 1;
    private bool _disposed;

    public int GetSchemaCallCount { get; private set; }
    public CancellationToken LastGetSchemaCancellationToken { get; private set; }
    public CancellationToken LastGetSchemaByGuidCancellationToken { get; private set; }
    public int GetOrRegisterSchemaCallCount { get; private set; }
    public CancellationToken LastGetOrRegisterSchemaCancellationToken { get; private set; }
    public int TryGetCachedSchemaCallCount { get; private set; }
    internal Action? BeforeTryGetCachedSchema { get; set; }
    public int GetSchemaFailuresRemaining { get; set; }
    public int GetOrRegisterSchemaFailuresRemaining { get; set; }
    public bool LookupRequiresRuleSetPresenceMatch { get; set; }
    public int AssociationLookupCallCount => Volatile.Read(ref _associationLookupCallCount);
    public int AssociationLookupFailuresRemaining { get; set; }
    public bool SupportsDeletedVersionLookup { get; init; }

    public void BlockNextAssociationLookup()
    {
        _associationLookupEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _associationLookupRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public void EnqueueAssociationLookup(Task<IReadOnlyList<Association>> response) =>
        _associationLookupResponses.Enqueue(response);

    public async Task WaitForBlockedAssociationLookupAsync(TimeSpan timeout)
    {
        var entered = _associationLookupEntered
            ?? throw new InvalidOperationException("No blocked association lookup was configured.");
        await entered.Task.WaitAsync(timeout);
    }

    public void ReleaseBlockedAssociationLookup()
    {
        _associationLookupRelease?.TrySetResult();
        _associationLookupEntered = null;
        _associationLookupRelease = null;
    }

    public void BlockNextGetSchema()
    {
        _getSchemaEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _getSchemaRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public async Task WaitForBlockedGetSchemaAsync(TimeSpan timeout)
    {
        var entered = _getSchemaEntered
            ?? throw new InvalidOperationException("No blocked GetSchemaAsync call was configured.");

        await entered.Task.WaitAsync(timeout).ConfigureAwait(false);
    }

    public void ReleaseBlockedGetSchema()
    {
        _getSchemaRelease?.TrySetResult();
        _getSchemaEntered = null;
        _getSchemaRelease = null;
    }

    /// <summary>
    /// Normalizes a JSON schema string by parsing and re-serializing.
    /// This ensures schemas with different whitespace/formatting compare equal.
    /// </summary>
    private static string NormalizeJsonSchema(string schemaString)
    {
        try
        {
            using var doc = JsonDocument.Parse(schemaString);
            return JsonSerializer.Serialize(doc.RootElement);
        }
        catch
        {
            // If not valid JSON, return original
            return schemaString;
        }
    }

    private static bool SchemasAreEquivalent(string schema1, string schema2)
    {
        return NormalizeJsonSchema(schema1) == NormalizeJsonSchema(schema2);
    }

    public void BlockNextGetOrRegisterSchema()
    {
        _getOrRegisterSchemaEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _getOrRegisterSchemaRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public async Task WaitForBlockedGetOrRegisterSchemaAsync(TimeSpan timeout)
    {
        var entered = _getOrRegisterSchemaEntered
            ?? throw new InvalidOperationException("No blocked GetOrRegisterSchemaAsync call was configured.");

        await entered.Task.WaitAsync(timeout).ConfigureAwait(false);
    }

    public void ReleaseBlockedGetOrRegisterSchema()
    {
        _getOrRegisterSchemaRelease?.TrySetResult();
        _getOrRegisterSchemaEntered = null;
        _getOrRegisterSchemaRelease = null;
    }

    public Task<int> RegisterSchemaAsync(string subject, Schema schema, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var id = _nextId++;
        _schemasById[id] = schema;
        _schemasByGuid[(GuidFromId(id), null)] = schema;

        if (!_schemasBySubject.TryGetValue(subject, out var list))
        {
            list = [];
            _schemasBySubject[subject] = list;
        }

        var version = list.Count + 1;
        list.Add((version, id, schema));

        return Task.FromResult(id);
    }

    internal void AddSchemaSubject(int id, string subject)
    {
        var schema = _schemasById[id];
        if (!_schemasBySubject.TryGetValue(subject, out var list))
        {
            list = [];
            _schemasBySubject[subject] = list;
        }

        list.Add((list.Count + 1, id, schema));
    }

    public async Task<Schema> GetSchemaAsync(int id, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        GetSchemaCallCount++;
        LastGetSchemaCancellationToken = cancellationToken;

        if (_getSchemaRelease is { } release)
        {
            _getSchemaEntered!.TrySetResult();
            await release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        if (GetSchemaFailuresRemaining > 0)
        {
            GetSchemaFailuresRemaining--;
            throw new SchemaRegistryException(50001, "Transient schema fetch failure");
        }

        if (_schemasById.TryGetValue(id, out var schema))
            return schema;

        throw new SchemaRegistryException(40403, $"Schema {id} not found");
    }

    public Task<Schema> GetSchemaByGuidAsync(
        string guid,
        string? format = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        LastGetSchemaByGuidCancellationToken = cancellationToken;
        cancellationToken.ThrowIfCancellationRequested();
        if (Guid.TryParse(guid, out var parsedGuid))
        {
            if (_schemasByGuid.TryGetValue((parsedGuid, format), out var cached))
                return Task.FromResult(cached);

            if (_schemasByGuid.TryGetValue((parsedGuid, null), out var schema))
            {
                _schemasByGuid[(parsedGuid, format)] = schema;
                return Task.FromResult(schema);
            }
        }

        throw new SchemaRegistryException(40403, $"Schema {guid} not found");
    }

    public Task<Schema> GetSchemaAsync(
        int id,
        string subject,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        GetSchemaCallCount++;

        if (_schemasBySubject.TryGetValue(subject, out var schemas))
        {
            for (var i = 0; i < schemas.Count; i++)
            {
                if (schemas[i].Id == id)
                    return Task.FromResult(schemas[i].Schema);
            }
        }

        throw new SchemaRegistryException(40403, $"Schema {id} not found under subject '{subject}'");
    }

    public Task<Schema> GetSchemaWithFormatAsync(
        int id,
        string subject,
        string format,
        CancellationToken cancellationToken = default) =>
        GetSchemaAsync(id, subject, cancellationToken);

    public bool TryGetCachedSchema(int id, out Schema schema)
    {
        ThrowIfDisposed();
        BeforeTryGetCachedSchema?.Invoke();
        TryGetCachedSchemaCallCount++;
        return _schemasById.TryGetValue(id, out schema!);
    }

    public bool TryGetCachedSchema(Guid guid, string? format, out Schema schema)
    {
        ThrowIfDisposed();
        TryGetCachedSchemaCallCount++;
        return _schemasByGuid.TryGetValue((guid, format), out schema!);
    }

    public bool TryGetCachedSchema(int id, string subject, out Schema schema)
    {
        ThrowIfDisposed();
        TryGetCachedSchemaCallCount++;
        if (_schemasBySubject.TryGetValue(subject, out var schemas))
        {
            for (var i = 0; i < schemas.Count; i++)
            {
                if (schemas[i].Id == id)
                {
                    schema = schemas[i].Schema;
                    return true;
                }
            }
        }

        schema = null!;
        return false;
    }

    public Task<RegisteredSchema> GetSchemaBySubjectAsync(string subject, string version = "latest", CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!_schemasBySubject.TryGetValue(subject, out var list) || list.Count == 0)
            throw new SchemaRegistryException(40401, $"Subject '{subject}' not found");

        var entry = version == "latest"
            ? list[^1]
            : list.FirstOrDefault(e => e.Version == int.Parse(version));

        if (entry == default)
            throw new SchemaRegistryException(40402, $"Version not found");

        return Task.FromResult(new RegisteredSchema
        {
            Id = entry.Id,
            Guid = GuidFromId(entry.Id).ToString(),
            Subject = subject,
            Version = entry.Version,
            Schema = entry.Schema
        });
    }

    public Task<RegisteredSchema> GetSchemaBySubjectAsync(
        string subject,
        string version,
        bool ignoreDeletedSchemas,
        CancellationToken cancellationToken = default)
    {
        if (!ignoreDeletedSchemas && !SupportsDeletedVersionLookup)
        {
            throw new NotSupportedException(
                $"This {nameof(ISchemaRegistryClient)} implementation does not support looking up deleted schema versions.");
        }

        return GetSchemaBySubjectAsync(subject, version, cancellationToken);
    }

    public Task<RegisteredSchema> GetSchemaBySubjectWithFormatAsync(
        string subject,
        string version,
        bool ignoreDeletedSchemas,
        string format,
        CancellationToken cancellationToken = default) =>
        GetSchemaBySubjectAsync(subject, version, ignoreDeletedSchemas, cancellationToken);

    public Task<RegisteredSchema> LookupSchemaAsync(
        string subject,
        Schema schema,
        bool ignoreDeletedSchemas = true,
        bool normalize = false,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!_schemasBySubject.TryGetValue(subject, out var list))
            throw new SchemaRegistryException(40401, $"Subject '{subject}' not found");

        var entry = list.FirstOrDefault(candidate =>
            SchemasAreEquivalent(candidate.Schema, schema) &&
            (!LookupRequiresRuleSetPresenceMatch ||
             (candidate.Schema.RuleSet is null) == (schema.RuleSet is null)));
        if (entry == default)
            throw new SchemaRegistryException(40403, $"Schema not found under subject '{subject}'");

        return Task.FromResult(new RegisteredSchema
        {
            Id = entry.Id,
            Guid = GuidFromId(entry.Id).ToString(),
            Subject = subject,
            Version = entry.Version,
            Schema = entry.Schema
        });
    }

    public async Task<int> GetOrRegisterSchemaAsync(string subject, Schema schema, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        GetOrRegisterSchemaCallCount++;
        LastGetOrRegisterSchemaCancellationToken = cancellationToken;

        if (_getOrRegisterSchemaRelease is { } release)
        {
            _getOrRegisterSchemaEntered!.TrySetResult();
            await release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        if (GetOrRegisterSchemaFailuresRemaining > 0)
        {
            GetOrRegisterSchemaFailuresRemaining--;
            throw new SchemaRegistryException(50002, "Transient schema registration failure");
        }

        // Check whether an equivalent schema is already registered under this subject.
        if (_schemasBySubject.TryGetValue(subject, out var list))
        {
            var existing = list.FirstOrDefault(e => SchemasAreEquivalent(e.Schema, schema));
            if (existing != default)
                return existing.Id;
        }

        // Register new schema
        return await RegisterSchemaAsync(subject, schema, cancellationToken).ConfigureAwait(false);
    }

    private static bool SchemasAreEquivalent(Schema left, Schema right)
    {
        if (left.SchemaType != right.SchemaType ||
            !SchemasAreEquivalent(left.SchemaString, right.SchemaString))
        {
            return false;
        }

        var leftReferences = left.References ?? [];
        var rightReferences = right.References ?? [];
        if (leftReferences.Count != rightReferences.Count)
            return false;

        for (var index = 0; index < leftReferences.Count; index++)
        {
            var leftReference = leftReferences[index];
            var rightReference = rightReferences[index];
            if (leftReference.Name != rightReference.Name ||
                leftReference.Subject != rightReference.Subject ||
                leftReference.Version != rightReference.Version)
            {
                return false;
            }
        }

        return true;
    }

    public Task<IReadOnlyList<string>> GetAllSubjectsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return Task.FromResult<IReadOnlyList<string>>(_schemasBySubject.Keys.ToList());
    }

    public Task<IReadOnlyList<int>> GetVersionsAsync(string subject, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!_schemasBySubject.TryGetValue(subject, out var list))
            throw new SchemaRegistryException(40401, $"Subject '{subject}' not found");

        return Task.FromResult<IReadOnlyList<int>>(list.Select(e => e.Version).ToList());
    }

    public Task<bool> IsCompatibleAsync(string subject, Schema schema, string version = "latest", CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<int>> DeleteSubjectAsync(string subject, bool permanent = false, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!_schemasBySubject.TryGetValue(subject, out var list))
            throw new SchemaRegistryException(40401, $"Subject '{subject}' not found");

        var versions = list.Select(e => e.Version).ToList();

        foreach (var entry in list)
        {
            _schemasById.Remove(entry.Id);
            var guid = GuidFromId(entry.Id);
            foreach (var key in _schemasByGuid.Keys.Where(key => key.Guid == guid).ToArray())
            {
                _schemasByGuid.Remove(key);
            }
        }

        _schemasBySubject.Remove(subject);

        return Task.FromResult<IReadOnlyList<int>>(versions);
    }

    public async Task<IReadOnlyList<Association>> GetAssociationsByResourceNameAsync(
        string resourceName,
        string resourceNamespace = "-",
        string? resourceType = null,
        IReadOnlyList<string>? associationTypes = null,
        string? lifecycle = null,
        int offset = 0,
        int limit = -1,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        AssociationValidation.ValidateGet(
            resourceName,
            resourceNamespace,
            resourceType,
            associationTypes,
            lifecycle,
            offset,
            limit);
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _associationLookupCallCount);
        if (_associationLookupResponses.Count > 0)
            return await _associationLookupResponses.Dequeue().WaitAsync(cancellationToken);
        if (_associationLookupEntered is { } entered && _associationLookupRelease is { } release)
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
        }

        if (AssociationLookupFailuresRemaining > 0)
        {
            AssociationLookupFailuresRemaining--;
            throw new SchemaRegistryException(500, "Simulated association lookup failure.");
        }

        IEnumerable<Association> filtered;
        if (resourceNamespace == "-")
        {
            filtered = _associationsByResource
                .Where(entry => entry.Key.Name == resourceName)
                .SelectMany(static entry => entry.Value);
        }
        else if (_associationsByResource.TryGetValue((resourceNamespace, resourceName), out var stored))
        {
            filtered = stored;
        }
        else
        {
            return [];
        }

        if (resourceType is not null)
            filtered = filtered.Where(association => association.ResourceType == resourceType);
        if (associationTypes is { Count: > 0 })
            filtered = filtered.Where(association => associationTypes.Contains(association.AssociationType));
        if (lifecycle is not null)
            filtered = filtered.Where(association => association.Lifecycle == lifecycle);
        if (offset > 0)
            filtered = filtered.Skip(offset);
        if (limit >= 0)
            filtered = filtered.Take(limit);

        return filtered.ToArray();
    }

    public Task<AssociationResponse> CreateAssociationAsync(
        AssociationCreateOrUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        AssociationValidation.ValidateCreate(request);
        cancellationToken.ThrowIfCancellationRequested();

        var key = (request.ResourceNamespace, request.ResourceName);
        if (!_associationsByResource.TryGetValue(key, out var stored))
        {
            stored = [];
            _associationsByResource[key] = stored;
        }

        var responseAssociations = new AssociationInfo[request.Associations.Count];
        for (var index = 0; index < request.Associations.Count; index++)
        {
            var association = request.Associations[index];
            stored.RemoveAll(existing =>
                existing.Subject == association.Subject &&
                existing.AssociationType == association.AssociationType);
            stored.Add(new Association
            {
                Subject = association.Subject,
                Guid = $"mock-{request.ResourceId}-{association.AssociationType}",
                ResourceName = request.ResourceName,
                ResourceNamespace = request.ResourceNamespace,
                ResourceId = request.ResourceId,
                ResourceType = request.ResourceType,
                AssociationType = association.AssociationType,
                Lifecycle = association.Lifecycle,
                Frozen = association.Frozen ?? false
            });
            responseAssociations[index] = new AssociationInfo
            {
                Subject = association.Subject,
                AssociationType = association.AssociationType,
                Lifecycle = association.Lifecycle,
                Frozen = association.Frozen ?? false,
                Schema = association.Schema
            };
        }

        return Task.FromResult(new AssociationResponse
        {
            ResourceName = request.ResourceName,
            ResourceNamespace = request.ResourceNamespace,
            ResourceId = request.ResourceId,
            ResourceType = request.ResourceType,
            Associations = responseAssociations
        });
    }

    public Task DeleteAssociationsAsync(
        string resourceId,
        string? resourceType = null,
        IReadOnlyList<string>? associationTypes = null,
        bool cascadeLifecycle = false,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        AssociationValidation.ValidateDelete(resourceId, resourceType, associationTypes);
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var stored in _associationsByResource.Values)
        {
            stored.RemoveAll(association =>
                association.ResourceId == resourceId &&
                (resourceType is null || association.ResourceType == resourceType) &&
                (associationTypes is not { Count: > 0 } || associationTypes.Contains(association.AssociationType)));
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static Guid GuidFromId(int id) => new(id, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
}
