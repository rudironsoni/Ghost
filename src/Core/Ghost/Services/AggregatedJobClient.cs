using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Abstractions;
using Ghost.Contracts.Jobs;
using Microsoft.Extensions.Logging;

namespace Ghost.Core.Services;

public class AggregatedJobClient : IJobClient
{
    private readonly IEnumerable<IJobScraper> _scrapers;
    private readonly IDeduplicationService _dedupe;
    private readonly ILogger<AggregatedJobClient>? _logger;

    public AggregatedJobClient(IEnumerable<IJobScraper> scrapers, IDeduplicationService dedupe, ILogger<AggregatedJobClient>? logger = null)
    {
        _scrapers = scrapers ?? Enumerable.Empty<IJobScraper>();
        _dedupe = dedupe;
        _logger = logger;
    }

    public string PlatformName => "Aggregated";

    public async Task<IReadOnlyList<JobListing>> SearchJobsAsync(JobSearchCriteria criteria, CancellationToken ct = default)
    {
        var tasks = _scrapers.Select(s => Task.Run(async () =>
        {
            try
            {
                return await s.SearchJobsAsync(criteria, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Scraper {Platform} failed", s.PlatformName);
                return (IReadOnlyList<JobListing>)new List<JobListing>();
            }
        }, ct)).ToArray();

        await Task.WhenAll(tasks);

        var all = tasks.SelectMany(t => t.Result ?? new List<JobListing>()).ToList();

        // dedupe by generated id
        var map = new Dictionary<string, JobListing>();
        foreach (var job in all)
        {
            var id = _dedupe.GenerateId(job.Title ?? string.Empty, job.Company ?? string.Empty);
            if (!map.ContainsKey(id)) map[id] = job;
        }

        return map.Values.ToList();
    }

    public Task<JobListing> GetJobDetailsAsync(string jobId, CancellationToken ct = default)
    {
        // naive: query scrapers sequentially until one returns details
        return Task.Run(async () =>
        {
            foreach (var s in _scrapers)
            {
                try
                {
                    var details = await s.GetJobDetailsAsync(jobId, ct).ConfigureAwait(false);
                    if (details != null && !string.IsNullOrEmpty(details.Title)) return details;
                }
                catch (OperationCanceledException) { throw; }
                catch { }
            }
            return new JobListing { Id = jobId };
        }, ct);
    }

    public Task<JobApplication> ApplyAsync(string jobId, ApplicationDetails details, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<JobApplication>> GetApplicationsAsync(ApplicationsFilter? filter = null, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<JobApplication>)new List<JobApplication>());
    public Task SaveJobAsync(string jobId, CancellationToken ct = default) => Task.CompletedTask;
    public Task<IReadOnlyList<JobListing>> GetSavedJobsAsync(CancellationToken ct = default) => Task.FromResult((IReadOnlyList<JobListing>)new List<JobListing>());
}
