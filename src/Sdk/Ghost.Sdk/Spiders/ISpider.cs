namespace Ghost.Sdk.Spiders;

/// <summary>
/// Base interface for all spider implementations.
/// </summary>
/// <remarks>
/// Spiders are responsible for defining how to navigate websites and extract data.
/// This interface provides the core contract that all spiders must implement.
/// </remarks>
public interface ISpider
{
    /// <summary>
    /// Gets the name of this spider.
    /// </summary>
    /// <value>A unique identifier for the spider.</value>
    public string Name { get; }

    /// <summary>
    /// Called when the spider starts executing.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    public Task StartAsync(CancellationToken ct);

    /// <summary>
    /// Parses a response and extracts data.
    /// </summary>
    /// <param name="response">The response to parse.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    public Task ParseAsync(Response response, CancellationToken ct);
}
