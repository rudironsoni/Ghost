using Ghost.Sdk.Spider.Adapters.Contracts;

namespace Ghost.Sdk.Deduplication;

/// <summary>
/// Request Fingerprint-based duplicate filter combining fingerprinting with storage.
/// </summary>
/// <remarks>
/// This class provides a high-level API for request deduplication by combining
/// the <see cref="RequestFingerprinter"/> for generating fingerprints with a
/// storage backend implementing <see cref="IDupeFilter"/> for tracking seen requests.
/// By default, it uses <see cref="InMemoryDupeFilter"/> for storage.
/// </remarks>
public sealed class RFPDupeFilter : IDupeFilter
{
    private readonly IDupeFilter _storage;

    /// <summary>
    /// Initializes a new instance of the <see cref="RFPDupeFilter"/> class
    /// with in-memory storage.
    /// </summary>
    public RFPDupeFilter()
        : this(new InMemoryDupeFilter())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RFPDupeFilter"/> class
    /// with a custom storage backend.
    /// </summary>
    /// <param name="storage">The storage backend to use for tracking fingerprints.</param>
    /// <remarks>
    /// This constructor allows injection of custom storage implementations,
    /// such as Redis-backed or database-backed filters for distributed systems.
    /// </remarks>
    public RFPDupeFilter(IDupeFilter storage)
    {
        ArgumentNullException.ThrowIfNull(storage);
        _storage = storage;
    }

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
    /// This method creates a fingerprint from the request using <see cref="RequestFingerprinter"/>
    /// and delegates duplicate checking to the underlying storage backend.
    /// </remarks>
    public Task<bool> IsDuplicateAsync(Request request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string fingerprint = RequestFingerprinter.CreateFingerprint(request);
        return _storage.IsDuplicateAsync(fingerprint, ct);
    }

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
    /// This method delegates directly to the underlying storage backend.
    /// </remarks>
    public Task<bool> IsDuplicateAsync(string fingerprint, CancellationToken ct = default)
    {
        return _storage.IsDuplicateAsync(fingerprint, ct);
    }

    /// <summary>
    /// Clears all stored fingerprints from the filter.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// This method delegates to the underlying storage backend to clear all data.
    /// </remarks>
    public Task ClearAsync(CancellationToken ct = default)
    {
        return _storage.ClearAsync(ct);
    }
}
