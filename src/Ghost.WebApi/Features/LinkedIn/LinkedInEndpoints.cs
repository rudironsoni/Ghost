using Ghost.Contracts.Jobs;
using Ghost.Contracts.News;
using Ghost.Contracts.Social;
using Ghost.Platform.LinkedIn;
using Microsoft.AspNetCore.Http;
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
        [FromServices] LinkedInJobClient jobClient,
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
        catch (Ghost.Platform.LinkedIn.BrowserServiceUnavailableException)
        {
            return Results.Problem(
                detail: "Browser automation service is currently unavailable. Please try again later.",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Service Unavailable");
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.Message);
        }
    }

    private static async Task<IResult> SearchJobs(
        JobSearchCriteria criteria,
        [FromQuery] string? strategy,
        [FromServices] LinkedInJobClient jobClient,
        CancellationToken ct)
    {
        try
        {
            // Check for strategy override via query parameter
            if (!string.IsNullOrEmpty(strategy) &&
                Enum.TryParse<JobScrapingStrategy>(strategy, ignoreCase: true, out var strategyOverride))
            {
                var results = await jobClient.SearchJobsAsync(criteria, strategyOverride, ct);
                return Results.Ok(results);
            }

            // Default: use configured strategy
            var defaultResults = await jobClient.SearchJobsAsync(criteria, ct);
            return Results.Ok(defaultResults);
        }
        catch (Ghost.Platform.LinkedIn.BrowserServiceUnavailableException)
        {
            return Results.Problem(
                detail: "Browser automation service is currently unavailable. Please try again later.",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Service Unavailable");
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.Message);
        }
    }

    private static async Task<IResult> GetProfile(
        string id,
        [FromServices] LinkedInSocialClient socialClient,
        CancellationToken ct)
    {
        try
        {
            var profile = await socialClient.GetProfileAsync(id, ct);
            return profile is not null ? Results.Ok(profile) : Results.NotFound();
        }
        catch (Ghost.Platform.LinkedIn.BrowserServiceUnavailableException)
        {
            return Results.Problem(
                detail: "Browser automation service is currently unavailable. Please try again later.",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Service Unavailable");
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.Message);
        }
    }

    private static async Task<IResult> SearchNews(
        NewsSearchRequest request,
        [FromServices] LinkedInNewsClient newsClient,
        CancellationToken ct)
    {
        try
        {
            var options = new NewsSearchOptions { MaxResults = request.MaxResults };
            var results = await newsClient.SearchAsync(request.Query, options, ct);
            return Results.Ok(results);
        }
        catch (Ghost.Platform.LinkedIn.BrowserServiceUnavailableException)
        {
            return Results.Problem(
                detail: "Browser automation service is currently unavailable. Please try again later.",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Service Unavailable");
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.Message);
        }
    }
}

public record NewsSearchRequest(string Query, int MaxResults = 20);
