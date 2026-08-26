using System.Buffers;
using System.Collections;
using System.Runtime.CompilerServices;
#if NETSTANDARD2_0
using System.Runtime.InteropServices;
#endif
using System.Text;

namespace Dekaf.Serialization;

/// <summary>
/// Collection of headers for a Kafka record.
/// </summary>
public sealed class Headers : IReadOnlyList<Header>
{
    // Keep synchronized with SchemaIdentityHeaderNames in Dekaf.SchemaRegistry.
    private const string KeySchemaIdentityHeader = "__key_schema_id";
    private const string ValueSchemaIdentityHeader = "__value_schema_id";
    private readonly List<Header> _headers;
    private int _deferredTraceparentIndex = -1;
    private int _deferredTracestateIndex = -1;
    private int _deferredTraceContextVersion;
    private int _nextDeferredTraceContextVersion;
    private DeferredTraceContextCheckpoint _previousDeferredTraceContext;
    private DeferredTraceContextCheckpoint[]? _deferredTraceContextStack;
    private int _deferredTraceContextDepth;
    private int _keySchemaIdentityIndex = -1;
    private int _valueSchemaIdentityIndex = -1;
    private Header _stagedHeader0;
    private Header _stagedHeader1;
    private Header[]? _stagedHeaderOverflow;
    private int _stagedHeaderCount;
    private int _stagedKeySchemaIdentityIndex = -1;
    private int _stagedValueSchemaIdentityIndex = -1;
    private bool _stagingRecordHeaders;
    private Headers? _serializationSource;
    private StagedRecordHeadersCheckpoint _previousStagedRecordHeaders;
    private StagedRecordHeadersCheckpoint[]? _stagedRecordHeadersStack;
    private int _stagedRecordHeadersDepth;

    /// <summary>
    /// Creates an empty headers collection.
    /// </summary>
    public Headers()
    {
        _headers = [];
    }

    /// <summary>
    /// Creates a headers collection with the specified capacity.
    /// </summary>
    public Headers(int capacity)
    {
        _headers = new List<Header>(capacity);
    }

    /// <summary>
    /// Creates a headers collection from existing headers.
    /// </summary>
    public Headers(IEnumerable<Header> headers)
    {
        _headers = [.. headers];
        InitializeSchemaIdentityIndexes();
    }

    /// <summary>
    /// Creates a new empty headers collection.
    /// </summary>
    /// <returns>A new empty Headers instance.</returns>
    public static Headers Create() => new();

    /// <summary>
    /// Creates a new headers collection with a single header.
    /// </summary>
    /// <param name="key">The header key.</param>
    /// <param name="value">The header value.</param>
    /// <returns>A new Headers instance with one header.</returns>
    public static Headers Create(string key, string value) => new Headers().Add(key, value);

    /// <summary>
    /// Creates a new headers collection with a single header.
    /// </summary>
    /// <param name="key">The header key.</param>
    /// <param name="value">The header value as bytes.</param>
    /// <returns>A new Headers instance with one header.</returns>
    public static Headers Create(string key, byte[]? value) => new Headers().Add(key, value);

    /// <summary>
    /// Gets the number of headers.
    /// </summary>
    public int Count => SerializationCount;

    internal int SerializationCount => (_serializationSource?._headers.Count ?? _headers.Count) + _stagedHeaderCount;

