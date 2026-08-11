using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

public class PooledReadOnlyListTests
{
    private const int MultipleItemCount = 256;

    [Test]
    public async Task Rent_EnumerableBeyondInlineCapacity_PreservesCountAndOrder()
    {
        var count = 0;
        var preservesOrder = true;

        {
            using var messages = PooledReadOnlyList<int>.Rent(Enumerate(MultipleItemCount));
            count = messages.Count;
            for (var i = 0; i < messages.Count; i++)
                preservesOrder &= messages[i] == i;
        }

        await Assert.That(count).IsEqualTo(MultipleItemCount);
        await Assert.That(preservesOrder).IsTrue();
    }

    [Test]
    public async Task Rent_Queue_PreservesCountAndOrder()
    {
        var count = 0;
        var preservesOrder = true;

        {
            using var messages = PooledReadOnlyList<int>.Rent(new Queue<int>(Enumerable.Range(0, MultipleItemCount)));
            count = messages.Count;
            for (var i = 0; i < messages.Count; i++)
                preservesOrder &= messages[i] == i;
        }

        await Assert.That(count).IsEqualTo(MultipleItemCount);
        await Assert.That(preservesOrder).IsTrue();
    }

    [Test]
    public async Task Rent_Stack_PreservesCountAndOrder()
    {
        var count = 0;
        var preservesOrder = true;

        {
            var stack = new Stack<int>(Enumerable.Range(0, MultipleItemCount).Reverse());
            using var messages = PooledReadOnlyList<int>.Rent(stack);
            count = messages.Count;
            for (var i = 0; i < messages.Count; i++)
                preservesOrder &= messages[i] == i;
        }

        await Assert.That(count).IsEqualTo(MultipleItemCount);
        await Assert.That(preservesOrder).IsTrue();
    }

    [Test]
    public async Task Rent_HashSetMultiple_PreservesCountAndOrder()
    {
        var source = new HashSet<int>(Enumerable.Range(0, MultipleItemCount));
        var expected = source.ToArray();
        var count = 0;
        var preservesOrder = true;

        {
            using var messages = PooledReadOnlyList<int>.Rent(source);
            count = messages.Count;
            for (var i = 0; i < messages.Count; i++)
                preservesOrder &= messages[i] == expected[i];
        }

        await Assert.That(count).IsEqualTo(MultipleItemCount);
        await Assert.That(preservesOrder).IsTrue();
    }

    [Test]
    public async Task Rent_Collection_PreservesCountAndOrder()
    {
        var count = 0;
        var preservesOrder = true;

        {
            var linkedList = new LinkedList<int>(Enumerable.Range(0, MultipleItemCount));
            using var messages = PooledReadOnlyList<int>.Rent(linkedList);
            count = messages.Count;
            for (var i = 0; i < messages.Count; i++)
                preservesOrder &= messages[i] == i;
        }

        await Assert.That(count).IsEqualTo(MultipleItemCount);
        await Assert.That(preservesOrder).IsTrue();
    }

    [Test]
    public async Task Rent_ArbitraryCollection_PreservesEnumerationOrder()
    {
        var source = new ReverseCopyCollection(Enumerable.Range(0, MultipleItemCount));
        var preservesOrder = true;

        {
            using var messages = PooledReadOnlyList<int>.Rent(source);
            for (var i = 0; i < messages.Count; i++)
                preservesOrder &= messages[i] == i;
        }

        await Assert.That(preservesOrder).IsTrue();
        await Assert.That(source.CopyToCalled).IsFalse();
    }

    [Test]
    public async Task Rent_SortedSetSingleton_PreservesValue()
    {
        var value = 0;

        {
            using var messages = PooledReadOnlyList<int>.Rent(new SortedSet<int> { 42 });
            value = messages[0];
        }

        await Assert.That(value).IsEqualTo(42);
    }

    [Test]
    public async Task Rent_SortedSetMultiple_PreservesCountAndOrder()
    {
        var count = 0;
        var preservesOrder = true;

        {
            var source = new SortedSet<int>(Enumerable.Range(0, MultipleItemCount).Reverse());
            using var messages = PooledReadOnlyList<int>.Rent(source);
            count = messages.Count;
            for (var i = 0; i < messages.Count; i++)
                preservesOrder &= messages[i] == i;
        }

        await Assert.That(count).IsEqualTo(MultipleItemCount);
        await Assert.That(preservesOrder).IsTrue();
    }

    [Test]
    public async Task Rent_EnumeratorDisposeThrows_PropagatesException()
    {
        Action act = static () =>
        {
            using var messages = PooledReadOnlyList<int>.Rent(new DisposeThrowingEnumerable());
        };

        await Assert.That(act).Throws<InvalidOperationException>()
            .WithMessage("dispose failed");
    }

    [Test]
    public async Task Rent_EnumerationAndDisposeThrow_PreservesEnumerationException()
    {
        Action act = static () =>
        {
            using var messages = PooledReadOnlyList<int>.Rent(new EnumerationAndDisposeThrowingEnumerable());
        };

        await Assert.That(act).Throws<ArgumentException>()
            .WithMessage("enumeration failed");
    }

    private static IEnumerable<int> Enumerate(int count)
    {
        for (var i = 0; i < count; i++)
            yield return i;
    }

    private sealed class ReverseCopyCollection(IEnumerable<int> items) : ICollection<int>
    {
        private readonly List<int> _items = [.. items];

        public bool CopyToCalled { get; private set; }

        public int Count => _items.Count;

        public bool IsReadOnly => true;

        public IEnumerator<int> GetEnumerator() => _items.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        public void CopyTo(int[] array, int arrayIndex)
        {
            CopyToCalled = true;
            for (var i = 0; i < _items.Count; i++)
                array[arrayIndex + i] = _items[_items.Count - i - 1];
        }

        public bool Contains(int item) => _items.Contains(item);

        public void Add(int item) => throw new NotSupportedException();

        public void Clear() => throw new NotSupportedException();

        public bool Remove(int item) => throw new NotSupportedException();
    }

    private sealed class DisposeThrowingEnumerable : IEnumerable<int>
    {
        public IEnumerator<int> GetEnumerator() => new Enumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        private sealed class Enumerator : IEnumerator<int>
        {
            private int _current = -1;

            public int Current => _current;

            object System.Collections.IEnumerator.Current => Current;

            public bool MoveNext() => ++_current < 2;

            public void Reset() => throw new NotSupportedException();

            public void Dispose() => throw new InvalidOperationException("dispose failed");
        }
    }

    private sealed class EnumerationAndDisposeThrowingEnumerable : IEnumerable<int>
    {
        public IEnumerator<int> GetEnumerator() => new Enumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        private sealed class Enumerator : IEnumerator<int>
        {
            private int _current = -1;

            public int Current => _current;

            object System.Collections.IEnumerator.Current => Current;

            public bool MoveNext()
            {
                if (++_current < 2)
                    return true;

                throw new ArgumentException("enumeration failed");
            }

            public void Reset() => throw new NotSupportedException();

            public void Dispose() => throw new InvalidOperationException("dispose failed");
        }
    }
}
