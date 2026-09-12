using System.IO.Hashing;
using System.Text;
using Dekaf.Serialization;

namespace Dekaf.ShareConsumer;

/// <summary>Retains configured routing names independently of the bounded global header cache.</summary>
internal sealed class ShareBatchHeaderKeys
{
    private readonly Dictionary<ReadOnlyMemory<byte>, string> _names = new(ByteComparer.Instance);

    internal ShareBatchHeaderKeys(RecordHeaderRoutingPlan plan)
    {
        foreach (var name in plan.HeaderNames)
        {
            var bytes = Encoding.UTF8.GetBytes(name);
            // An unpaired surrogate cannot match its replacement-encoded wire name.
            if (Encoding.UTF8.GetString(bytes) == name)
                _names.TryAdd(bytes, name);
        }
    }

    internal string Get(ReadOnlyMemory<byte> bytes)
    {
        if (HeaderProtocol.TryGetCachedKey(bytes, out var name, out var hash))
            return name;
        return _names.TryGetValue(bytes, out name) ? name : HeaderProtocol.InternUncachedKey(bytes, hash);
    }

    private sealed class ByteComparer : IEqualityComparer<ReadOnlyMemory<byte>>
    {
        internal static readonly ByteComparer Instance = new();
        public bool Equals(ReadOnlyMemory<byte> x, ReadOnlyMemory<byte> y) => x.Span.SequenceEqual(y.Span);
        public int GetHashCode(ReadOnlyMemory<byte> obj) => unchecked((int)XxHash64.HashToUInt64(obj.Span));
    }
}
