using Dekaf.SchemaRegistry;

namespace Dekaf.Tests.Unit.SchemaRegistry;

/// <summary>
/// Mock implementation of ISchemaRegistryClient for unit testing.
/// </summary>
internal sealed class MockSchemaRegistryClient : ISchemaRegistryClient
{
    private readonly Dictionary<int, Schema> _schemasById = new();
    private readonly Dictionary<string, List<(int Version, int Id, Schema Schema)>> _schemasBySubject = new();
    private int _nextId = 1;
    private bool _disposed;

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

        if (_schemasById.TryGetValue(id, out var schema))
            return Task.FromResult(schema);

        throw new SchemaRegistryException(40403, $"Schema {id} not found");
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

    public Task<int> GetOrRegisterSchemaAsync(string subject, Schema schema, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Check if schema already exists
        if (_schemasBySubject.TryGetValue(subject, out var list))
        {
            var existing = list.FirstOrDefault(e => e.Schema.SchemaString == schema.SchemaString);
            if (existing != default)
                return Task.FromResult(existing.Id);
        }

        // Register new schema
        return RegisterSchemaAsync(subject, schema, cancellationToken);
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
