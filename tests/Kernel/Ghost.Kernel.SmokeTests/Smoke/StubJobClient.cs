using Ghost.Contracts.Jobs;
using System.Collections.Concurrent;

namespace Ghost.Smoke.Tests.Smoke;

/// <summary>
/// Stub job client that returns test data without requiring external services.
/// Use this for smoke tests that need deterministic, fast results.
/// </summary>
public sealed class StubJobClient : IJobClient
{
    private readonly ConcurrentDictionary<string, JobListing> _jobs = new();

    public StubJobClient(string platformName)
    {
        PlatformName = platformName;
        SeedTestData();
    }

    public string PlatformName { get; }

    public Task<IReadOnlyList<JobListing>> SearchJobsAsync(JobSearchCriteria criteria, CancellationToken ct = default)
    {
        var results = _jobs.Values
            .Where(j => string.IsNullOrEmpty(criteria.Query) || 
                       j.Title.Contains(criteria.Query, StringComparison.OrdinalIgnoreCase))
            .Take(criteria.MaxResults > 0 ? criteria.MaxResults : 10)
            .ToList();

        return Task.FromResult<IReadOnlyList<JobListing>>(results);
    }

    public Task<JobListing> GetJobDetailsAsync(string jobId, CancellationToken ct = default)
    {
        if (_jobs.TryGetValue(jobId, out JobListing? job))
        {
            return Task.FromResult(job);
        }

        throw new InvalidOperationException($"Job {jobId} not found");
    }

    public Task<JobApplication> ApplyAsync(string jobId, ApplicationDetails details, CancellationToken ct = default)
    {
        throw new NotImplementedException("Apply functionality requires external infrastructure");
    }

    public Task<IReadOnlyList<JobListing>> GetSavedJobsAsync(CancellationToken ct = default)
    {
        throw new NotImplementedException("GetSavedJobs requires external authentication");
    }

    public Task<IReadOnlyList<JobApplication>> GetApplicationsAsync(ApplicationsFilter? filter = null, CancellationToken ct = default)
    {
        throw new NotImplementedException("GetApplications requires external authentication");
    }

    public Task SaveJobAsync(string jobId, CancellationToken ct = default)
    {
        throw new NotImplementedException("SaveJob requires external authentication");
    }

    private void SeedTestData()
    {
        var platformLower = PlatformName.ToLowerInvariant();
        var jobs = new[]
        {
            new JobListing
            {
                Id = $"{platformLower}-job-001",
                Title = "Senior Software Engineer",
                Company = "TechCorp",
                Location = "San Francisco, CA",
                Description = "Looking for an experienced software engineer...",
                Url = $"https://example.com/{platformLower}/job/001",
                Source = PlatformName,
                PostedAt = DateTimeOffset.UtcNow.AddDays(-2)
            },
            new JobListing
            {
                Id = $"{platformLower}-job-002",
                Title = "Full Stack Developer",
                Company = "StartupXYZ",
                Location = "Remote",
                Description = "Join our growing team as a full stack developer...",
                Url = $"https://example.com/{platformLower}/job/002",
                Source = PlatformName,
                PostedAt = DateTimeOffset.UtcNow.AddDays(-5)
            },
            new JobListing
            {
                Id = $"{platformLower}-job-003",
                Title = "Backend Engineer",
                Company = "DataSystems Inc",
                Location = "New York, NY",
                Description = "Build scalable backend systems...",
                Url = $"https://example.com/{platformLower}/job/003",
                Source = PlatformName,
                PostedAt = DateTimeOffset.UtcNow.AddDays(-1)
            }
        };

        foreach (var job in jobs)
        {
            _jobs[job.Id] = job;
        }
    }
}
