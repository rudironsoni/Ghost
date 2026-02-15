using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Playwright;

namespace Ghost.Consent;

#pragma warning disable CA1848

/// <summary>
/// Handles complex multi-step consent flows with support for shadow DOM and iframes.
/// </summary>
public class ConsentFlowHandler
{
    private readonly ILogger<ConsentFlowHandler> _logger;
    private readonly int _stepDelayMs;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsentFlowHandler"/> class.
    /// </summary>
    /// <param name="logger">Optional logger instance.</param>
    /// <param name="stepDelayMs">Delay between steps in milliseconds (default: 500).</param>
    public ConsentFlowHandler(ILogger<ConsentFlowHandler>? logger = null, int stepDelayMs = 500)
    {
        _logger = logger ?? NullLogger<ConsentFlowHandler>.Instance;
        _stepDelayMs = stepDelayMs;
    }

    /// <summary>
    /// Executes a multi-step consent flow with advanced detection.
    /// </summary>
    /// <param name="page">The page containing the consent dialog.</param>
    /// <param name="config">The CMP configuration with step definitions.</param>
    /// <returns>True if all steps completed successfully, otherwise false.</returns>
    public async Task<bool> ExecuteMultiStepFlowAsync(IPage page, CMPConfig config)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(config);

        if (config.Steps == null || config.Steps.Length == 0)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning("No steps defined for multi-step flow: {CmpName}", config.Name);
            }
            return false;
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Starting multi-step consent flow for {CmpName} ({StepCount} steps)",
                config.Name, config.Steps.Length);
        }

        for (int i = 0; i < config.Steps.Length; i++)
        {
            string stepSelector = config.Steps[i];
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Executing step {StepNumber}/{TotalSteps}: {Selector}",
                    i + 1, config.Steps.Length, stepSelector);
            }

            bool success = await ExecuteStepAsync(page, stepSelector, config).ConfigureAwait(false);
            if (!success)
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning("Failed to execute step {StepNumber}: {Selector}", i + 1, stepSelector);
                }
                return false;
            }

            // Wait between steps
            if (i < config.Steps.Length - 1)
            {
                await Task.Delay(_stepDelayMs).ConfigureAwait(false);
            }
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Successfully completed multi-step consent flow for {CmpName}", config.Name);
        }
        return true;
    }

    /// <summary>
    /// Executes a single step in the consent flow.
    /// Tries regular DOM, shadow DOM, and iframe detection.
    /// </summary>
    private async Task<bool> ExecuteStepAsync(IPage page, string selector, CMPConfig config)
    {
        // Try regular DOM first
        bool clicked = await TryClickRegularAsync(page, selector).ConfigureAwait(false);
        if (clicked)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Clicked element in regular DOM: {Selector}", selector);
            }
            return true;
        }

        // Try shadow DOM
        clicked = await TryClickShadowDOMAsync(page, selector).ConfigureAwait(false);
        if (clicked)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Clicked element in shadow DOM: {Selector}", selector);
            }
            return true;
        }

        // Try iframe if configured
        if (config.IsIframe)
        {
            clicked = await TryClickIframeAsync(page, selector, config).ConfigureAwait(false);
            if (clicked)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Clicked element in iframe: {Selector}", selector);
                }
                return true;
            }
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Could not find element for selector: {Selector}", selector);
        }
        return false;
    }

    /// <summary>
    /// Tries to click an element in the regular DOM.
    /// </summary>
    private async Task<bool> TryClickRegularAsync(IPage page, string selector)
    {
        try
        {
            IElement? element = await page.QuerySelectorAsync(selector).ConfigureAwait(false);
            if (element != null)
            {
                bool isVisible = await element.IsVisibleAsync().ConfigureAwait(false);
                bool isEnabled = await element.IsEnabledAsync().ConfigureAwait(false);

                if (isVisible && isEnabled)
                {
                    try
                    {
                        await element.ClickAsync().ConfigureAwait(false);
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
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(ex, "Regular DOM click failed for: {Selector}", selector);
            }
        }

        return false;
    }

    /// <summary>
    /// Tries to click an element inside shadow DOM.
    /// </summary>
    private async Task<bool> TryClickShadowDOMAsync(IPage page, string selector)
    {
        try
        {
            return await ShadowDOMHelper.ClickInShadowDOMAsync(page, selector).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(ex, "Shadow DOM click failed for: {Selector}", selector);
            }
            return false;
        }
    }

    /// <summary>
    /// Tries to click an element inside an iframe.
    /// </summary>
    private async Task<bool> TryClickIframeAsync(IPage page, string selector, CMPConfig config)
    {
        try
        {
            if (config.Detectors.Length == 0)
            {
                return false;
            }

            string iframeSelector = config.Detectors[0]; // First detector is the iframe selector
            bool clicked = await page.EvaluateAsync<bool>($@"
                () => {{
                    var iframe = document.querySelector('{iframeSelector.Replace("'", "\\'")}');
                    if (iframe && iframe.contentDocument) {{
                        var btn = iframe.contentDocument.querySelector('{selector.Replace("'", "\\'")}');
                        if (btn) {{
                            btn.click();
                            return true;
                        }}
                    }}
                    return false;
                }}
            ").ConfigureAwait(false);

            return clicked;
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(ex, "Iframe click failed for: {Selector}", selector);
            }
            return false;
        }
    }

    /// <summary>
    /// Detects if an element exists using all available methods.
    /// </summary>
    public static async Task<bool> DetectElementAsync(IPage page, string selector, bool checkShadowDOM = true)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(selector);

        // Check regular DOM
        try
        {
            IElement? element = await page.QuerySelectorAsync(selector).ConfigureAwait(false);
            if (element != null)
            {
                bool isVisible = await element.IsVisibleAsync().ConfigureAwait(false);
                if (isVisible)
                {
                    return true;
                }
            }
        }
        catch
        {
            // Continue to shadow DOM check
        }

        // Check shadow DOM if enabled
        if (checkShadowDOM)
        {
            try
            {
                bool found = await ShadowDOMHelper.FindInShadowDOMAsync(page, selector).ConfigureAwait(false);
                if (found)
                {
                    return true;
                }
            }
            catch
            {
                // Element not found in shadow DOM
            }
        }

        return false;
    }
}
