namespace Ghostwright.Contracts.Social;

/// <summary>
/// Options controlling feed retrieval.
/// </summary>
public sealed record FeedOptions
{
    /// <summary>
    /// Page size, defaults to 25.
    /// </summary>
    public int PageSize { get; init; } = 25;

    /// <summary>
    /// Optional continuation token for pagination.
    /// </summary>
    public string? PageToken { get; init; }
}
