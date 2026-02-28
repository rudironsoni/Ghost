using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Playwright;

namespace Ghost.Consent;

/// <summary>
/// Default implementation of IConsentHandler for automated consent management.
/// Detects and handles 25+ Consent Management Platforms (CMPs) with advanced features:
/// - Shadow DOM support
/// - Multi-step consent flows
/// - Iframe-based CMPs
/// - Region-aware detection (GDPR, CCPA, LGPD)
/// </summary>
public class ConsentHandler : IConsentHandler
{
    private readonly ILogger<ConsentHandler> _logger;
    private readonly int _timeoutMs;
    private readonly ConsentFlowHandler _flowHandler;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsentHandler"/> class.
    /// </summary>
    /// <param name="logger">Optional logger instance.</param>
    /// <param name="timeoutMs">Timeout in milliseconds for consent operations (default: 5000).</param>
    /// <param name="timeProvider">Optional time provider for testability.</param>
    public ConsentHandler(ILogger<ConsentHandler>? logger = null, int timeoutMs = 5000, TimeProvider? timeProvider = null)
    {
        _logger = logger ?? NullLogger<ConsentHandler>.Instance;
        _timeoutMs = timeoutMs;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _flowHandler = new ConsentFlowHandler(timeProvider: _timeProvider);
    }

    /// <inheritdoc/>
    public async Task<string?> DetectCMPAsync(IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Detecting CMP on page: {Url}", page.Url);
        }

        IReadOnlyList<CMPConfig> configs = CMPDatabase.GetAllConfigs();

