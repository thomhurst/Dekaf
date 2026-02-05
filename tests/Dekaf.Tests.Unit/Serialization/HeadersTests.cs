using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Serialization;

public class HeadersTests
{
    #region Constructor Tests

    [Test]
    public async Task Constructor_Default_CreatesEmptyCollection()
    {
        var headers = new Headers();
        await Assert.That(headers.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Constructor_WithCapacity_CreatesEmptyCollection()
    {
        var headers = new Headers(10);
        await Assert.That(headers.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Constructor_FromCollection_CopiesHeaders()
    {
        var source = new[] { new Header("key1", "value1"u8.ToArray()), new Header("key2", "value2"u8.ToArray()) };
        var headers = new Headers(source);
        await Assert.That(headers.Count).IsEqualTo(2);
    }

    #endregion

    #region Factory Tests

    [Test]
    public async Task Create_ReturnsEmptyHeaders()
    {
        var headers = Headers.Create();
        await Assert.That(headers.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Create_WithKeyAndStringValue_ReturnsHeadersWithOneItem()
    {
        var headers = Headers.Create("key", "value");
        await Assert.That(headers.Count).IsEqualTo(1);
        await Assert.That(headers[0].Key).IsEqualTo("key");
        await Assert.That(headers[0].GetValueAsString()).IsEqualTo("value");
    }

    [Test]
    public async Task Create_WithKeyAndByteArrayValue_ReturnsHeadersWithOneItem()
    {
        var value = "value"u8.ToArray();
        var headers = Headers.Create("key", value);
        await Assert.That(headers.Count).IsEqualTo(1);
        await Assert.That(headers[0].Key).IsEqualTo("key");
    }

    [Test]
    public async Task Create_WithNullByteArray_ReturnsHeadersWithNullValue()
    {
        var headers = Headers.Create("key", (byte[]?)null);
        await Assert.That(headers.Count).IsEqualTo(1);
        await Assert.That(headers[0].IsValueNull).IsTrue();
    }

    #endregion

    #region Count and Indexer Tests

    [Test]
    public async Task Count_ReflectsAddedHeaders()
    {
        var headers = new Headers();
        headers.Add("key1", "value1");
        headers.Add("key2", "value2");
        headers.Add("key3", "value3");
        await Assert.That(headers.Count).IsEqualTo(3);
    }

    [Test]
    public async Task Indexer_ReturnsCorrectHeader()
    {
        var headers = new Headers();
        headers.Add("key1", "value1");
        headers.Add("key2", "value2");
        await Assert.That(headers[0].Key).IsEqualTo("key1");
        await Assert.That(headers[1].Key).IsEqualTo("key2");
    }

    [Test]
    public async Task Indexer_OutOfRange_ThrowsException()
    {
        var headers = new Headers();
        var act = () => headers[0];
        await Assert.That(act).ThrowsException();
    }

    #endregion

    #region Add Tests

    [Test]
    public async Task Add_StringValue_AddsHeader()
    {
        var headers = new Headers();
        headers.Add("key", "value");
        await Assert.That(headers.Count).IsEqualTo(1);
        await Assert.That(headers[0].GetValueAsString()).IsEqualTo("value");
    }

    [Test]
    public async Task Add_ByteArrayValue_AddsHeader()
    {
        var headers = new Headers();
        var value = new byte[] { 1, 2, 3 };
        headers.Add("key", value);
        await Assert.That(headers.Count).IsEqualTo(1);
        await Assert.That(headers[0].GetValueAsArray()).IsEquivalentTo(value);
    }

    [Test]
    public async Task Add_HeaderStruct_AddsHeader()
    {
        var headers = new Headers();
        var header = new Header("key", "value"u8.ToArray());
        headers.Add(header);
        await Assert.That(headers.Count).IsEqualTo(1);
        await Assert.That(headers[0].Key).IsEqualTo("key");
    }

    [Test]
    public async Task Add_ReturnsThisForChaining()
    {
        var headers = new Headers();
        var result = headers.Add("key", "value");
        await Assert.That(result).IsSameReferenceAs(headers);
    }

    [Test]
    public async Task Add_ByteArray_ReturnsThisForChaining()
    {
        var headers = new Headers();
        var result = headers.Add("key", new byte[] { 1 });
        await Assert.That(result).IsSameReferenceAs(headers);
    }

    [Test]
    public async Task Add_Header_ReturnsThisForChaining()
    {
        var headers = new Headers();
        var result = headers.Add(new Header("key", "value"u8.ToArray()));
        await Assert.That(result).IsSameReferenceAs(headers);
    }

    #endregion

    #region GetFirst Tests

    [Test]
    public async Task GetFirst_ExistingKey_ReturnsHeader()
    {
        var headers = new Headers();
        headers.Add("key", "value");
        var result = headers.GetFirst("key");
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsEqualTo("key");
    }

    [Test]
    public async Task GetFirst_NonExistentKey_ReturnsNull()
    {
        var headers = new Headers();
        headers.Add("key", "value");
        var result = headers.GetFirst("nonexistent");
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetFirst_MultipleWithSameKey_ReturnsFirst()
    {
        var headers = new Headers();
        headers.Add("key", "first");
        headers.Add("key", "second");
        headers.Add("key", "third");
        var result = headers.GetFirst("key");
        await Assert.That(result!.Value.GetValueAsString()).IsEqualTo("first");
    }

    #endregion

    #region GetAll Tests

    [Test]
    public async Task GetAll_ExistingKey_ReturnsAllMatching()
    {
        var headers = new Headers();
        headers.Add("key", "first");
        headers.Add("other", "other");
        headers.Add("key", "second");
        var result = headers.GetAll("key").ToList();
        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result[0].GetValueAsString()).IsEqualTo("first");
        await Assert.That(result[1].GetValueAsString()).IsEqualTo("second");
    }

    [Test]
    public async Task GetAll_NonExistentKey_ReturnsEmpty()
    {
        var headers = new Headers();
        headers.Add("key", "value");
        var result = headers.GetAll("nonexistent").ToList();
        await Assert.That(result.Count).IsEqualTo(0);
    }

    #endregion

    #region GetFirstAsString Tests

    [Test]
    public async Task GetFirstAsString_ExistingKey_ReturnsString()
    {
        var headers = new Headers();
        headers.Add("key", "value");
        var result = headers.GetFirstAsString("key");
        await Assert.That(result).IsEqualTo("value");
    }

    [Test]
    public async Task GetFirstAsString_NonExistentKey_ReturnsNull()
    {
        var headers = new Headers();
        var result = headers.GetFirstAsString("nonexistent");
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetFirstAsString_NullValue_ReturnsNull()
    {
        var headers = new Headers();
        headers.Add("key", (byte[]?)null);
        var result = headers.GetFirstAsString("key");
        await Assert.That(result).IsNull();
    }

    #endregion

    #region Remove Tests

    [Test]
    public async Task Remove_ExistingKey_RemovesAllWithThatKey()
    {
        var headers = new Headers();
        headers.Add("key", "first");
        headers.Add("other", "other");
        headers.Add("key", "second");
        headers.Remove("key");
        await Assert.That(headers.Count).IsEqualTo(1);
        await Assert.That(headers[0].Key).IsEqualTo("other");
    }

    [Test]
    public async Task Remove_NonExistentKey_DoesNothing()
    {
        var headers = new Headers();
        headers.Add("key", "value");
        headers.Remove("nonexistent");
        await Assert.That(headers.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Remove_ReturnsThisForChaining()
    {
        var headers = new Headers();
        var result = headers.Remove("key");
        await Assert.That(result).IsSameReferenceAs(headers);
    }

    #endregion

    #region Clear Tests

    [Test]
    public async Task Clear_RemovesAllHeaders()
    {
        var headers = new Headers();
        headers.Add("key1", "value1");
        headers.Add("key2", "value2");
        headers.Clear();
        await Assert.That(headers.Count).IsEqualTo(0);
    }

    #endregion

    #region AddRange Tests

    [Test]
    public async Task AddRange_AddsAllFromCollection()
    {
        var headers = new Headers();
        var kvps = new Dictionary<string, string>
        {
            ["key1"] = "value1",
            ["key2"] = "value2"
        };
        headers.AddRange(kvps);
        await Assert.That(headers.Count).IsEqualTo(2);
    }

    [Test]
    public async Task AddRange_ReturnsThisForChaining()
    {
        var headers = new Headers();
        var result = headers.AddRange([]);
        await Assert.That(result).IsSameReferenceAs(headers);
    }

    #endregion

    #region AddIf Tests

    [Test]
    public async Task AddIf_ConditionTrue_AddsHeader()
    {
        var headers = new Headers();
        headers.AddIf(true, "key", "value");
        await Assert.That(headers.Count).IsEqualTo(1);
    }

    [Test]
    public async Task AddIf_ConditionFalse_DoesNotAddHeader()
    {
        var headers = new Headers();
        headers.AddIf(false, "key", "value");
        await Assert.That(headers.Count).IsEqualTo(0);
    }

    [Test]
    public async Task AddIf_ReturnsThisForChaining()
    {
        var headers = new Headers();
        var result = headers.AddIf(true, "key", "value");
        await Assert.That(result).IsSameReferenceAs(headers);
    }

    #endregion

    #region AddIfNotNull Tests

    [Test]
    public async Task AddIfNotNull_NonNullValue_AddsHeader()
    {
        var headers = new Headers();
        headers.AddIfNotNull("key", "value");
        await Assert.That(headers.Count).IsEqualTo(1);
    }

    [Test]
    public async Task AddIfNotNull_NullValue_DoesNotAddHeader()
    {
        var headers = new Headers();
        headers.AddIfNotNull("key", null);
        await Assert.That(headers.Count).IsEqualTo(0);
    }

    [Test]
    public async Task AddIfNotNull_ReturnsThisForChaining()
    {
        var headers = new Headers();
        var result = headers.AddIfNotNull("key", "value");
        await Assert.That(result).IsSameReferenceAs(headers);
    }

    #endregion

    #region AddIfNotNullOrEmpty Tests

    [Test]
    public async Task AddIfNotNullOrEmpty_NonEmptyValue_AddsHeader()
    {
        var headers = new Headers();
        headers.AddIfNotNullOrEmpty("key", "value");
        await Assert.That(headers.Count).IsEqualTo(1);
    }

    [Test]
    public async Task AddIfNotNullOrEmpty_NullValue_DoesNotAddHeader()
    {
        var headers = new Headers();
        headers.AddIfNotNullOrEmpty("key", null);
        await Assert.That(headers.Count).IsEqualTo(0);
    }

    [Test]
    public async Task AddIfNotNullOrEmpty_EmptyValue_DoesNotAddHeader()
    {
        var headers = new Headers();
        headers.AddIfNotNullOrEmpty("key", "");
        await Assert.That(headers.Count).IsEqualTo(0);
    }

    [Test]
    public async Task AddIfNotNullOrEmpty_ReturnsThisForChaining()
    {
        var headers = new Headers();
        var result = headers.AddIfNotNullOrEmpty("key", "value");
        await Assert.That(result).IsSameReferenceAs(headers);
    }

    #endregion

    #region ToList Tests

    [Test]
    public async Task ToList_ReturnsReadOnlyList()
    {
        var headers = new Headers();
        headers.Add("key1", "value1");
        headers.Add("key2", "value2");
        var list = headers.ToList();
        await Assert.That(list.Count).IsEqualTo(2);
        await Assert.That(list).IsAssignableTo<IReadOnlyList<Header>>();
    }

    #endregion

    #region GetEnumerator Tests

    [Test]
    public async Task GetEnumerator_IteratesAllHeaders()
    {
        var headers = new Headers();
        headers.Add("key1", "value1");
        headers.Add("key2", "value2");
        headers.Add("key3", "value3");

        var keys = new List<string>();
        foreach (var header in headers)
        {
            keys.Add(header.Key);
        }

        await Assert.That(keys.Count).IsEqualTo(3);
        await Assert.That(keys).Contains("key1");
        await Assert.That(keys).Contains("key2");
        await Assert.That(keys).Contains("key3");
    }

    #endregion
}
