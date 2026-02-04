using Ghost.Sdk.Spider.Adapters.Contracts;

namespace Ghost.Sdk.Spider.Engine;

/// <summary>
/// Base class for all spider implementations.
/// </summary>
/// <remarks>
/// Inherit from this class to create custom spiders. Override the appropriate
/// methods to define spider behavior, parsing logic, and data extraction.
/// </remarks>
public abstract class Spider
{
    /// <summary>
    /// Gets the name of this spider.
    /// </summary>
    /// <value>A unique identifier for the spider.</value>
    public abstract string Name { get; }

    /// <summary>
    /// Gets the spider configuration options.
    /// </summary>
    /// <value>The options controlling spider behavior.</value>
    public virtual SpiderOptions Options { get; } = new();

    /// <summary>
    /// Gets the start URLs for this spider.
    /// </summary>
    /// <returns>List of URLs to begin crawling.</returns>
    public abstract IEnumerable<string> GetStartUrls();

    /// <summary>
    /// Processes a response and extracts data.
    /// </summary>
    /// <param name="response">The response to process.</param>
    /// <param name="context">The execution context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    public abstract Task ProcessResponseAsync(
        Response response,
        ExecutionContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Called when the spider starts executing.
    /// </summary>
    /// <param name="context">The execution context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    public virtual Task OnStartAsync(ExecutionContext context, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when the spider finishes executing.
    /// </summary>
    /// <param name="context">The execution context.</param>
    /// <param name="result">The spider result.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    public virtual Task OnCompleteAsync(
        ExecutionContext context,
        SpiderResult result,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when an error occurs during spider execution.
    /// </summary>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="context">The execution context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    public virtual Task OnErrorAsync(
        Exception exception,
        ExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Determines whether a URL should be followed.
    /// </summary>
    /// <param name="url">The URL to evaluate.</param>
    /// <param name="context">The execution context.</param>
    /// <returns><c>true</c> to follow the URL; otherwise, <c>false</c>.</returns>
    public virtual bool ShouldFollowUrl(string url, ExecutionContext context)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        // Check allowed domains
        if (Options.AllowedDomains.Count > 0)
        {
            var host = uri.Host.ToLowerInvariant();
            if (!Options.AllowedDomains.Any(d => host.Contains(d.ToLowerInvariant())))
                return false;
        }

        // Check exclude patterns
        if (Options.ExcludePatterns.Count > 0)
        {
            foreach (var pattern in Options.ExcludePatterns)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(url, pattern))
                    return false;
            }
        }

        return true;
    }
}
