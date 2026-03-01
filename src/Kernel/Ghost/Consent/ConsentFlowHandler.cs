using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Playwright;

namespace Ghost.Consent;

/// <summary>
/// Handles complex multi-step consent flows with support for shadow DOM and iframes.
/// </summary>
public partial class ConsentFlowHandler
{
    private readonly ILogger<ConsentFlowHandler> _logger;
    private readonly int _stepDelayMs;
    private readonly TimeProvider _timeProvider;

    // LoggerMessage source generators (EventIds 2000-2099 for Consent)
    [LoggerMessage(EventId = 2000, Level = LogLevel.Warning, Message = "No steps defined for multi-step flow: {CmpName}")]
    private static partial void LogNoStepsDefined(ILogger<ConsentFlowHandler> logger, string cmpName);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Debug, Message = "Starting multi-step consent flow for {CmpName} ({StepCount} steps)")]
    private static partial void LogMultiStepStart(ILogger<ConsentFlowHandler> logger, string cmpName, int stepCount);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Debug, Message = "Executing step {StepNumber}/{TotalSteps}: {Selector}")]
    private static partial void LogExecutingStep(ILogger<ConsentFlowHandler> logger, int stepNumber, int totalSteps, string selector);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Warning, Message = "Failed to execute step {StepNumber}: {Selector}")]
    private static partial void LogStepFailed(ILogger<ConsentFlowHandler> logger, int stepNumber, string selector);

    [LoggerMessage(EventId = 2004, Level = LogLevel.Information, Message = "Successfully completed multi-step consent flow for {CmpName}")]
    private static partial void LogMultiStepComplete(ILogger<ConsentFlowHandler> logger, string cmpName);

    [LoggerMessage(EventId = 2005, Level = LogLevel.Debug, Message = "Clicked element in regular DOM: {Selector}")]
    private static partial void LogClickedRegularDom(ILogger<ConsentFlowHandler> logger, string selector);

    [LoggerMessage(EventId = 2006, Level = LogLevel.Debug, Message = "Clicked element in shadow DOM: {Selector}")]
    private static partial void LogClickedShadowDom(ILogger<ConsentFlowHandler> logger, string selector);

    [LoggerMessage(EventId = 2007, Level = LogLevel.Debug, Message = "Clicked element in iframe: {Selector}")]
    private static partial void LogClickedIframe(ILogger<ConsentFlowHandler> logger, string selector);

    [LoggerMessage(EventId = 2008, Level = LogLevel.Debug, Message = "Could not find element for selector: {Selector}")]
    private static partial void LogElementNotFound(ILogger<ConsentFlowHandler> logger, string selector);

    [LoggerMessage(EventId = 2009, Level = LogLevel.Warning, Message = "Invalid CSS selector rejected: {Selector}")]
    private static partial void LogInvalidSelector(ILogger<ConsentFlowHandler> logger, string selector);

    [LoggerMessage(EventId = 2010, Level = LogLevel.Debug, Message = "Regular DOM click failed for: {Selector}")]
    private static partial void LogRegularDomClickFailed(ILogger<ConsentFlowHandler> logger, Exception ex, string selector);

    [LoggerMessage(EventId = 2011, Level = LogLevel.Debug, Message = "Shadow DOM click failed for: {Selector}")]
    private static partial void LogShadowDomClickFailed(ILogger<ConsentFlowHandler> logger, Exception ex, string selector);

    [LoggerMessage(EventId = 2012, Level = LogLevel.Warning, Message = "Invalid CSS selector rejected for iframe interaction")]
    private static partial void LogInvalidIframeSelector(ILogger<ConsentFlowHandler> logger);

    [LoggerMessage(EventId = 2013, Level = LogLevel.Debug, Message = "Iframe click failed for: {Selector}")]
    private static partial void LogIframeClickFailed(ILogger<ConsentFlowHandler> logger, Exception ex, string selector);

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsentFlowHandler"/> class.
    /// </summary>
    /// <param name="logger">Optional logger instance.</param>
    /// <param name="stepDelayMs">Delay between steps in milliseconds (default: 500).</param>
    /// <param name="timeProvider">Optional time provider for testability.</param>
    public ConsentFlowHandler(ILogger<ConsentFlowHandler>? logger = null, int stepDelayMs = 500, TimeProvider? timeProvider = null)
    {
        _logger = logger ?? NullLogger<ConsentFlowHandler>.Instance;
        _stepDelayMs = stepDelayMs;
        _timeProvider = timeProvider ?? TimeProvider.System;
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
            LogNoStepsDefined(_logger, config.Name);
            return false;
        }

        LogMultiStepStart(_logger, config.Name, config.Steps.Length);

        for (int i = 0; i < config.Steps.Length; i++)
        {
            string stepSelector = config.Steps[i];
            LogExecutingStep(_logger, i + 1, config.Steps.Length, stepSelector);

            bool success = await ExecuteStepAsync(page, stepSelector, config).ConfigureAwait(false);
            if (!success)
            {
                LogStepFailed(_logger, i + 1, stepSelector);
                return false;
            }

            // Wait between steps
            if (i < config.Steps.Length - 1)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(_stepDelayMs), _timeProvider, CancellationToken.None).ConfigureAwait(false);
            }
        }

        LogMultiStepComplete(_logger, config.Name);
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
            LogClickedRegularDom(_logger, selector);
            return true;
        }

        // Try shadow DOM
        clicked = await TryClickShadowDOMAsync(page, selector).ConfigureAwait(false);
        if (clicked)
        {
            LogClickedShadowDom(_logger, selector);
            return true;
        }

        // Try iframe if configured
        if (config.IsIframe)
        {
            clicked = await TryClickIframeAsync(page, selector, config).ConfigureAwait(false);
            if (clicked)
            {
                LogClickedIframe(_logger, selector);
                return true;
            }
        }

        LogElementNotFound(_logger, selector);
        return false;
    }

    /// <summary>
    /// Validates that a string is a safe CSS selector and does not contain script injection attempts.
    /// </summary>
    private static bool IsValidCssSelector(string selector)
    {
        if (string.IsNullOrWhiteSpace(selector))
        {
            return false;
        }

        // Check for script injection patterns
        string[] forbiddenPatterns = new[]
        {
            "<script",
            "javascript:",
            "onerror=",
            "onload=",
            "onclick=",
            "eval(",
            "function(",
            "=>",
            "${",
            ";",
            "//",
            "/*",
            "@import",
            "behavior:",
            "expression("
        };

        string lowerSelector = selector.ToLowerInvariant();
        foreach (string pattern in forbiddenPatterns)
        {
            if (lowerSelector.Contains(pattern))
            {
                return false;
            }
        }

        // Validate selector only contains safe CSS selector characters
        foreach (char c in selector)
        {
            if (!char.IsLetterOrDigit(c) &&
                c != ' ' && c != '.' && c != '#' && c != '[' && c != ']' && c != ':' && c != '-' && c != '_' &&
                c != '>' && c != '+' && c != '~' && c != '=' && c != '"' && c != '\'' && c != '^' && c != '$' &&
                c != '*' && c != '|' && c != '(' && c != ')' && c != ',')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Tries to click an element in the regular DOM.
    /// </summary>
    private async Task<bool> TryClickRegularAsync(IPage page, string selector)
    {
        // Validate selector to prevent injection
        if (!IsValidCssSelector(selector))
        {
            LogInvalidSelector(_logger, selector);
            return false;
        }

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
                        // Fallback to JavaScript click with typed parameter passing
                        await page.EvaluateAsync<object>(
                            "(selector) => document.querySelector(selector)?.click()",
                            selector).ConfigureAwait(false);
                        return true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogRegularDomClickFailed(_logger, ex, selector);
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
            LogShadowDomClickFailed(_logger, ex, selector);
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

            // Validate both selectors to prevent injection
            if (!IsValidCssSelector(iframeSelector) || !IsValidCssSelector(selector))
            {
                LogInvalidIframeSelector(_logger);
                return false;
            }

            // Use typed parameter passing instead of string interpolation
            bool clicked = await page.EvaluateAsync<bool>(
                @"
                (selectors) => {
                    const iframeSelector = selectors.iframeSelector;
                    const buttonSelector = selectors.buttonSelector;
                    var iframe = document.querySelector(iframeSelector);
                    if (iframe && iframe.contentDocument) {
                        var btn = iframe.contentDocument.querySelector(buttonSelector);
                        if (btn) {
                            btn.click();
                            return true;
                        }
                    }
                    return false;
                }
                ",
                new { iframeSelector, buttonSelector = selector }).ConfigureAwait(false);

            return clicked;
        }
        catch (Exception ex)
        {
            LogIframeClickFailed(_logger, ex, selector);
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
