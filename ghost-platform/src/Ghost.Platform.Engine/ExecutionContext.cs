using System.Text.Json;
using System.Threading;
using Ghost.Sdk.Contracts;

namespace Ghost.Platform.Engine;

/// <summary>
/// Context for job execution, tracking state and budgets.
/// </summary>
public sealed class ExecutionContext
{
    private int _requestCount;
    private int _itemCount;
    private readonly BudgetEnforcer _budgetEnforcer;

    public string RunId { get; }
    public JobDefinition Job { get; }
    public SpiderSpec Spec { get; }
    public IEngineObserver Observer { get; }
    public CancellationToken CancellationToken { get; }
    public int CurrentDepth { get; set; }
    public int RequestCount => _requestCount;
    public int ItemCount => _itemCount;

    public ExecutionContext(
        JobDefinition job,
        SpiderSpec spec,
        IEngineObserver observer,
        CancellationToken cancellationToken)
    {
        RunId = Guid.NewGuid().ToString("N");
        Job = job ?? throw new ArgumentNullException(nameof(job));
        Spec = spec ?? throw new ArgumentNullException(nameof(spec));
        Observer = observer ?? throw new ArgumentNullException(nameof(observer));
        CancellationToken = cancellationToken;
        _budgetEnforcer = new BudgetEnforcer(job.Budgets);
    }

    /// <summary>
    /// Attempts to increment the request count. Returns false if budget exceeded.
    /// </summary>
    public bool TryIncrementRequest()
    {
        var newCount = Interlocked.Increment(ref _requestCount);
        return _budgetEnforcer.CheckRequestBudget(newCount);
    }

    /// <summary>
    /// Attempts to increment the item count. Returns false if budget exceeded.
    /// </summary>
    public bool TryIncrementItem()
    {
        var newCount = Interlocked.Increment(ref _itemCount);
        return _budgetEnforcer.CheckItemBudget(newCount);
    }

    /// <summary>
    /// Checks if the depth budget allows the current depth.
    /// </summary>
    public bool CheckDepthBudget()
    {
        return _budgetEnforcer.CheckDepthBudget(CurrentDepth);
    }

    /// <summary>
    /// Checks if the time budget has been exceeded.
    /// </summary>
    public bool CheckTimeBudget(DateTimeOffset startTime)
    {
        return _budgetEnforcer.CheckTimeBudget(startTime);
    }
}

/// <summary>
/// Step specification for execution.
/// </summary>
public sealed record StepSpec(
    string StepId,
    string Kind,
    JsonElement Config);
