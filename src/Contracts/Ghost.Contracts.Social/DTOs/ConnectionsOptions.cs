namespace Ghost.Contracts.Social;

/// <summary>
/// Options for retrieving connections.
/// </summary>
public sealed record ConnectionsOptions
{
    /// <summary>
    /// Profile id to load connections for. If null, loads for the authenticated user.
    /// </summary>
    public string? ProfileId { get; init; }

    /// <summary>
    /// Maximum number of connections to return. Defaults to 50.
    /// </summary>
    public int MaxResults { get; init; } = 50;
}
