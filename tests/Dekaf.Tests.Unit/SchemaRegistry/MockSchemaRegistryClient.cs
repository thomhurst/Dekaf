using System.Text.Json;
using Dekaf.SchemaRegistry;

namespace Dekaf.Tests.Unit.SchemaRegistry;

/// <summary>
/// Mock implementation of ISchemaRegistryClient for unit testing.
/// </summary>
internal sealed class MockSchemaRegistryClient : ISchemaRegistryClient, ISchemaRegistryCache
{
    private readonly Dictionary<int, Schema> _schemasById = new();
    private readonly Dictionary<string, List<(int Version, int Id, Schema Schema)>> _schemasBySubject = new();
    private TaskCompletionSource? _getOrRegisterSchemaEntered;
    private TaskCompletionSource? _getOrRegisterSchemaRelease;
    private int _nextId = 1;
    private bool _disposed;

    public int GetSchemaCallCount { get; private set; }
    public int GetOrRegisterSchemaCallCount { get; private set; }
    public CancellationToken LastGetOrRegisterSchemaCancellationToken { get; private set; }
    public int TryGetCachedSchemaCallCount { get; private set; }
    public int GetSchemaFailuresRemaining { get; set; }
    public int GetOrRegisterSchemaFailuresRemaining { get; set; }

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

        if (!_schemasBySubject.TryGetValue(subject, out var list))
        {
            list = [];
            _schemasBySubject[subject] = list;
        }

        var version = list.Count + 1;
        list.Add((version, id, schema));

        return Task.FromResult(id);
    }

    public Task<Schema> GetSchemaAsync(int id, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        GetSchemaCallCount++;

        if (GetSchemaFailuresRemaining > 0)
        {
            GetSchemaFailuresRemaining--;
            throw new SchemaRegistryException(50001, "Transient schema fetch failure");
        }

        if (_schemasById.TryGetValue(id, out var schema))
            return Task.FromResult(schema);

        throw new SchemaRegistryException(40403, $"Schema {id} not found");
    }

    public bool TryGetCachedSchema(int id, out Schema schema)
    {
        ThrowIfDisposed();
        TryGetCachedSchemaCallCount++;
        return _schemasById.TryGetValue(id, out schema!);
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
            Subject = subject,
            Version = entry.Version,
            Schema = entry.Schema
        });
    }

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

        var entry = list.FirstOrDefault(candidate => SchemasAreEquivalent(candidate.Schema, schema));
        if (entry == default)
            throw new SchemaRegistryException(40403, $"Schema not found under subject '{subject}'");

        return Task.FromResult(new RegisteredSchema
        {
            Id = entry.Id,
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

        var leftReferences = left.References;
        var rightReferences = right.References;
        if (leftReferences is null || rightReferences is null)
            return leftReferences is null && rightReferences is null;
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
            _schemasById.Remove(entry.Id);

        _schemasBySubject.Remove(subject);

        return Task.FromResult<IReadOnlyList<int>>(versions);
    }

    public void Dispose()
    {
        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
