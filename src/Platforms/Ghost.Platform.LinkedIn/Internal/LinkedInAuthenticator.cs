using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.Platform.LinkedIn.Internal;

public sealed class LinkedInAuthenticator
{
    private readonly Ghost.IBrowserSession _session;
    private readonly LinkedInOptions _options;
    private readonly ILogger<LinkedInAuthenticator> _logger;

    public LinkedInAuthenticator(Ghost.IBrowserSession session, IOptions<LinkedInOptions> options, ILogger<LinkedInAuthenticator> logger)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<LinkedInAuthenticator>.Instance;
    }

    public async Task LoginWithCookieAsync(string liAt, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(liAt)) throw new ArgumentNullException(nameof(liAt));

        var page = await _session.NewPageAsync(ct: ct);
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

    public async Task WarmUpAsync(CancellationToken ct = default)
    {
        var page = await _session.NewPageAsync(ct: ct);
        try
        {
            await page.NavigateAsync("https://www.google.com", ct: ct);
            await page.NavigateAsync("https://github.com", ct: ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.WarmUpFailed(ex);
        }
        finally
        {
            try { await page.DisposeAsync(); } catch { }
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
