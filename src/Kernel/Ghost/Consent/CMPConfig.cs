namespace Ghost.Consent;

/// <summary>
/// Configuration for a Consent Management Platform (CMP).
/// </summary>
public class CMPConfig
{
    /// <summary>
    /// Unique identifier for the CMP (e.g., "onetrust", "cookiebot").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// CSS selectors used to detect the presence of this CMP.
    /// </summary>
    public required string[] Detectors { get; init; }

    /// <summary>
    /// CSS selector for the primary accept button.
    /// </summary>
    public required string AcceptButton { get; init; }

    /// <summary>
    /// Indicates whether this CMP requires multiple steps to accept consent.
    /// </summary>
    public bool MultiStep { get; init; }

    /// <summary>
    /// CSS selectors for each step in a multi-step consent flow.
    /// </summary>
    public string[]? Steps { get; init; }

    /// <summary>
    /// Indicates whether this CMP is iframe-based.
    /// </summary>
    public bool IsIframe { get; init; }

    /// <summary>
    /// Additional CSS selectors that can be used to accept consent.
    /// </summary>
    public string[]? AlternativeAcceptSelectors { get; init; }
}
