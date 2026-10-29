using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Dekaf.Serialization;

/// <summary>
/// Thread-confined writers configured from their serializer options.
/// </summary>
internal sealed class JsonWriterCache
{
    // Only used as a Reset target; serialization never writes to this buffer.
    private static readonly ArrayBufferWriter<byte> DetachedBuffer = new();

    private ConditionalWeakTable<JsonSerializerOptions, Utf8JsonWriter>? _writers;
    private JsonSerializerOptions? _lastOptions;
    private Utf8JsonWriter? _lastWriter;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Utf8JsonWriter GetWriter(IBufferWriter<byte> buffer, JsonSerializerOptions options)
    {
        var writer = _lastWriter;
        if (writer is not null && ReferenceEquals(_lastOptions, options) && options.IsReadOnly)
        {
            writer.Reset(buffer);
            return writer;
        }

        return GetConfiguredWriter(buffer, options);
    }

    private Utf8JsonWriter GetConfiguredWriter(IBufferWriter<byte> buffer, JsonSerializerOptions options)
    {
        // Inactive cached writers must not retain an old scratch array or buffer.
        _lastWriter?.Reset(DetachedBuffer);

        // Options can still change before System.Text.Json first uses them. Only immutable
        // configurations enter the weak cache; the last writer becomes reusable once frozen.
        Utf8JsonWriter writer;
        if (options.IsReadOnly)
        {
            var writers = _writers ??= new();
            if (!writers.TryGetValue(options, out writer!))
            {
                writer = CreateWriter(options);
                writers.Add(options, writer);
            }
        }
        else
        {
            writer = CreateWriter(options);
        }
        _lastOptions = options;
        _lastWriter = writer;
        writer.Reset(buffer);
        return writer;
    }

    public void DiscardCurrent()
    {
        _lastWriter?.Reset(DetachedBuffer);
        if (_lastOptions is not null)
            _writers?.Remove(_lastOptions);
        _lastOptions = null;
        _lastWriter = null;
    }

    private static Utf8JsonWriter CreateWriter(JsonSerializerOptions options)
    {
        var writerOptions = new JsonWriterOptions
        {
            Encoder = options.Encoder,
            Indented = options.WriteIndented,
            MaxDepth = options.MaxDepth == 0 ? 64 : options.MaxDepth,
#if NET10_0_OR_GREATER
            IndentCharacter = options.IndentCharacter,
            IndentSize = options.IndentSize,
            NewLine = options.NewLine,
#endif
        };
        return new Utf8JsonWriter(DetachedBuffer, writerOptions);
    }
}
