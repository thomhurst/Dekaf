using System.Runtime.CompilerServices;
using Dekaf.Consumer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class ConsumerExtensionsTests
{
    #region Where

    [Test]
    public async Task Where_FiltersMatchingItems()
    {
        var source = CreateResults(("topic", 0, 0), ("topic", 0, 1), ("topic", 0, 2));

        var results = new List<ConsumeResult<string, string>>();
        await foreach (var item in source.Where(r => r.Offset % 2 == 0))
        {
            results.Add(item);
        }

        await Assert.That(results).Count().IsEqualTo(2);
        await Assert.That(results[0].Offset).IsEqualTo(0);
        await Assert.That(results[1].Offset).IsEqualTo(2);
    }

    [Test]
    public async Task Where_NoMatches_ReturnsEmpty()
    {
        var source = CreateResults(("topic", 0, 0), ("topic", 0, 1));

        var results = new List<ConsumeResult<string, string>>();
        await foreach (var item in source.Where(_ => false))
        {
            results.Add(item);
        }

        await Assert.That(results).Count().IsEqualTo(0);
    }

    [Test]
    public async Task Where_NullSource_ThrowsArgumentNullException()
    {
        IAsyncEnumerable<ConsumeResult<string, string>>? source = null;

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in source!.Where(_ => true)) { }
        });
    }

    [Test]
    public async Task Where_NullPredicate_ThrowsArgumentNullException()
    {
        var source = CreateResults(("topic", 0, 0));
        Func<ConsumeResult<string, string>, bool> predicate = null!;

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in source.Where(predicate)) { }
        });
    }

    #endregion

    #region Select

    [Test]
    public async Task Select_ProjectsItems()
    {
        var source = CreateResults(("topic", 0, 10), ("topic", 0, 20));

        var results = new List<long>();
        await foreach (var offset in source.Select(r => r.Offset))
        {
            results.Add(offset);
        }

        await Assert.That(results).Count().IsEqualTo(2);
        await Assert.That(results[0]).IsEqualTo(10);
        await Assert.That(results[1]).IsEqualTo(20);
    }

    [Test]
    public async Task Select_NullSource_ThrowsArgumentNullException()
    {
        IAsyncEnumerable<ConsumeResult<string, string>>? source = null;

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in source!.Select(r => r.Offset)) { }
        });
    }

    [Test]
    public async Task Select_NullSelector_ThrowsArgumentNullException()
    {
        var source = CreateResults(("topic", 0, 0));

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in source.Select<string, string, long>(null!)) { }
        });
    }

    #endregion

    #region Take

    [Test]
    public async Task Take_ReturnsFirstNItems()
    {
        var source = CreateResults(("topic", 0, 0), ("topic", 0, 1), ("topic", 0, 2), ("topic", 0, 3));

        var results = new List<ConsumeResult<string, string>>();
        await foreach (var item in source.Take(2))
        {
            results.Add(item);
        }

        await Assert.That(results).Count().IsEqualTo(2);
        await Assert.That(results[0].Offset).IsEqualTo(0);
        await Assert.That(results[1].Offset).IsEqualTo(1);
    }

    [Test]
    public async Task Take_Zero_ReturnsEmpty()
    {
        var source = CreateResults(("topic", 0, 0), ("topic", 0, 1));

        var results = new List<ConsumeResult<string, string>>();
        await foreach (var item in source.Take(0))
        {
            results.Add(item);
        }

        await Assert.That(results).Count().IsEqualTo(0);
    }

    [Test]
    public async Task Take_MoreThanAvailable_ReturnsAll()
    {
        var source = CreateResults(("topic", 0, 0), ("topic", 0, 1));

        var results = new List<ConsumeResult<string, string>>();
        await foreach (var item in source.Take(100))
        {
            results.Add(item);
        }

        await Assert.That(results).Count().IsEqualTo(2);
    }

    [Test]
    public async Task Take_Negative_ThrowsArgumentOutOfRangeException()
    {
        var source = CreateResults(("topic", 0, 0));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
        {
            await foreach (var _ in ConsumerExtensions.Take<string, string>(source, -1)) { }
        });
    }

    #endregion

    #region Batch

    [Test]
    public async Task Batch_GroupsIntoCorrectSizes()
    {
        var source = CreateResults(
            ("topic", 0, 0), ("topic", 0, 1), ("topic", 0, 2),
            ("topic", 0, 3), ("topic", 0, 4));

        var batches = new List<IReadOnlyList<ConsumeResult<string, string>>>();
        await foreach (var batch in source.Batch(2))
        {
            batches.Add(batch);
        }

        await Assert.That(batches).Count().IsEqualTo(3);
        await Assert.That(batches[0]).Count().IsEqualTo(2);
        await Assert.That(batches[1]).Count().IsEqualTo(2);
        await Assert.That(batches[2]).Count().IsEqualTo(1); // Remainder
    }

    [Test]
    public async Task Batch_ExactMultiple_NoRemainder()
    {
        var source = CreateResults(("topic", 0, 0), ("topic", 0, 1), ("topic", 0, 2), ("topic", 0, 3));

        var batches = new List<IReadOnlyList<ConsumeResult<string, string>>>();
        await foreach (var batch in source.Batch(2))
        {
            batches.Add(batch);
        }

        await Assert.That(batches).Count().IsEqualTo(2);
        await Assert.That(batches[0]).Count().IsEqualTo(2);
        await Assert.That(batches[1]).Count().IsEqualTo(2);
    }

    [Test]
    public async Task Batch_LessThan1_ThrowsArgumentOutOfRangeException()
    {
        var source = CreateResults(("topic", 0, 0));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
        {
            await foreach (var _ in source.Batch(0)) { }
        });
    }

    #endregion

    #region SkipWhile

    [Test]
    public async Task SkipWhile_SkipsMatchingItemsThenYieldsRest()
    {
        var source = CreateResults(("topic", 0, 0), ("topic", 0, 1), ("topic", 0, 2), ("topic", 0, 3));

        var results = new List<ConsumeResult<string, string>>();
        await foreach (var item in source.SkipWhile(r => r.Offset < 2))
        {
            results.Add(item);
        }

        await Assert.That(results).Count().IsEqualTo(2);
        await Assert.That(results[0].Offset).IsEqualTo(2);
        await Assert.That(results[1].Offset).IsEqualTo(3);
    }

    [Test]
    public async Task SkipWhile_AllMatch_ReturnsEmpty()
    {
        var source = CreateResults(("topic", 0, 0), ("topic", 0, 1));

        var results = new List<ConsumeResult<string, string>>();
        await foreach (var item in source.SkipWhile(_ => true))
        {
            results.Add(item);
        }

        await Assert.That(results).Count().IsEqualTo(0);
    }

    [Test]
    public async Task SkipWhile_NoneMatch_ReturnsAll()
    {
        var source = CreateResults(("topic", 0, 0), ("topic", 0, 1));

        var results = new List<ConsumeResult<string, string>>();
        await foreach (var item in source.SkipWhile(_ => false))
        {
            results.Add(item);
        }

        await Assert.That(results).Count().IsEqualTo(2);
    }

    #endregion

    #region TakeWhile

    [Test]
    public async Task TakeWhile_TakesWhilePredicateIsTrue()
    {
        var source = CreateResults(("topic", 0, 0), ("topic", 0, 1), ("topic", 0, 2), ("topic", 0, 3));

        var results = new List<ConsumeResult<string, string>>();
        await foreach (var item in source.TakeWhile(r => r.Offset < 2))
        {
            results.Add(item);
        }

        await Assert.That(results).Count().IsEqualTo(2);
        await Assert.That(results[0].Offset).IsEqualTo(0);
        await Assert.That(results[1].Offset).IsEqualTo(1);
    }

    [Test]
    public async Task TakeWhile_AllMatch_ReturnsAll()
    {
        var source = CreateResults(("topic", 0, 0), ("topic", 0, 1));

        var results = new List<ConsumeResult<string, string>>();
        await foreach (var item in source.TakeWhile(_ => true))
        {
            results.Add(item);
        }

        await Assert.That(results).Count().IsEqualTo(2);
    }

    [Test]
    public async Task TakeWhile_NoneMatch_ReturnsEmpty()
    {
        var source = CreateResults(("topic", 0, 0), ("topic", 0, 1));

        var results = new List<ConsumeResult<string, string>>();
        await foreach (var item in source.TakeWhile(_ => false))
        {
            results.Add(item);
        }

        await Assert.That(results).Count().IsEqualTo(0);
    }

    #endregion

    #region Helpers

    private static async IAsyncEnumerable<ConsumeResult<string, string>> CreateResults(
        params (string Topic, int Partition, long Offset)[] items)
    {
        foreach (var (topic, partition, offset) in items)
        {
            yield return new ConsumeResult<string, string>(
                topic: topic,
                partition: partition,
                offset: offset,
                keyData: default,
                isKeyNull: true,
                valueData: default,
                isValueNull: true,
                headers: null,
                timestamp: default,
                timestampType: TimestampType.NotAvailable,
                leaderEpoch: null,
                keyDeserializer: null,
                valueDeserializer: null);
        }

        await Task.CompletedTask; // Ensure truly async
    }

    #endregion
}
