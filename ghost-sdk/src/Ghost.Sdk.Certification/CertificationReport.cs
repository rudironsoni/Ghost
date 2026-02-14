namespace Ghost.Sdk.Certification;

/// <summary>
/// Overall certification report for a plugin.
/// </summary>
public sealed record CertificationReport(
    bool Passed,
    CertificationMode Mode,
    DateTimeOffset Timestamp,
    IReadOnlyList<CertificationResult> Results,
    string Summary);

/// <summary>
/// Result for a single spider/test within a certification run.
/// </summary>
public sealed record CertificationResult(
    string TestId,
    bool Passed,
    string? FailureReason,
    TimeSpan Duration,
    IReadOnlyDictionary<string, object> Metrics);
