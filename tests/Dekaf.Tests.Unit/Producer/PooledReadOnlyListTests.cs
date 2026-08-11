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

    private static IEnumerable<int> Enumerate(int count)
    {
        for (var i = 0; i < count; i++)
            yield return i;
    }
}
