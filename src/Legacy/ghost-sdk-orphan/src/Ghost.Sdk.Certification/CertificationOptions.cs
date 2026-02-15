namespace Ghost.Sdk.Certification;

/// <summary>
/// Certification mode - determines what resources are available during certification.
/// </summary>
public enum CertificationMode
{
    /// <summary>
    /// No network access - uses fixtures only. Required for CI gating.
    /// </summary>
    Offline,

    /// <summary>
    /// Network access only to local mock servers. Optional.
    /// </summary>
    SemiOffline,

    /// <summary>
    /// Full network access to live targets. Optional, never PR-blocking.
    /// </summary>
    LiveSmoke
}

/// <summary>
/// Options for plugin certification.
/// </summary>
public sealed record CertificationOptions(
    CertificationMode Mode,
    string FixturesPath,
    string? MockServerUrl,
    TimeSpan Timeout);
