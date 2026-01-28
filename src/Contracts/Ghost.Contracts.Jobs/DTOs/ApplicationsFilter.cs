namespace Ghost.Contracts.Jobs;

/// <summary>
/// Filter when listing applications.
/// </summary>
public sealed record ApplicationsFilter
{
    /// <summary>
    /// Filter by applicant id.
    /// </summary>
    public string? ApplicantId { get; init; }

    /// <summary>
    /// Filter by job id.
    /// </summary>
    public string? JobId { get; init; }

    /// <summary>
    /// Filter by status.
    /// </summary>
    public string? Status { get; init; }

    /// <summary>
    /// Maximum number of results to return.
    /// </summary>
    public int MaxResults { get; init; } = 25;
}
