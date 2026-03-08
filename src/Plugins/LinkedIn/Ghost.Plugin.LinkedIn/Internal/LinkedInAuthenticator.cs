using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghost;
using Ghost.Kernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.Plugin.LinkedIn.Internal;

public sealed class LinkedInAuthenticator
{
    private static readonly Action<ILogger, string, Exception?> s_logWarmUpVisit =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(1, nameof(WarmUpAsync)), "Warm-up: Visiting {Url}...");

    private static readonly Action<ILogger, Exception?> s_logWarmUpComplete =
        LoggerMessage.Define(LogLevel.Information, new EventId(2, nameof(WarmUpAsync)), "Warm-up sequence completed.");

    private readonly Ghost.IBrowserSession _session;
    private readonly LinkedInOptions _options;
    private readonly ILogger<LinkedInAuthenticator> _logger;

    public LinkedInAuthenticator(Ghost.IBrowserSession session, IOptions<LinkedInOptions> options, ILogger<LinkedInAuthenticator> logger)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(options);
        _session = session;
        _options = options.Value;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<LinkedInAuthenticator>.Instance;
    }

    public async Task LoginWithCookieAsync(string liAt, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(liAt)) throw new ArgumentNullException(nameof(liAt));

        // Validate liAt cookie value to prevent injection
        if (!IsValidCookieValue(liAt))
        {
            throw new ArgumentException("Invalid li_at cookie value provided.", nameof(liAt));
        }

        PageOptions? pageOpts = _options.GetPageOptions();
        IPage page = await _session.NewPageAsync(pageOpts, ct: ct).ConfigureAwait(false);
        try
        {
            await page.NavigateAsync(_options.BaseUrl, ct: ct).ConfigureAwait(false);
            // set cookie using Playwright's typed cookie API instead of JavaScript evaluation
            await page.AddCookiesAsync(new[]
            {
                new Cookie
                {
                    Name = "li_at",
                    Value = liAt,
                    Domain = ".linkedin.com",
                    Path = "/"
                }
            }, ct).ConfigureAwait(false);
            await page.NavigateAsync($"{_options.BaseUrl}/feed/", ct: ct).ConfigureAwait(false);

            bool logged = await IsLoggedInAsync(page, ct).ConfigureAwait(false);
            if (!logged)
            {
                _logger.LoginCookieSetNotLoggedIn();
            }
        }
        finally
        {
            try { await page.DisposeAsync().ConfigureAwait(false); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to dispose page: {ex}"); }
        }
    }

    /// <summary>
    /// Validates that a cookie value is safe and does not contain injection attempts.
    /// </summary>
    /// <param name="cookieValue">The cookie value to validate.</param>
    /// <returns>True if the cookie value is valid and safe; otherwise false.</returns>
    private static bool IsValidCookieValue(string cookieValue)
    {
        if (string.IsNullOrEmpty(cookieValue))
        {
            return false;
        }

        // Check for script injection patterns
        string[] forbiddenPatterns = new[]
        {
            "'",
            "\"",
            ";",
            "\n",
            "\r",
            "<script",
            "javascript:",
            "onerror=",
            "onload=",
            "onclick=",
            "eval(",
            "function(",
            "=>",
            "${",
            "//",
            "/*",
            "-->",
            "</script>"
        };

        foreach (string pattern in forbiddenPatterns)
        {
            if (cookieValue.Contains(pattern))
            {
                return false;
            }
        }

        // Cookie values should generally be alphanumeric with limited safe characters
        // LinkedIn li_at cookies are typically base64url encoded
        foreach (char c in cookieValue)
        {
            if (!char.IsLetterOrDigit(c) && c != '-' && c != '_' && c != '=' && c != '+')
            {
                return false;
            }
        }

        return true;
    }

    public async Task WarmUpAsync(IPage page, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(page);

        // safe urls to warm up the browser/page
        string[] safe = new[]
        {
            "https://www.google.com",
            "https://github.com",
            "https://stackoverflow.com",
            "https://www.bing.com"
        };

        try
        {
            // pick 2 random urls
            IEnumerable<string> pick = safe.OrderBy(_ => Random.Shared.Next()).Take(2);
            foreach (string? url in pick)
            {
                ct.ThrowIfCancellationRequested();
                s_logWarmUpVisit(_logger, url, null);
                await page.NavigateAsync(url, new NavigationOptions { WaitUntil = WaitUntil.DomContentLoaded }, ct: ct).ConfigureAwait(false);
                // wait a short random delay to simulate browsing
                int delay = Random.Shared.Next(1500, 3001);
                await Task.Delay(delay, ct).ConfigureAwait(false);
                // scroll a bit
                try { await page.EvaluateAsync<object>("window.scrollBy(0,500);", ct: ct).ConfigureAwait(false); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Scroll failed: {ex}"); }
            }
            s_logWarmUpComplete(_logger, null);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.WarmUpFailed(ex);
        }
    }

    public async Task<bool> IsLoggedInAsync(Ghost.IPage page, CancellationToken ct = default)
    {
        if (page == null) return false;

        try
        {
            // Check URL contains /feed
            if (!string.IsNullOrEmpty(page.Url) && page.Url.Contains("/feed", StringComparison.OrdinalIgnoreCase))
                return true;

            // Or check for nav.global-nav__nav selector
            IElement? nav = await page.QuerySelectorAsync("nav.global-nav__nav", ct).ConfigureAwait(false);
            return nav != null;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.IsLoggedInCheckFailed(ex);
            return false;
        }
    }
}
