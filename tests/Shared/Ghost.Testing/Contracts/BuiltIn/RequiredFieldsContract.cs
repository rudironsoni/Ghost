using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Contracts.Jobs;

namespace Ghost.Testing.Contracts.BuiltIn;

/// <summary>
/// Contract that validates all required fields are present in job listings.
/// </summary>
public sealed class RequiredFieldsContract : ProviderContractBase
{
    /// <inheritdoc />
    public override string Name => "RequiredFields";

    /// <inheritdoc />
    public override async Task<ContractResult> ExecuteAsync(
        IProviderContractAdapter adapter,
        CancellationToken ct = default)
    {
        var criteria = new JobSearchCriteria
        {
            Query = "software engineer",
            Location = "remote"
        };

        var jobs = await adapter.GetJobsAsync(criteria, ct);

        if (jobs.Count == 0)
        {
            return Failure("No jobs returned to validate required fields");
        }

        var errors = new List<string>();
        var context = new Dictionary<string, object>
        {
            ["TotalJobs"] = jobs.Count
        };

        foreach (var job in jobs)
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(job.Id))
            {
                errors.Add($"Job missing required field: Id (Title: '{job.Title}')");
            }

            if (string.IsNullOrWhiteSpace(job.Title))
            {
                errors.Add($"Job missing required field: Title (Id: '{job.Id}')");
            }

            if (string.IsNullOrWhiteSpace(job.Company))
            {
                errors.Add($"Job missing required field: Company (Id: '{job.Id}', Title: '{job.Title}')");
            }

            // Validate optional but important fields
            if (string.IsNullOrWhiteSpace(job.Url))
            {
                errors.Add($"Job missing recommended field: Url (Id: '{job.Id}', Title: '{job.Title}')");
            }

            if (string.IsNullOrWhiteSpace(job.Source))
            {
                errors.Add($"Job missing recommended field: Source (Id: '{job.Id}', Title: '{job.Title}')");
            }
        }

        context["Errors"] = errors.Count;
        context["JobsWithMissingFields"] = errors.Count;

        if (errors.Count > 0)
        {
            return Failure(context, errors.ToArray());
        }

        return Success();
    }
}
