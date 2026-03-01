using Microsoft.Playwright;

namespace Ghost.Sdk.Spider.Adapters.Contracts;

/// <summary>
/// Represents an active browser session for JavaScript content extraction.
/// </summary>
/// <remarks>
/// A browser session encapsulates a browser context and page, providing isolated
/// execution environment for web scraping operations. Sessions should be returned
/// to the pool after use to enable resource reuse.
/// </remarks>
public interface IBrowserSession : IAsyncDisposable
{
    /// <summary>
    /// Gets the unique identifier for this browser session.
    /// </summary>
    /// <value>A unique session identifier for tracking and logging.</value>
    public string SessionId { get; }

    /// <summary>
    /// Gets the Playwright page instance for browser interaction.
    /// </summary>
    /// <value>The page object that provides browser automation capabilities.</value>
    public IPage Page { get; }

    /// <summary>
    /// Gets the Playwright browser context instance.
    /// </summary>
    /// <value>The browser context providing an isolated browsing session.</value>
    public IBrowserContext Context { get; }

    /// <summary>
    /// Gets a value indicating whether this session is currently in use.
    /// </summary>
    /// <value><c>true</c> if the session is active; otherwise, <c>false</c>.</value>
    public bool IsActive { get; }

    /// <summary>
    /// Gets the timestamp when this session was created.
    /// </summary>
    /// <value>The UTC timestamp of session creation.</value>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets the timestamp of the last activity on this session.
    /// </summary>
    /// <value>The UTC timestamp of the most recent session activity.</value>
    public DateTimeOffset LastActivityAt { get; }

    /// <summary>
    /// Navigates to the specified URL and waits for the page to load.
    /// </summary>
    /// <param name="url">The URL to navigate to.</param>
    /// <param name="options">Optional navigation options.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// the navigation response.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="url"/> is null.</exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is canceled via <paramref name="cancellationToken"/>.
    /// </exception>
    public Task<IResponse?> NavigateAsync(string url, PageGotoOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes JavaScript code in the page context.
    /// </summary>
    /// <typeparam name="T">The type of the expected return value.</typeparam>
    /// <param name="script">The JavaScript code to execute.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// the script execution result.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="script"/> is null.</exception>
    public Task<T> EvaluateAsync<T>(string script, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the HTML content of the current page.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// the page HTML content.
    /// </returns>
    public Task<string> GetContentAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Waits for a selector to appear in the DOM.
    /// </summary>
    /// <param name="selector">The CSS selector to wait for.</param>
    /// <param name="options">Optional wait options.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// the element handle if found, or null if the timeout is reached.
    /// </returns>
    public Task<IElementHandle?> WaitForSelectorAsync(string selector, PageWaitForSelectorOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Waits for the network to become idle.
    /// </summary>
    /// <param name="timeout">The maximum time to wait.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task WaitForNetworkIdleAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes a screenshot of the current page.
    /// </summary>
    /// <param name="options">Optional screenshot options.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// the screenshot as a byte array.
    /// </returns>
    public Task<byte[]> ScreenshotAsync(PageScreenshotOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the session to a clean state for reuse.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// This method clears cookies, cache, and navigation history to prepare
    /// the session for reuse by another request.
    /// </remarks>
    public Task ResetAsync(CancellationToken cancellationToken = default);
}
