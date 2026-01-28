using FastEndpoints;
using Ghost.Contracts.Jobs;

namespace Ghost.WebApi.Features.LinkedIn.SearchJobs;

public class SearchJobsEndpoint : Endpoint<JobSearchCriteria, IEnumerable<JobListing>>
{
    private readonly IJobClient _jobClient;

    public SearchJobsEndpoint(IJobClient jobClient)
    {
        _jobClient = jobClient;
    }

    public override void Configure()
    {
        Post("/api/linkedin/jobs/search");
        AllowAnonymous();
    }

    public override async Task HandleAsync(JobSearchCriteria req, CancellationToken ct)
    {
        // Optional: Verify this is indeed the LinkedIn client if multiple are loaded
        if (_jobClient.PlatformName != "LinkedIn")
        {
            // In a real multi-provider scenario, we'd use Keyed Services or a Factory
            // For now, we assume LinkedIn is the primary or only Job Client enabled
        }

        var results = await _jobClient.SearchJobsAsync(req, ct);
        await SendAsync(results, cancellation: ct);
    }
}
