using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Ghost.Core;
using Ghost;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.Platform.LinkedIn.Internal;

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

        var pageOpts = _options.GetPageOptions();
        var page = await _session.NewPageAsync(pageOpts, ct: ct);
            try
            {
                await page.NavigateAsync(_options.BaseUrl, ct: ct);
                // set cookie via document.cookie
                await page.EvaluateAsync<object>($"document.cookie = 'li_at={liAt}; domain=.linkedin.com; path=/';", ct: ct);
                await page.NavigateAsync($"{_options.BaseUrl}/feed/", ct: ct);

                var logged = await IsLoggedInAsync(page, ct).ConfigureAwait(false);
                if (!logged)
                {
                    _logger.LoginCookieSetNotLoggedIn();
                }
            }
        finally
        {
            try { await page.DisposeAsync(); } catch { }
        }
    }

    public async Task WarmUpAsync(IPage page, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(page);

        // safe urls to warm up the browser/page
        var safe = new[]
        {
            "https://www.google.com",
            "https://github.com",
            "https://stackoverflow.com",
            "https://www.bing.com"
        };

        try
        {
            // pick 2 random urls
            var pick = safe.OrderBy(_ => Random.Shared.Next()).Take(2);
            foreach (var url in pick)
            {
                ct.ThrowIfCancellationRequested();
                s_logWarmUpVisit(_logger, url, null);
                await page.NavigateAsync(url, new NavigationOptions { WaitUntil = WaitUntil.DomContentLoaded }, ct: ct);
                // wait a short random delay to simulate browsing
                var delay = Random.Shared.Next(1500, 3001);
                await Task.Delay(delay, ct);
                // scroll a bit
                try { await page.EvaluateAsync<object>("window.scrollBy(0,500);", ct: ct); } catch { }
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
            var nav = await page.QuerySelectorAsync("nav.global-nav__nav", ct).ConfigureAwait(false);
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
