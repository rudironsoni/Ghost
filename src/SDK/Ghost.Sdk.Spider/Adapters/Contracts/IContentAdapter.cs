namespace Ghost.Sdk.Spider.Adapters.Contracts;

/// <summary>
/// Defines the contract for content adapters that extract content from various sources.
/// </summary>
/// <remarks>
/// Content adapters provide a unified interface for extracting content from different
/// sources such as static HTML, JavaScript-rendered pages, GraphQL endpoints, WebSockets,
/// and other protocols. Each adapter implementation handles the specifics of its
/// content source while presenting a consistent API.
/// </remarks>
public interface IContentAdapter
{
    /// <summary>
    /// Gets the name of the adapter.
    /// </summary>
    /// <value>A unique identifier for this adapter type (e.g., "StaticHtml", "JavaScript", "GraphQL").</value>
    string Name { get; }

    /// <summary>
    /// Gets the content type this adapter is designed to handle.
    /// </summary>
    /// <value>The content type supported by this adapter.</value>
    ContentType ContentType { get; }

    /// <summary>
    /// Gets a value indicating whether this adapter is available for use.
    /// </summary>
    /// <value>
    /// <c>true</c> if the adapter is properly configured and can be used; otherwise, <c>false</c>.
    /// </value>
    /// <remarks>
    /// An adapter may be unavailable if required dependencies are missing, configuration is invalid,
    /// or external resources are not accessible.
    /// </remarks>
    bool IsAvailable { get; }

    /// <summary>
    /// Determines whether this adapter can handle the specified request.
    /// </summary>
    /// <param name="request">The content request to evaluate.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// <c>true</c> if this adapter can handle the request; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This method allows adapters to inspect request properties (URL, headers, metadata)
    /// to determine if they are suitable for handling the request. This supports
    /// automatic adapter selection and fallback strategies.
    /// </remarks>
    Task<bool> CanHandleAsync(Request request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts content from the specified request.
    /// </summary>
    /// <param name="request">The content request containing URL and configuration.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// a <see cref="Response"/> with the extracted content and metadata.
    /// </returns>
    /// <remarks>
    /// This is the primary method for content extraction. The adapter should:
    /// <list type="bullet">
    /// <item>Fetch content from the source specified in the request</item>
    /// <item>Process and extract the relevant content</item>
    /// <item>Return a response with the content and any relevant metadata</item>
    /// <item>Handle errors gracefully and include error details in the response</item>
    /// </list>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via <paramref name="cancellationToken"/>.</exception>
    Task<Response> ExtractAsync(Request request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts content using the specified options.
    /// </summary>
    /// <param name="request">The content request containing URL and configuration.</param>
    /// <param name="options">Adapter-specific options for content extraction.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// a <see cref="Response"/> with the extracted content and metadata.
    /// </returns>
    /// <remarks>
    /// This overload allows callers to provide adapter-specific options that control
    /// extraction behavior. Options may include timeouts, retry policies, authentication,
    /// parsing rules, and other adapter-specific configurations.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> or <paramref name="options"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via <paramref name="cancellationToken"/>.</exception>
    Task<Response> ExtractAsync(Request request, AdapterOptions options, CancellationToken cancellationToken = default);
}
