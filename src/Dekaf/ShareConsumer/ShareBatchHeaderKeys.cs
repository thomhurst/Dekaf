using System.IO.Hashing;
using System.Text;
using Dekaf.Serialization;

namespace Dekaf.ShareConsumer;

/// <summary>Retains configured routing names independently of the bounded global header cache.</summary>
internal sealed class ShareBatchHeaderKeys
{
    private readonly Dictionary<HashedName, string> _names = new();
    private readonly HashSet<int>? _oversizedNameLengths;

    internal ShareBatchHeaderKeys(RecordHeaderRoutingPlan plan)
    {
        foreach (var name in plan.HeaderNames)
        {
            var bytes = Encoding.UTF8.GetBytes(name);
            // An unpaired surrogate cannot match its replacement-encoded wire name.
            if (Encoding.UTF8.GetString(bytes) == name)
            {
                _names.TryAdd(new HashedName(bytes, XxHash64.HashToUInt64(bytes)), name);
                if (bytes.Length > HeaderProtocol.MaxCachedKeyBytes)
                    (_oversizedNameLengths ??= new()).Add(bytes.Length);
            }
        }
    }

    internal string Get(ReadOnlyMemory<byte> bytes)
    {
        if (HeaderProtocol.TryGetCachedKey(bytes, out var name, out var hash))
            return name;
        // The global lookup skips hashing oversized names; all other misses already have a hash.
        if (bytes.Length > HeaderProtocol.MaxCachedKeyBytes)
        {
            // A different byte length cannot match any configured name. Decode without scanning for a hash.
            if (_oversizedNameLengths is null || !_oversizedNameLengths.Contains(bytes.Length))
                return HeaderProtocol.InternUncachedKey(bytes, hash);
            hash = XxHash64.HashToUInt64(bytes.Span);
        }
        return _names.TryGetValue(new HashedName(bytes, hash), out name)
            ? name
            : HeaderProtocol.InternUncachedKey(bytes, hash);
    }

    private readonly struct HashedName(ReadOnlyMemory<byte> bytes, ulong hash) : IEquatable<HashedName>
    {
        private readonly ReadOnlyMemory<byte> _bytes = bytes;
        private readonly int _hashCode = unchecked((int)hash);

        public bool Equals(HashedName other) => _bytes.Span.SequenceEqual(other._bytes.Span);
        public override bool Equals(object? obj) => obj is HashedName other && Equals(other);
        public override int GetHashCode() => _hashCode;
    }
}
