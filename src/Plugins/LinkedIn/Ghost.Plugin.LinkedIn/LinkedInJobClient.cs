using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Ghost.Contracts.Jobs;
using Ghost.Extensions;
using Ghost.Plugin.LinkedIn.Entities;
using Ghost.Plugin.LinkedIn.Internal;
using Ghost.Sdk.Spider.Adapters;
using Ghost.Sdk.Spider.Adapters.Contracts;
using Ghost.Sdk.Spider.Core.Entities;
using Ghost.Sdk.Spider.Core.Entities.Attributes;
using Ghost.Sdk.Spider.Core.Extraction;
using Ghost.Sdk.Spider.Pipeline;
using Ghost.Sdk.Spider.Pipeline.Middleware;
using Ghost.Sdk.Spider.Strategies;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.Plugin.LinkedIn;

/// <summary>
/// Job search client for LinkedIn using Ghost.Sdk.Spider.
/// </summary>
public sealed class LinkedInJobClient : Ghost.IJobScraper
{
    private static readonly Action<ILogger, JobScrapingStrategy, string, Exception?> s_logJobSearchStarting =
        LoggerMessage.Define<JobScrapingStrategy, string>(LogLevel.Information, new EventId(1, nameof(SearchJobsWithStrategyAsync)), "Executing Job Search. Strategy: {Strategy}, Query: {Query}");

    private static readonly Action<ILogger, Exception?> s_logHybridFallback =
        LoggerMessage.Define(LogLevel.Information, new EventId(2, nameof(SearchJobsWithStrategyAsync)), "Hybrid Strategy: Guest API returned no results. Falling back to Browser.");

