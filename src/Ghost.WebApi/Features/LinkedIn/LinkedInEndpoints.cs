using Ghost.Contracts.Jobs;
using Ghost.Contracts.News;
using Ghost.Contracts.Social;
using Ghost.Plugin.LinkedIn;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Ghost.WebApi.Features.LinkedIn;

public static class LinkedInEndpoints
{
    public static void MapLinkedInEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/linkedin")
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
            JobListing? job = await jobClient.GetJobDetailsAsync(id, ct).ConfigureAwait(false);
            if (job is null)
            {
                return Results.NotFound();
            }
            return Results.Ok(job);
        }
        catch (Ghost.Plugin.LinkedIn.BrowserServiceUnavailableException)
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
                Enum.TryParse<JobScrapingStrategy>(strategy, ignoreCase: true, out JobScrapingStrategy strategyOverride))
            {
                List<JobListing> results = await jobClient.SearchJobsAsync(criteria, strategyOverride, ct).ConfigureAwait(false);
                return Results.Ok(results);
            }

            // Default: use configured strategy
            IReadOnlyList<JobListing> defaultResults = await jobClient.SearchJobsAsync(criteria, ct).ConfigureAwait(false);
            return Results.Ok(defaultResults);
        }
        catch (Ghost.Plugin.LinkedIn.BrowserServiceUnavailableException)
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
            SocialProfile? profile = await socialClient.GetProfileAsync(id, ct).ConfigureAwait(false);
            return profile is not null ? Results.Ok(profile) : Results.NotFound();
        }
        catch (Ghost.Plugin.LinkedIn.BrowserServiceUnavailableException)
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
            IReadOnlyList<NewsArticle> results = await newsClient.SearchAsync(request.Query, options, ct).ConfigureAwait(false);
            return Results.Ok(results);
        }
        catch (Ghost.Plugin.LinkedIn.BrowserServiceUnavailableException)
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
