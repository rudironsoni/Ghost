namespace Ghost.Sdk.Spider.Meta;

/// <summary>
/// Represents a type-safe metadata dictionary for storing spider metadata.
/// </summary>
/// <remarks>
/// This interface provides a strongly-typed wrapper around a standard dictionary,
/// enabling type-safe access to metadata values while maintaining flexibility
/// for dynamic metadata storage. Common use cases include:
/// <list type="bullet">
/// <item>Tracking request depth in recursive scraping</item>
/// <item>Storing start URLs for reference</item>
/// <item>Maintaining session identifiers</item>
/// <item>Passing custom data between spider components</item>
/// </list>
/// </remarks>
public interface IMetaDictionary : IDictionary<string, object>
{
    /// <summary>
    /// Gets the value associated with the specified key, cast to the specified type.
    /// </summary>
    /// <typeparam name="T">The type to cast the value to.</typeparam>
    /// <param name="key">The key of the value to get.</param>
    /// <returns>The value associated with the specified key, cast to type <typeparamref name="T"/>.</returns>
    /// <exception cref="KeyNotFoundException">The key does not exist in the dictionary.</exception>
    /// <exception cref="InvalidCastException">The value cannot be cast to type <typeparamref name="T"/>.</exception>
    public T GetValue<T>(string key);

    /// <summary>
    /// Sets the value associated with the specified key.
    /// </summary>
    /// <typeparam name="T">The type of the value to set.</typeparam>
    /// <param name="key">The key of the value to set.</param>
    /// <param name="value">The value to associate with the key.</param>
    public void SetValue<T>(string key, T value);

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
    public bool TryGet<T>(string key, out T value);
}
