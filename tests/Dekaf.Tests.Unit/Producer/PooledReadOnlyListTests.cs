using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

public class PooledReadOnlyListTests
{
    [Test]
    public async Task Rent_EnumerableBeyondInlineCapacity_PreservesCountAndOrder()
    {
        var count = 0;
        var preservesOrder = true;

        {
            using var messages = PooledReadOnlyList<int>.Rent(Enumerate(100));
            count = messages.Count;
            for (var i = 0; i < messages.Count; i++)
                preservesOrder &= messages[i] == i;
        }

        await Assert.That(count).IsEqualTo(100);
        await Assert.That(preservesOrder).IsTrue();
    }

    private static IEnumerable<int> Enumerate(int count)
    {
        for (var i = 0; i < count; i++)
            yield return i;
    }
}