        foreach (CMPConfig config in configs)
        {
            try
            {
                bool detected = await DetectCMPInternalAsync(page, config).ConfigureAwait(false);
                if (detected)
                {
                    if (_logger.IsEnabled(LogLevel.Information))
                    {
                        _logger.LogInformation("Detected CMP: {CmpName}", config.Name);
                    }
                    return config.Name;
                }
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(ex, "Error detecting CMP {CmpName}", config.Name);
                }
            }
        }

        _logger.LogDebug("No CMP detected on page");
        return null;
    }

    /// <inheritdoc/>
    public async Task<bool> AcceptConsentAsync(IPage page, string cmpType)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(cmpType);

        CMPConfig? config = CMPDatabase.GetConfig(cmpType);
        if (config == null)
        {
            _logger.LogWarning("Unknown CMP type: {CmpType}", cmpType);
            return false;
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Attempting to accept consent for CMP: {CmpType}", cmpType);
        }

        try
        {
            bool accepted = await AcceptConsentInternalAsync(page, config).ConfigureAwait(false);
            if (accepted)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Successfully accepted consent for CMP: {CmpType}", cmpType);
                }

                // Wait for banner to disappear
                await Task.Delay(TimeSpan.FromMilliseconds(1000), _timeProvider, CancellationToken.None).ConfigureAwait(false);

                // Verify banner is gone
                bool stillPresent = await DetectCMPInternalAsync(page, config).ConfigureAwait(false);
                if (!stillPresent)
                {
                    _logger.LogInformation("Consent banner successfully dismissed");
                    return true;
                }

                _logger.LogWarning("Consent banner still present after acceptance");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accepting consent for CMP: {CmpType}", cmpType);
        }

        return false;
    }

    /// <inheritdoc/>
    public async Task<bool> HandleConsentAsync(IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Checking for consent banners on page: {Url}", page.Url);
        }

        // Detect privacy regulation for context
        RegionDetector.PrivacyRegulation regulation = await RegionDetector.DetectRegulationAsync(page).ConfigureAwait(false);
        if (regulation != RegionDetector.PrivacyRegulation.Unknown)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Detected privacy regulation: {Regulation}", regulation);
            }
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Strategy: {Strategy}", RegionDetector.GetConsentStrategy(regulation));
            }
        }

        string? cmpType = await DetectCMPAsync(page).ConfigureAwait(false);
        if (cmpType == null)
        {
            _logger.LogDebug("No consent banner detected");
            return false;
        }

        return await AcceptConsentAsync(page, cmpType).ConfigureAwait(false);
    }

    /// <summary>
    /// Internal method to detect if a specific CMP is present on the page.
    /// Enhanced with shadow DOM support.
    /// </summary>
    private async Task<bool> DetectCMPInternalAsync(IPage page, CMPConfig config)
    {
        foreach (string selector in config.Detectors)
        {
            try
            {
                if (config.IsIframe)
                {
                    // For iframe-based CMPs, check if iframe exists
                    IElement? frame = await page.QuerySelectorAsync(selector).ConfigureAwait(false);
                    if (frame != null)
                    {
                        if (_logger.IsEnabled(LogLevel.Debug))
                        {
                            _logger.LogDebug("Found iframe CMP: {Selector}", selector);
                        }
                        return true;
                    }
                }
                else
                {
                    // Check regular DOM first
                    IElement? element = await page.QuerySelectorAsync(selector).ConfigureAwait(false);
                    if (element != null)
                    {
                        bool isVisible = await element.IsVisibleAsync().ConfigureAwait(false);
                        if (isVisible)
                        {
                            if (_logger.IsEnabled(LogLevel.Debug))
                            {
                                _logger.LogDebug("Found CMP element in regular DOM: {Selector}", selector);
                            }
                            return true;
                        }
                    }

                    // Check shadow DOM if not found in regular DOM
                    bool foundInShadow = await ShadowDOMHelper.FindInShadowDOMAsync(page, selector).ConfigureAwait(false);
                    if (foundInShadow)
                    {
                        if (_logger.IsEnabled(LogLevel.Debug))
                        {
                            _logger.LogDebug("Found CMP element in shadow DOM: {Selector}", selector);
                        }
                        return true;
                    }
                }
            }
            catch
            {
                // Continue to next selector
            }
        }

        return false;
    }

    /// <summary>
    /// Internal method to accept consent for a specific CMP.
    /// </summary>
    private async Task<bool> AcceptConsentInternalAsync(IPage page, CMPConfig config)
    {
        // Build list of all possible accept selectors
        var selectors = new List<string> { config.AcceptButton };
        if (config.AlternativeAcceptSelectors != null)
        {
            selectors.AddRange(config.AlternativeAcceptSelectors);
        }

        // Handle multi-step flows
        if (config.MultiStep && config.Steps != null)
        {
            return await HandleMultiStepConsentAsync(page, config).ConfigureAwait(false);
        }

        // Single-step flow: try each selector
        foreach (string selector in selectors)
        {
            try
            {
                if (config.IsIframe)
                {
                    bool clicked = await page.EvaluateAsync<bool>($@"
                        () => {{
                            var iframe = document.querySelector('{config.Detectors.First().Replace("'", "\\'")}');
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
                    // Try regular DOM first
                    IElement? button = await page.QuerySelectorAsync(selector).ConfigureAwait(false);
                    if (button != null)
                    {
                        bool isVisible = await button.IsVisibleAsync().ConfigureAwait(false);
                        bool isEnabled = await button.IsEnabledAsync().ConfigureAwait(false);

                        if (isVisible && isEnabled)
                        {
                            if (_logger.IsEnabled(LogLevel.Debug))
                            {
                                _logger.LogDebug("Clicking consent button in regular DOM: {Selector}", selector);
                            }

                            try
                            {
                                await button.ClickAsync().ConfigureAwait(false);
                                return true;
                            }
                            catch
                            {
                                // Fallback to JavaScript click
                                await page.EvaluateAsync<object>($"document.querySelector('{selector.Replace("'", "\\'")}')?.click()").ConfigureAwait(false);
                                return true;
                            }
                        }
                    }

                    // Try shadow DOM if regular DOM failed
                    bool clickedInShadow = await ShadowDOMHelper.ClickInShadowDOMAsync(page, selector).ConfigureAwait(false);
                    if (clickedInShadow)
                    {
                        if (_logger.IsEnabled(LogLevel.Debug))
                        {
                            _logger.LogDebug("Clicked consent button in shadow DOM: {Selector}", selector);
                        }
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(ex, "Failed to click selector: {Selector}", selector);
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Handles multi-step consent flows.
    /// Delegates to ConsentFlowHandler for advanced detection.
    /// </summary>
    private async Task<bool> HandleMultiStepConsentAsync(IPage page, CMPConfig config)
    {
        return await _flowHandler.ExecuteMultiStepFlowAsync(page, config).ConfigureAwait(false);
    }
}
