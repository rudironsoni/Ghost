using Ghost.Sdk.Contracts;

namespace Ghost.Platform.Engine;

/// <summary>
/// Enforces job budgets (time, requests, items, depth).
/// </summary>
public sealed class BudgetEnforcer
{
    private readonly JobBudgets _budgets;

    public BudgetEnforcer(JobBudgets budgets)
    {
        _budgets = budgets;
    }

    /// <summary>
    /// Checks if the request budget allows the given count.
    /// </summary>
    public bool CheckRequestBudget(int requestCount)
    {
        return requestCount <= _budgets.MaxRequests;
    }

    /// <summary>
    /// Checks if the item budget allows the given count.
    /// </summary>
    public bool CheckItemBudget(int itemCount)
    {
        return itemCount <= _budgets.MaxItems;
    }

    /// <summary>
    /// Checks if the depth budget allows the given depth.
    /// </summary>
    public bool CheckDepthBudget(int depth)
    {
        return depth <= _budgets.MaxDepth;
    }

    /// <summary>
    /// Checks if the time budget has been exceeded from the start time.
    /// </summary>
    public bool CheckTimeBudget(DateTimeOffset startTime)
    {
        var elapsed = DateTimeOffset.UtcNow - startTime;
        return elapsed <= _budgets.MaxDuration;
    }

    /// <summary>
    /// Validates all budgets and throws BudgetExceededException if any are exceeded.
    /// </summary>
    public void ValidateBudgets(
        int requestCount,
        int itemCount,
        int depth,
        DateTimeOffset startTime)
    {
        if (!CheckRequestBudget(requestCount))
        {
            throw new BudgetExceededException($"Request budget exceeded: {requestCount} > {_budgets.MaxRequests}");
        }

        if (!CheckItemBudget(itemCount))
        {
            throw new BudgetExceededException($"Item budget exceeded: {itemCount} > {_budgets.MaxItems}");
        }

        if (!CheckDepthBudget(depth))
        {
            throw new BudgetExceededException($"Depth budget exceeded: {depth} > {_budgets.MaxDepth}");
        }

        if (!CheckTimeBudget(startTime))
        {
            var elapsed = DateTimeOffset.UtcNow - startTime;
            throw new BudgetExceededException($"Time budget exceeded: {elapsed} > {_budgets.MaxDuration}");
        }
    }
}

/// <summary>
/// Exception thrown when a budget is exceeded.
/// </summary>
public sealed class BudgetExceededException : Exception
{
    public BudgetExceededException(string message) : base(message)
    {
    }

    public BudgetExceededException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
