namespace Ghost.Platform.Rpc;

/// <summary>
/// Client interface for communicating with an out-of-process executor.
/// </summary>
public interface IExecutorClient : IAsyncDisposable
{
    /// <summary>
    /// Performs handshake with the executor.
    /// </summary>
    /// <param name="request">Handshake request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Handshake response.</returns>
    Task<HandshakeResponse> HandshakeAsync(
        HandshakeRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams messages from the executor.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Async enumerable of executor messages.</returns>
    IAsyncEnumerable<ExecutorMessage> StreamMessagesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a start job request to the executor.
    /// </summary>
    /// <param name="request">Start job request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Start job response.</returns>
    Task<StartJobResponse> StartJobAsync(
        StartJobRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a stop job request to the executor.
    /// </summary>
    /// <param name="request">Stop job request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Stop job response.</returns>
    Task<StopJobResponse> StopJobAsync(
        StopJobRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a shutdown request to the executor.
    /// </summary>
    /// <param name="request">Shutdown request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Shutdown response.</returns>
    Task<ShutdownResponse> ShutdownAsync(
        ShutdownRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether the client is connected.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Gets the executor ID if connected.
    /// </summary>
    string? ExecutorId { get; }
}
