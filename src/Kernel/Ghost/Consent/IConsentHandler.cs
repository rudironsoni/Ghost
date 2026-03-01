using Microsoft.Playwright;

namespace Ghost.Consent;

/// <summary>
/// Interface for detecting and handling consent management platforms (CMPs).
/// </summary>
public interface IConsentHandler
{
    /// <summary>
    /// Detects the Consent Management Platform (CMP) present on the page.
    /// </summary>
    /// <param name="page">The Playwright page instance.</param>
    /// <returns>The CMP identifier if detected, otherwise null.</returns>
    public Task<string?> DetectCMPAsync(IPage page);

    /// <summary>
    /// Accepts consent for a specific CMP type.
    /// </summary>
    /// <param name="page">The Playwright page instance.</param>
    /// <param name="cmpType">The CMP identifier to handle.</param>
    /// <returns>True if consent was successfully accepted, otherwise false.</returns>
    public Task<bool> AcceptConsentAsync(IPage page, string cmpType);

    /// <summary>
    /// Detects and handles consent on the page automatically.
    /// </summary>
    /// <param name="page">The Playwright page instance.</param>
    /// <returns>True if consent was detected and accepted, otherwise false.</returns>
    public Task<bool> HandleConsentAsync(IPage page);
}
