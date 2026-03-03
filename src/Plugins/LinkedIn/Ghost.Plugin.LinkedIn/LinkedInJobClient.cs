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
using Ghost.Sdk.Spider.Strategies.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

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
        ".job-card__title-link",   // Job Card: text is in the <a> tag inside <h3>
        ".job-card__title",
        "h3"
    };

    private static readonly string[] s_companySelectors = new[]
    {
        ".base-search-card__subtitle",
        ".job-card__company-name",
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
    private readonly IStrategyRouter _strategyRouter;

    /// <summary>
    /// Initializes a new instance of the <see cref="LinkedInJobClient"/> class.
    /// </summary>
    public LinkedInJobClient(
        Ghost.IBrowserSession session,
        IOptions<LinkedInOptions> options,
        ILogger<LinkedInJobClient> logger,
        JavaScriptAdapter jsAdapter,
        EntityParser entityParser)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(jsAdapter);
        ArgumentNullException.ThrowIfNull(entityParser);

        _session = session;
        _options = options.Value;
        _logger = logger;
        _jsAdapter = jsAdapter;
        _entityParser = entityParser;

        // Initialize Spider StrategyRouter
        var strategyRouter = new StrategyRouter(null);

        // Register strategies: Browser only (GuestApi removed as per migration requirement)
        strategyRouter.RegisterStrategy("Browser", BrowserStrategyAsync);

        _strategyRouter = strategyRouter;
    }

    /// <summary>
    /// Test constructor that allows injecting a custom strategy router.
    /// </summary>
    internal LinkedInJobClient(
        Ghost.IBrowserSession session,
        IOptions<LinkedInOptions> options,
        ILogger<LinkedInJobClient> logger,
        JavaScriptAdapter jsAdapter,
        EntityParser entityParser,
        IStrategyRouter strategyRouter)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(jsAdapter);
        ArgumentNullException.ThrowIfNull(entityParser);
        ArgumentNullException.ThrowIfNull(strategyRouter);

        _session = session;
        _options = options.Value;
        _logger = logger;
        _jsAdapter = jsAdapter;
        _entityParser = entityParser;
        _strategyRouter = strategyRouter;
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

            // Use manual extraction instead of EntityParser for reliability
            LinkedInJobEntity? entity = null;
            try
            {
                AngleSharp.Html.Parser.HtmlParser parser = new AngleSharp.Html.Parser.HtmlParser();
                AngleSharp.Html.Dom.IHtmlDocument doc = parser.ParseDocument(html ?? string.Empty);
                AngleSharp.Dom.IElement? bodyEl = doc.QuerySelector("body");

                if (bodyEl != null)
                {
                    entity = new LinkedInJobEntity
                    {
                        SourceUrl = url,
                        ExtractedAt = DateTime.UtcNow
                    };

                    // Extract properties using CSS selectors
                    AngleSharp.Dom.IElement? titleEl = bodyEl.QuerySelector(".job-title, .top-card-layout__title, h1");
                    AngleSharp.Dom.IElement? companyEl = bodyEl.QuerySelector(".company-name, .topcard__org-name-link");
                    AngleSharp.Dom.IElement? locationEl = bodyEl.QuerySelector(".location, .topcard__flavor--bullet");
                    AngleSharp.Dom.IElement? descriptionEl = bodyEl.QuerySelector("[data-test-id='job-description'], .job-description");
                    AngleSharp.Dom.IElement? salaryEl = bodyEl.QuerySelector("[data-test-id='salary'], .salary");
                    AngleSharp.Dom.IElement? easyApplyEl = bodyEl.QuerySelector(".jobs-apply-button--top-card button, .jobs-s-apply button");

                    entity.Title = titleEl?.TextContent?.Trim();
                    entity.Company = companyEl?.TextContent?.Trim();
                    entity.Location = locationEl?.TextContent?.Trim();
                    entity.Description = descriptionEl?.TextContent?.Trim();
                    entity.Salary = salaryEl?.TextContent?.Trim();
                    entity.EasyApplyButton = easyApplyEl?.TextContent?.Trim();
                    entity.Url = url;
                    entity.JobId = jobId;
                }
            }
            catch
            {
                // Ignore extraction errors
            }

            if (entity == null || string.IsNullOrWhiteSpace(entity.Title))
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
                try { await page.DisposeAsync().ConfigureAwait(false); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to dispose page: {ex.Message}"); }
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
            try { await page.WaitForLoadStateAsync(ct: ct).ConfigureAwait(false); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Wait for load state failed: {ex.Message}"); }

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
            try { await page.DisposeAsync().ConfigureAwait(false); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to dispose page: {ex.Message}"); }
        }
    }

    public Task<IReadOnlyList<JobApplication>> GetApplicationsAsync(ApplicationsFilter? filter = null, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<JobApplication>>(Array.Empty<JobApplication>());
    }

    public Task SaveJobAsync(string jobId, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<JobApplication>>(Array.Empty<JobApplication>());
    }

    public Task<IReadOnlyList<JobListing>> GetSavedJobsAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<JobApplication>>(Array.Empty<JobApplication>());
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to clear cookies: {ex.Message}");
            }

            string url = BuildBrowserSearchUrl(keywords, location);
            var navOptions = new NavigationOptions { Timeout = 30_000, WaitUntil = WaitUntil.Load };
            await page.NavigateAsync(url, navOptions, ct: ct).ConfigureAwait(false);
            await page.WaitForLoadStateAsync(ct: ct).ConfigureAwait(false);

            // Wait for job cards to appear
            try
            {
                await page.WaitForSelectorAsync(".job-card, .base-card", new WaitOptions { Timeout = 5000, State = WaitState.Visible }, ct).ConfigureAwait(false);
            }
            catch { /* Continue even if timeout */ }

            string pageTitle = await page.EvaluateAsync<string>("document.title", ct: ct).ConfigureAwait(false);

            // Extract all job data using a single JavaScript evaluation
            // Return JSON string to avoid deserialization issues
            List<Dictionary<string, string>>? jobDataList = null;
            try
            {
                string? jsonResult = await page.EvaluateAsync<string>(
                @"() => {
                    try {
                        const results = [];
                        const containers = document.querySelectorAll('.jobs-search-results__list-item, .jobs-search__results-list li, .base-card, .job-card');
                        for (let i = 0; i < containers.length; i++) {
                            const container = containers[i];
                            let id = container.getAttribute('data-job-id') || container.getAttribute('data-id');
                            if (!id) {
                                const urn = container.getAttribute('data-entity-urn');
                                if (urn) {
                                    const match = urn.match(/\d+/);
                                    if (match) id = match[0];
                                }
                            }
                            if (!id) continue;

                            // Title selectors in order of preference
                            const titleSelectors = ['.base-search-card__title', '.job-card__title-link', '.job-card__title', 'h3'];
                            let title = '';
                            for (let t = 0; t < titleSelectors.length; t++) {
                                const el = container.querySelector(titleSelectors[t]);
                                if (el) {
                                    title = (el.innerText || el.textContent || '').trim();
                                    if (title) break;
                                }
                            }

                            // Company selectors
                            const companySelectors = ['.base-search-card__subtitle', '.job-card__company-name', '.job-card-container__company-name', 'h4'];
                            let company = '';
                            for (let c = 0; c < companySelectors.length; c++) {
                                const el = container.querySelector(companySelectors[c]);
                                if (el) {
                                    company = (el.innerText || el.textContent || '').trim();
                                    if (company) break;
                                }
                            }

                            // Location selectors
                            const locationSelectors = ['.job-search-card__location', '.job-card-container__metadata-item', '.job-card__location'];
                            let location = '';
                            for (let l = 0; l < locationSelectors.length; l++) {
                                const el = container.querySelector(locationSelectors[l]);
                                if (el) {
                                    location = (el.innerText || el.textContent || '').trim();
                                    if (location) break;
                                }
                            }

                            // URL from link
                            const linkEl = container.querySelector('a.base-card__full-link, a.job-card-list__title, a.job-card__title-link, a');
                            const url = linkEl ? linkEl.getAttribute('href') : '';

                            results.push({id: id, title: title, company: company, location: location, url: url});
                        }
                        return JSON.stringify(results);
                    } catch (e) {
                        return JSON.stringify({error: e.message});
                    }
                }", ct: ct).ConfigureAwait(false);

                if (!string.IsNullOrEmpty(jsonResult))
                {
                    if (jsonResult.Contains("\"error\""))
                    {
                        LinkedInLog.LogJavaScriptError(_logger, jsonResult);
                        jobDataList = new List<Dictionary<string, string>>();
                    }
                    else
                    {
                        jobDataList = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, string>>>(jsonResult);
                    }
                }
            }
            catch (Exception ex)
            {
                LinkedInLog.LogJavaScriptExtractionFailed(_logger, ex.Message, ex);
                jobDataList = new List<Dictionary<string, string>>();
            }

            if (jobDataList == null || jobDataList.Count == 0)
            {
                s_logZeroJobsFound(_logger, pageTitle ?? "Unknown", null);
            }

            int count = 0;
            foreach (Dictionary<string, string> jobData in jobDataList ?? new List<Dictionary<string, string>>())
            {
                if (count++ >= limit) break;
                try
                {
                    string id = jobData.GetValueOrDefault("id") ?? Guid.NewGuid().ToString();
                    string title = jobData.GetValueOrDefault("title") ?? string.Empty;
                    string company = jobData.GetValueOrDefault("company") ?? string.Empty;
                    string locationText = jobData.GetValueOrDefault("location") ?? string.Empty;
                    string? jobUrl = jobData.GetValueOrDefault("url");

                    if (string.IsNullOrEmpty(jobUrl))
                    {
                        jobUrl = $"/jobs/view/{id}";
                    }

                    if (Guid.TryParse(id, out _) && !string.IsNullOrEmpty(jobUrl) && jobUrl.Contains('-'))
                    {
                        Match urlIdMatch = System.Text.RegularExpressions.Regex.Match(jobUrl, @"-(\d{6,})(?:\?|$)");
                        if (urlIdMatch.Success && urlIdMatch.Groups[1].Success)
                        {
                            id = urlIdMatch.Groups[1].Value;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(title))
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

                // Only use deep fetch result if it has valid data, otherwise keep shallow data
                if (deepJob is not null && !string.IsNullOrWhiteSpace(deepJob.Title) && !string.IsNullOrWhiteSpace(deepJob.Company))
                {
                    detailedJobs.Add(deepJob);
                }
                else
                {
                    detailedJobs.Add(shallow);
                }
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
                try { await page.DisposeAsync().ConfigureAwait(false); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to dispose page: {ex.Message}"); }
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
                PostedAt = DateTimeOffset.UtcNow.AddDays(-Random.Shared.Next(1, 30)),
                Url = $"https://linkedin.com/jobs/view/linkedin-job-{i + 1}",
                JobType = JobType.FullTime,
                ExperienceLevel = ExperienceLevel.MidLevel,
                IsEasyApply = i % 2 == 0
            });
        }

        return mockJobs;
    }

}
