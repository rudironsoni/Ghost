using System;

namespace Ghostwright.Contracts.Jobs;

/// <summary>
/// Represents an application submitted to a job.
/// </summary>
public sealed record JobApplication
{
    /// <summary>
    /// Application id.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// The job id this application targets.
    /// </summary>
    public string JobId { get; init; } = string.Empty;

    /// <summary>
    /// Applicant id (on the platform) or external identifier.
    /// </summary>
    public string ApplicantId { get; init; } = string.Empty;

    /// <summary>
    /// Current status of the application.
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// When the application was submitted.
    /// </summary>
    public DateTimeOffset SubmittedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Details that were provided with the application.
    /// </summary>
    public ApplicationDetails? Details { get; init; }
}
