using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Contracts.Jobs;
using Ghost.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace Ghost.Platform.Google.Jobs.Internal;

public sealed class GoogleJobsBrowserClient
{
    private static readonly Random s_random = new Random();
    private readonly IGhostKernel _kernel;
    private readonly ILogger<GoogleJobsBrowserClient> _logger;
    private readonly IOptions<GoogleJobsOptions> _options;

    private static readonly Action<ILogger, string, Exception?> s_logNavigating =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(1, nameof(SearchAsync)), "Navigating to: {Url}");

    private static readonly Action<ILogger, string, Exception?> s_logConsentDetected =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(2, nameof(SearchAsync)), "Consent page detected at {Url}, attempting to handle");

    private static readonly Action<ILogger, int, Exception?> s_logJobsFound =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(3, nameof(SearchAsync)), "Found {Count} jobs via browser");

    private static readonly Action<ILogger, string, Exception?> s_logError =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(4, nameof(SearchAsync)), "Browser search error: {Message}");

    private static readonly Action<ILogger, Exception?> s_logSessionCreating =
        LoggerMessage.Define(LogLevel.Debug, new EventId(5, nameof(SearchAsync)), "Creating browser session for Google Jobs");

    private static readonly Action<ILogger, Exception?> s_logCookieInjection =
        LoggerMessage.Define(LogLevel.Debug, new EventId(6, nameof(SearchAsync)), "Injecting consent bypass cookies");

    private static readonly Action<ILogger, string, Exception?> s_logUserAgentRotation =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(7, nameof(SearchAsync)), "Using user agent: {UserAgent}");

    private static readonly Action<ILogger, string, string, int, Exception?> s_logSearchStarting =
        LoggerMessage.Define<string, string, int>(LogLevel.Information, new EventId(8, nameof(SearchAsync)), "GoogleJobsBrowserClient starting search: Query={Query}, Location={Location}, MaxResults={MaxResults}");

    private static readonly Action<ILogger, int, Exception?> s_logSearchCompleted =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(9, nameof(SearchAsync)), "GoogleJobsBrowserClient completed search, found {Count} jobs");

    private static readonly Action<ILogger, Exception?> s_logSearchCancelled =
        LoggerMessage.Define(LogLevel.Warning, new EventId(10, nameof(SearchAsync)), "GoogleJobsBrowserClient search was cancelled");

    private static readonly Action<ILogger, Exception?> s_logSearchFailed =
        LoggerMessage.Define(LogLevel.Error, new EventId(11, nameof(SearchAsync)), "GoogleJobsBrowserClient search failed");

    private static readonly Action<ILogger, Exception?> s_logPageDisposeFailed =
        LoggerMessage.Define(LogLevel.Warning, new EventId(12, nameof(SearchAsync)), "Failed to dispose page");

    private static readonly Action<ILogger, Exception?> s_logSessionDisposeFailed =
        LoggerMessage.Define(LogLevel.Warning, new EventId(13, nameof(SearchAsync)), "Failed to dispose session");

    private int _sessionRequestCount;
    private const int MaxRequestsPerSession = 5;

    public GoogleJobsBrowserClient(
        IGhostKernel kernel,
        IOptions<GoogleJobsOptions> options,
        ILogger<GoogleJobsBrowserClient> logger)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<GoogleJobsBrowserClient>.Instance;
    }

    private static string GetRandomUserAgent()
    {
        var agents = new[]
        {
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120.0.0.0",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) Chrome/120.0.0.0",
            "Mozilla/5.0 (X11; Linux x86_64) Chrome/120.0.0.0"
        };

        return agents[s_random.Next(0, agents.Length)];
    }

    private static async Task InjectConsentCookiesAsync(IPage page, CancellationToken ct)
    {
        try
        {
            var script = $@"() => {{
                document.cookie = '{GoogleJobsConstants.ConsentCookie}; domain={GoogleJobsConstants.CookieDomain}; path={GoogleJobsConstants.CookiePath}; Secure; SameSite=None';
                document.cookie = '{GoogleJobsConstants.SocsCookie}; domain={GoogleJobsConstants.CookieDomain}; path={GoogleJobsConstants.CookiePath}; Secure; SameSite=None';
                return 'ok';
            }}";

            await page.EvaluateAsync<string>(script, null, ct);
        }
        catch
        {
        }
    }

    public async Task<IReadOnlyList<JobListing>> SearchAsync(
        string query,
        string location,
        int maxResults = 25,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        s_logSearchStarting(_logger, query, location, maxResults, null);

        var jobs = new List<JobListing>();
        var sessionOptions = new SessionOptions();

        s_logSessionCreating(_logger, null);

        var session = await _kernel.NewSessionAsync(sessionOptions, ct);
        var page = await session.NewPageAsync(ct: ct);

        try
        {
            s_logCookieInjection(_logger, null);
            await InjectConsentCookiesAsync(page, ct);

            var q = Uri.EscapeDataString(query);
            var loc = string.IsNullOrEmpty(location) ? "" : Uri.EscapeDataString(location);
            var url = $"https://www.google.com/search?q={q}+{loc}&ibp=htl;jobs&udm=8&gl=us&hl=en&pws=0";

            // rotate user agent per-request to appear more human-like
            try
            {
                var ua = GetRandomUserAgent();
                s_logUserAgentRotation(_logger, ua, null);
                // Set user agent via Playwright's SetExtraHTTPHeaders isn't available on IPage in this environment,
                // so set a common header via Evaluate to override navigator.userAgent and rely on session-level
                // options where available. Best-effort only.
                try
                {
                    await page.EvaluateAsync<string>("(ua) => { try { Object.defineProperty(navigator, 'userAgent', {get: () => ua}); return 'ok'; } catch(e) { return 'err'; } }", ua, ct);
                }
                catch { }

                // also try to patch navigator.userAgent in-page (best-effort)
                try
                {
                    // fallback: evaluate a basic userAgent override without cancellation token
                    await page.EvaluateAsync<string>("() => { try { Object.defineProperty(navigator, 'userAgent', {get: () => '" + ua + "'}); return 'ok'; } catch(e) { return 'err'; } }", null, ct);
                }
                catch { }
            }
            catch { }

            s_logNavigating(_logger, url, null);

            await page.NavigateAsync(url, ct: ct);
            await page.WaitForLoadStateAsync(ct: ct);

            _sessionRequestCount++;
            if (_sessionRequestCount >= MaxRequestsPerSession)
            {
                _sessionRequestCount = 0;
            }

            var isConsentPage = await IsConsentPageAsync(page, ct);
            if (isConsentPage)
            {
                s_logConsentDetected(_logger, url, null);
                // Try handling consent page with retries and human-like actions
                var handled = await RetryAsync(async () => await HandleConsentPageAsync(page, ct), 4, ct);
                if (!handled)
                {
                    s_logError(_logger, "Failed to handle consent page", null);
                    return jobs;
                }

                await page.WaitForLoadStateAsync(ct: ct);
                await RandomDelayAsync(800, 1800, ct);
            }

            await WaitForJobListingsAsync(page, ct);
            jobs = await ExtractJobsFromPageAsync(page, maxResults, ct);

            s_logJobsFound(_logger, jobs.Count, null);
            s_logSearchCompleted(_logger, jobs.Count, null);

            return jobs;
        }
        catch (OperationCanceledException)
        {
            s_logSearchCancelled(_logger, null);
            throw;
        }
        catch (Exception ex)
        {
            s_logError(_logger, ex.Message, ex);
            s_logSearchFailed(_logger, ex);
            return jobs;
        }
        finally
        {
            try { await page.DisposeAsync(); } catch (Exception ex) { s_logPageDisposeFailed(_logger, ex); }
            try { await session.DisposeAsync(); } catch (Exception ex) { s_logSessionDisposeFailed(_logger, ex); }
        }
    }

    private static async Task<bool> IsConsentPageAsync(IPage page, CancellationToken ct)
    {
        try
        {
            var url = page.Url;
            if (url.Contains("consent.google.com") || url.Contains("consent.youtube.com"))
            {
                return true;
            }

            var html = await page.GetContentAsync(ct);
            if (string.IsNullOrEmpty(html))
            {
                return false;
            }

            return html.Contains("Before you continue to Google Search", StringComparison.OrdinalIgnoreCase) ||
                   html.Contains("We need to verify you're human", StringComparison.OrdinalIgnoreCase) ||
                   html.Contains("consent.google.com", StringComparison.OrdinalIgnoreCase) ||
                   html.Contains("Reject all", StringComparison.OrdinalIgnoreCase) ||
                   html.Contains("Accept all", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> HandleConsentPageAsync(IPage page, CancellationToken ct)
    {
        try
        {
            // Strategy 1: Try rejecting explicitly
            await RandomDelayAsync(200, 600, ct);
            await SimulateGlobalMouseMovementAsync(page, ct);

            var rejectSelectors = new[]
            {
                "button:has-text(\"Reject all\")",
                "button[aria-label*=\"Reject\"]",
                "div[role=\\\"button\\\"]:has-text(\"Reject all\")",
                "button:has-text(\"Reject\")",
                "[data-action=\"reject\"]"
            };

            foreach (var sel in rejectSelectors)
            {
                try
                {
                    var btn = await page.QuerySelectorAsync(sel, ct);
                    if (btn != null)
                    {
                        await RandomDelayAsync(120, 450, ct);
                        await SimulateGlobalMouseMovementAsync(page, ct);
                        await btn.ClickAsync(ct: ct);
                        await RandomDelayAsync(800, 1400, ct);
                        return true;
                    }
                }
                catch { }
            }

            // Strategy 2: Customize -> Confirm
            var customizeSelectors = new[]
            {
                "button:has-text(\"Customize\")",
                "div[role=\\\"button\\\"]:has-text(\"Customize\")",
                "button:has-text(\"Manage options\")"
            };

            foreach (var sel in customizeSelectors)
            {
                try
                {
                    var btn = await page.QuerySelectorAsync(sel, ct);
                    if (btn != null)
                    {
                        await SimulateGlobalMouseMovementAsync(page, ct);
                        await btn.ClickAsync(ct: ct);
                        await RandomDelayAsync(600, 1200, ct);

                        var confirmSelectors = new[] { "button:has-text(\"Confirm\")", "button:has-text(\"Done\")", "div[role=\\\"button\\\"]:has-text(\"Confirm\")" };
                        foreach (var csel in confirmSelectors)
                        {
                            try
                            {
                                var cbtn = await page.QuerySelectorAsync(csel, ct);
                                if (cbtn != null)
                                {
                                    await SimulateGlobalMouseMovementAsync(page, ct);
                                    await cbtn.ClickAsync(ct: ct);
                                    await RandomDelayAsync(800, 1300, ct);
                                    return true;
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }

            // Strategy 3: Search for any negative/decline textual button
            var buttons = await page.QuerySelectorAllAsync("button, div[role=\\\"button\\\"]", ct);
            foreach (var button in buttons)
            {
                try
                {
                    var text = await button.GetTextContentAsync(ct);
                    if (!string.IsNullOrEmpty(text))
                    {
                        var lowerText = text.ToLowerInvariant();
                        if (lowerText.Contains("reject") || lowerText.Contains("decline") || lowerText.Contains("no thanks") || lowerText.Contains("dismiss"))
                        {
                            await SimulateGlobalMouseMovementAsync(page, ct);
                            await RandomDelayAsync(80, 300, ct);
                            await button.ClickAsync(ct: ct);
                            await RandomDelayAsync(800, 1200, ct);
                            return true;
                        }
                    }
                }
                catch { }
            }

            // Strategy 4: Try keyboard navigation (tab -> enter)
            // Strategy 4: Try focusing first actionable negative button via JS and click it
            try
            {
                var script = @"() => {
                    const texts = ['reject','decline','no thanks','dismiss','no, thanks','not now'];
                    const buttons = Array.from(document.querySelectorAll('button, [role=""button""]'));
                    for (const b of buttons) {
                        try {
                            const t = (b.innerText || b.textContent || '').toLowerCase();
                            for (const s of texts) { if (t.includes(s)) { b.click(); return true; } }
                        } catch(e){}
                    }
                    return false;
                }";

                var clicked = await page.EvaluateAsync<bool>(script, null, ct);
                if (clicked)
                {
                    await RandomDelayAsync(800, 1500, ct);
                    var still = await IsConsentPageAsync(page, ct);
                    if (!still) return true;
                }
            }
            catch { }

            // Strategy 5: Try setting a consent cookie (best-effort) and reload
            try
            {
                // Use a null logger here since this method is static and instance logger isn't available
                s_logCookieInjection(Microsoft.Extensions.Logging.Abstractions.NullLogger<GoogleJobsBrowserClient>.Instance, null);
                await InjectConsentCookiesAsync(page, ct);
                await RandomDelayAsync(400, 900, ct);
                await page.ReloadAsync(ct: ct);
                await RandomDelayAsync(1000, 2000, ct);

                var still2 = await IsConsentPageAsync(page, ct);
                if (!still2) return true;
            }
            catch { }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static async Task RandomDelayAsync(int minMs, int maxMs, CancellationToken ct)
    {
        try
        {
            var ms = s_random.Next(Math.Max(1, minMs), Math.Max(minMs + 1, maxMs + 1));
            await Task.Delay(ms, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch { }
    }

    private static async Task SimulateGlobalMouseMovementAsync(IPage page, CancellationToken ct)
    {
        try
        {
            // Dispatch synthetic mousemove events via page.evaluate to mimic activity
            var script = @"() => {
                const steps = Math.floor(Math.random()*5)+3;
                for (let i=0;i<steps;i++){
                    const x = Math.floor(Math.random()*window.innerWidth);
                    const y = Math.floor(Math.random()*window.innerHeight);
                    const ev = new MouseEvent('mousemove', {clientX: x, clientY: y, bubbles: true});
                    document.dispatchEvent(ev);
                }
                return true;
            }";

            await page.EvaluateAsync<string>(script, null, ct);
            await RandomDelayAsync(40, 160, ct);
        }
        catch { }
    }

    private static async Task HumanLikeScrollAsync(IPage page, CancellationToken ct)
    {
        try
        {
            var height = await page.EvaluateAsync<int>("() => document.body.scrollHeight", null, ct);
            var viewport = await page.EvaluateAsync<int>("() => window.innerHeight", null, ct);
            var maxScroll = Math.Max(0, height - viewport);
            if (maxScroll <= 0) return;

            var passes = s_random.Next(2, 5);
            for (var i = 0; i < passes; i++)
            {
                var pos = s_random.Next(0, maxScroll);
                await page.EvaluateAsync<string>("(y) => window.scrollTo({top: y, behavior: 'smooth'})", pos, ct);
                await RandomDelayAsync(300, 900, ct);
            }
        }
        catch { }
    }

    private static async Task<bool> RetryAsync(Func<Task<bool>> action, int maxAttempts, CancellationToken ct)
    {
        var attempt = 0;
        var backoff = 300;
        while (attempt < maxAttempts)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var ok = await action();
                if (ok) return true;
            }
            catch { }

            attempt++;
            var delay = backoff * (int)Math.Pow(2, attempt - 1);
            try { await Task.Delay(delay + s_random.Next(0, 200), ct); } catch { }
        }

        return false;
    }

    private static async Task WaitForJobListingsAsync(IPage page, CancellationToken ct)
    {
        try
        {
            var timeout = TimeSpan.FromSeconds(10);
            var startTime = DateTime.UtcNow;

            while (DateTime.UtcNow - startTime < timeout)
            {
                ct.ThrowIfCancellationRequested();

                var selectors = new[]
                {
                    "[data-ved]",
                    "[data-async-fc]",
                    ".gws-plugins-horizon-jobs__li-ed",
                    "[jsname]",
                    "div[role=\"listitem\"]",
                };

                foreach (var selector in selectors)
                {
                    try
                    {
                        var elements = await page.QuerySelectorAllAsync(selector, ct);
                        if (elements.Count > 0)
                        {
                            return;
                        }
                    }
                    catch { }
                }

                await Task.Delay(500, ct);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
        }
    }

    private async Task<List<JobListing>> ExtractJobsFromPageAsync(IPage page, int maxResults, CancellationToken ct)
    {
        var jobs = new List<JobListing>();

        try
        {
            var html = await page.GetContentAsync(ct);

            if (!string.IsNullOrEmpty(html))
            {
                var parsedJobs = GoogleJobsParser.ParseFromHtml(html, _logger);

                foreach (var job in parsedJobs.Take(maxResults))
                {
                    jobs.Add(job);
                }
            }

            if (jobs.Count == 0)
            {
                jobs = await ExtractJobsFromDomAsync(page, maxResults, ct);
            }
        }
        catch (Exception ex)
        {
            s_logError(_logger, $"Error extracting jobs: {ex.Message}", ex);
        }

        return jobs;
    }

    private static async Task<List<JobListing>> ExtractJobsFromDomAsync(IPage page, int maxResults, CancellationToken ct)
    {
        var jobs = new List<JobListing>();

        try
        {
            var jobElements = await page.QuerySelectorAllAsync(
                "[data-ved] div[role=\"listitem\"], " +
                ".gws-plugins-horizon-jobs__li-ed, " +
                "div[data-async-fc] div[role=\"listitem\"], " +
                "div[jsname] div[role=\"listitem\"]", ct);

            foreach (var element in jobElements.Take(maxResults))
            {
                try
                {
                    var job = await ExtractJobFromElementAsync(element, ct);
                    if (job != null && !string.IsNullOrEmpty(job.Title))
                    {
                        jobs.Add(job);
                    }
                }
                catch { }
            }
        }
        catch { }

        return jobs;
    }

    private static async Task<JobListing?> ExtractJobFromElementAsync(IElement element, CancellationToken ct)
    {
        try
        {
            var titleSelectors = new[] { "h3", "[role=\"heading\"]", ".BjJfJf", "div[jsname=\"Cpkphb\"]" };
            var companySelectors = new[] { ".vNEEBe", "div[jsname=\"V7iZ7c\"]", "span:has-text(\"·\")" };
            var locationSelectors = new[] { ".Qk3sIe", "div[jsname=\"s2gQvd\"]", "span:has-text(\",\")" };
            var descriptionSelectors = new[] { ".HBvzbc", "div[jsname=\"o7OJ4\"]", ".YgLbBe" };

            string? title = null;
            foreach (var selector in titleSelectors)
            {
                var el = await element.QuerySelectorAsync(selector, ct);
                if (el != null)
                {
                    title = await el.GetTextContentAsync(ct);
                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        title = title.Trim();
                        break;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                return null;
            }

            string? company = null;
            foreach (var selector in companySelectors)
            {
                var el = await element.QuerySelectorAsync(selector, ct);
                if (el != null)
                {
                    company = await el.GetTextContentAsync(ct);
                    if (!string.IsNullOrWhiteSpace(company))
                    {
                        company = company.Trim();
                        break;
                    }
                }
            }

            string? location = null;
            foreach (var selector in locationSelectors)
            {
                var el = await element.QuerySelectorAsync(selector, ct);
                if (el != null)
                {
                    location = await el.GetTextContentAsync(ct);
                    if (!string.IsNullOrWhiteSpace(location))
                    {
                        location = location.Trim();
                        break;
                    }
                }
            }

            string? description = null;
            foreach (var selector in descriptionSelectors)
            {
                var el = await element.QuerySelectorAsync(selector, ct);
                if (el != null)
                {
                    description = await el.GetTextContentAsync(ct);
                    if (!string.IsNullOrWhiteSpace(description))
                    {
                        description = description.Trim();
                        break;
                    }
                }
            }

            var id = $"{title}-{company}".ToLowerInvariant()
                .Replace(" ", "-")
                .Replace(",", "")
                .Replace(".", "");

            if (id.Length > 100)
            {
                id = id.Substring(0, 100);
            }

            return new JobListing
            {
                Id = id,
                Title = title,
                Company = company ?? "Unknown",
                Location = location,
                Description = description,
                Source = "Google",
                PostedAt = DateTimeOffset.UtcNow,
                Url = $"https://www.google.com/search?q={Uri.EscapeDataString(title)}+{Uri.EscapeDataString(company ?? "")}&ibp=htl;jobs&udm=8&pws=0"
            };
        }
        catch
        {
            return null;
        }
    }
}
