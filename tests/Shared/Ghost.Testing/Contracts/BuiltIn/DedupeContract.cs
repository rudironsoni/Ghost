using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Contracts.Jobs;

namespace Ghost.Testing.Contracts.BuiltIn;

/// <summary>
/// Contract that validates deduplication correctness.
/// Ensures that duplicate jobs are properly identified and removed.
/// </summary>
public sealed class DedupeContract : ProviderContractBase
{
    /// <inheritdoc />
    public override string Name => "Dedupe";

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

        // Get jobs from multiple pages to test deduplication
        IReadOnlyList<JobListing> jobs = await adapter.SearchWithPaginationAsync(criteria, maxPages: 3, ct).ConfigureAwait(false);

        if (jobs.Count == 0)
        {
            return Failure("No jobs returned to validate deduplication");
        }

        var errors = new List<string>();
        var context = new Dictionary<string, object>
        {
            ["TotalJobs"] = jobs.Count
        };

        // Check for duplicate IDs
        var duplicateIds = jobs
            .GroupBy(j => j.Id)
            .Where(g => g.Count() > 1)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToList();

        if (duplicateIds.Count > 0)
        {
            foreach (var dup in duplicateIds)
            {
                errors.Add($"Duplicate job ID found: '{dup.Id}' appears {dup.Count} times");
            }
            context["DuplicateIds"] = duplicateIds.Count;
        }

        // Check for duplicate URLs (canonicalization test)
        var jobsWithUrls = jobs.Where(j => !string.IsNullOrWhiteSpace(j.Url)).ToList();
        var urlGroups = jobsWithUrls
            .GroupBy(j => NormalizeUrl(j.Url!))
            .Where(g => g.Count() > 1)
            .Select(g => new { Url = g.Key, Count = g.Count() })
            .ToList();

        if (urlGroups.Count > 0)
        {
            foreach (var dup in urlGroups)
            {
                errors.Add($"Duplicate canonical URL found: '{dup.Url}' appears {dup.Count} times");
            }
            context["DuplicateUrls"] = urlGroups.Count;
        }

        // Check for duplicate (Title + Company) combinations
        var titleCompanyGroups = jobs
            .GroupBy(j => new { j.Title, j.Company })
            .Where(g => g.Count() > 1)
            .Select(g => new { Title = g.Key.Title, Company = g.Key.Company, Count = g.Count() })
            .ToList();

        if (titleCompanyGroups.Count > 0)
        {
            foreach (var dup in titleCompanyGroups)
            {
                errors.Add($"Duplicate (Title + Company) found: '{dup.Title}' at '{dup.Company}' appears {dup.Count} times");
            }
            context["DuplicateTitleCompany"] = titleCompanyGroups.Count;
        }

        context["Errors"] = errors.Count;

        if (errors.Count > 0)
        {
            return Failure(context, errors.ToArray());
        }

        return Success();
    }

    /// <summary>
    /// Normalizes URL for deduplication comparison.
    /// Removes tracking parameters, normalizes case, etc.
    /// </summary>
    private static string NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        string normalized = url.ToLowerInvariant().Trim();

        // Remove common tracking parameters
        string[] trackingParams = new[] { "utm_source", "utm_medium", "utm_campaign", "utm_term", "utm_content", "fbclid", "gclid" };
        var uri = new System.Uri(normalized);
        NameValueCollection query = System.Web.HttpUtility.ParseQueryString(uri.Query);

        foreach (string? param in trackingParams)
        {
            query.Remove(param);
        }

        string? newQuery = query.ToString();
        var newUri = new System.UriBuilder(uri)
        {
            Query = newQuery
        };

        return newUri.Uri.ToString();
    }
}
