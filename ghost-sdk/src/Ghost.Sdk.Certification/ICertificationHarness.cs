namespace Ghost.Sdk.Certification;

/// <summary>
/// Main entry point for plugin certification.
/// Validates plugins offline, compares output to golden files, and produces certification reports.
/// </summary>
public interface ICertificationHarness
{
    /// <summary>
    /// Certifies a plugin against its fixtures and golden outputs.
    /// </summary>
    /// <param name="manifest">The plugin manifest to certify</param>
    /// <param name="options">Certification options (mode, fixtures path, timeout)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Certification report with pass/fail status and detailed results</returns>
    Task<CertificationReport> CertifyAsync(
        Ghost.Sdk.Contracts.PluginManifest manifest,
        CertificationOptions options,
        CancellationToken ct = default);
}