    internal int CountWithoutDeferredTraceContext
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var source = _serializationSource;
            var headerCount = source?._headers.Count ?? _headers.Count;
            var count = headerCount;
            var traceparentIndex = source?._deferredTraceparentIndex ?? _deferredTraceparentIndex;
            var tracestateIndex = source?._deferredTracestateIndex ?? _deferredTracestateIndex;
            if ((uint)traceparentIndex < (uint)headerCount)
                count--;
            if ((uint)tracestateIndex < (uint)headerCount)
                count--;
            return count;
        }
    }

    /// <summary>
    /// Gets the header at the specified index.
    /// </summary>
    public Header this[int index] => GetSerializationHeader(index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Header GetSerializationHeader(int index)
    {
        var source = _serializationSource;
        var headers = source?._headers ?? _headers;
        var stagedCount = _stagedHeaderCount;
        if (stagedCount == 0)
            return headers[index];

        var traceparentIndex = source?._deferredTraceparentIndex ?? _deferredTraceparentIndex;
        var insertionIndex = traceparentIndex >= 0 ? traceparentIndex : headers.Count;
        if (index < insertionIndex)
            return headers[index];
        if (index < insertionIndex + stagedCount)
            return GetStagedHeader(index - insertionIndex);
        return headers[index - stagedCount];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void CopySerializationHeadersTo(Header[] destination)
    {
        var source = _serializationSource;
        var headers = source?._headers ?? _headers;
        var stagedCount = _stagedHeaderCount;
        if (stagedCount == 0)
        {
            for (var i = 0; i < headers.Count; i++)
                destination[i] = headers[i];
            return;
        }

        var traceparentIndex = source?._deferredTraceparentIndex ?? _deferredTraceparentIndex;
        var insertionIndex = traceparentIndex >= 0 ? traceparentIndex : headers.Count;
        for (var i = 0; i < insertionIndex; i++)
            destination[i] = headers[i];

        for (var i = 0; i < stagedCount; i++)
            destination[insertionIndex + i] = GetStagedHeader(i);

        for (var i = insertionIndex; i < headers.Count; i++)
            destination[stagedCount + i] = headers[i];
    }

    /// <summary>
    /// Adds a header with a string value.
    /// </summary>
    public Headers Add(string key, string value)
    {
        AddHeader(new Header(key, Encoding.UTF8.GetBytes(value)));
        return this;
    }

    /// <summary>
    /// Adds a header with a byte array value.
    /// </summary>
    public Headers Add(string key, byte[]? value)
    {
        AddHeader(new Header(key, value));
        return this;
    }

    /// <summary>
    /// Adds a header.
    /// </summary>
    public Headers Add(Header header)
    {
        AddHeader(header);
        return this;
    }

    /// <summary>
    /// Gets the first header with the specified key.
    /// </summary>
    public Header? GetFirst(string key)
    {
        // Manual loop to avoid closure allocation from lambda predicate.
        var count = Count;
        for (var index = 0; index < count; index++)
        {
            var header = GetSerializationHeader(index);
            if (header.Key == key)
                return header;
        }
        return null;
    }

    /// <summary>
    /// Gets all headers with the specified key.
    /// Uses yield return for deferred execution without list allocation.
    /// </summary>
    public IEnumerable<Header> GetAll(string key)
    {
        // Use iterator method for zero-allocation deferred execution.
        // The state machine is only allocated when the caller enumerates.
        var count = Count;
        for (var index = 0; index < count; index++)
        {
            var header = GetSerializationHeader(index);
            if (header.Key == key)
                yield return header;
        }
    }

    /// <summary>
    /// Gets the first header value with the specified key as a string.
    /// </summary>
    public string? GetFirstAsString(string key)
    {
        var header = GetFirst(key);
        return header?.GetValueAsString();
    }

    /// <summary>
    /// Removes all headers with the specified key.
    /// </summary>
    public Headers Remove(string key)
    {
        if (_serializationSource is not null && ContainsSerializationHeader(key))
        {
            MaterializeWithout(key);
            return this;
        }

        // Manual loop to avoid closure allocation from RemoveAll predicate
        for (var i = _headers.Count - 1; i >= 0; i--)
        {
            if (_headers[i].Key == key)
                RemoveAt(i);
        }
        RemoveStagedHeaders(key);
        return this;
    }

    /// <summary>
    /// Clears all headers.
    /// </summary>
    public void Clear()
    {
        _serializationSource?.RemoveDeferredTraceContext();
        _headers.Clear();
        _deferredTraceparentIndex = -1;
        _deferredTracestateIndex = -1;
        _deferredTraceContextVersion = 0;
        _keySchemaIdentityIndex = -1;
        _valueSchemaIdentityIndex = -1;
        _serializationSource = null;
        EndRecordHeaderStaging();
        ClearPreviousStagedRecordHeaders();
        ClearPreviousDeferredTraceContexts();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ResetSerializationWorkspace()
    {
        if (_serializationSource is null && _headers.Count == 0 && !_stagingRecordHeaders)
            return;

        _serializationSource = null;
        Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void BeginRecordHeaderStaging()
        => BeginRecordHeaderStaging(source: null);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void BeginRecordHeaderStaging(Headers? source)
    {
        _serializationSource = source;
        if (_stagingRecordHeaders)
            PushStagedRecordHeaders();
        EndRecordHeaderStaging();
        _stagingRecordHeaders = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Checkpoint CaptureCheckpoint() => new(
        CountWithoutDeferredTraceContext,
        _keySchemaIdentityIndex,
        _valueSchemaIdentityIndex,
        _deferredTraceContextVersion);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Restore(in Checkpoint checkpoint)
    {
        EndRecordHeaderStaging();
        RestorePreviousStagedRecordHeaders();
        if (checkpoint.DeferredTraceContextVersion != 0)
        {
            RestoreTraceAware(in checkpoint);
            return;
        }

        if ((uint)checkpoint.Count > (uint)_headers.Count)
            throw new ArgumentOutOfRangeException(nameof(checkpoint));

        if (checkpoint.Count != _headers.Count)
            _headers.RemoveRange(checkpoint.Count, _headers.Count - checkpoint.Count);

        _deferredTraceparentIndex = -1;
        _deferredTracestateIndex = -1;
        _keySchemaIdentityIndex = checkpoint.KeySchemaIdentityIndex;
        _valueSchemaIdentityIndex = checkpoint.ValueSchemaIdentityIndex;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void RestoreTraceAware(in Checkpoint checkpoint)
    {
        RemoveDeferredTraceContext(checkpoint.DeferredTraceContextVersion);
        var countWithoutTraceContext = CountWithoutDeferredTraceContext;
        if ((uint)checkpoint.Count > (uint)countWithoutTraceContext)
            throw new ArgumentOutOfRangeException(nameof(checkpoint));

        if (checkpoint.Count != countWithoutTraceContext)
            RemoveNonTraceHeaders(checkpoint.Count, countWithoutTraceContext - checkpoint.Count);

        _keySchemaIdentityIndex = checkpoint.KeySchemaIdentityIndex;
        _valueSchemaIdentityIndex = checkpoint.ValueSchemaIdentityIndex;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RemoveNonTraceHeaders(int index, int count)
    {
        _headers.RemoveRange(index, count);
        _deferredTraceparentIndex = AdjustTrackedIndex(_deferredTraceparentIndex, index, count);
        _deferredTracestateIndex = AdjustTrackedIndex(_deferredTracestateIndex, index, count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Truncate(int count)
    {
        if ((uint)count > (uint)_headers.Count)
            throw new ArgumentOutOfRangeException(nameof(count));

        if (count == _headers.Count)
            return;

        _headers.RemoveRange(count, _headers.Count - count);
        _deferredTraceparentIndex = _deferredTraceparentIndex < count ? _deferredTraceparentIndex : -1;
        _deferredTracestateIndex = _deferredTracestateIndex < count ? _deferredTracestateIndex : -1;
        _keySchemaIdentityIndex = GetRetainedSchemaIdentityIndex(
            _keySchemaIdentityIndex,
            count,
            KeySchemaIdentityHeader);
        _valueSchemaIdentityIndex = GetRetainedSchemaIdentityIndex(
            _valueSchemaIdentityIndex,
            count,
            ValueSchemaIdentityHeader);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AddDeferredTraceContext(object activity, string? traceState)
    {
        if (_deferredTraceContextVersion != 0)
        {
            PushDeferredTraceContext();
            RemoveCurrentDeferredTraceContext();
        }

        _deferredTraceparentIndex = _headers.Count;
        var version = unchecked(_nextDeferredTraceContextVersion + 1);
        if (version == 0)
            version = 1;
        _nextDeferredTraceContextVersion = version;
        _deferredTraceContextVersion = version;
        _headers.Add(Header.CreateDeferredTraceparent("traceparent", activity));
        if (string.IsNullOrEmpty(traceState))
            return;

        _deferredTracestateIndex = _headers.Count;
        _headers.Add(Header.CreateDeferredTracestate("tracestate", traceState!));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void RemoveDeferredTraceContext()
    {
        if (_serializationSource is { } source)
        {
            source.RemoveDeferredTraceContext();
            return;
        }

        RemoveCurrentDeferredTraceContext();
        RestorePreviousDeferredTraceContext();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void RemoveDeferredTraceContext(object? expectedTraceparentToken)
    {
        if (expectedTraceparentToken is null ||
            (uint)_deferredTraceparentIndex >= (uint)_headers.Count ||
            !ReferenceEquals(
                expectedTraceparentToken,
                _headers[_deferredTraceparentIndex].DeferredTraceparentToken))
        {
            return;
        }

        RemoveDeferredTraceContext();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RemoveDeferredTraceContext(int expectedVersion)
    {
        if (expectedVersion == _deferredTraceContextVersion)
            RemoveDeferredTraceContext();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RemoveCurrentDeferredTraceContext()
    {
        var traceparentIndex = _deferredTraceparentIndex;
        var tracestateIndex = _deferredTracestateIndex;
        _deferredTraceContextVersion = 0;
        if (traceparentIndex < 0 && tracestateIndex < 0)
            return;

        _deferredTraceparentIndex = -1;
        _deferredTracestateIndex = -1;

        if ((uint)tracestateIndex < (uint)_headers.Count)
            RemoveAt(tracestateIndex);

        if ((uint)traceparentIndex < (uint)_headers.Count
            && _headers[traceparentIndex].HasDeferredTraceparent)
            RemoveAt(traceparentIndex);
    }

    private void PushDeferredTraceContext()
    {
        var checkpoint = new DeferredTraceContextCheckpoint(
            (uint)_deferredTraceparentIndex < (uint)_headers.Count
                ? _headers[_deferredTraceparentIndex]
                : default,
            (uint)_deferredTracestateIndex < (uint)_headers.Count
                ? _headers[_deferredTracestateIndex]
                : default,
            _deferredTraceContextVersion);
        var depth = _deferredTraceContextDepth;
        if (depth == 0)
        {
            _previousDeferredTraceContext = checkpoint;
        }
        else
        {
            var stackIndex = depth - 1;
            var stack = _deferredTraceContextStack;
            if (stack is null)
            {
                stack = new DeferredTraceContextCheckpoint[2];
                _deferredTraceContextStack = stack;
            }
            else if (stackIndex == stack.Length)
            {
                Array.Resize(ref stack, stack.Length * 2);
                _deferredTraceContextStack = stack;
            }

            stack[stackIndex] = checkpoint;
        }

        _deferredTraceContextDepth = depth + 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RestorePreviousDeferredTraceContext()
    {
        var depth = _deferredTraceContextDepth;
        if (depth == 0)
            return;

        depth--;
        DeferredTraceContextCheckpoint checkpoint;
        if (depth == 0)
        {
            checkpoint = _previousDeferredTraceContext;
            _previousDeferredTraceContext = default;
        }
        else
        {
            var stackIndex = depth - 1;
            checkpoint = _deferredTraceContextStack![stackIndex];
            _deferredTraceContextStack[stackIndex] = default;
        }

        _deferredTraceContextDepth = depth;
        _deferredTraceparentIndex = -1;
        _deferredTracestateIndex = -1;
        _deferredTraceContextVersion = checkpoint.Version;
        if (!checkpoint.Traceparent.HasDeferredTraceparent)
            return;

        _deferredTraceparentIndex = _headers.Count;
        _headers.Add(checkpoint.Traceparent);
        if (checkpoint.Tracestate.Key is null)
            return;

        _deferredTracestateIndex = _headers.Count;
        _headers.Add(checkpoint.Tracestate);
    }

    private void ClearPreviousDeferredTraceContexts()
    {
        _previousDeferredTraceContext = default;
        if (_deferredTraceContextStack is not null)
            Array.Clear(_deferredTraceContextStack, 0, _deferredTraceContextStack.Length);
        _deferredTraceContextDepth = 0;
    }

    private void RemoveAt(int index)
    {
        _headers.RemoveAt(index);
        _deferredTraceparentIndex = AdjustTrackedIndex(_deferredTraceparentIndex, index);
        _deferredTracestateIndex = AdjustTrackedIndex(_deferredTracestateIndex, index);
        _keySchemaIdentityIndex = AdjustSchemaIdentityIndex(
            _keySchemaIdentityIndex,
            index,
            KeySchemaIdentityHeader);
        _valueSchemaIdentityIndex = AdjustSchemaIdentityIndex(
            _valueSchemaIdentityIndex,
            index,
            ValueSchemaIdentityHeader);
    }

    private bool ContainsSerializationHeader(string key)
    {
        var count = SerializationCount;
        for (var index = 0; index < count; index++)
        {
            if (GetSerializationHeader(index).Key == key)
                return true;
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void MaterializeWithout(string key)
    {
        var count = SerializationCount;
        var source = _serializationSource!;
        _headers.Clear();
        if (_headers.Capacity < count)
            _headers.Capacity = count;
        var traceparentIndex = -1;
        var tracestateIndex = -1;
        _keySchemaIdentityIndex = -1;
        _valueSchemaIdentityIndex = -1;
        for (var index = 0; index < count; index++)
        {
            var header = GetSerializationHeader(index);
            if (header.Key == key)
                continue;

            var materializedIndex = _headers.Count;
            _headers.Add(header);
            if (header.HasDeferredTraceparent)
                traceparentIndex = materializedIndex;
            else if (header.DeferredValue is string)
                tracestateIndex = materializedIndex;
            TrackSchemaIdentityCandidate(header.Key, materializedIndex);
        }

        source.RemoveDeferredTraceContext();
        EndRecordHeaderStaging();
        _serializationSource = null;
        _deferredTraceparentIndex = traceparentIndex;
        _deferredTracestateIndex = tracestateIndex;
        _stagingRecordHeaders = true;
    }

    private void RemoveStagedHeaders(string key)
    {
        var count = _stagedHeaderCount;
        var retained = 0;
        for (var index = 0; index < count; index++)
        {
            var header = GetStagedHeader(index);
            if (header.Key != key)
                SetStagedHeader(retained++, header);
        }
        if (retained == count)
            return;

        for (var index = retained; index < count; index++)
            SetStagedHeader(index, default);
        _stagedHeaderCount = retained;
        if (retained <= 2)
            ReturnStagedHeaderOverflow();
        RebuildStagedSchemaIdentityIndexes();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetStagedHeader(int index, Header header)
    {
        if (index == 0)
            _stagedHeader0 = header;
        else if (index == 1)
            _stagedHeader1 = header;
        else
            _stagedHeaderOverflow![index - 2] = header;
    }

    private void RebuildStagedSchemaIdentityIndexes()
    {
        _stagedKeySchemaIdentityIndex = -1;
        _stagedValueSchemaIdentityIndex = -1;
        for (var index = 0; index < _stagedHeaderCount; index++)
        {
            var key = GetStagedHeader(index).Key;
            if (string.Equals(key, KeySchemaIdentityHeader, StringComparison.Ordinal))
                _stagedKeySchemaIdentityIndex = index;
            else if (string.Equals(key, ValueSchemaIdentityHeader, StringComparison.Ordinal))
                _stagedValueSchemaIdentityIndex = index;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddHeader(Header header)
    {
        if (_stagingRecordHeaders)
        {
            AddStagedRecordHeader(header);
            return;
        }

        var traceparentIndex = _deferredTraceparentIndex;
        if (traceparentIndex < 0)
        {
            _headers.Add(header);
            TrackSchemaIdentityCandidate(header.Key, _headers.Count - 1);
            return;
        }

        AddHeaderBeforeDeferredTraceContext(header, traceparentIndex);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddHeaderBeforeDeferredTraceContext(Header header, int traceparentIndex)
    {
        var traceparent = _headers[traceparentIndex];
        var tracestateIndex = _deferredTracestateIndex;
        if (tracestateIndex < 0)
        {
            _headers.Add(traceparent);
            _headers[traceparentIndex] = header;
            _deferredTraceparentIndex = traceparentIndex + 1;
        }
        else
        {
            var tracestate = _headers[tracestateIndex];
            _headers.Add(tracestate);
            _headers[traceparentIndex] = header;
            _headers[tracestateIndex] = traceparent;
            _deferredTraceparentIndex = traceparentIndex + 1;
            _deferredTracestateIndex = tracestateIndex + 1;
        }

        TrackSchemaIdentityCandidate(header.Key, traceparentIndex);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryGetLastSchemaIdentity(SerializationComponent component, out Header header)
    {
        var stagedIndex = component switch
        {
            SerializationComponent.Key => _stagedKeySchemaIdentityIndex,
            SerializationComponent.Value => _stagedValueSchemaIdentityIndex,
            _ => throw new ArgumentOutOfRangeException(nameof(component), component, "Unknown serialization component.")
        };
        if ((uint)stagedIndex < (uint)_stagedHeaderCount)
        {
            header = GetStagedHeader(stagedIndex);
            return true;
        }

        var source = _serializationSource;
        var index = component == SerializationComponent.Key
            ? source?._keySchemaIdentityIndex ?? _keySchemaIdentityIndex
            : source?._valueSchemaIdentityIndex ?? _valueSchemaIdentityIndex;
        var headers = source?._headers ?? _headers;
        if ((uint)index < (uint)headers.Count)
        {
            header = headers[index];
            return true;
        }

        header = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddStagedRecordHeader(Header header)
    {
        var stagedIndex = _stagedHeaderCount;
        if (stagedIndex == 0)
            _stagedHeader0 = header;
        else if (stagedIndex == 1)
            _stagedHeader1 = header;
        else
            AddStagedOverflowHeader(header, stagedIndex - 2);

        _stagedHeaderCount = stagedIndex + 1;
        var key = header.Key;
        if (key.Length == KeySchemaIdentityHeader.Length
            && string.Equals(key, KeySchemaIdentityHeader, StringComparison.Ordinal))
            _stagedKeySchemaIdentityIndex = stagedIndex;
        else if (key.Length == ValueSchemaIdentityHeader.Length
                 && string.Equals(key, ValueSchemaIdentityHeader, StringComparison.Ordinal))
            _stagedValueSchemaIdentityIndex = stagedIndex;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EndRecordHeaderStaging()
    {
        if (!_stagingRecordHeaders)
            return;

        _stagingRecordHeaders = false;
        _stagedHeaderCount = 0;
        _stagedHeader0 = default;
        _stagedHeader1 = default;
        ReturnStagedHeaderOverflow();
        _stagedKeySchemaIdentityIndex = -1;
        _stagedValueSchemaIdentityIndex = -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Header GetStagedHeader(int index) => index switch
    {
        0 => _stagedHeader0,
        1 => _stagedHeader1,
        _ => _stagedHeaderOverflow![index - 2]
    };

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddStagedOverflowHeader(Header header, int overflowIndex)
    {
        var overflow = _stagedHeaderOverflow;
        if (overflow is null)
        {
            overflow = ArrayPool<Header>.Shared.Rent(4);
            _stagedHeaderOverflow = overflow;
        }
        else if (overflowIndex == overflow.Length)
        {
            var resized = ArrayPool<Header>.Shared.Rent(checked(overflow.Length * 2));
            overflow.AsSpan().CopyTo(resized);
            ArrayPool<Header>.Shared.Return(overflow, clearArray: true);
            overflow = resized;
            _stagedHeaderOverflow = resized;
        }

        overflow[overflowIndex] = header;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReturnStagedHeaderOverflow()
    {
        var overflow = _stagedHeaderOverflow;
        if (overflow is null)
            return;

        _stagedHeaderOverflow = null;
        ArrayPool<Header>.Shared.Return(overflow, clearArray: true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PushStagedRecordHeaders()
    {
        var checkpoint = new StagedRecordHeadersCheckpoint(
            _stagedHeader0,
            _stagedHeader1,
            _stagedHeaderOverflow,
            _stagedHeaderCount,
            _stagedKeySchemaIdentityIndex,
            _stagedValueSchemaIdentityIndex);
        var depth = _stagedRecordHeadersDepth;
        if (depth == 0)
        {
            _previousStagedRecordHeaders = checkpoint;
        }
        else
        {
            var stackIndex = depth - 1;
            var stack = _stagedRecordHeadersStack;
            if (stack is null)
            {
                stack = new StagedRecordHeadersCheckpoint[2];
                _stagedRecordHeadersStack = stack;
            }
            else if (stackIndex == stack.Length)
            {
                Array.Resize(ref stack, stack.Length * 2);
                _stagedRecordHeadersStack = stack;
            }

            stack[stackIndex] = checkpoint;
        }

        _stagedRecordHeadersDepth = depth + 1;
        _stagedHeaderOverflow = null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RestorePreviousStagedRecordHeaders()
    {
        var depth = _stagedRecordHeadersDepth;
        if (depth == 0)
            return;

        depth--;
        StagedRecordHeadersCheckpoint checkpoint;
        if (depth == 0)
        {
            checkpoint = _previousStagedRecordHeaders;
            _previousStagedRecordHeaders = default;
        }
        else
        {
            var stackIndex = depth - 1;
            checkpoint = _stagedRecordHeadersStack![stackIndex];
            _stagedRecordHeadersStack[stackIndex] = default;
        }

        _stagedRecordHeadersDepth = depth;
        _stagedHeader0 = checkpoint.Header0;
        _stagedHeader1 = checkpoint.Header1;
        _stagedHeaderOverflow = checkpoint.Overflow;
        _stagedHeaderCount = checkpoint.Count;
        _stagedKeySchemaIdentityIndex = checkpoint.KeySchemaIdentityIndex;
        _stagedValueSchemaIdentityIndex = checkpoint.ValueSchemaIdentityIndex;
        _stagingRecordHeaders = true;
    }

    private void ClearPreviousStagedRecordHeaders()
    {
        ReturnStagedHeaderOverflow(_previousStagedRecordHeaders.Overflow);
        _previousStagedRecordHeaders = default;
        if (_stagedRecordHeadersStack is not null)
        {
            for (var index = 0; index < _stagedRecordHeadersDepth - 1; index++)
                ReturnStagedHeaderOverflow(_stagedRecordHeadersStack[index].Overflow);
            Array.Clear(_stagedRecordHeadersStack, 0, _stagedRecordHeadersStack.Length);
        }
        _stagedRecordHeadersDepth = 0;
    }

    private static void ReturnStagedHeaderOverflow(Header[]? overflow)
    {
        if (overflow is not null)
            ArrayPool<Header>.Shared.Return(overflow, clearArray: true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void TrackSchemaIdentityCandidate(string? key, int index)
    {
        if (key is null)
            return;

        if (key.Length == KeySchemaIdentityHeader.Length
            && string.Equals(key, KeySchemaIdentityHeader, StringComparison.Ordinal))
        {
            _keySchemaIdentityIndex = index;
        }
        else if (key.Length == ValueSchemaIdentityHeader.Length
                 && string.Equals(key, ValueSchemaIdentityHeader, StringComparison.Ordinal))
        {
            _valueSchemaIdentityIndex = index;
        }
    }

    private void InitializeSchemaIdentityIndexes()
    {
        for (var index = _headers.Count - 1; index >= 0; index--)
        {
            var key = _headers[index].Key;
            if (_keySchemaIdentityIndex < 0
                && string.Equals(key, KeySchemaIdentityHeader, StringComparison.Ordinal))
            {
                _keySchemaIdentityIndex = index;
            }
            else if (_valueSchemaIdentityIndex < 0
                     && string.Equals(key, ValueSchemaIdentityHeader, StringComparison.Ordinal))
            {
                _valueSchemaIdentityIndex = index;
            }

            if (_keySchemaIdentityIndex >= 0 && _valueSchemaIdentityIndex >= 0)
                return;
        }
    }

    private int AdjustSchemaIdentityIndex(int trackedIndex, int removedIndex, string key)
    {
        if (trackedIndex != removedIndex)
            return trackedIndex > removedIndex ? trackedIndex - 1 : trackedIndex;

        return FindLastSchemaIdentityIndex(key, _headers.Count);
    }

    private int GetRetainedSchemaIdentityIndex(int trackedIndex, int count, string key) =>
        trackedIndex < count ? trackedIndex : FindLastSchemaIdentityIndex(key, count);

    private int FindLastSchemaIdentityIndex(string key, int count)
    {
        for (var index = count - 1; index >= 0; index--)
        {
            if (string.Equals(_headers[index].Key, key, StringComparison.Ordinal))
                return index;
        }

        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int AdjustTrackedIndex(int trackedIndex, int removedIndex) =>
        trackedIndex == removedIndex ? -1 : trackedIndex > removedIndex ? trackedIndex - 1 : trackedIndex;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int AdjustTrackedIndex(int trackedIndex, int removedIndex, int removedCount) =>
        trackedIndex < removedIndex
            ? trackedIndex
            : trackedIndex < removedIndex + removedCount
                ? -1
                : trackedIndex - removedCount;

    internal readonly record struct Checkpoint(
        int Count,
        int KeySchemaIdentityIndex,
        int ValueSchemaIdentityIndex,
        int DeferredTraceContextVersion);

    private readonly record struct DeferredTraceContextCheckpoint(
        Header Traceparent,
        Header Tracestate,
        int Version);

    private readonly record struct StagedRecordHeadersCheckpoint(
        Header Header0,
        Header Header1,
        Header[]? Overflow,
        int Count,
        int KeySchemaIdentityIndex,
        int ValueSchemaIdentityIndex);

    /// <summary>
    /// Adds multiple headers from a collection of key-value pairs.
    /// </summary>
    /// <param name="headers">The headers to add.</param>
    /// <returns>This Headers instance for chaining.</returns>
    public Headers AddRange(IEnumerable<KeyValuePair<string, string>> headers)
    {
        foreach (var kvp in headers)
        {
            Add(kvp.Key, kvp.Value);
        }
        return this;
    }

    /// <summary>
    /// Adds a header conditionally.
    /// </summary>
    /// <param name="condition">If true, the header is added; otherwise, nothing happens.</param>
    /// <param name="key">The header key.</param>
    /// <param name="value">The header value.</param>
    /// <returns>This Headers instance for chaining.</returns>
    public Headers AddIf(bool condition, string key, string value)
    {
        if (condition)
        {
            Add(key, value);
        }
        return this;
    }

    /// <summary>
    /// Adds a header if the value is not null.
    /// </summary>
    /// <param name="key">The header key.</param>
    /// <param name="value">The header value (if null, header is not added).</param>
    /// <returns>This Headers instance for chaining.</returns>
    public Headers AddIfNotNull(string key, string? value)
    {
        if (value is not null)
        {
            Add(key, value);
        }
        return this;
    }

    /// <summary>
    /// Adds a header if the value is not null or empty.
    /// </summary>
    /// <param name="key">The header key.</param>
    /// <param name="value">The header value (if null or empty, header is not added).</param>
    /// <returns>This Headers instance for chaining.</returns>
    public Headers AddIfNotNullOrEmpty(string key, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            Add(key, value!);
        }
        return this;
    }

    /// <summary>
    /// Gets all headers as a list.
    /// </summary>
    public IReadOnlyList<Header> ToList() => this;

    public IEnumerator<Header> GetEnumerator()
    {
        var count = Count;
        for (var index = 0; index < count; index++)
            yield return GetSerializationHeader(index);
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Represents a single header in a Kafka record.
/// Uses ReadOnlyMemory to avoid copying header data.
/// This is a struct to avoid heap allocations in the hot path.
/// </summary>
public readonly record struct Header
{
    internal delegate void DeferredTraceparentWriter(object value, Span<byte> destination);

    internal const int TraceparentLength = 55;
    private static DeferredTraceparentWriter? s_deferredTraceparentWriter;
    private readonly ReadOnlyMemory<byte> _value;
    private readonly object? _deferredValue;

    /// <summary>
    /// Creates a new header with a byte array value.
    /// </summary>
    public Header(string key, byte[]? value)
    {
        Key = key;
        _value = value.AsMemory();
        _deferredValue = null;
        IsValueNull = value is null;
    }

    /// <summary>
    /// Creates a new header with a memory value (zero-copy).
    /// </summary>
    public Header(string key, ReadOnlyMemory<byte> value, bool isNull = false)
    {
        Key = key;
        _value = value;
        _deferredValue = null;
        IsValueNull = isNull;
    }

    private Header(string key, object deferredValue)
    {
        Key = key;
        _value = default;
        _deferredValue = deferredValue;
        IsValueNull = false;
    }

    internal static Header CreateDeferredTraceparent(string key, object activity) =>
        new(key, activity);

    internal static Header CreateDeferredTracestate(string key, string traceState) =>
        new(key, traceState);

    internal bool HasDeferredTraceparent => _deferredValue is not null and not string;
    internal object? DeferredTraceparentToken => HasDeferredTraceparent ? _deferredValue : null;

    /// <summary>
    /// The header key.
    /// </summary>
    public string Key { get; init; }

    /// <summary>
    /// The header value as bytes. Check IsValueNull before accessing.
    /// </summary>
    public ReadOnlyMemory<byte> Value
    {
        // Preserve the metadata emitted by the previously shipped auto-property accessors.
        [CompilerGenerated]
        get
        {
            return _deferredValue switch
            {
                null => _value,
                string traceState => Encoding.UTF8.GetBytes(traceState),
                { } deferredTraceparent => EncodeTraceparent(deferredTraceparent)
            };
        }
        [CompilerGenerated]
        init
        {
            _value = value;
            _deferredValue = null;
        }
    }

    /// <summary>
    /// Returns true if the header value is null.
    /// </summary>
    public bool IsValueNull { get; init; }

    /// <summary>
    /// Gets the value as a byte array. Prefer using Value property to avoid allocation.
    /// </summary>
    public byte[]? GetValueAsArray()
    {
        if (IsValueNull)
            return null;

        return _deferredValue switch
        {
            null => _value.ToArray(),
            string traceState => Encoding.UTF8.GetBytes(traceState),
            { } deferredTraceparent => EncodeTraceparent(deferredTraceparent)
        };
    }

    /// <summary>
    /// Gets the value as a UTF-8 string.
    /// </summary>
    public string? GetValueAsString()
    {
        if (IsValueNull)
            return null;

        if (_deferredValue is string traceState)
            return traceState;
        if (_deferredValue is null)
        {
#if NETSTANDARD2_0
            if (MemoryMarshal.TryGetArray(_value, out var segment))
            {
                return Encoding.UTF8.GetString(
                    segment.Array!,
                    segment.Offset,
                    segment.Count);
            }

            return GetUtf8String(_value.Span);
#else
            return Encoding.UTF8.GetString(_value.Span);
#endif
        }

        Span<byte> value = stackalloc byte[TraceparentLength];
        WriteTraceparentUnchecked(_deferredValue, value);
#if NETSTANDARD2_0
        return GetUtf8String(value);
#else
        return Encoding.UTF8.GetString(value);
#endif
    }

    /// <inheritdoc/>
    public override string ToString() => $"{Key}={GetValueAsString() ?? "(null)"}";

    internal ReadOnlyMemory<byte> RawValue => _value;

    internal object? DeferredValue => _deferredValue;

    internal static void ConfigureDeferredTraceparentWriter(DeferredTraceparentWriter writer) =>
        s_deferredTraceparentWriter = writer;

#if NETSTANDARD2_0
    private static unsafe string GetUtf8String(ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty)
            return string.Empty;

        fixed (byte* valuePointer = value)
        {
            return Encoding.UTF8.GetString(valuePointer, value.Length);
        }
    }
#endif

    private static byte[] EncodeTraceparent(object activity)
    {
#if NETSTANDARD2_0
        var value = new byte[TraceparentLength];
#else
        var value = GC.AllocateUninitializedArray<byte>(TraceparentLength);
#endif
        WriteTraceparentUnchecked(activity, value);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteTraceparentUnchecked(object activity, Span<byte> destination)
    {
        var writer = s_deferredTraceparentWriter
            ?? throw new InvalidOperationException("Deferred traceparent writer is not configured.");
        writer(activity, destination);
    }
}
