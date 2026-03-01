namespace Ghost.Sdk.Spider.Meta;

/// <summary>
/// Provides a type-safe metadata dictionary implementation for storing spider metadata.
/// </summary>
/// <remarks>
/// This class extends <see cref="Dictionary{TKey, TValue}"/> to provide type-safe
/// access methods for metadata values. It is particularly useful in spider scenarios
/// where metadata needs to be passed between requests, responses, and processing components.
/// <para>
/// Example usage:
/// <code>
/// var meta = new MetaDictionary();
/// meta.SetValue("depth", 3);
/// meta.SetValue("start_url", "https://example.com");
/// meta.SetValue("retry_count", 0);
/// 
/// var depth = meta.GetValue&lt;int&gt;("depth");
/// if (meta.TryGet&lt;string&gt;("start_url", out var startUrl))
/// {
///     Console.WriteLine($"Started from: {startUrl}");
/// }
/// </code>
/// </para>
/// </remarks>
public class MetaDictionary : Dictionary<string, object>, IMetaDictionary
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MetaDictionary"/> class.
    /// </summary>
    public MetaDictionary()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MetaDictionary"/> class
    /// with the specified initial capacity.
    /// </summary>
    /// <param name="capacity">The initial capacity of the dictionary.</param>
    public MetaDictionary(int capacity) : base(capacity)
    {
    }

    /// <summary>
    /// Gets the value associated with the specified key, cast to the specified type.
    /// </summary>
    /// <typeparam name="T">The type to cast the value to.</typeparam>
    /// <param name="key">The key of the value to get.</param>
    /// <returns>The value associated with the specified key, cast to type <typeparamref name="T"/>.</returns>
    /// <exception cref="KeyNotFoundException">The key does not exist in the dictionary.</exception>
    /// <exception cref="InvalidCastException">The value cannot be cast to type <typeparamref name="T"/>.</exception>
    public T GetValue<T>(string key) => (T)this[key];

    /// <summary>
    /// Sets the value associated with the specified key.
    /// </summary>
    /// <typeparam name="T">The type of the value to set.</typeparam>
    /// <param name="key">The key of the value to set.</param>
    /// <param name="value">The value to associate with the key.</param>
    public void SetValue<T>(string key, T value) => this[key] = value!;

    /// <summary>
    /// Attempts to get the value associated with the specified key, cast to the specified type.
    /// </summary>
    /// <typeparam name="T">The type to cast the value to.</typeparam>
    /// <param name="key">The key of the value to get.</param>
    /// <param name="value">
    /// When this method returns, contains the value associated with the specified key
    /// if the key is found and the value can be cast to type <typeparamref name="T"/>;
    /// otherwise, the default value for type <typeparamref name="T"/>.
    /// </param>
    /// <returns>
    /// <c>true</c> if the dictionary contains an element with the specified key
    /// and the value can be cast to type <typeparamref name="T"/>; otherwise, <c>false</c>.
    /// </returns>
    public bool TryGet<T>(string key, out T value)
    {
        if (TryGetValue(key, out object? obj) && obj is T typed)
        {
            value = typed;
            return true;
        }
        value = default!;
        return false;
    }
}
