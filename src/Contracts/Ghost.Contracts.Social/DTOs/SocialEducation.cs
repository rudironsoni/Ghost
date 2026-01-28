using System;

namespace Ghost.Contracts.Social;
public sealed record SocialEducation
{
    public string School { get; init; } = string.Empty;
    public string? Degree { get; init; }
    public string? FieldOfStudy { get; init; }
    public DateTimeOffset? StartDate { get; init; }
    public DateTimeOffset? EndDate { get; init; }
    public string? Grade { get; init; }
    public string? Description { get; init; }
}
