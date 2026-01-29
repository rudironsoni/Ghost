using System.Threading;
using System.Threading.Tasks;
using Ghost.Contracts.Jobs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Builder;

namespace Ghost.WebApi.Features.Jobs;

public static class JobsEndpoints
{
    public static void MapJobsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/jobs").MapPost("/search", SearchJobs);
    }

    static async Task<IResult> SearchJobs([FromBody] JobSearchCriteria criteria, IJobClient client, CancellationToken ct)
    {
        var result = await client.SearchJobsAsync(criteria, ct);
        return Results.Ok(result);
    }
}
