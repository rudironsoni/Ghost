namespace Ghost.Testing.Scenarios.Models;

/// <summary>
/// Represents a synthetic job posting for testing purposes.
/// </summary>
public sealed class SyntheticJobPosting
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Company { get; init; }
    public required string Location { get; init; }
    public required string Description { get; init; }
    public DateTime PostedDate { get; init; }
    public string? Salary { get; init; }
    public List<string> Requirements { get; init; } = [];
    public string? ApplyUrl { get; init; }
}

/// <summary>
/// Configuration for a scenario.
/// </summary>
public sealed class ScenarioConfig
{
    public required string ScenarioId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public Dictionary<string, object> Parameters { get; init; } = new();
}

/// <summary>
/// Types of consent mechanisms.
/// </summary>
public enum ConsentType
{
    None,
    ModalBlocking,
    BannerSoft,
    IframeCmp
}

/// <summary>
/// Types of pagination.
/// </summary>
public enum PaginationType
{
    None,
    Numbered,
    Cursor,
    Mixed
}

/// <summary>
/// Types of infinite scroll.
/// </summary>
public enum ScrollType
{
    None,
    AutoThreshold,
    ButtonDriven,
    Virtualized
}
