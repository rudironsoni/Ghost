using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Contracts.Jobs;
using Ghost.Platform.Indeed.Internal;
using Microsoft.Extensions.Logging;

namespace Ghost.Platform.Indeed;

public class IndeedJobClient : Ghost.Abstractions.IJobScraper
{
    private readonly IndeedApiClient _api;
    private readonly ILogger<IndeedJobClient> _logger;
    private static readonly Action<ILogger, int, Exception?> LogRawCount =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(1001, "IndeedRawCount"), "IndeedApiClient returned {Count} raw items.");
    private static readonly Action<ILogger, int, Exception?> LogParsedCount =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(1002, "ParsedJobListings"), "Parsed {Count} JobListings.");

    public IndeedJobClient(IndeedApiClient api, ILogger<IndeedJobClient> logger)
    {
        _api = api;
        _logger = logger;
    }

    public string PlatformName => "Indeed";

    public async Task<IReadOnlyList<JobListing>> SearchJobsAsync(JobSearchCriteria criteria, CancellationToken ct = default)
    {
        var list = new List<JobListing>();
        int rawCount = 0;
        int parsedCount = 0;
        await foreach (var root in _api.SearchAsync(criteria.Query ?? string.Empty, criteria.Location ?? string.Empty, criteria.MaxResults))
        {
            // count raw items (results array length) if present
            if (root.TryGetProperty("data", out var data) && data.TryGetProperty("jobSearch", out var jobSearch) && jobSearch.TryGetProperty("results", out var results))
            {
                rawCount += results.GetArrayLength();
            }

            var parsed = IndeedJobParser.ParseJobs(root).ToList();
            parsedCount += parsed.Count;
            list.AddRange(parsed);
        }

        LogRawCount(_logger, rawCount, null);
        LogParsedCount(_logger, parsedCount, null);

        return list;
    }

    public Task<JobListing> GetJobDetailsAsync(string jobId, CancellationToken ct = default) =>
        Task.FromResult(new JobListing { Id = jobId, Source = "Indeed" });

    public Task<JobApplication> ApplyAsync(string jobId, ApplicationDetails details, CancellationToken ct = default) =>
        Task.FromResult(new JobApplication());

    public Task<IReadOnlyList<JobApplication>> GetApplicationsAsync(ApplicationsFilter? filter = null, CancellationToken ct = default) =>
        Task.FromResult((IReadOnlyList<JobApplication>)new List<JobApplication>());

    public Task SaveJobAsync(string jobId, CancellationToken ct = default) => Task.CompletedTask;

    public Task<IReadOnlyList<JobListing>> GetSavedJobsAsync(CancellationToken ct = default) =>
        Task.FromResult((IReadOnlyList<JobListing>)new List<JobListing>());
}