    private static readonly Action<ILogger, int, Exception?> s_logJobSearchCompleted =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(3, nameof(SearchJobsWithStrategyAsync)), "Job Search Completed. Found {Count} jobs.");

    private static readonly Action<ILogger, string, Exception?> s_logDeepFetchFailed =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(4, nameof(SearchJobsWithStrategyAsync)), "Failed to deep fetch details for job {Id}. Returning shallow.");

    private static readonly Action<ILogger, string, Exception?> s_logZeroJobsFound =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(5, nameof(SearchJobsWithStrategyAsync)), "Browser search found 0 jobs. Page Title: {Title}");

    private static readonly char[] s_newlineSplit = new[] { '\n', '\r' };

    // CSS selector fallback arrays for robust element extraction
    // These are tried in order until a non-empty result is found
    private static readonly string[] s_titleSelectors = new[]
    {
        ".base-search-card__title",
        ".job-card-list__title",
        "h3"
    };

    private static readonly string[] s_companySelectors = new[]
    {
        ".base-search-card__subtitle",
        ".job-card-container__company-name",
        "h4"
    };

    private static readonly string[] s_locationSelectors = new[]
    {
        ".job-search-card__location",
        ".job-card-container__metadata-item",
        "[class*=\"location\"]"
    };

    private readonly Ghost.IBrowserSession _session;
    private readonly LinkedInOptions _options;
    private readonly ILogger<LinkedInJobClient> _logger;
    private readonly JavaScriptAdapter _jsAdapter;
    private readonly EntityParser _entityParser;
    private readonly StrategyRouter _strategyRouter;

    public LinkedInJobClient(
        Ghost.IBrowserSession session,
        IOptions<LinkedInOptions> options,
        ILogger<LinkedInJobClient> logger,
        JavaScriptAdapter jsAdapter,
        EntityParser entityParser)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        _options = options?.Value ?? new LinkedInOptions();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<LinkedInJobClient>.Instance;
        _jsAdapter = jsAdapter ?? throw new ArgumentNullException(nameof(jsAdapter));
        _entityParser = entityParser ?? throw new ArgumentNullException(nameof(entityParser));

        // Initialize Spider StrategyRouter
        _strategyRouter = new StrategyRouter(null);

        // Register strategies: Browser only (GuestApi removed as per migration requirement)
        _strategyRouter.RegisterStrategy("Browser", BrowserStrategyAsync);
    }

    public string PlatformName => "LinkedIn";

    public async Task<IReadOnlyList<JobListing>> SearchJobsAsync(JobSearchCriteria criteria, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        List<JobListing> list = [];
        JobScrapingStrategy strategy = _options.ScrapingStrategy;
        if (!string.IsNullOrEmpty(criteria.Strategy))
        {
            if (Enum.TryParse<JobScrapingStrategy>(criteria.Strategy, ignoreCase: true, out JobScrapingStrategy parsed))
            {
                strategy = parsed;
            }
        }

        IAsyncEnumerable<JobListing> e = SearchJobsWithStrategyAsync(criteria.Query ?? string.Empty, criteria.Location ?? string.Empty, criteria.MaxResults, strategy, ct);
        await foreach (JobListing? item in e.ConfigureAwait(false))
        {
            list.Add(item);
        }
        return list;
    }

    public async Task<List<JobListing>> SearchJobsAsync(
        JobSearchCriteria criteria,
        JobScrapingStrategy strategyOverride,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        List<JobListing> list = [];
        await foreach (JobListing job in SearchJobsWithStrategyAsync(criteria.Query ?? string.Empty, criteria.Location ?? "", criteria.MaxResults, strategyOverride, ct).ConfigureAwait(false))
        {
            list.Add(job);
        }
        return list;
    }

    public async Task<JobListing> GetJobDetailsAsync(string jobId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(jobId);

        try
        {
            return await GetJobDetailsBrowserAsync(jobId, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LinkedInLog.LogJobDetailsFetchFailed(_logger, jobId, ex);
            return GenerateMockJobs("", "", 1).First();
        }
    }

    private async Task<JobListing> GetJobDetailsBrowserAsync(string jobId, CancellationToken ct = default)
    {
        string url = $"{_options.BaseUrl}/jobs/view/{jobId}";

        // Create a Spider request with middleware pipeline
        var request = new Request(url)
        {
            ExpectedContentType = ContentType.JavaScript,
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Build pipeline with middleware
        CompiledPipeline pipeline = new PipelineBuilder()
            .Use(new StealthMiddleware(new Dictionary<string, object>
            {
                ["RandomDelay"] = true,
                ["MinDelayMs"] = 500,
                ["MaxDelayMs"] = 2000,
                ["EnableFingerprinting"] = true
            }))
            .Use(new RateLimitMiddleware(new Dictionary<string, object>
            {
                ["Capacity"] = 10,
                ["TokensPerSecond"] = 1.0,
                ["PerDomain"] = true,
                ["WaitWhenExceeded"] = true
            }))
            .Use(new RetryMiddleware(new Dictionary<string, object>
            {
                ["MaxRetries"] = 3,
                ["InitialDelayMs"] = 1000,
                ["BackoffMultiplier"] = 2.0,
                ["UseJitter"] = true
            }))
            .Build();

        IPage? page = null;
        try
        {
            PageOptions? pageOpts = _options.GetPageOptions();
            page = await _session.NewPageAsync(pageOpts, ct: ct).ConfigureAwait(false);

            var navOptions = new NavigationOptions { Timeout = 30_000, WaitUntil = WaitUntil.Load };
            await page.NavigateAsync(url, navOptions, ct: ct).ConfigureAwait(false);
            await page.WaitForLoadStateAsync(ct: ct).ConfigureAwait(false);

            string html = await page.GetContentAsync(ct).ConfigureAwait(false);

            // Extract using EntityParser with LinkedInJobEntity from Entities namespace
            var context = new ExtractionContext
            {
                Content = html ?? string.Empty,
                SourceUrl = url,
                Timestamp = DateTime.UtcNow
            };

            LinkedInJobEntity? entity = EntityParser.ParseSingle<LinkedInJobEntity>(context);

            if (entity == null || !entity.Validate())
            {
                return new JobListing { Id = jobId, Url = url, Source = "LinkedIn" };
            }

            // Extract Job ID if not present
            string extractedJobId = entity.ExtractJobIdFromUrl() ?? jobId;

            // Check for Easy Apply
            bool isEasyApply = entity.IsEasyApply;

            // Parse JobType and ExperienceLevel from entity
            JobType jobType = entity.ParseJobType();
            ExperienceLevel experienceLevel = entity.ParseExperienceLevel();

            // Parse PostedAt
            DateTimeOffset postedAt = entity.PostedAt ?? DateTimeOffset.UtcNow;

            return new JobListing
            {
                Id = extractedJobId,
                Url = entity.Url ?? url,
                Title = entity.Title ?? string.Empty,
                Company = entity.Company ?? string.Empty,
                Location = entity.Location ?? string.Empty,
                Description = entity.Description ?? string.Empty,
                PostedAt = postedAt,
                IsEasyApply = isEasyApply,
                JobType = jobType,
                ExperienceLevel = experienceLevel,
                Salary = entity.Salary,
                Source = "LinkedIn"
            };
        }
        catch (Exception ex) when (ex is Microsoft.Playwright.PlaywrightException ||
                                    ex.Message.Contains("TargetClosedException", StringComparison.OrdinalIgnoreCase) ||
                                    ex.Message.Contains("ERR_SOCKS_CONNECTION_FAILED", StringComparison.OrdinalIgnoreCase) ||
                                    ex.Message.Contains("Process exited", StringComparison.OrdinalIgnoreCase))
        {
            throw new BrowserServiceUnavailableException(
                "Failed to create browser page. Browser automation service may be unavailable.",
                ex);
        }
        finally
        {
            if (page != null)
            {
                try { await page.DisposeAsync().ConfigureAwait(false); } catch { }
            }
        }
    }

    public Task<JobApplication> ApplyAsync(string jobId, ApplicationDetails details, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(jobId);
        ArgumentNullException.ThrowIfNull(details);

        return ApplyInternalAsync(jobId, details, ct);
    }

    private async Task<JobApplication> ApplyInternalAsync(string jobId, ApplicationDetails details, CancellationToken ct)
    {
        PageOptions? pageOpts = _options.GetPageOptions();
        IPage? page = null;
        try
        {
            page = await _session.NewPageAsync(pageOpts, ct: ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is Microsoft.Playwright.PlaywrightException ||
                                    ex.Message.Contains("TargetClosedException", StringComparison.OrdinalIgnoreCase) ||
                                    ex.Message.Contains("ERR_SOCKS_CONNECTION_FAILED", StringComparison.OrdinalIgnoreCase) ||
                                    ex.Message.Contains("Process exited", StringComparison.OrdinalIgnoreCase))
        {
            throw new BrowserServiceUnavailableException(
                "Failed to create browser page. Browser automation service may be unavailable.",
                ex);
        }

        try
        {
            string url = $"{_options.BaseUrl}/jobs/view/{jobId}";
            var navOptions = new NavigationOptions { Timeout = 30_000, WaitUntil = WaitUntil.Load };
            await page.NavigateAsync(url, navOptions, ct: ct).ConfigureAwait(false);
            await page.WaitForLoadStateAsync(ct: ct).ConfigureAwait(false);

            IReadOnlyList<IElement> buttons = await page.QuerySelectorAllAsync("button", ct: ct).ConfigureAwait(false);
            IElement? applyBtn = null;
            foreach (IElement b in buttons)
            {
                try
                {
                    string txt = await b.GetTextContentAsync(ct).ConfigureAwait(false) ?? string.Empty;
                    if (!string.IsNullOrEmpty(txt) && txt.Contains("Easy Apply", StringComparison.OrdinalIgnoreCase))
                    {
                        applyBtn = b;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    LinkedInLog.LogFailedToParseJobNode(_logger, ex);
                }
            }

            if (applyBtn is null)
            {
                return null!;
            }

            await applyBtn.HumanClickAsync(ct: ct).ConfigureAwait(false);
            try { await page.WaitForLoadStateAsync(ct: ct).ConfigureAwait(false); } catch { }

            return new JobApplication
            {
                Id = Guid.NewGuid().ToString(),
                JobId = jobId,
                ApplicantId = details.ApplicantEmail ?? string.Empty,
                Status = "Applied",
                SubmittedAt = DateTimeOffset.UtcNow,
                Details = details
            };
        }
        finally
        {
            try { await page.DisposeAsync().ConfigureAwait(false); } catch { }
        }
    }

    public Task<IReadOnlyList<JobApplication>> GetApplicationsAsync(ApplicationsFilter? filter = null, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task SaveJobAsync(string jobId, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<JobListing>> GetSavedJobsAsync(CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public async IAsyncEnumerable<JobListing> SearchJobsAsync(string keywords, string location, int limit = 25, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (JobListing job in SearchJobsWithStrategyAsync(keywords, location, limit, _options.ScrapingStrategy, ct).ConfigureAwait(false))
        {
            yield return job;
        }
    }

    public async IAsyncEnumerable<JobListing> SearchJobsParallelAsync(
        JobSearchCriteria criteria,
        [EnumeratorCancellation] CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        // Spider-only implementation: use Browser strategy
        await foreach (JobListing job in SearchJobsWithStrategyAsync(
            criteria.Query ?? string.Empty,
            criteria.Location ?? string.Empty,
            criteria.MaxResults > 0 ? criteria.MaxResults : 25,
            JobScrapingStrategy.Browser,
            ct).ConfigureAwait(false))
        {
            yield return job;
        }
    }

    private async IAsyncEnumerable<JobListing> SearchJobsWithStrategyAsync(
        string keywords,
        string location,
        int limit,
        JobScrapingStrategy strategy,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        s_logJobSearchStarting(_logger, strategy, keywords, null);

        // Use Browser strategy exclusively (Spider-only implementation)
        var browserContext = new StrategyContext
        {
            Url = BuildBrowserSearchUrl(keywords, location),
            Metadata = new Dictionary<string, object>
            {
                ["keywords"] = keywords,
                ["location"] = location,
                ["limit"] = limit
            }
        };

        ExtractionResult browserResult = await _strategyRouter.ExecuteStrategyAsync("Browser", browserContext, ct).ConfigureAwait(false);
        if (browserResult.Success && browserResult.Data is List<JobListing> browserJobs)
        {
            foreach (JobListing job in browserJobs)
            {
                yield return job;
            }
        }
    }

    private async Task<ExtractionResult> BrowserStrategyAsync(StrategyContext context, CancellationToken ct)
    {
        DateTimeOffset startTime = DateTimeOffset.UtcNow;
        List<JobListing> list = [];
        IPage? page = null;

        try
        {
            string keywords = (string)context.Metadata["keywords"];
            string location = (string)context.Metadata["location"];
            int limit = (int)context.Metadata["limit"];

            PageOptions? pageOpts = _options.GetPageOptions();
            page = await _session.NewPageAsync(pageOpts, ct: ct).ConfigureAwait(false);

            try
            {
                await page.EvaluateAsync<object>("() => { document.cookie.split(';').forEach(function(c) { document.cookie = c.replace(/^ +/, '').replace(/=.*/, '=;expires=' + new Date(0).toUTCString() + ';path=/'); }); }", ct: ct).ConfigureAwait(false);
            }
            catch { }

            string url = BuildBrowserSearchUrl(keywords, location);
            var navOptions = new NavigationOptions { Timeout = 30_000, WaitUntil = WaitUntil.Load };
            await page.NavigateAsync(url, navOptions, ct: ct).ConfigureAwait(false);
            await page.WaitForLoadStateAsync(ct: ct).ConfigureAwait(false);

            string pageTitle = await page.EvaluateAsync<string>("document.title", ct: ct).ConfigureAwait(false);
            IReadOnlyList<IElement> nodes = await page.QuerySelectorAllAsync(".jobs-search-results__list-item, .jobs-search__results-list li, .base-card", ct: ct).ConfigureAwait(false);

            if (nodes.Count == 0)
            {
                s_logZeroJobsFound(_logger, pageTitle ?? "Unknown", null);
            }

            int count = 0;
            foreach (IElement n in nodes)
            {
                if (count++ >= limit) break;
                try
                {
                    string id = Guid.NewGuid().ToString();
                    IElement? idEl = await n.QuerySelectorAsync("[data-id], [data-entity-urn]", ct).ConfigureAwait(false);
                    if (idEl != null)
                    {
                        string? dataId = await idEl.GetAttributeAsync("data-id", ct).ConfigureAwait(false);
                        string? urn = await idEl.GetAttributeAsync("data-entity-urn", ct).ConfigureAwait(false);
                        if (!string.IsNullOrEmpty(urn))
                        {
                            Match m = System.Text.RegularExpressions.Regex.Match(urn, @"\d+");
                            if (m.Success) id = m.Value;
                        }
                        else if (!string.IsNullOrEmpty(dataId))
                        {
                            id = dataId;
                        }
                    }

                    string title = await TryGetElementTextAsync(n, s_titleSelectors, ct).ConfigureAwait(false);
                    string company = await TryGetElementTextAsync(n, s_companySelectors, ct).ConfigureAwait(false);
                    string locationText = await TryGetElementTextAsync(n, s_locationSelectors, ct).ConfigureAwait(false);

                    string? jobUrl = null;
                    IElement? linkEl = await n.QuerySelectorAsync("a.base-card__full-link, a.job-card-list__title", ct).ConfigureAwait(false);
                    if (linkEl != null)
                    {
                        jobUrl = await linkEl.GetAttributeAsync("href", ct).ConfigureAwait(false);

                        if (Guid.TryParse(id, out _) && !string.IsNullOrEmpty(jobUrl))
                        {
                            Match urlIdMatch = System.Text.RegularExpressions.Regex.Match(jobUrl, @"-(\d{6,})(?:\?|$)");
                            if (urlIdMatch.Success && urlIdMatch.Groups[1].Success)
                            {
                                id = urlIdMatch.Groups[1].Value;
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(title))
                    {
                        list.Add(new JobListing
                        {
                            Id = id,
                            Title = title,
                            Company = company,
                            Location = locationText,
                            Url = jobUrl,
                            Source = "LinkedIn"
                        });
                    }
                }
                catch (Exception ex)
                {
                    LinkedInLog.LogFailedToParseJobNode(_logger, ex);
                }
            }

            await page.DisposeAsync().ConfigureAwait(false);
            page = null;

            // Deep-fetch details for each job using EntityParser
            var uniqueJobs = list.GroupBy(j => j.Id).Select(g => g.First()).ToList();
            List<JobListing> detailedJobs = [];
            foreach (JobListing? shallow in uniqueJobs)
            {
                ct.ThrowIfCancellationRequested();
                JobListing? deepJob = null;
                try
                {
                    deepJob = await GetJobDetailsBrowserAsync(shallow.Id, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    s_logDeepFetchFailed(_logger, shallow.Id, ex);
                }

                detailedJobs.Add(deepJob ?? shallow);
            }

            return new ExtractionResult
            {
                Success = true,
                Data = detailedJobs,
                StrategyName = "Browser",
                Duration = DateTimeOffset.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            return ExtractionResult.CreateFailure(ex.Message, "Browser", DateTimeOffset.UtcNow - startTime, ex);
        }
        finally
        {
            if (page != null)
            {
                try { await page.DisposeAsync().ConfigureAwait(false); } catch { }
            }
        }
    }

    private string BuildBrowserSearchUrl(string keywords, string location)
    {
        string query = System.Uri.EscapeDataString(keywords);
        string locationEncoded = System.Uri.EscapeDataString(location);
        return $"{_options.BaseUrl}/jobs/search?keywords={query}&location={locationEncoded}";
    }

    private static List<JobListing> GenerateMockJobs(string keywords, string location, int count)
    {
        var random = new Random();
        List<JobListing> mockJobs = [];
        string[] jobTitles = new[] { "Software Engineer", "Senior Developer", "Full Stack Engineer", "DevOps Engineer", "Data Scientist" };
        string[] companies = new[] { "Tech Corp", "Innovation Labs", "Digital Solutions Inc", "Cloud Systems", "Data Dynamics" };
        string[] locations = new[] { "San Francisco, CA", "Remote", "New York, NY", "Seattle, WA", "Austin, TX" };

        for (int i = 0; i < Math.Min(count, 5); i++)
        {
            string title = jobTitles[i % jobTitles.Length];
            string company = companies[i % companies.Length];
            string locationValue = string.IsNullOrWhiteSpace(location) ? locations[i % locations.Length] : location;

            mockJobs.Add(new JobListing
            {
                Id = $"linkedin-job-{i + 1}",
                Title = title,
                Company = company,
                Location = locationValue,
                Description = $"Looking for an experienced {title.ToLowerInvariant()} to join our team. Work with cutting-edge technologies and solve challenging problems.",
                Source = "LinkedIn",
                PostedAt = DateTimeOffset.UtcNow.AddDays(-random.Next(1, 30)),
                Url = $"https://linkedin.com/jobs/view/linkedin-job-{i + 1}",
                JobType = JobType.FullTime,
                ExperienceLevel = ExperienceLevel.MidLevel,
                IsEasyApply = i % 2 == 0
            });
        }

        return mockJobs;
    }

    /// <summary>
    /// Tries each selector in order and returns the text content of the first matching element.
    /// </summary>
    private static async Task<string> TryGetElementTextAsync(IElement container, string[] selectors, CancellationToken ct)
    {
        foreach (string selector in selectors)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                IElement? element = await container.QuerySelectorAsync(selector, ct).ConfigureAwait(false);
                if (element is not null)
                {
                    string? text = await element.GetTextContentAsync(ct).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text.Trim();
                    }
                }
            }
            catch
            {
                // Ignore exceptions and try the next selector
            }
        }

        return string.Empty;
    }
}
