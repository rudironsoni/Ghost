using System;

namespace Ghostwright.Contracts.Jobs;

/// <summary>
/// Represents a job listing.
/// </summary>
public sealed record JobListing
{
    /// <summary>
    /// Unique job id.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Job title.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Company offering the role.
    /// </summary>
    public string Company { get; init; } = string.Empty;

    /// <summary>
    /// Location of the role (city, remote, etc.).
    /// </summary>
    public string? Location { get; init; }

    /// <summary>
    /// Full job description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Salary info if available (free-form string).
    /// </summary>
    public string? Salary { get; init; }

    /// <summary>
    /// The type of job (FullTime/PartTime/etc.).
    /// </summary>
    public JobType JobType { get; init; } = JobType.Unknown;

    /// <summary>
    /// Experience level required.
    /// </summary>
    public ExperienceLevel ExperienceLevel { get; init; } = ExperienceLevel.Unknown;

    /// <summary>
    /// When the job was posted.
    /// </summary>
    public DateTimeOffset PostedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Whether the role is remote.
    /// </summary>
    public bool Remote { get; init; }

    /// <summary>
    /// Application url.
    /// </summary>
    public string? Url { get; init; }
}
