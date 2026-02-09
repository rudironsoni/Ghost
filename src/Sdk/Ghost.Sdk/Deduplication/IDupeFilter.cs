using Ghost.Sdk.Spider.Adapters.Contracts;

namespace Ghost.Sdk.Deduplication;

/// <summary>
/// Interface for duplicate request detection and filtering.
/// </summary>
/// <remarks>
/// Implementations of this interface are responsible for tracking request fingerprints
/// to prevent duplicate requests from being processed. This is essential for efficient
/// web scraping and crawling operations.
/// </remarks>
public interface IDupeFilter
{
    /// <summary>
    /// Checks if a request is a duplicate based on its fingerprint.
    /// </summary>
    /// <param name="request">The request to check.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>true</c> if the request is a duplicate (has been seen before);
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This method will mark the request as seen if it is not a duplicate.
    /// Subsequent calls with the same request fingerprint will return <c>true</c>.
    /// </remarks>
    Task<bool> IsDuplicateAsync(Request request, CancellationToken ct = default);

    /// <summary>
    /// Checks if a fingerprint is a duplicate.
    /// </summary>
    /// <param name="fingerprint">The fingerprint to check.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>true</c> if the fingerprint has been seen before;
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This method will mark the fingerprint as seen if it is not a duplicate.
    /// Subsequent calls with the same fingerprint will return <c>true</c>.
    /// </remarks>
    Task<bool> IsDuplicateAsync(string fingerprint, CancellationToken ct = default);

    /// <summary>
    /// Clears all stored fingerprints from the filter.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// After calling this method, all previously seen requests/fingerprints
    /// will be treated as new.
    /// </remarks>
    Task ClearAsync(CancellationToken ct = default);
}
