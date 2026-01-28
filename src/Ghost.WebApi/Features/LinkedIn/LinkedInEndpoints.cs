using Ghost.Contracts.Jobs;
using Microsoft.AspNetCore.Mvc;

namespace Ghost.WebApi.Features.LinkedIn;

public static class LinkedInEndpoints
{
    public static void MapLinkedInEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/linkedin/jobs")
            .WithTags("LinkedIn");

        group.MapGet("/{id}", GetJob)
            .WithName("GetLinkedInJob")
            .AllowAnonymous();

        group.MapPost("/search", SearchJobs)
            .WithName("SearchLinkedInJobs")
            .AllowAnonymous();
    }

    private static async Task<IResult> GetJob(
        string id,
        [FromServices] IJobClient jobClient,
        CancellationToken ct)
    {
        try
        {
            var job = await jobClient.GetJobDetailsAsync(id, ct);
            if (job is null)
            {
                return Results.NotFound();
            }
            return Results.Ok(job);
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.Message);
        }
    }

    private static async Task<IResult> SearchJobs(
        [FromBody] JobSearchCriteria criteria,
        [FromServices] IJobClient jobClient,
        CancellationToken ct)
    {
        // Optional: Verify this is indeed the LinkedIn client if multiple are loaded
        if (jobClient.PlatformName != "LinkedIn")
        {
            // In a real multi-provider scenario, we'd use Keyed Services or a Factory
            // For now, we assume LinkedIn is the primary or only Job Client enabled
        }

        var results = await jobClient.SearchJobsAsync(criteria, ct);
        return Results.Ok(results);
    }
}
