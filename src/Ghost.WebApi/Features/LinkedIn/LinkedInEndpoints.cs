using Ghost.Contracts.Jobs;
using Ghost.Contracts.News;
using Ghost.Contracts.Social;
using Microsoft.AspNetCore.Mvc;

namespace Ghost.WebApi.Features.LinkedIn;

public static class LinkedInEndpoints
{
    public static void MapLinkedInEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/linkedin")
            .WithTags("LinkedIn");

        // Jobs
        group.MapGet("/jobs/{id}", GetJob)
            .WithName("GetLinkedInJob")
            .AllowAnonymous();

        group.MapPost("/jobs/search", SearchJobs)
            .WithName("SearchLinkedInJobs")
            .AllowAnonymous();

        // Social
        group.MapGet("/social/profile/{id}", GetProfile)
            .WithName("GetSocialProfile")
            .AllowAnonymous();

        // News
        group.MapPost("/news/search", SearchNews)
            .WithName("SearchNews")
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
        if (jobClient.PlatformName != "LinkedIn")
        {
            // validation logic
        }

        var results = await jobClient.SearchJobsAsync(criteria, ct);
        return Results.Ok(results);
    }

    private static async Task<IResult> GetProfile(
        string id,
        [FromServices] ISocialClient socialClient,
        CancellationToken ct)
    {
        try
        {
            var profile = await socialClient.GetProfileAsync(id, ct);
            return profile is not null ? Results.Ok(profile) : Results.NotFound();
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.Message);
        }
    }

    private static async Task<IResult> SearchNews(
        [FromBody] NewsSearchRequest request,
        [FromServices] INewsClient newsClient,
        CancellationToken ct)
    {
        try
        {
            var options = new NewsSearchOptions { MaxResults = request.MaxResults };
            var results = await newsClient.SearchAsync(request.Query, options, ct);
            return Results.Ok(results);
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.Message);
        }
    }
}

public record NewsSearchRequest(string Query, int MaxResults = 20);
