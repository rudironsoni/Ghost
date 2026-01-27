namespace Ghostwright.Contracts.Jobs;

/// <summary>
/// Details supplied when applying to a job.
/// </summary>
public sealed record ApplicationDetails
{
    /// <summary>
    /// Applicant's full name.
    /// </summary>
    public string ApplicantName { get; init; } = string.Empty;

    /// <summary>
    /// Applicant's email address.
    /// </summary>
    public string ApplicantEmail { get; init; } = string.Empty;

    /// <summary>
    /// URL to the applicant's resume.
    /// </summary>
    public string? ResumeUrl { get; init; }

    /// <summary>
    /// Optional cover letter text.
    /// </summary>
    public string? CoverLetter { get; init; }
}
