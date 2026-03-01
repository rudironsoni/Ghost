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
public partial class ConsentHandler : IConsentHandler
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

    // LoggerMessage source generators (EventIds 2200-2299 for ConsentHandler)
    [LoggerMessage(EventId = 2200, Level = LogLevel.Debug, Message = "Detecting CMP on page: {Url}")]
    private static partial void LogDetectingCmp(ILogger<ConsentHandler> logger, string url);

    [LoggerMessage(EventId = 2201, Level = LogLevel.Information, Message = "Detected CMP: {CmpName}")]
    private static partial void LogCmpDetected(ILogger<ConsentHandler> logger, string cmpName);

    [LoggerMessage(EventId = 2202, Level = LogLevel.Debug, Message = "No CMP detected on page")]
    private static partial void LogNoCmpDetected(ILogger<ConsentHandler> logger);

    [LoggerMessage(EventId = 2203, Level = LogLevel.Warning, Message = "Unknown CMP type: {CmpType}")]
    private static partial void LogUnknownCmpType(ILogger<ConsentHandler> logger, string cmpType);

    [LoggerMessage(EventId = 2204, Level = LogLevel.Debug, Message = "Attempting to accept consent for CMP: {CmpType}")]
    private static partial void LogAcceptingConsent(ILogger<ConsentHandler> logger, string cmpType);

    [LoggerMessage(EventId = 2205, Level = LogLevel.Information, Message = "Successfully accepted consent for CMP: {CmpType}")]
    private static partial void LogConsentAccepted(ILogger<ConsentHandler> logger, string cmpType);

    [LoggerMessage(EventId = 2206, Level = LogLevel.Information, Message = "Consent banner successfully dismissed")]
    private static partial void LogBannerDismissed(ILogger<ConsentHandler> logger);

    [LoggerMessage(EventId = 2207, Level = LogLevel.Warning, Message = "Consent banner still present after acceptance")]
    private static partial void LogBannerStillPresent(ILogger<ConsentHandler> logger);

    [LoggerMessage(EventId = 2208, Level = LogLevel.Error, Message = "Error accepting consent for CMP: {CmpType}")]
    private static partial void LogAcceptConsentError(ILogger<ConsentHandler> logger, Exception ex, string cmpType);

    [LoggerMessage(EventId = 2209, Level = LogLevel.Debug, Message = "Checking for consent banners on page: {Url}")]
    private static partial void LogCheckingBanners(ILogger<ConsentHandler> logger, string url);

    [LoggerMessage(EventId = 2210, Level = LogLevel.Information, Message = "Detected privacy regulation: {Regulation}")]
    private static partial void LogRegulationDetected(ILogger<ConsentHandler> logger, string regulation);

    [LoggerMessage(EventId = 2211, Level = LogLevel.Debug, Message = "Strategy: {Strategy}")]
    private static partial void LogStrategy(ILogger<ConsentHandler> logger, string strategy);

    [LoggerMessage(EventId = 2212, Level = LogLevel.Debug, Message = "No consent banner detected")]
    private static partial void LogNoBannerDetected(ILogger<ConsentHandler> logger);

    [LoggerMessage(EventId = 2213, Level = LogLevel.Debug, Message = "Error detecting CMP {CmpName}")]
    private static partial void LogCmpDetectionError(ILogger<ConsentHandler> logger, Exception ex, string cmpName);

    [LoggerMessage(EventId = 2214, Level = LogLevel.Debug, Message = "Found iframe CMP: {Selector}")]
    private static partial void LogIframeCmpFound(ILogger<ConsentHandler> logger, string selector);

    [LoggerMessage(EventId = 2215, Level = LogLevel.Debug, Message = "Found CMP element in regular DOM: {Selector}")]
    private static partial void LogCmpElementFound(ILogger<ConsentHandler> logger, string selector);

    [LoggerMessage(EventId = 2216, Level = LogLevel.Debug, Message = "Found CMP element in shadow DOM: {Selector}")]
    private static partial void LogCmpElementFoundInShadow(ILogger<ConsentHandler> logger, string selector);

    [LoggerMessage(EventId = 2217, Level = LogLevel.Debug, Message = "Clicked iframe consent button: {Selector}")]
    private static partial void LogIframeButtonClicked(ILogger<ConsentHandler> logger, string selector);

    [LoggerMessage(EventId = 2218, Level = LogLevel.Debug, Message = "Clicking consent button in regular DOM: {Selector}")]
    private static partial void LogClickingConsentButton(ILogger<ConsentHandler> logger, string selector);

    [LoggerMessage(EventId = 2219, Level = LogLevel.Debug, Message = "Clicked consent button in shadow DOM: {Selector}")]
    private static partial void LogClickedInShadow(ILogger<ConsentHandler> logger, string selector);

    [LoggerMessage(EventId = 2220, Level = LogLevel.Debug, Message = "Failed to click selector: {Selector}")]
    private static partial void LogClickFailed(ILogger<ConsentHandler> logger, Exception ex, string selector);

    /// <inheritdoc/>
    public async Task<string?> DetectCMPAsync(IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        LogDetectingCmp(_logger, page.Url);

        IReadOnlyList<CMPConfig> configs = CMPDatabase.GetAllConfigs();

        foreach (CMPConfig config in configs)
        {
            try
            {
                bool detected = await DetectCMPInternalAsync(page, config).ConfigureAwait(false);
                if (detected)
                {
                    LogCmpDetected(_logger, config.Name);
                    return config.Name;
                }
            }
            catch (Exception ex)
            {
                LogCmpDetectionError(_logger, ex, config.Name);
            }
        }

        LogNoCmpDetected(_logger);
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
            LogUnknownCmpType(_logger, cmpType);
            return false;
        }

        LogAcceptingConsent(_logger, cmpType);

        try
        {
            bool accepted = await AcceptConsentInternalAsync(page, config).ConfigureAwait(false);
            if (accepted)
            {
                LogConsentAccepted(_logger, cmpType);

                // Wait for banner to disappear
                await Task.Delay(TimeSpan.FromMilliseconds(1000), _timeProvider, CancellationToken.None).ConfigureAwait(false);

                // Verify banner is gone
                bool stillPresent = await DetectCMPInternalAsync(page, config).ConfigureAwait(false);
                if (!stillPresent)
                {
                    LogBannerDismissed(_logger);
                    return true;
                }

                LogBannerStillPresent(_logger);
            }
        }
        catch (Exception ex)
        {
            LogAcceptConsentError(_logger, ex, cmpType);
        }

        return false;
    }

    /// <inheritdoc/>
    public async Task<bool> HandleConsentAsync(IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        LogCheckingBanners(_logger, page.Url);

        // Detect privacy regulation for context
        RegionDetector.PrivacyRegulation regulation = await RegionDetector.DetectRegulationAsync(page).ConfigureAwait(false);
        if (regulation != RegionDetector.PrivacyRegulation.Unknown)
        {
            string regulationStr = regulation.ToString();
            string strategy = RegionDetector.GetConsentStrategy(regulation);
            LogRegulationDetected(_logger, regulationStr);
            LogStrategy(_logger, strategy);
        }

        string? cmpType = await DetectCMPAsync(page).ConfigureAwait(false);
        if (cmpType == null)
        {
            LogNoBannerDetected(_logger);
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
                        LogIframeCmpFound(_logger, selector);
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
                            LogCmpElementFound(_logger, selector);
                            return true;
                        }
                    }

                    // Check shadow DOM if not found in regular DOM
                    bool foundInShadow = await ShadowDOMHelper.FindInShadowDOMAsync(page, selector).ConfigureAwait(false);
                    if (foundInShadow)
                    {
                        LogCmpElementFoundInShadow(_logger, selector);
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
                        LogIframeButtonClicked(_logger, selector);
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
                            LogClickingConsentButton(_logger, selector);

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
                        LogClickedInShadow(_logger, selector);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                LogClickFailed(_logger, ex, selector);
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
