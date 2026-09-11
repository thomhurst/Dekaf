using System.Reflection;
using System.Runtime.CompilerServices;
using Dekaf.Consumer;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class KeyOrderedStorageTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ScalarDictionaryKey_DoesNotPayForBinaryHashStorage(bool customComparer)
    {
        var dictionary = CreateDictionary<int>(customComparer ? EqualityComparer<int>.Default : null);
        var keyType = dictionary.GetType().GenericTypeArguments[0];
        var keySize = (int)typeof(KeyOrderedStorageTests)
            .GetMethod(nameof(SizeOf), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(keyType).Invoke(null, null)!;

        // Eight bytes fit the original 24-byte dictionary entry (hash, next, key, lane).
        // A twelve-byte key enlarges every preallocated entry to 32 bytes on x64.
        await Assert.That(keySize).IsEqualTo(8);
    }

    [Test]
    public async Task BinaryDictionaryKey_RetainsFullCachedHashRepresentation()
    {
        await Assert.That(CreateDictionary<byte[]>().GetType().GenericTypeArguments[0])
            .IsEqualTo(typeof(PartitionMessageKey<byte[]>));
        await Assert.That(CreateDictionary<ReadOnlyMemory<byte>>().GetType().GenericTypeArguments[0])
            .IsEqualTo(typeof(PartitionMessageKey<ReadOnlyMemory<byte>>));
    }

    private static int SizeOf<T>() => Unsafe.SizeOf<T>();

    [Test]
    public async Task CompactKeys_CustomComparerPreservesWireNullAndDefaultValue()
    {
        var comparer = new CustomUncachedPartitionMessageKeyComparer<int>(new ModuloTenComparer());
        var wireNull = new PartitionMessageKey<int>.Uncached(PartitionMessageKey<int>.From(0, isKeyNull: true));
        var zero = new PartitionMessageKey<int>.Uncached(PartitionMessageKey<int>.From(0));
        var ten = new PartitionMessageKey<int>.Uncached(PartitionMessageKey<int>.From(10));
        var keys = new Dictionary<PartitionMessageKey<int>.Uncached, string>(comparer)
        {
            [wireNull] = "wire-null",
            [zero] = "value"
        };
        await Assert.That(keys.Count).IsEqualTo(2);
        await Assert.That(keys[wireNull]).IsEqualTo("wire-null");
        await Assert.That(keys[ten]).IsEqualTo("value");
        await Assert.That(comparer.GetHashCode(zero)).IsEqualTo(comparer.GetHashCode(ten));
    }

    internal sealed class ModuloTenComparer : IEqualityComparer<int>
    {
        public bool Equals(int x, int y) => x % 10 == y % 10;
        public int GetHashCode(int obj) => 0; // Also exercise collisions with the wire-null key.
    }

    private static object CreateDictionary<TKey>(IEqualityComparer<TKey>? comparer = null)
    {
        var lane = new PartitionLane<TKey, int>(new TopicPartition("topic", 0), 128,
            static (_, _) => default, static _ => { }, static (_, error) => throw error);
        var dispatcher = new KeyOrderedPartitionDispatcher<TKey, int>(
            new PartitionProcessorContext<TKey, int>(lane), 16, 2, 128,
            static (_, _) => default, comparer);
        return typeof(KeyOrderedPartitionDispatcher<TKey, int>)
            .GetField("_lanes", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dispatcher)!;
    }
}
