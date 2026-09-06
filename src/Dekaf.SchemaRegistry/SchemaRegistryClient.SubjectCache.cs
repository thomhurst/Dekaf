namespace Dekaf.SchemaRegistry;

public sealed partial class SchemaRegistryClient
{
    // Entries exist only while subject HTTP requests are active. Completed requests release
    // their subject references, so repeated deletion does not accumulate generation tombstones.
    private readonly Dictionary<string, SubjectCacheRequestState> _subjectCacheRequests = new(StringComparer.Ordinal);
    private long _nextSubjectCacheGeneration;

    internal int ActiveSubjectCacheRequestCount
    {
        get
        {
            lock (_cacheLock)
                return _subjectCacheRequests.Count;
        }
    }

    private SubjectCacheRequest BeginSubjectCacheRequest(string subject)
    {
        if (_maxCachedSchemas == 0 || subject is null)
            return default;

        lock (_cacheLock)
        {
            if (!_subjectCacheRequests.TryGetValue(subject, out var state))
                state.Generation = unchecked(++_nextSubjectCacheGeneration);
            state.Count++;
            _subjectCacheRequests[subject] = state;
            return new SubjectCacheRequest(this, subject, state.Generation);
        }
    }

    private void EndSubjectCacheRequest(string subject)
    {
        lock (_cacheLock)
        {
            var state = _subjectCacheRequests[subject];
            if (--state.Count == 0)
                _subjectCacheRequests.Remove(subject);
            else
                _subjectCacheRequests[subject] = state;
        }
    }

    // Called while holding _cacheLock, which also serializes deletion and cache population.
    private bool IsSubjectCacheRequestCurrent(string subject, long? generation) =>
        generation is null ||
        (_subjectCacheRequests.TryGetValue(subject, out var state) && state.Generation == generation.Value);

    private void InvalidateSubjectCaches(string subject)
    {
        lock (_cacheLock)
        {
            if (_subjectCacheRequests.TryGetValue(subject, out var state))
            {
                state.Generation = unchecked(++_nextSubjectCacheGeneration);
                _subjectCacheRequests[subject] = state;
            }

            // Subject deletion is a cold operation; cache-hit paths keep their direct
            // dictionary lookups. Both normalization settings and every format are removed.
            foreach (var entry in _idBySchemaCache)
            {
                if (string.Equals(entry.Key.Subject, subject, StringComparison.Ordinal))
                    _idBySchemaCache.TryRemove(entry.Key, out _);
            }
            foreach (var entry in _schemaBySubjectAndIdCache)
            {
                if (string.Equals(entry.Key.Subject, subject, StringComparison.Ordinal))
                    _schemaBySubjectAndIdCache.TryRemove(entry.Key, out _);
            }

            // Integer IDs and GUIDs identify schema content shared across subjects.
            // Retain those cached definitions; they do not prove subject membership.
        }
    }

    private struct SubjectCacheRequestState
    {
        internal long Generation;
        internal int Count;
    }

    private readonly struct SubjectCacheRequest(SchemaRegistryClient? owner, string? subject, long generation) : IDisposable
    {
        internal long Generation { get; } = generation;

        public void Dispose() => owner?.EndSubjectCacheRequest(subject!);
    }
}
