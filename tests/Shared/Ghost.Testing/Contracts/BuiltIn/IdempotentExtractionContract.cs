using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Contracts.Jobs;

namespace Ghost.Testing.Contracts.BuiltIn;

/// <summary>
/// Contract that validates idempotent extraction.
/// Ensures that the same input produces the same output across multiple runs.
/// </summary>
public sealed class IdempotentExtractionContract : ProviderContractBase
{
    /// <inheritdoc />
    public override string Name => "IdempotentExtraction";

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

        var errors = new List<string>();
        var context = new Dictionary<string, object>();

        try
        {
            // Test idempotency by running the same extraction twice
            var (firstRun, secondRun) = await adapter.TestIdempotencyAsync(criteria, ct);

            context["FirstRunCount"] = firstRun.Count;
            context["SecondRunCount"] = secondRun.Count;

            // Verify that both runs returned the same number of jobs
            if (firstRun.Count != secondRun.Count)
            {
                errors.Add($"Idempotency violation: first run returned {firstRun.Count} jobs, second run returned {secondRun.Count} jobs");
            }

            // Verify that the same job IDs are returned
            var firstIds = new HashSet<string>(firstRun.Select(j => j.Id));
            var secondIds = new HashSet<string>(secondRun.Select(j => j.Id));

            var missingInSecond = firstIds.Except(secondIds).ToList();
            var extraInSecond = secondIds.Except(firstIds).ToList();

            if (missingInSecond.Count > 0)
            {
                errors.Add($"Idempotency violation: {missingInSecond.Count} jobs from first run are missing in second run");
                context["MissingInSecond"] = missingInSecond.Count;
            }

            if (extraInSecond.Count > 0)
            {
                errors.Add($"Idempotency violation: {extraInSecond.Count} extra jobs in second run not in first run");
                context["ExtraInSecond"] = extraInSecond.Count;
            }

            // Verify that job details are consistent
            if (firstRun.Count == secondRun.Count && firstRun.Count > 0)
            {
                var firstRunDict = firstRun.ToDictionary(j => j.Id);
                var secondRunDict = secondRun.ToDictionary(j => j.Id);

                var inconsistentJobs = new List<string>();

                foreach (var id in firstIds.Intersect(secondIds))
                {
                    var firstJob = firstRunDict[id];
                    var secondJob = secondRunDict[id];

                    if (firstJob.Title != secondJob.Title)
                    {
                        inconsistentJobs.Add($"Job {id}: Title mismatch ('{firstJob.Title}' vs '{secondJob.Title}')");
                    }

                    if (firstJob.Company != secondJob.Company)
                    {
                        inconsistentJobs.Add($"Job {id}: Company mismatch ('{firstJob.Company}' vs '{secondJob.Company}')");
                    }
                }

                if (inconsistentJobs.Count > 0)
                {
                    errors.Add($"Idempotency violation: {inconsistentJobs.Count} jobs have inconsistent data between runs");
                    context["InconsistentJobs"] = inconsistentJobs.Count;
                    context["InconsistencyDetails"] = inconsistentJobs.Take(5).ToList(); // Limit to first 5
                }
            }

            // Verify order consistency (if applicable)
            if (firstRun.Count == secondRun.Count && firstRun.Count > 0)
            {
                var orderMatches = true;
                for (int i = 0; i < Math.Min(firstRun.Count, secondRun.Count); i++)
                {
                    if (firstRun[i].Id != secondRun[i].Id)
                    {
                        orderMatches = false;
                        break;
                    }
                }

                context["OrderMatches"] = orderMatches;

                // Note: Order mismatch is not necessarily a failure, but worth documenting
                if (!orderMatches)
                {
                    context["OrderMismatch"] = true;
                }
            }
        }
        catch (System.Exception ex)
        {
            errors.Add($"Idempotency test threw exception: {ex.Message}");
            context["Exception"] = ex.GetType().Name;
        }

        context["Errors"] = errors.Count;

        if (errors.Count > 0)
        {
            return Failure(context, errors.ToArray());
        }

        return Success();
    }
}
