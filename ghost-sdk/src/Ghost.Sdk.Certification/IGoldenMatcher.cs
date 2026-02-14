namespace Ghost.Sdk.Certification;

/// <summary>
/// Compares actual plugin output to golden (expected) outputs.
/// </summary>
public interface IGoldenMatcher
{
    /// <summary>
    /// Matches actual output against golden files.
    /// </summary>
    /// <param name="fixtures">Fixtures with actual output</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Match result with pass/fail status</returns>
    Task<GoldenMatchResult> MatchAsync(
        IReadOnlyList<Fixture> fixtures,
        CancellationToken ct = default);
}

/// <summary>
/// Result of golden matching.
/// </summary>
public sealed record GoldenMatchResult(
    bool AllMatch,
    int MatchCount,
    int MismatchCount,
    string? FirstMismatch);
