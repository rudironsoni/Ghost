using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Playwright;

namespace Ghost.ConsentManagement;

#pragma warning disable CA1848, CA1852

/// <summary>
/// Service for detecting and handling consent/cookie banners on websites.
/// Uses selectors from consentcrawl database.
/// </summary>
public class ConsentManagerService
{
    private readonly ILogger<ConsentManagerService> _logger;

    public ConsentManagerService(ILogger<ConsentManagerService>? logger = null)
    {
        _logger = logger ?? NullLogger<ConsentManagerService>.Instance;
    }

    /// <summary>
    /// Consent manager definitions with their detection and acceptance selectors
    /// </summary>
    private static readonly List<ConsentManagerDefinition> ConsentManagers = new()
    {
        // Google Funding Choices
        new ConsentManagerDefinition(
            "google-funding-choices",
            new[] { ".fc-cta-consent", ".fc-consent-root", "[aria-label*='consent' i]", "[data-ved*='consent']" },
            new[] { ".fc-cta-consent", "button.fc-cta-consent", ".fc-consent-root .fc-button.fc-cta-consent" }
        ),

        // OneTrust variants
        new ConsentManagerDefinition(
            "onetrust-cookiepro",
            new[] { "#onetrust-consent-sdk", "#onetrust-banner-sdk", "[class*='onetrust']" },
            new[] { ".cmp-button__accept", "#onetrust-accept-btn-handler", "#accept-recommended-btn-handler", "[class*='onetrust'] .accept-btn" }
        ),

        new ConsentManagerDefinition(
            "onetrust-optanon",
            new[] { "#onetrust-banner-sdk", "#optanon-root", "#onetrust-pc-sdk" },
            new[] { ".optanon-allow-all", "#accept-recommended-btn-handler", ".save-preference-btn-handler" }
        ),

        new ConsentManagerDefinition(
            "onetrust-cookielaw",
            new[] { "#onetrust-banner-sdk", "[id*='onetrust']" },
            new[] { "#cookielaw_accept", "#accept-recommended-btn-handler" }
        ),

        // CookieBot
        new ConsentManagerDefinition(
            "cookiebot",
            new[] { "#CybotCookiebotDialog", "[data-cybot]", "[id*='CookiebotDialog']" },
            new[] { "#CybotCookiebotDialogBodyLevelButtonLevelOptinAllowAll", "#CybotCookiebotDialogBodyLevelButtonAccept", "#CybotCookiebotDialog [data-controller*='accept']" }
        ),

        // Sourcepoint
        new ConsentManagerDefinition(
            "sourcepoint-cmp",
            new[] { "[id*='sp_message_iframe']", "[class*='sp_message']", "#sp_message" },
            new[] { "button.sp_choice_type_11", "[title*='accept' i]", "[title*='agree' i]", "[title*='kzept']", "[title*='ustimmen']" }
        ),

        // UserCentrics
        new ConsentManagerDefinition(
            "usercentrics",
            new[] { "#usercentrics-root", "[data-testid='uc-accept-all-button']", "#uc-main-view" },
            new[] { "[data-testid='uc-accept-all-button']", "#uc-btn-accept-banner", "cmm-cookie-banner .button--accept-all", "#uc-deny-all-button + .uc-button-primary" }
        ),

        // Quantcast
        new ConsentManagerDefinition(
            "quantcast",
            new[] { "#qc-cmp2-ui", ".qc-cmp2-persistent-link", "[id*='qc-cmp']" },
            new[] { "#qc-cmp2-ui button[mode='primary']", ".qc-cmp2-consent-button", ".qc-cmp2-button[label='Accept All']" }
        ),

        // Didomi
        new ConsentManagerDefinition(
            "didomi",
            new[] { "#didomi-host", ".didomi-popup", "[id*='didomi']" },
            new[] { "#didomi-notice-agree-button", ".didomi-continue-without-agreeing", ".Cmp__action--yes", "#didomi-policy-accept-all" }
        ),

        // CookieFirst
        new ConsentManagerDefinition(
            "cookiefirst",
            new[] { "[data-cookiefirst-action]", "#cookiefirst", "#cf-root" },
            new[] { "[data-cookiefirst-action='accept']", "#cf-accept", "[data-cf-action='accept']" }
        ),

        // Osano
        new ConsentManagerDefinition(
            "osano",
            new[] { ".osano-cm-window", "#osano-cm" },
            new[] { ".osano-cm-accept-all", ".osano-cm-btn-accept-all" }
        ),

        // Generic - accept all buttons (fallback)
        new ConsentManagerDefinition(
            "generic-accept",
            new[] { ".cookie-banner", "#cookie-banner", "[class*='cookie-consent']", "[id*='cookie-consent']", "[class*='accept-all']", "[id*='accept-all']" },
            new[] {
                "button[class*='accept']:not([class*='reject']):not([class*='decline'])",
                "button[id*='accept']:not([id*='reject']):not([id*='decline'])",
                "a[class*='accept']:not([class*='reject']):not([class*='decline'])",
                "[class*='accept-all']", "[id*='accept-all']",
                "button:has-text('Accept'):not(:has-text('Reject')):not(:has-text('Decline'))",
                "button:has-text('Agree'):not(:has-text('Disagree'))",
                "button:has-text('OK')",
                "button:has-text('Yes')",
                "button:has-text('Aceptar')",
                "button:has-text('Aceptar')",
                "button:has-text('Akzeptieren')",
                "button:has-text('Accepter')",
                "button[aria-label*='accept' i]", "[title*='accept' i]",
                "button[title*='Accept' i]", "button[title*='Aceptar' i]"
            }
        ),

        // Generic - iframe-based consent
        new ConsentManagerDefinition(
            "generic-iframe",
            new[] { "iframe[src*='consent']", "iframe[src*='cookie']", "iframe[src*='gdpr']" },
            new[] { "button:has-text('Accept')", "button:has-text('Agree')", "button:has-text('OK')" },
            true
        )
    };

