using System.Collections.Concurrent;
using Ghost.Sdk.Spider.Adapters.Contracts;

namespace Ghost.Sdk.Deduplication;

/// <summary>
/// In-memory implementation of duplicate request filter using a thread-safe hash set.
/// </summary>
/// <remarks>
/// This implementation stores fingerprints in memory using a <see cref="ConcurrentDictionary{TKey,TValue}"/>
/// for thread-safe operations. Suitable for single-process, short-lived scraping sessions.
/// For distributed or persistent deduplication, consider implementing a Redis-backed or
/// database-backed filter.
/// </remarks>
public sealed class InMemoryDupeFilter : IDupeFilter
{
    private readonly ConcurrentDictionary<string, byte> _seenFingerprints = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryDupeFilter"/> class.
    /// </summary>
    public InMemoryDupeFilter()
    {
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
    /// This method creates a fingerprint from the request and checks if it has been seen.
    /// If the fingerprint is new, it is added to the internal set.
    /// </remarks>
    public Task<bool> IsDuplicateAsync(Request request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string fingerprint = RequestFingerprinter.CreateFingerprint(request);
        return IsDuplicateAsync(fingerprint, ct);
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
    /// Uses TryAdd to atomically check and add the fingerprint if it's new.
    /// This ensures thread-safety without explicit locking.
    /// </remarks>
    public Task<bool> IsDuplicateAsync(string fingerprint, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);

        // TryAdd returns false if key already exists (duplicate)
        // Returns true if key was added (not a duplicate)
        bool wasAdded = _seenFingerprints.TryAdd(fingerprint, 0);

        // Return true if it's a duplicate (was NOT added)
        return Task.FromResult(!wasAdded);
    }

    /// <summary>
    /// Clears all stored fingerprints from the filter.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task ClearAsync(CancellationToken ct = default)
    {
        _seenFingerprints.Clear();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the count of unique fingerprints stored in the filter.
    /// </summary>
    /// <value>The number of unique requests seen.</value>
    /// <remarks>
    /// This property is useful for monitoring and debugging purposes.
    /// </remarks>
    public int Count => _seenFingerprints.Count;
}
