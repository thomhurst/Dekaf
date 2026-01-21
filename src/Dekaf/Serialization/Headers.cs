using System.Collections;
using System.Text;

namespace Dekaf.Serialization;

/// <summary>
/// Collection of headers for a Kafka record.
/// </summary>
public sealed class Headers : IEnumerable<Header>
{
    private readonly List<Header> _headers = [];

    /// <summary>
    /// Creates an empty headers collection.
    /// </summary>
    public Headers()
    {
    }

    /// <summary>
    /// Creates a headers collection from existing headers.
    /// </summary>
    public Headers(IEnumerable<Header> headers)
    {
        _headers.AddRange(headers);
    }

    /// <summary>
    /// Gets the number of headers.
    /// </summary>
    public int Count => _headers.Count;

    /// <summary>
    /// Adds a header with a string value.
    /// </summary>
    public Headers Add(string key, string value)
    {
        _headers.Add(new Header(key, Encoding.UTF8.GetBytes(value)));
        return this;
    }

    /// <summary>
    /// Adds a header with a byte array value.
    /// </summary>
    public Headers Add(string key, byte[]? value)
    {
        _headers.Add(new Header(key, value));
        return this;
    }

    /// <summary>
    /// Adds a header.
    /// </summary>
    public Headers Add(Header header)
    {
        _headers.Add(header);
        return this;
    }

    /// <summary>
    /// Gets the first header with the specified key.
    /// </summary>
    public Header? GetFirst(string key)
    {
        return _headers.Find(h => h.Key == key);
    }

    /// <summary>
    /// Gets all headers with the specified key.
    /// </summary>
    public IEnumerable<Header> GetAll(string key)
    {
        return _headers.Where(h => h.Key == key);
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
        _headers.RemoveAll(h => h.Key == key);
        return this;
    }

    /// <summary>
    /// Clears all headers.
    /// </summary>
    public void Clear()
    {
        _headers.Clear();
    }

    /// <summary>
    /// Gets all headers as a list.
    /// </summary>
    public IReadOnlyList<Header> ToList() => _headers.AsReadOnly();

    public IEnumerator<Header> GetEnumerator() => _headers.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Represents a single header in a Kafka record.
/// </summary>
public sealed class Header
{
    /// <summary>
    /// Creates a new header.
    /// </summary>
    public Header(string key, byte[]? value)
    {
        Key = key;
        Value = value;
    }

    /// <summary>
    /// The header key.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// The header value as bytes.
    /// </summary>
    public byte[]? Value { get; }

    /// <summary>
    /// Gets the value as a UTF-8 string.
    /// </summary>
    public string? GetValueAsString()
    {
        return Value is null ? null : Encoding.UTF8.GetString(Value);
    }

    public override string ToString() => $"{Key}={GetValueAsString() ?? "(null)"}";
}