    /// <summary>
    /// Attempts to handle consent banner on the current page
    /// </summary>
    /// <param name="page">Playwright page instance</param>
    /// <param name="timeoutMs">Timeout for waiting for selectors</param>
    /// <returns>True if consent was handled, false otherwise</returns>
    public async Task<bool> HandleConsentAsync(IPage page, int timeoutMs = 5000)
    {
        ArgumentNullException.ThrowIfNull(page);

        _logger.LogDebug("Checking for consent banners...");

        foreach (ConsentManagerDefinition manager in ConsentManagers)
        {
            try
            {
                // Check if this consent manager is present and get the detected element
                IElement? detectedElement = await DetectConsentManagerAsync(page, manager, timeoutMs).ConfigureAwait(false);
                if (detectedElement == null)
                    continue;

                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Detected consent manager: {ManagerId}", manager.Id);
                }

                // Try to accept/click the consent
                bool handled = await AcceptConsentAsync(page, manager, detectedElement, timeoutMs).ConfigureAwait(false);
                if (handled)
                {
                    if (_logger.IsEnabled(LogLevel.Information))
                    {
                        _logger.LogInformation("Successfully handled consent for: {ManagerId}", manager.Id);
                    }

                    // Wait a moment for the banner to disappear and page to settle
                    await Task.Delay(1000).ConfigureAwait(false);

                    // Check if banner is actually gone
                    IElement? stillPresent = await DetectConsentManagerAsync(page, manager, 1000).ConfigureAwait(false);
                    if (stillPresent == null)
                    {
                        if (_logger.IsEnabled(LogLevel.Information))
                        {
                            _logger.LogInformation("Consent banner successfully dismissed");
                        }
                        return true;
                    }
                    else
                    {
                        if (_logger.IsEnabled(LogLevel.Warning))
                        {
                            _logger.LogWarning("Consent banner still present after acceptance attempt");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(ex, "Error handling consent manager {ManagerId}", manager.Id);
                }
            }
        }

        _logger.LogDebug("No consent banners detected or handled");
        return false;
    }

    /// <summary>
    /// Checks if a specific consent manager is present on the page
    /// </summary>
    /// <returns>The detected element if found and visible, null otherwise</returns>
    private async Task<IElement?> DetectConsentManagerAsync(IPage page, ConsentManagerDefinition manager, int timeoutMs)
    {
        foreach (string selector in manager.DetectionSelectors)
        {
            try
            {
                if (manager.IsIframe)
                {
                    // For iframe-based consent managers, check if iframe exists
                    IElement? frame = await page.QuerySelectorAsync(selector).ConfigureAwait(false);
                    if (frame != null)
                    {
                        if (_logger.IsEnabled(LogLevel.Debug))
                        {
                            _logger.LogDebug("Found iframe consent manager: {Selector}", selector);
                        }
                        return frame;
                    }
                }
                else
                {
                    // For regular selectors, check visibility
                    IElement? element = await page.QuerySelectorAsync(selector).ConfigureAwait(false);
                    if (element != null)
                    {
                        // Check if element is visible
                        bool isVisible = await element.IsVisibleAsync().ConfigureAwait(false);
                        if (isVisible)
                        {
                            if (_logger.IsEnabled(LogLevel.Debug))
                            {
                                _logger.LogDebug("Found consent element: {Selector}", selector);
                            }
                            return element;
                        }
                    }
                }
            }
            catch
            {
                // Continue to next selector
            }
        }

        return null;
    }

    /// <summary>
    /// Attempts to click the accept/agree button for a consent manager
    /// </summary>
    private async Task<bool> AcceptConsentAsync(IPage page, ConsentManagerDefinition manager, IElement? detectedElement, int timeoutMs)
    {
        // First try the acceptance selectors
        foreach (string selector in manager.AcceptanceSelectors)
        {
            try
            {
                if (manager.IsIframe)
                {
                    bool clicked = await page.EvaluateAsync<bool>($@"
                        () => {{
                            var iframe = document.querySelector('{manager.DetectionSelectors.First().Replace("'", "\\'")}');
                            if (iframe && iframe.contentDocument) {{
                                var btn = iframe.contentDocument.querySelector('{selector.Replace("'", "\\'")}');
                                if (btn) {{ btn.click(); return true; }}
                            }}
                            return false;
                        }}
                    ").ConfigureAwait(false);

                    if (clicked)
                    {
                        if (_logger.IsEnabled(LogLevel.Debug))
                        {
                            _logger.LogDebug("Clicked iframe consent button: {Selector}", selector);
                        }
                        return true;
                    }
                }
                else
                {
                    IElement? button = await page.QuerySelectorAsync(selector).ConfigureAwait(false);
                    if (button != null)
                    {
                        bool isVisible = await button.IsVisibleAsync().ConfigureAwait(false);
                        bool isEnabled = await button.IsEnabledAsync().ConfigureAwait(false);

                        if (isVisible && isEnabled)
                        {
                            if (_logger.IsEnabled(LogLevel.Debug))
                            {
                                _logger.LogDebug("Clicking consent button: {Selector}", selector);
                            }

                            try
                            {
                                await button.ClickAsync().ConfigureAwait(false);
                                return true;
                            }
                            catch
                            {
                                await page.EvaluateAsync<object>($"document.querySelector('{selector.Replace("'", "\\'")}')?.click()").ConfigureAwait(false);
                                return true;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(ex, "Failed to click selector {Selector}", selector);
                }
            }
        }

        // If no acceptance button found, try clicking the detected element itself
        // This handles cases where the detection selector returns the button directly
        if (detectedElement != null && !manager.IsIframe)
        {
            try
            {
                bool isVisible = await detectedElement.IsVisibleAsync().ConfigureAwait(false);
                bool isEnabled = await detectedElement.IsEnabledAsync().ConfigureAwait(false);

                if (isVisible && isEnabled)
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("Clicking detected element as fallback");
                    }

                    try
                    {
                        await detectedElement.ClickAsync().ConfigureAwait(false);
                        return true;
                    }
                    catch
                    {
                        // Try to click via JavaScript as fallback
                        await page.EvaluateAsync<object>("document.querySelector('button')?.click()").ConfigureAwait(false);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(ex, "Failed to click detected element as fallback");
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Waits for and handles consent banner with retry logic
    /// </summary>
    public async Task<bool> WaitAndHandleConsentAsync(IPage page, int maxWaitMs = 10000, int checkIntervalMs = 500)
    {
        DateTime startTime = DateTime.UtcNow;

        while ((DateTime.UtcNow - startTime).TotalMilliseconds < maxWaitMs)
        {
            bool handled = await HandleConsentAsync(page, checkIntervalMs).ConfigureAwait(false);
            if (handled)
                return true;

            await Task.Delay(checkIntervalMs).ConfigureAwait(false);
        }

        _logger.LogWarning("Consent banner not detected within {MaxWaitMs}ms", maxWaitMs);
        return false;
    }

    /// <summary>
    /// Definition of a consent manager with its selectors
    /// </summary>
    private class ConsentManagerDefinition
    {
        public string Id { get; }
        public string[] DetectionSelectors { get; }
        public string[] AcceptanceSelectors { get; }
        public bool IsIframe { get; }

        public ConsentManagerDefinition(string id, string[] detectionSelectors, string[] acceptanceSelectors, bool isIframe = false)
        {
            Id = id;
            DetectionSelectors = detectionSelectors;
            AcceptanceSelectors = acceptanceSelectors;
            IsIframe = isIframe;
        }
    }
}
