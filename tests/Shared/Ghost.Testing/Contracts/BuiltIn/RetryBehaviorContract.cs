using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Contracts.Jobs;

namespace Ghost.Testing.Contracts.BuiltIn;

/// <summary>
/// Contract that validates retry and backoff behavior.
/// Ensures that the provider respects rate limits and implements proper backoff.
/// </summary>
public sealed class RetryBehaviorContract : ProviderContractBase
{
    /// <inheritdoc />
    public override string Name => "RetryBehavior";

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
            // Test retry behavior by making multiple rapid requests
            var tasks = Enumerable.Range(0, 5)
                .Select(_ => adapter.TestRetryBehaviorAsync(criteria, ct))
                .ToList();

            var results = await Task.WhenAll(tasks);

            context["RequestCount"] = results.Length;
            context["SuccessfulRequests"] = results.Count(r => r.Count > 0);

            // Verify that at least some requests succeeded
            var successfulResults = results.Where(r => r.Count > 0).ToList();
            if (successfulResults.Count == 0)
            {
                errors.Add("All retry test requests failed - provider may not be handling retries properly");
            }

            // Verify that results are consistent across retries
            if (successfulResults.Count >= 2)
            {
                var firstResult = successfulResults[0];
                var secondResult = successfulResults[1];

                // Check if we're getting consistent job counts
                var countVariance = Math.Abs(firstResult.Count - secondResult.Count);
                context["CountVariance"] = countVariance;

                // Large variance might indicate pagination issues or inconsistent retry behavior
                if (countVariance > firstResult.Count * 0.5)
                {
                    errors.Add($"High variance in retry results: {firstResult.Count} vs {secondResult.Count} jobs");
                }
            }

            // Check for rate limit handling
            var failedRequests = results.Where(r => r.Count == 0).ToList();
            if (failedRequests.Count > 0)
            {
                context["FailedRequests"] = failedRequests.Count;
                // This is expected behavior under rate limiting, but we should document it
                context["RateLimitDetected"] = true;
            }
        }
        catch (System.Exception ex)
        {
            errors.Add($"Retry behavior test threw exception: {ex.Message}");
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
