namespace Ghostwright.Hosting;

/// <summary>
/// Exception thrown when extension validation or loading fails.
/// </summary>
public sealed class ExtensionException : Exception
{
    /// <summary>
    /// Extension friendly name that caused the error.
    /// </summary>
    public string ExtensionName { get; }

    /// <summary>
    /// Creates a new instance of <see cref="ExtensionException"/>.
    /// </summary>
    /// <param name="extensionName">Extension name.</param>
    /// <param name="message">Error message.</param>
    public ExtensionException(string extensionName, string message) : base(message)
    {
        ExtensionName = extensionName;
    }
}
