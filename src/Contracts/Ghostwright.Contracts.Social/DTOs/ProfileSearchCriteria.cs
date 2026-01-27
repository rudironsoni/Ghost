namespace Ghostwright.Contracts.Social;

/// <summary>
/// Criteria used to search for social profiles.
/// </summary>
public sealed record ProfileSearchCriteria
{
    /// <summary>
    /// Textual query to match against name, username, and bio.
    /// </summary>
    public string Query { get; init; } = string.Empty;

    /// <summary>
    /// Maximum number of results to return. Defaults to 25.
    /// </summary>
    public int MaxResults { get; init; } = 25;
}
