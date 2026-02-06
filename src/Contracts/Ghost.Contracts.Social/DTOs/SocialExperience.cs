using System;

namespace Ghost.Contracts.Social;

public sealed record SocialExperience
{
    public string Title { get; init; } = string.Empty;
    public string Company { get; init; } = string.Empty;
    public string? Location { get; init; }
    public string? Description { get; init; }
    public DateTimeOffset? StartDate { get; init; }
    public DateTimeOffset? EndDate { get; init; }
    public string? Duration { get; init; } // e.g., "2 yrs 4 mos"
    public bool IsCurrent { get; init; }
}
