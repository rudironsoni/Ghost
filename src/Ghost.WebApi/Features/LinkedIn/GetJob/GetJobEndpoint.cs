using FastEndpoints;
using Ghost.Contracts.Jobs;

namespace Ghost.WebApi.Features.LinkedIn.GetJob;

public class GetJobRequest
{
    public string Id { get; set; } = default!;
}

public class GetJobEndpoint : Endpoint<GetJobRequest, JobListing>
{
    private readonly IJobClient _jobClient;

    public GetJobEndpoint(IJobClient jobClient)
    {
        _jobClient = jobClient;
    }

    public override void Configure()
    {
        Get("/api/linkedin/jobs/{id}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetJobRequest req, CancellationToken ct)
    {
        try
        {
            var job = await _jobClient.GetJobDetailsAsync(req.Id, ct);
            if (job is null)
            {
                await SendNotFoundAsync(ct);
                return;
            }
            await SendAsync(job, cancellation: ct);
        }
        catch (Exception ex)
        {
            // If the job is not found or error occurs
            AddError(ex.Message);
            await SendErrorsAsync(cancellation: ct);
        }
    }
}
