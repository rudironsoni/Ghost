using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Contracts.Jobs;

namespace Ghost.Testing.Contracts.BuiltIn;

/// <summary>
/// Contract that validates pagination completeness.
/// Ensures that pagination doesn't skip items and terminates properly.
/// </summary>
public sealed class PaginationContract : ProviderContractBase
{
    /// <inheritdoc />
    public override string Name => "Pagination";

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

        List<string> errors = [];
        Dictionary<string, object> context = [];

        // Test pagination with different page counts
        IReadOnlyList<JobListing> page1Jobs = await adapter.SearchWithPaginationAsync(criteria, maxPages: 1, ct);
        IReadOnlyList<JobListing> page3Jobs = await adapter.SearchWithPaginationAsync(criteria, maxPages: 3, ct);
        IReadOnlyList<JobListing> page5Jobs = await adapter.SearchWithPaginationAsync(criteria, maxPages: 5, ct);

        context["Page1Count"] = page1Jobs.Count;
        context["Page3Count"] = page3Jobs.Count;
        context["Page5Count"] = page5Jobs.Count;

        // Verify that more pages return more or equal jobs
        if (page3Jobs.Count < page1Jobs.Count)
        {
            errors.Add($"Pagination regression: 3 pages ({page3Jobs.Count}) returned fewer jobs than 1 page ({page1Jobs.Count})");
        }

        if (page5Jobs.Count < page3Jobs.Count)
        {
            errors.Add($"Pagination regression: 5 pages ({page5Jobs.Count}) returned fewer jobs than 3 pages ({page3Jobs.Count})");
        }

        // Verify that page 1 jobs are included in page 3 jobs
        var page1Ids = new HashSet<string>(page1Jobs.Select(j => j.Id));
        var page3Ids = new HashSet<string>(page3Jobs.Select(j => j.Id));

        var missingInPage3 = page1Ids.Except(page3Ids).ToList();
        if (missingInPage3.Count > 0)
        {
            errors.Add($"Pagination inconsistency: {missingInPage3.Count} jobs from page 1 are missing in page 3");
            context["MissingInPage3"] = missingInPage3.Count;
        }

        // Check for duplicates across pages
        var allIds = page5Jobs.Select(j => j.Id).ToList();
        var duplicateIds = allIds
            .GroupBy(id => id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateIds.Count > 0)
        {
            errors.Add($"Pagination returned duplicate job IDs: {duplicateIds.Count} duplicates found");
            context["DuplicateIds"] = duplicateIds.Count;
        }

        // Verify proper termination (should not return same jobs repeatedly)
        if (page5Jobs.Count == page3Jobs.Count && page3Jobs.Count > 0)
        {
            // Check if we're getting the same jobs (pagination not advancing)
            var page3IdsSet = new HashSet<string>(page3Jobs.Select(j => j.Id));
            var page5IdsSet = new HashSet<string>(page5Jobs.Select(j => j.Id));

            if (page3IdsSet.SetEquals(page5IdsSet))
            {
                errors.Add("Pagination may not be terminating: page 3 and page 5 returned identical job sets");
            }
        }

        context["Errors"] = errors.Count;

        if (errors.Count > 0)
        {
            return Failure(context, errors.ToArray());
        }

        return Success();
    }
}
