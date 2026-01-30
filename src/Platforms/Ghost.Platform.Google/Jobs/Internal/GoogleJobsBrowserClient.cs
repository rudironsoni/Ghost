using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly GhostKernel _kernel;
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

    public GoogleJobsBrowserClient(
        GhostKernel kernel,
        IOptions<GoogleJobsOptions> options,
        ILogger<GoogleJobsBrowserClient> logger)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<GoogleJobsBrowserClient>.Instance;
    }

    public async Task<IReadOnlyList<JobListing>> SearchAsync(
        string query,
        string location,
        int maxResults = 25,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var jobs = new List<JobListing>();
        var sessionOptions = new SessionOptions();

        s_logSessionCreating(_logger, null);

        var session = await _kernel.NewSessionAsync(sessionOptions, ct);
        var page = await session.NewPageAsync(ct: ct);

        try
        {
            var q = Uri.EscapeDataString(query);
            var loc = string.IsNullOrEmpty(location) ? "" : Uri.EscapeDataString(location);
            var url = $"https://www.google.com/search?q={q}+{loc}&ibp=htl;jobs&udm=8&gl=us&hl=en";

            s_logNavigating(_logger, url, null);

            await page.NavigateAsync(url, ct: ct);
            await page.WaitForLoadStateAsync(ct: ct);

            var isConsentPage = await IsConsentPageAsync(page, ct);
            if (isConsentPage)
            {
                s_logConsentDetected(_logger, url, null);
                var handled = await HandleConsentPageAsync(page, ct);
                if (!handled)
                {
                    s_logError(_logger, "Failed to handle consent page", null);
                    return jobs;
                }

                await page.WaitForLoadStateAsync(ct: ct);
                await Task.Delay(2000, ct);
            }

            await WaitForJobListingsAsync(page, ct);
            jobs = await ExtractJobsFromPageAsync(page, maxResults, ct);

            s_logJobsFound(_logger, jobs.Count, null);

            return jobs;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            s_logError(_logger, ex.Message, ex);
            return jobs;
        }
        finally
        {
            try { await page.DisposeAsync(); } catch { }
            try { await session.DisposeAsync(); } catch { }
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
            var rejectButton = await page.QuerySelectorAsync(
                "button:has-text(\"Reject all\"), " +
                "button[aria-label*=\"Reject\"], " +
                "div[role=\"button\"]:has-text(\"Reject all\"), " +
                "button:has-text(\"Reject\"), " +
                "[data-action=\"reject\"]", ct);

            if (rejectButton != null)
            {
                await rejectButton.ClickAsync(ct: ct);
                await Task.Delay(1000, ct);
                return true;
            }

            var customizeButton = await page.QuerySelectorAsync(
                "button:has-text(\"Customize\"), " +
                "div[role=\"button\"]:has-text(\"Customize\"), " +
                "button:has-text(\"Manage options\")", ct);

            if (customizeButton != null)
            {
                await customizeButton.ClickAsync(ct: ct);
                await Task.Delay(1000, ct);

                var confirmButton = await page.QuerySelectorAsync(
                    "button:has-text(\"Confirm\"), " +
                    "button:has-text(\"Done\"), " +
                    "div[role=\"button\"]:has-text(\"Confirm\")", ct);

                if (confirmButton != null)
                {
                    await confirmButton.ClickAsync(ct: ct);
                    await Task.Delay(1000, ct);
                    return true;
                }
            }

            var buttons = await page.QuerySelectorAllAsync("button, div[role=\"button\"]", ct);
            foreach (var button in buttons)
            {
                try
                {
                    var text = await button.GetTextContentAsync(ct);
                    if (!string.IsNullOrEmpty(text))
                    {
                        var lowerText = text.ToLowerInvariant();
                        if (lowerText.Contains("reject") ||
                            lowerText.Contains("decline") ||
                            lowerText.Contains("no thanks") ||
                            lowerText.Contains("dismiss"))
                        {
                            await button.ClickAsync(ct: ct);
                            await Task.Delay(1000, ct);
                            return true;
                        }
                    }
                }
                catch { }
            }

            return false;
        }
        catch
        {
            return false;
        }
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
                Url = $"https://www.google.com/search?q={Uri.EscapeDataString(title)}+{Uri.EscapeDataString(company ?? "")}&ibp=htl;jobs&udm=8"
            };
        }
        catch
        {
            return null;
        }
    }
}
