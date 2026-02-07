using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Playwright;

namespace Ghost.Consent;

#pragma warning disable CA1848

/// <summary>
/// Default implementation of IConsentHandler for automated consent management.
/// Detects and handles 25+ Consent Management Platforms (CMPs).
/// </summary>
public class ConsentHandler : IConsentHandler
{
    private readonly ILogger<ConsentHandler> _logger;
    private readonly int _timeoutMs;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsentHandler"/> class.
    /// </summary>
    /// <param name="logger">Optional logger instance.</param>
    /// <param name="timeoutMs">Timeout in milliseconds for consent operations (default: 5000).</param>
    public ConsentHandler(ILogger<ConsentHandler>? logger = null, int timeoutMs = 5000)
    {
        _logger = logger ?? NullLogger<ConsentHandler>.Instance;
        _timeoutMs = timeoutMs;
    }

    /// <inheritdoc/>
    public async Task<string?> DetectCMPAsync(IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        _logger.LogDebug("Detecting CMP on page: {Url}", page.Url);

        var configs = CMPDatabase.GetAllConfigs();

        foreach (var config in configs)
        {
            try
            {
                var detected = await DetectCMPInternalAsync(page, config);
                if (detected)
                {
                    _logger.LogInformation("Detected CMP: {CmpName}", config.Name);
                    return config.Name;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error detecting CMP {CmpName}", config.Name);
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

        var config = CMPDatabase.GetConfig(cmpType);
        if (config == null)
        {
            _logger.LogWarning("Unknown CMP type: {CmpType}", cmpType);
            return false;
        }

        _logger.LogDebug("Attempting to accept consent for CMP: {CmpType}", cmpType);

        try
        {
            var accepted = await AcceptConsentInternalAsync(page, config);
            if (accepted)
            {
                _logger.LogInformation("Successfully accepted consent for CMP: {CmpType}", cmpType);

                // Wait for banner to disappear
                await Task.Delay(1000);

                // Verify banner is gone
                var stillPresent = await DetectCMPInternalAsync(page, config);
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

        _logger.LogDebug("Checking for consent banners on page: {Url}", page.Url);

        var cmpType = await DetectCMPAsync(page);
        if (cmpType == null)
        {
            _logger.LogDebug("No consent banner detected");
            return false;
        }

        return await AcceptConsentAsync(page, cmpType);
    }

    /// <summary>
    /// Internal method to detect if a specific CMP is present on the page.
    /// </summary>
    private async Task<bool> DetectCMPInternalAsync(IPage page, CMPConfig config)
    {
        foreach (var selector in config.Detectors)
        {
            try
            {
                if (config.IsIframe)
                {
                    // For iframe-based CMPs, check if iframe exists
                    var frame = await page.QuerySelectorAsync(selector);
                    if (frame != null)
                    {
                        _logger.LogDebug("Found iframe CMP: {Selector}", selector);
                        return true;
                    }
                }
                else
                {
                    // For regular selectors, check visibility
                    var element = await page.QuerySelectorAsync(selector);
                    if (element != null)
                    {
                        var isVisible = await element.IsVisibleAsync();
                        if (isVisible)
                        {
                            _logger.LogDebug("Found CMP element: {Selector}", selector);
                            return true;
                        }
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
            return await HandleMultiStepConsentAsync(page, config);
        }

        // Single-step flow: try each selector
        foreach (var selector in selectors)
        {
            try
            {
                if (config.IsIframe)
                {
                    var clicked = await page.EvaluateAsync<bool>($@"
                        () => {{
                            var iframe = document.querySelector('{config.Detectors.First().Replace("'", "\\'")}');
                            if (iframe && iframe.contentDocument) {{
                                var btn = iframe.contentDocument.querySelector('{selector.Replace("'", "\\'")}');
                                if (btn) {{ btn.click(); return true; }}
                            }}
                            return false;
                        }}
                    ");

                    if (clicked)
                    {
                        _logger.LogDebug("Clicked iframe consent button: {Selector}", selector);
                        return true;
                    }
                }
                else
                {
                    var button = await page.QuerySelectorAsync(selector);
                    if (button != null)
                    {
                        var isVisible = await button.IsVisibleAsync();
                        var isEnabled = await button.IsEnabledAsync();

                        if (isVisible && isEnabled)
                        {
                            _logger.LogDebug("Clicking consent button: {Selector}", selector);

                            try
                            {
                                await button.ClickAsync();
                                return true;
                            }
                            catch
                            {
                                // Fallback to JavaScript click
                                await page.EvaluateAsync<object>($"document.querySelector('{selector.Replace("'", "\\'")}')?.click()");
                                return true;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to click selector: {Selector}", selector);
            }
        }

        return false;
    }

    /// <summary>
    /// Handles multi-step consent flows.
    /// </summary>
    private async Task<bool> HandleMultiStepConsentAsync(IPage page, CMPConfig config)
    {
        if (config.Steps == null || config.Steps.Length == 0)
        {
            _logger.LogWarning("Multi-step CMP {CmpName} has no steps defined", config.Name);
            return false;
        }

        _logger.LogDebug("Handling multi-step consent for CMP: {CmpName}", config.Name);

        foreach (var stepSelector in config.Steps)
        {
            try
            {
                var button = await page.QuerySelectorAsync(stepSelector);
                if (button != null)
                {
                    var isVisible = await button.IsVisibleAsync();
                    var isEnabled = await button.IsEnabledAsync();

                    if (isVisible && isEnabled)
                    {
                        _logger.LogDebug("Clicking step: {Selector}", stepSelector);
                        await button.ClickAsync();

                        // Wait between steps
                        await Task.Delay(500);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to click step: {Selector}", stepSelector);
                return false;
            }
        }

        return true;
    }
}
