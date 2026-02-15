using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Contracts.Jobs;

namespace Ghost.Testing.Contracts.BuiltIn;

/// <summary>
/// Contract that validates consent flow compliance.
/// Ensures that the provider handles consent dialogs and banners properly.
/// </summary>
public sealed class ConsentComplianceContract : ProviderContractBase
{
    /// <inheritdoc />
    public override string Name => "ConsentCompliance";

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
            // Test consent flow handling
            IReadOnlyList<JobListing> jobs = await adapter.TestConsentFlowAsync(criteria, ct).ConfigureAwait(false);

            context["JobsReturned"] = jobs.Count;

            // Verify that we got results despite consent flows
            if (jobs.Count == 0)
            {
                errors.Add("Consent flow test returned no jobs - provider may not be handling consent dialogs properly");
            }
            else
            {
                // Verify that jobs have required fields after consent handling
                var jobsWithMissingFields = jobs.Where(j =>
                    string.IsNullOrWhiteSpace(j.Id) ||
                    string.IsNullOrWhiteSpace(j.Title) ||
                    string.IsNullOrWhiteSpace(j.Company)).ToList();

                if (jobsWithMissingFields.Count > 0)
                {
                    errors.Add($"{jobsWithMissingFields.Count} jobs have missing required fields after consent flow");
                    context["JobsWithMissingFields"] = jobsWithMissingFields.Count;
                }
            }

            // Test that consent flow doesn't cause infinite loops or hangs
            // This is implicitly tested by the fact that we got a result
            context["ConsentFlowCompleted"] = true;
        }
        catch (System.TimeoutException)
        {
            errors.Add("Consent flow test timed out - provider may be stuck on consent dialog");
            context["Timeout"] = true;
        }
        catch (System.Exception ex)
        {
            errors.Add($"Consent flow test threw exception: {ex.Message}");
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
